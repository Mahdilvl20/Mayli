using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsBuddy.Services
{
    /// <summary>
    /// Real-time WASAPI Loopback 2048-point FFT Audio Spectrum Analyzer (28 logarithmic bands)
    /// and Album Art Dominant Color Extractor matching Vayli's native music visualizer.
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
        private bool _live;
        private volatile bool _running;
        private volatile bool _isActive;
        private Thread _thread;

        private AudioSpectrumService()
        {
            for (int i = 0; i < FftSize; i++)
            {
                _window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftSize - 1))));
            }
            Start();
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
        /// Symmetrical center-outward band mapping matching CapsuleWindow.MapBands(bands, bars, n).
        /// Places low sub-bass bands in the center and mid/high bands symmetrically on the left and right wings.
        /// </summary>
        public static void MapBands(float[] bands, float[] bars, int n)
        {
            if (bands == null || bars == null || n <= 0) return;
            int bandCount = bands.Length;
            double half = (n - 1) * 0.5;
            if (half <= 0) half = 1.0;

            for (int i = 0; i < n && i < bars.Length; i++)
            {
                double norm = Math.Abs(i - half) / half; // 0 at center, 1 at outer edges
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
                // Gentle organic motion when audio is playing softly
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

        private void CaptureLoop()
        {
            while (_running)
            {
                while (_running && !_isActive)
                {
                    Thread.Sleep(300);
                }
                if (!_running) break;

                IMMDeviceEnumerator enumerator = null;
                IMMDevice device = null;
                IAudioClient client = null;
                IAudioCaptureClient capture = null;
                IntPtr mixFormatPtr = IntPtr.Zero;

                try
                {
                    var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"); // MMDeviceEnumerator
                    var type = Type.GetTypeFromCLSID(clsid);
                    if (type == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type);
                    if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device == null)
                    {
                        Thread.Sleep(800);
                        continue;
                    }

                    var iidAudioClient = typeof(IAudioClient).GUID;
                    if (device.Activate(ref iidAudioClient, CLSCTX_ALL, IntPtr.Zero, out object clientObj) != 0 || clientObj == null)
                    {
                        Thread.Sleep(800);
                        continue;
                    }
                    client = (IAudioClient)clientObj;

                    if (client.GetMixFormat(out mixFormatPtr) != 0 || mixFormatPtr == IntPtr.Zero)
                    {
                        Thread.Sleep(800);
                        continue;
                    }

                    int channels = Marshal.ReadInt16(mixFormatPtr, 2);
                    int sampleRate = Marshal.ReadInt32(mixFormatPtr, 4);
                    int bitsPerSample = Marshal.ReadInt16(mixFormatPtr, 14);
                    int formatTag = (ushort)Marshal.ReadInt16(mixFormatPtr, 0);
                    bool isFloat = formatTag == 3; // WAVE_FORMAT_IEEE_FLOAT
                    if (formatTag == 0xFFFE && Marshal.ReadInt16(mixFormatPtr, 16) >= 22)
                    {
                        // WAVE_FORMAT_EXTENSIBLE SubFormat first 4 bytes: 0x00000003 = IEEE_FLOAT
                        int subFormat = Marshal.ReadInt32(mixFormatPtr, 24);
                        isFloat = subFormat == 3;
                    }

                    if (client.Initialize(0, AUDCLNT_STREAMFLAGS_LOOPBACK, 500000, 0, mixFormatPtr, IntPtr.Zero) != 0)
                    {
                        Thread.Sleep(800);
                        continue;
                    }

                    var iidCapture = typeof(IAudioCaptureClient).GUID;
                    if (client.GetService(ref iidCapture, out object capObj) != 0 || capObj == null)
                    {
                        Thread.Sleep(800);
                        continue;
                    }
                    capture = (IAudioCaptureClient)capObj;

                    client.Start();

                    int bytesPerSample = Math.Max(1, bitsPerSample / 8);
                    int frameStride = Math.Max(1, channels * bytesPerSample);
                    DateTime lastActiveSoundUtc = DateTime.MinValue;

                    while (_running && _isActive)
                    {
                        bool isStillLive = (DateTime.UtcNow - lastActiveSoundUtc).TotalMilliseconds < 450;
                        Thread.Sleep(isStillLive ? 25 : 85);
                        bool gotSamples = false;

                        while (capture.GetNextPacketSize(out uint packetFrames) == 0 && packetFrames > 0)
                        {
                            if (capture.GetBuffer(out IntPtr dataPtr, out uint numFrames, out uint flags, out _, out _) != 0)
                                break;

                            try
                            {
                                bool silent = (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0 || dataPtr == IntPtr.Zero;
                                for (uint f = 0; f < numFrames; f++)
                                {
                                    float s = 0f;
                                    if (!silent)
                                    {
                                        IntPtr framePtr = dataPtr + (int)(f * frameStride);
                                        if (isFloat && bitsPerSample == 32)
                                        {
                                            float ch0 = Marshal.PtrToStructure<float>(framePtr);
                                            float ch1 = channels > 1 ? Marshal.PtrToStructure<float>(framePtr + 4) : ch0;
                                            s = (ch0 + ch1) * 0.5f;
                                        }
                                        else if (bitsPerSample == 16)
                                        {
                                            short ch0 = Marshal.ReadInt16(framePtr);
                                            short ch1 = channels > 1 ? Marshal.ReadInt16(framePtr + 2) : ch0;
                                            s = ((ch0 + ch1) * 0.5f) / 32768f;
                                        }
                                    }
                                    _ring[_ringPos] = s;
                                    _ringPos = (_ringPos + 1) & (RingSize - 1);
                                }
                                gotSamples = true;
                            }
                            finally
                            {
                                capture.ReleaseBuffer(numFrames);
                            }
                        }

                        if (gotSamples)
                        {
                            Analyze(sampleRate > 0 ? sampleRate : 48000, ref lastActiveSoundUtc);
                        }
                        else
                        {
                            // Decay smoothly when no new packet arrives
                            bool stillLive = (DateTime.UtcNow - lastActiveSoundUtc).TotalMilliseconds < 350;
                            lock (_lock)
                            {
                                for (int b = 0; b < Bands; b++)
                                {
                                    _smooth[b] *= 0.82f;
                                    _levels[b] = _smooth[b] < 0.01f ? 0f : _smooth[b];
                                }
                                _live = stillLive;
                            }
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(1000);
                }
                finally
                {
                    if (mixFormatPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(mixFormatPtr);
                    try { client?.Stop(); } catch { }
                    if (capture != null) Marshal.ReleaseComObject(capture);
                    if (client != null) Marshal.ReleaseComObject(client);
                    if (device != null) Marshal.ReleaseComObject(device);
                    if (enumerator != null) Marshal.ReleaseComObject(enumerator);
                }
            }
        }

        private void Analyze(int sampleRate, ref DateTime lastActiveSoundUtc)
        {
            double energySum = 0.0;
            int pos = _ringPos;
            for (int i = 0; i < FftSize; i++)
            {
                int idx = (pos - FftSize + i) & (RingSize - 1);
                float sample = _ring[idx];
                energySum += sample * sample;
                _re[i] = sample * _window[i];
                _im[i] = 0f;
            }

            double rms = Math.Sqrt(energySum / FftSize);
            bool hasSound = rms > 0.0005;
            if (hasSound)
            {
                lastActiveSoundUtc = DateTime.UtcNow;
            }
            else
            {
                lock (_lock)
                {
                    for (int b = 0; b < Bands; b++)
                    {
                        _smooth[b] *= 0.72f;
                        _levels[b] = _smooth[b] < 0.005f ? 0f : _smooth[b];
                    }
                    _live = false;
                }
                return;
            }

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
                double tilt = bb.Tilt;
                float db = (float)(20.0 * Math.Log10(avgMag * tilt + 1e-9));
                if (db > maxBandDb) maxBandDb = db;
                _target[b] = db;
            }

            if (maxBandDb > _peakDb)
                _peakDb = _peakDb * 0.80f + maxBandDb * 0.20f;
            else
                _peakDb = Math.Max(-40f, _peakDb * 0.992f + maxBandDb * 0.008f);

            float floorDb = _peakDb - 42f;
            lock (_lock)
            {
                for (int b = 0; b < Bands; b++)
                {
                    // Suppress sub-bass DC rumble in lowest 5 bands so center dots stay clean like the video
                    float scale = b < 5 ? (b / 5.0f) * 0.25f : 1.0f;
                    float norm = hasSound ? Math.Clamp((_target[b] - floorDb) / 42f, 0f, 1f) * scale : 0f;
                    float rate = norm > _smooth[b] ? 0.60f : 0.22f;
                    _smooth[b] += (norm - _smooth[b]) * rate;
                    _levels[b] = _smooth[b];
                }
                _live = (DateTime.UtcNow - lastActiveSoundUtc).TotalMilliseconds < 350;
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
            int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
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
