using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsBuddy.Services
{
    /// <summary>
    /// Real-time WASAPI Loopback Audio Spectrum & Bass Beat Analyzer (28 logarithmic bands)
    /// with automatic multi-endpoint active render device discovery and dual-domain bass kick extraction.
    /// </summary>
    public sealed class AudioSpectrumService : IDisposable
    {
        public const int Bands = 28;
        private const int FftSize = 2048;
        private const int RingSize = 4096;
        private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x00000002;
        private const uint CLSCTX_ALL = 23;

        private static readonly Lazy<AudioSpectrumService> _lazy = new(() => new AudioSpectrumService());
        public static AudioSpectrumService Instance => _lazy.Value;

        private readonly object _lock = new();
        private readonly float[] _ring = new float[RingSize];
        private int _ringPos;
        private readonly float[] _window = new float[FftSize];
        private readonly float[] _re = new float[FftSize];
        private readonly float[] _im = new float[FftSize];
        private readonly float[] _target = new float[Bands];
        private readonly float[] _smooth = new float[Bands];
        private readonly float[] _levels = new float[Bands];
        private float _peakDb = -32f;

        // From-scratch dual-domain bass kick detector state
        private float _lpStage1;
        private float _lpStage2;
        private float _bassRefPeak = 0.006f;
        private float _bassFloor = 0.0015f;
        private float _midRefPeak = 0.005f;
        private float _reactiveBass;
        private float _reactiveMid;
        private float _reactiveOverall;
        private bool _live;

        private volatile bool _running;
        private volatile bool _isActive;
        private volatile bool _forceReconnect;
        private Thread _thread;

        private AudioSpectrumService()
        {
            for (int i = 0; i < FftSize; i++)
            {
                _window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftSize - 1))));
            }
            Start();
        }

        public void RequestReconnect() => _forceReconnect = true;

        private static void LogSpec(string msg)
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mayli", "logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "spectrum.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
            }
            catch { }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_running) return;
                _running = true;
                _thread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "MayliAudioSpectrum"
                };
                _thread.SetApartmentState(ApartmentState.MTA);
                _thread.Start();
            }
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (!active)
            {
                lock (_lock)
                {
                    _live = false;
                    _reactiveBass = 0f;
                    _reactiveMid = 0f;
                    _reactiveOverall = 0f;
                    Array.Clear(_smooth, 0, Bands);
                    Array.Clear(_levels, 0, Bands);
                }
            }
        }

        private struct BandBinRange
        {
            public int Bin0;
            public int Bin1;
            public double Tilt;
        }
        private static BandBinRange[] _cachedBandBins;
        private static int _cachedSampleRate = -1;

        private static BandBinRange[] GetBandBins(int sampleRate)
        {
            if (_cachedBandBins != null && _cachedSampleRate == sampleRate) return _cachedBandBins;
            int halfFft = FftSize / 2;
            var res = new BandBinRange[Bands];
            for (int b = 0; b < Bands; b++)
            {
                double t0 = Math.Pow((double)b / Bands, 2.2);
                double t1 = Math.Pow((double)(b + 1) / Bands, 2.2);
                double f0 = 40.0 * Math.Pow(12000.0 / 40.0, t0);
                double f1 = 40.0 * Math.Pow(12000.0 / 40.0, t1);
                int bin0 = Math.Clamp((int)(f0 * FftSize / sampleRate), 1, halfFft - 1);
                int bin1 = Math.Clamp((int)(f1 * FftSize / sampleRate), bin0, halfFft - 1);
                double tilt = 1.0 + (b / (double)Bands) * 2.2;
                res[b] = new BandBinRange { Bin0 = bin0, Bin1 = bin1, Tilt = tilt };
            }
            _cachedSampleRate = sampleRate;
            _cachedBandBins = res;
            return res;
        }

        public void Stop()
        {
            _running = false;
        }

        public void Read(float[] into28, out bool isLive)
        {
            lock (_lock)
            {
                int len = Math.Min(into28.Length, Bands);
                Array.Copy(_levels, 0, into28, 0, len);
                isLive = _live;
            }
        }

        /// <summary>
        /// Reads low-latency bass beat pulse, mid, and overall normalized energy (0..1).
        /// </summary>
        public void ReadReactiveEnergy(out float bass, out float mid, out float overall, out bool isLive)
        {
            lock (_lock)
            {
                isLive = _live;
                if (!_live)
                {
                    bass = 0f;
                    mid = 0f;
                    overall = 0f;
                    return;
                }

                bass = _reactiveBass;
                mid = _reactiveMid;
                overall = _reactiveOverall;
            }
        }

        /// <summary>
        /// Symmetrical center-outward band mapping matching CapsuleWindow.MapBands(bands, bars, n).
        /// </summary>
        public static void MapBands(float[] bands, float[] bars, int n)
        {
            if (bands == null || bars == null || n <= 0) return;
            int bandCount = bands.Length;
            double half = (n - 1) * 0.5;
            if (half <= 0) half = 1.0;

            for (int i = 0; i < n && i < bars.Length; i++)
            {
                double norm = Math.Abs(i - half) / half;
                double pos = norm * 0.92 * (bandCount - 1);
                int idx0 = Math.Clamp((int)pos, 0, bandCount - 1);
                int idx1 = Math.Clamp(idx0 + 1, 0, bandCount - 1);
                float frac = (float)(pos - idx0);
                bars[i] = bands[idx0] * (1f - frac) + bands[idx1] * frac;
            }
        }

        /// <summary>
        /// Computes 4 representative equalizer bar levels (0..1) from the 28 FFT bands for the top-right mini equalizer.
        /// </summary>
        public static void ComputeMiniBars(float[] bands, float[] mini4, double phase)
        {
            if (bands == null || mini4 == null || mini4.Length < 4) return;
            float b0 = AverageRange(bands, 5, 9);
            float b1 = AverageRange(bands, 10, 14);
            float b2 = AverageRange(bands, 15, 20);
            float b3 = AverageRange(bands, 21, 26);

            float energy = Math.Max(Math.Max(b0, b1), Math.Max(b2, b3));
            if (energy > 0.03f)
            {
                mini4[0] = Math.Clamp(b0 * 1.15f, 0.15f, 1.0f);
                mini4[1] = Math.Clamp(b1 * 0.95f, 0.12f, 1.0f);
                mini4[2] = Math.Clamp(b2 * 1.20f, 0.18f, 1.0f);
                mini4[3] = Math.Clamp(b3 * 1.10f, 0.15f, 1.0f);
            }
            else
            {
                mini4[0] = (float)(0.35 + 0.30 * Math.Abs(Math.Sin(phase * 2.1)));
                mini4[1] = (float)(0.20 + 0.25 * Math.Abs(Math.Cos(phase * 1.7)));
                mini4[2] = (float)(0.45 + 0.35 * Math.Abs(Math.Sin(phase * 2.6 + 1.1)));
                mini4[3] = (float)(0.30 + 0.35 * Math.Abs(Math.Cos(phase * 2.3 + 0.5)));
            }
        }

        private static float AverageRange(float[] arr, int start, int endInclusive)
        {
            float sum = 0f;
            int count = 0;
            for (int i = Math.Max(0, start); i <= Math.Min(arr.Length - 1, endInclusive); i++)
            {
                sum += arr[i];
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        private static float ReadEndpointHardwarePeak(IMMDevice dev, out IAudioMeterInformation meterOut)
        {
            meterOut = null;
            try
            {
                var iidMeter = typeof(IAudioMeterInformation).GUID;
                if (dev.Activate(ref iidMeter, CLSCTX_ALL, IntPtr.Zero, out object meterObj) == 0 &&
                    meterObj is IAudioMeterInformation meter)
                {
                    meterOut = meter;
                    if (meter.GetPeakValue(out float peak) == 0 && float.IsFinite(peak))
                        return peak;
                }
            }
            catch { }
            return 0f;
        }

        /// <summary>
        /// Scans all active Windows eRender endpoints and returns the endpoint currently outputting audio
        /// (highest hardware peak > 0.00025). Falls back to Windows default multimedia/console endpoint if silent.
        /// </summary>
        private static IMMDevice SelectBestRenderEndpoint(
            IMMDeviceEnumerator enumerator,
            out string selectedId,
            out float selectedPeak,
            out IAudioMeterInformation selectedMeter)
        {
            selectedId = string.Empty;
            selectedPeak = 0f;
            selectedMeter = null;

            IMMDevice bestActiveDev = null;
            string bestActiveId = string.Empty;
            float bestActivePeak = 0f;
            IAudioMeterInformation bestActiveMeter = null;

            try
            {
                if (enumerator.EnumAudioEndpoints(0, 1, out IMMDeviceCollection col) == 0 && col != null)
                {
                    col.GetCount(out uint count);
                    for (uint i = 0; i < count; i++)
                    {
                        if (col.Item(i, out IMMDevice candidate) == 0 && candidate != null)
                        {
                            string id = string.Empty;
                            try { candidate.GetId(out id); } catch { }
                            float peak = ReadEndpointHardwarePeak(candidate, out IAudioMeterInformation meter);
                            if (peak > 0.00025f && peak > bestActivePeak)
                            {
                                if (bestActiveMeter != null) { try { Marshal.ReleaseComObject(bestActiveMeter); } catch { } }
                                if (bestActiveDev != null) { try { Marshal.ReleaseComObject(bestActiveDev); } catch { } }
                                bestActiveDev = candidate;
                                bestActiveId = id ?? string.Empty;
                                bestActivePeak = peak;
                                bestActiveMeter = meter;
                            }
                            else
                            {
                                if (meter != null) { try { Marshal.ReleaseComObject(meter); } catch { } }
                                try { Marshal.ReleaseComObject(candidate); } catch { }
                            }
                        }
                    }
                    try { Marshal.ReleaseComObject(col); } catch { }
                }
            }
            catch { }

            if (bestActiveDev != null)
            {
                selectedId = bestActiveId;
                selectedPeak = bestActivePeak;
                selectedMeter = bestActiveMeter;
                return bestActiveDev;
            }

            IMMDevice fallback = null;
            if ((enumerator.GetDefaultAudioEndpoint(0, 1, out fallback) == 0 && fallback != null) ||
                (enumerator.GetDefaultAudioEndpoint(0, 0, out fallback) == 0 && fallback != null) ||
                (enumerator.GetDefaultAudioEndpoint(0, 2, out fallback) == 0 && fallback != null))
            {
                try { fallback.GetId(out selectedId); } catch { selectedId = "default"; }
                selectedPeak = ReadEndpointHardwarePeak(fallback, out selectedMeter);
                return fallback;
            }

            return null;
        }

        /// <summary>
        /// Checks whether any other active eRender endpoint has started outputting audio while the current endpoint is silent.
        /// </summary>
        private static bool HasOtherEndpointPlayingAudio(
            IMMDeviceEnumerator enumerator,
            string currentEndpointId,
            out string activeEndpointId,
            out float activePeak)
        {
            activeEndpointId = string.Empty;
            activePeak = 0f;
            try
            {
                if (enumerator.EnumAudioEndpoints(0, 1, out IMMDeviceCollection col) == 0 && col != null)
                {
                    col.GetCount(out uint count);
                    for (uint i = 0; i < count; i++)
                    {
                        if (col.Item(i, out IMMDevice candidate) == 0 && candidate != null)
                        {
                            string id = string.Empty;
                            try { candidate.GetId(out id); } catch { }
                            if (!string.Equals(id, currentEndpointId, StringComparison.OrdinalIgnoreCase))
                            {
                                float peak = ReadEndpointHardwarePeak(candidate, out IAudioMeterInformation meter);
                                if (meter != null) { try { Marshal.ReleaseComObject(meter); } catch { } }
                                if (peak > 0.0005f && peak > activePeak)
                                {
                                    activePeak = peak;
                                    activeEndpointId = id ?? string.Empty;
                                }
                            }
                            try { Marshal.ReleaseComObject(candidate); } catch { }
                        }
                    }
                    try { Marshal.ReleaseComObject(col); } catch { }
                }
            }
            catch { }
            return activePeak > 0.0005f;
        }

        private void CaptureLoop()
        {
            while (_running)
            {
                while (_running && !_isActive)
                {
                    Thread.Sleep(200);
                }
                if (!_running) break;

                IMMDeviceEnumerator enumerator = null;
                IMMDevice device = null;
                IAudioMeterInformation deviceMeter = null;
                IAudioClient client = null;
                IAudioCaptureClient capture = null;
                IntPtr mixFormatPtr = IntPtr.Zero;

                try
                {
                    var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
                    var type = Type.GetTypeFromCLSID(clsid);
                    if (type == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type);
                    device = SelectBestRenderEndpoint(enumerator, out string currentEndpointId, out float initPeak, out deviceMeter);
                    if (device == null)
                    {
                        LogSpec("SelectBestRenderEndpoint found no active render endpoint.");
                        Thread.Sleep(1000);
                        continue;
                    }

                    var iidAudioClient = typeof(IAudioClient).GUID;
                    if (device.Activate(ref iidAudioClient, CLSCTX_ALL, IntPtr.Zero, out object clientObj) != 0 || clientObj == null)
                    {
                        LogSpec("device.Activate(IAudioClient) failed");
                        Thread.Sleep(1000);
                        continue;
                    }
                    client = (IAudioClient)clientObj;

                    if (client.GetMixFormat(out mixFormatPtr) != 0 || mixFormatPtr == IntPtr.Zero)
                    {
                        LogSpec("client.GetMixFormat failed");
                        Thread.Sleep(1000);
                        continue;
                    }

                    ushort formatTag = (ushort)Marshal.ReadInt16(mixFormatPtr, 0);
                    int channels = Marshal.ReadInt16(mixFormatPtr, 2);
                    int sampleRate = Marshal.ReadInt32(mixFormatPtr, 4);
                    int blockAlign = Marshal.ReadInt16(mixFormatPtr, 12);
                    int bitsPerSample = Marshal.ReadInt16(mixFormatPtr, 14);
                    int cbSize = Marshal.ReadInt16(mixFormatPtr, 16);

                    bool isFloat = (formatTag == 3);
                    if (formatTag == 0xFFFE && cbSize >= 22)
                    {
                        byte[] subFormatBytes = new byte[16];
                        Marshal.Copy(IntPtr.Add(mixFormatPtr, 24), subFormatBytes, 0, 16);
                        var subFormat = new Guid(subFormatBytes);
                        if (subFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"))
                            isFloat = true;
                    }

                    int hrInit = client.Initialize(0, AUDCLNT_STREAMFLAGS_LOOPBACK, 400_000, 0, mixFormatPtr, IntPtr.Zero);
                    if (hrInit != 0)
                    {
                        LogSpec($"client.Initialize failed hr=0x{hrInit:X8}");
                        Thread.Sleep(1000);
                        continue;
                    }

                    var iidCapture = typeof(IAudioCaptureClient).GUID;
                    if (client.GetService(ref iidCapture, out object captureObj) != 0 || captureObj == null)
                    {
                        LogSpec("client.GetService(IAudioCaptureClient) failed");
                        Thread.Sleep(1000);
                        continue;
                    }
                    capture = (IAudioCaptureClient)captureObj;

                    if (client.Start() != 0)
                    {
                        LogSpec("client.Start failed");
                        Thread.Sleep(1000);
                        continue;
                    }

                    LogSpec($"Loopback started on endpoint={currentEndpointId} initPeak={initPeak:F4} ch={channels} sr={sampleRate} bps={bitsPerSample} float={isFloat}");
                    DateTime lastActiveSoundUtc = DateTime.UtcNow;
                    DateTime lastEndpointCheckUtc = DateTime.UtcNow;
                    _forceReconnect = false;

                    while (_running && _isActive && !_forceReconnect)
                    {
                        bool gotFrames = false;
                        float maxAbsSample = 0f;
                        int frameStep = Math.Max(4, blockAlign);
                        bool deviceInvalidated = false;

                        while (true)
                        {
                            int hrPacket = capture.GetNextPacketSize(out uint packetFrames);
                            if (hrPacket != 0)
                            {
                                deviceInvalidated = true;
                                break;
                            }
                            if (packetFrames == 0)
                                break;

                            int hrBuf = capture.GetBuffer(out IntPtr dataPtr, out uint numFrames, out uint flags, out _, out _);
                            if (hrBuf != 0)
                            {
                                deviceInvalidated = true;
                                break;
                            }

                            try
                            {
                                bool isSilent = (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0;
                                for (uint f = 0; f < numFrames; f++)
                                {
                                    float mono = 0f;
                                    if (!isSilent && dataPtr != IntPtr.Zero)
                                    {
                                        IntPtr framePtr = IntPtr.Add(dataPtr, (int)(f * (uint)frameStep));
                                        if (isFloat && bitsPerSample == 32)
                                        {
                                            int chCount = Math.Min(channels, 2);
                                            float sum = 0f;
                                            for (int c = 0; c < chCount; c++)
                                            {
                                                int bits = Marshal.ReadInt32(framePtr, c * 4);
                                                float s = BitConverter.Int32BitsToSingle(bits);
                                                if (float.IsNaN(s) || float.IsInfinity(s)) s = 0f;
                                                sum += s;
                                            }
                                            mono = chCount > 0 ? sum / chCount : 0f;
                                        }
                                        else if (bitsPerSample == 16)
                                        {
                                            int chCount = Math.Min(channels, 2);
                                            float sum = 0f;
                                            for (int c = 0; c < chCount; c++)
                                            {
                                                short pcm = Marshal.ReadInt16(framePtr, c * 2);
                                                sum += pcm / 32768f;
                                            }
                                            mono = chCount > 0 ? sum / chCount : 0f;
                                        }
                                        else if (bitsPerSample == 24)
                                        {
                                            int chCount = Math.Min(channels, 2);
                                            float sum = 0f;
                                            for (int c = 0; c < chCount; c++)
                                            {
                                                int b0 = Marshal.ReadByte(framePtr, c * 3);
                                                int b1 = Marshal.ReadByte(framePtr, c * 3 + 1);
                                                int b2 = (sbyte)Marshal.ReadByte(framePtr, c * 3 + 2);
                                                int pcm24 = b0 | (b1 << 8) | (b2 << 16);
                                                sum += pcm24 / 8388608f;
                                            }
                                            mono = chCount > 0 ? sum / chCount : 0f;
                                        }
                                        else if (!isFloat && bitsPerSample == 32)
                                        {
                                            int chCount = Math.Min(channels, 2);
                                            float sum = 0f;
                                            for (int c = 0; c < chCount; c++)
                                            {
                                                int pcm32 = Marshal.ReadInt32(framePtr, c * 4);
                                                sum += pcm32 / 2147483648f;
                                            }
                                            mono = chCount > 0 ? sum / chCount : 0f;
                                        }
                                    }

                                    float abs = Math.Abs(mono);
                                    if (abs > maxAbsSample) maxAbsSample = abs;

                                    _ring[_ringPos] = mono;
                                    _ringPos = (_ringPos + 1) & (RingSize - 1);
                                }
                                gotFrames = numFrames > 0;
                            }
                            finally
                            {
                                capture.ReleaseBuffer(numFrames);
                            }
                        }

                        if (deviceInvalidated)
                        {
                            LogSpec("Endpoint stream invalidated; reconnecting.");
                            break;
                        }

                        float meterPeak = 0f;
                        if (deviceMeter != null)
                        {
                            try
                            {
                                if (deviceMeter.GetPeakValue(out float mp) == 0 && float.IsFinite(mp))
                                    meterPeak = mp;
                            }
                            catch { }
                        }

                        if (gotFrames && (maxAbsSample > 0.00015f || meterPeak > 0.0004f))
                        {
                            lastActiveSoundUtc = DateTime.UtcNow;
                        }
                        else if (!gotFrames && meterPeak > 0.0008f)
                        {
                            lastActiveSoundUtc = DateTime.UtcNow;
                        }

                        Analyze(sampleRate, lastActiveSoundUtc, meterPeak);

                        // If current endpoint has been silent for >280ms, check if audio is playing on another active render endpoint
                        var nowUtc = DateTime.UtcNow;
                        if ((nowUtc - lastActiveSoundUtc).TotalMilliseconds > 280 &&
                            (nowUtc - lastEndpointCheckUtc).TotalMilliseconds > 320)
                        {
                            lastEndpointCheckUtc = nowUtc;
                            if (HasOtherEndpointPlayingAudio(enumerator, currentEndpointId, out string newId, out float newPeak))
                            {
                                LogSpec($"Switching from silent endpoint {currentEndpointId} to active endpoint {newId} (peak={newPeak:F4})");
                                break;
                            }
                        }

                        Thread.Sleep(15);
                    }

                    try { client.Stop(); } catch { }
                }
                catch (Exception ex)
                {
                    LogSpec($"CaptureLoop exception: {ex.Message}");
                    Thread.Sleep(1000);
                }
                finally
                {
                    if (mixFormatPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(mixFormatPtr);
                    if (capture != null) { try { Marshal.ReleaseComObject(capture); } catch { } }
                    if (client != null) { try { Marshal.ReleaseComObject(client); } catch { } }
                    if (deviceMeter != null) { try { Marshal.ReleaseComObject(deviceMeter); } catch { } }
                    if (device != null) { try { Marshal.ReleaseComObject(device); } catch { } }
                    if (enumerator != null) { try { Marshal.ReleaseComObject(enumerator); } catch { } }
                }
            }
        }

        private void Analyze(int sampleRate, DateTime lastActiveSoundUtc, float hardwareMeterPeak)
        {
            bool hasSound = (DateTime.UtcNow - lastActiveSoundUtc).TotalMilliseconds < 320;
            if (!hasSound)
            {
                lock (_lock)
                {
                    bool any = false;
                    for (int b = 0; b < Bands; b++)
                    {
                        _smooth[b] *= 0.75f;
                        if (_smooth[b] < 0.004f) _smooth[b] = 0f;
                        else any = true;
                        _levels[b] = _smooth[b];
                    }
                    _reactiveBass *= 0.65f;
                    _reactiveMid *= 0.70f;
                    _reactiveOverall *= 0.70f;
                    if (_reactiveBass < 0.005f) _reactiveBass = 0f;
                    if (_reactiveMid < 0.005f) _reactiveMid = 0f;
                    if (_reactiveOverall < 0.005f) _reactiveOverall = 0f;
                    _live = any || _reactiveBass > 0.01f;
                }
                return;
            }

            // Copy latest 2048 samples and simultaneously run a 2-pole time-domain low-pass bass filter (~135 Hz cutoff)
            // on the latest 512 samples (~10.6 ms at 48kHz) for instant zero-smear kick drum transient detection
            float lpAlpha = 1f - MathF.Exp(-2f * MathF.PI * 135f / Math.Max(8000, sampleRate));
            float lp1 = _lpStage1;
            float lp2 = _lpStage2;
            float recentBassSq = 0f;
            float recentMidSq = 0f;
            int recentStart = FftSize - 512;

            int start = (_ringPos - FftSize + RingSize) & (RingSize - 1);
            for (int i = 0; i < FftSize; i++)
            {
                float s = _ring[(start + i) & (RingSize - 1)];
                if (!float.IsFinite(s)) s = 0f;

                if (i >= recentStart)
                {
                    lp1 += lpAlpha * (s - lp1);
                    lp2 += lpAlpha * (lp1 - lp2);
                    float hi = s - lp2;
                    recentBassSq += lp2 * lp2;
                    recentMidSq += hi * hi;
                }

                _re[i] = s * _window[i];
                _im[i] = 0f;
            }

            _lpStage1 = float.IsFinite(lp1) ? lp1 : 0f;
            _lpStage2 = float.IsFinite(lp2) ? lp2 : 0f;

            float tdBassRms = MathF.Sqrt(recentBassSq / 512f);
            float tdMidRms = MathF.Sqrt(recentMidSq / 512f);

            Fft(_re, _im);

            var bandBins = GetBandBins(sampleRate);
            float maxBandDb = -100f;

            for (int b = 0; b < Bands; b++)
            {
                ref readonly var bb = ref bandBins[b];
                int bin0 = bb.Bin0;
                int bin1 = bb.Bin1;

                double magSum = 0.0;
                int count = 0;
                for (int k = bin0; k <= bin1; k++)
                {
                    double mag = Math.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]) / (FftSize * 0.25);
                    magSum += mag;
                    count++;
                }

                double avgMag = count > 0 ? magSum / count : 0.0;
                float db = (float)(20.0 * Math.Log10(avgMag * bb.Tilt + 1e-9));
                if (db > maxBandDb) maxBandDb = db;
                _target[b] = db;
            }

            if (maxBandDb > _peakDb)
                _peakDb = _peakDb * 0.78f + maxBandDb * 0.22f;
            else
                _peakDb = Math.Max(-54f, _peakDb * 0.985f + maxBandDb * 0.015f);

            float floorDb = _peakDb - 42f;

            // Compute linear FFT sub-bass/kick magnitude (~40 Hz .. 180 Hz)
            int kickBin0 = Math.Max(1, (int)(40.0 * FftSize / sampleRate));
            int kickBin1 = Math.Max(kickBin0 + 1, (int)(180.0 * FftSize / sampleRate));
            float fftKickMag = 0f;
            int kickCount = 0;
            for (int k = kickBin0; k <= kickBin1 && k < FftSize / 2; k++)
            {
                float mag = MathF.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]) / (FftSize * 0.25f);
                fftKickMag += mag;
                kickCount++;
            }
            if (kickCount > 0) fftKickMag /= kickCount;

            // Fuse time-domain 135Hz low-pass RMS and FFT 40-180Hz kick magnitude
            float rawBass = (tdBassRms * 1.40f) + (fftKickMag * 1.10f);
            if (!float.IsFinite(rawBass)) rawBass = 0f;

            // Volume-adaptive peak reference: adapts quickly to track volume so 5% volume and 100% volume both pulse cleanly
            if (rawBass > _bassRefPeak)
                _bassRefPeak += (rawBass - _bassRefPeak) * 0.48f;
            else
                _bassRefPeak = Math.Max(0.0012f, _bassRefPeak * 0.992f);

            // Slow moving average of bass energy (floor) to isolate drum kick transients
            _bassFloor += (rawBass - _bassFloor) * 0.09f;
            _bassFloor = Math.Max(0.0002f, _bassFloor);

            // Normalized bass envelope (0..1) + gentle transient onset above rolling bass floor (0..1)
            float normBass = Math.Clamp(rawBass / Math.Max(0.0012f, _bassRefPeak), 0f, 1f);
            float onsetDelta = Math.Max(0f, rawBass - (_bassFloor * 0.96f));
            float kickOnset = Math.Clamp(onsetDelta / Math.Max(0.0008f, _bassRefPeak * 0.55f), 0f, 1f);

            // Smooth, eye-friendly beat shaping: avoids harsh spikes while tracking bass rhythm fluidly
            float targetBass = Math.Clamp((MathF.Pow(normBass, 1.20f) * 0.52f) + (kickOnset * 0.52f), 0f, 1f);

            if (tdMidRms > _midRefPeak)
                _midRefPeak += (tdMidRms - _midRefPeak) * 0.38f;
            else
                _midRefPeak = Math.Max(0.0010f, _midRefPeak * 0.992f);

            float targetMid = Math.Clamp(tdMidRms / Math.Max(0.0010f, _midRefPeak), 0f, 1f);
            float targetOverall = Math.Clamp((targetBass * 0.70f) + (targetMid * 0.30f), 0f, 1f);

            lock (_lock)
            {
                for (int b = 0; b < Bands; b++)
                {
                    float scale = b < 5 ? (b / 5.0f) * 0.25f : 1.0f;
                    float norm = Math.Clamp((_target[b] - floorDb) / 42f, 0f, 1f) * scale;
                    float rate = norm > _smooth[b] ? 0.60f : 0.22f;
                    _smooth[b] += (norm - _smooth[b]) * rate;
                    _levels[b] = _smooth[b];
                }

                float bassRate = targetBass > _reactiveBass ? 0.45f : 0.18f;
                float midRate = targetMid > _reactiveMid ? 0.42f : 0.20f;
                _reactiveBass = Math.Clamp(_reactiveBass + (targetBass - _reactiveBass) * bassRate, 0f, 1f);
                _reactiveMid = Math.Clamp(_reactiveMid + (targetMid - _reactiveMid) * midRate, 0f, 1f);
                _reactiveOverall = Math.Clamp((_reactiveBass * 0.72f) + (_reactiveMid * 0.28f), 0f, 1f);
                _live = true;
            }
        }

        private static void Fft(float[] re, float[] im)
        {
            int n = re.Length;
            int j = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
                int m = n >> 1;
                while (m >= 1 && j >= m)
                {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                int half = len >> 1;
                double angle = -2.0 * Math.PI / len;
                float wReStep = (float)Math.Cos(angle);
                float wImStep = (float)Math.Sin(angle);

                for (int i = 0; i < n; i += len)
                {
                    float wRe = 1f;
                    float wIm = 0f;
                    for (int k = 0; k < half; k++)
                    {
                        int even = i + k;
                        int odd = even + half;
                        float tRe = wRe * re[odd] - wIm * im[odd];
                        float tIm = wRe * im[odd] + wIm * re[odd];

                        re[odd] = re[even] - tRe;
                        im[odd] = im[even] - tIm;
                        re[even] += tRe;
                        im[even] += tIm;

                        float nextWRe = wRe * wReStep - wIm * wImStep;
                        wIm = wRe * wImStep + wIm * wReStep;
                        wRe = nextWRe;
                    }
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        #region WASAPI COM Interop Declarations

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        }

        [ComImport]
        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            int GetCount(out uint pcDevices);
            int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(uint stgmAccess, out IntPtr ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out uint pdwState);
        }

        [ComImport]
        [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            int GetPeakValue(out float pfPeak);
        }

        [ComImport]
        [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
            int GetBufferSize(out uint numBufferFrames);
            int GetStreamLatency(out long phnsLatency);
            int GetCurrentPadding(out uint numPaddingFrames);
            int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
            int GetMixFormat(out IntPtr ppDeviceFormat);
            int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
            int Start();
            int Stop();
            int Reset();
            int SetEventHandle(IntPtr eventHandle);
            int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [ComImport]
        [Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            int ReleaseBuffer(uint numFramesRead);
            int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        #endregion
    }

    /// <summary>
    /// Extracts the dominant vibrant accent color from a track's album cover art
    /// using Vayli's exact 24x24 18-bucket HSV histogram + Lighten algorithm.
    /// </summary>
    public static class DominantColorExtractor
    {
        public static Color ExtractBeatColor(BitmapSource art, string trackTitle = null, string artistName = null)
        {
            if (art != null)
            {
                try
                {
                    Color dom = DominantColor(art);
                    Color lit = Lighten(dom);
                    double lum = (0.299 * lit.R + 0.587 * lit.G + 0.114 * lit.B) / 255.0;
                    double maxC = Math.Max(lit.R, Math.Max(lit.G, lit.B));
                    double minC = Math.Min(lit.R, Math.Min(lit.G, lit.B));
                    double sat = maxC > 0 ? (maxC - minC) / maxC : 0;
                    // Protect against washed-out / white colors that wash out text in Dark Mode
                    if (lum <= 0.82 && sat >= 0.15)
                    {
                        return lit;
                    }
                }
                catch { }
            }

            // Curated fallback palette based on track/artist signature if no thumbnail stream was provided
            string sig = $"{trackTitle} {artistName}".ToLowerInvariant();
            if (sig.Contains("kaskade") || sig.Contains("vision blurred"))
                return Color.FromRgb(217, 155, 82); // Warm golden amber
            if (sig.Contains("swish") || sig.Contains("crazy for you"))
                return Color.FromRgb(229, 57, 53);  // Vibrant crimson red
            if (sig.Contains("zoya") || sig.Contains("future calling"))
                return Color.FromRgb(100, 210, 255); // Vibrant cyan blue

            // Default rich vibrant fallback color instead of washed-out white
            return Color.FromRgb(255, 60, 90);
        }

        public static Color DominantColor(BitmapSource source)
        {
            if (source == null) return Color.FromRgb(255, 60, 90);

            const int SampleSize = 24;
            var transformed = new TransformedBitmap(source, new ScaleTransform(
                (double)SampleSize / Math.Max(1, source.PixelWidth),
                (double)SampleSize / Math.Max(1, source.PixelHeight)));
            var formatted = new FormatConvertedBitmap(transformed, PixelFormats.Bgra32, null, 0);

            int stride = SampleSize * 4;
            byte[] pixels = new byte[SampleSize * stride];
            formatted.CopyPixels(new Int32Rect(0, 0, SampleSize, SampleSize), pixels, stride, 0);

            const int Buckets = 18;
            double[] weights = new double[Buckets];
            double[] sumR = new double[Buckets];
            double[] sumG = new double[Buckets];
            double[] sumB = new double[Buckets];
            double totalWeight = 0.0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte a = pixels[i + 3];
                if (a < 128) continue;

                RgbToHsv(r, g, b, out double h, out double s, out double v);
                if (s < 0.28 || v < 0.18) continue;

                int bucket = Math.Clamp((int)(h / (360.0 / Buckets)), 0, Buckets - 1);
                double w = s * v;
                weights[bucket] += w;
                sumR[bucket] += r * w;
                sumG[bucket] += g * w;
                sumB[bucket] += b * w;
                totalWeight += w;
            }

            if (totalWeight < SampleSize * SampleSize * 0.04)
            {
                return Color.FromRgb(255, 60, 90);
            }

            int bestBucket = 0;
            for (int k = 1; k < Buckets; k++)
            {
                if (weights[k] > weights[bestBucket])
                    bestBucket = k;
            }

            double bw = weights[bestBucket];
            if (bw <= 1e-6) return Color.FromRgb(255, 60, 90);

            return Color.FromRgb(
                (byte)Math.Clamp(sumR[bestBucket] / bw, 0, 255),
                (byte)Math.Clamp(sumG[bestBucket] / bw, 0, 255),
                (byte)Math.Clamp(sumB[bestBucket] / bw, 0, 255));
        }

        public static Color Lighten(Color c)
        {
            RgbToHsv(c.R, c.G, c.B, out double h, out double s, out double v);
            if (s <= 0.12)
            {
                // When desaturated / grayscale, return vivid cyan rather than pure white
                return Color.FromRgb(56, 189, 248);
            }
            s = Math.Clamp(s, 0.50, 0.85);
            v = Math.Clamp(v, 0.65, 0.85);
            return HsvToRgb(h, s, v);
        }

        private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rf = r / 255.0;
            double gf = g / 255.0;
            double bf = b / 255.0;

            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;

            v = max;
            s = max <= 1e-6 ? 0.0 : delta / max;

            if (delta <= 1e-6)
            {
                h = 0.0;
            }
            else if (max == rf)
            {
                h = 60.0 * (((gf - bf) / delta) % 6.0);
            }
            else if (max == gf)
            {
                h = 60.0 * (((bf - rf) / delta) + 2.0);
            }
            else
            {
                h = 60.0 * (((rf - gf) / delta) + 4.0);
            }

            if (h < 0.0) h += 360.0;
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1.0 - Math.Abs(((h / 60.0) % 2.0) - 1.0));
            double m = v - c;

            double r1, g1, b1;
            if (h < 60.0) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120.0) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180.0) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240.0) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300.0) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            return Color.FromRgb(
                (byte)Math.Clamp(Math.Round((r1 + m) * 255.0), 0, 255),
                (byte)Math.Clamp(Math.Round((g1 + m) * 255.0), 0, 255),
                (byte)Math.Clamp(Math.Round((b1 + m) * 255.0), 0, 255));
        }
    }
}
