using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace WindowsBuddy.Services
{
    /// <summary>
    /// Monitors real-time system screen brightness changes cleanly without polling overhead on WMI panels,
    /// with an automatic DDC/CI (dxva2.dll) fallback for supported external desktop monitors.
    /// </summary>
    public sealed class ScreenBrightnessService : IDisposable
    {
        private static readonly Lazy<ScreenBrightnessService> _lazy = new(() => new ScreenBrightnessService());
        public static ScreenBrightnessService Instance => _lazy.Value;

        private const int WbemErrTimedout = unchecked((int)0x80043001);

        private readonly Thread _workerThread;
        private volatile bool _disposed;
        private int _brightnessPercent = 100;

        public int BrightnessPercent => _brightnessPercent;
        public bool IsSupported { get; private set; }
        public event Action<int> BrightnessChanged;

        private ScreenBrightnessService()
        {
            _workerThread = new Thread(MonitorLoop)
            {
                Name = "Mayli.BrightnessMonitor",
                IsBackground = true
            };
            _workerThread.SetApartmentState(ApartmentState.MTA);
                        _workerThread.Start();
        }

        private void MonitorLoop()
        {
            while (!_disposed)
            {
                try
                {
                    if (TryRunWmiBrightnessMonitor())
                        continue;

                    if (TryRunDdcCiFallbackMonitor())
                        continue;

                    // Neither internal WMI panel nor DDC/CI monitor is supported on this display setup
                    break;
                }
                catch
                {
                    if (_disposed) break;
                    Thread.Sleep(2500);
                }
            }
        }

        private bool TryRunWmiBrightnessMonitor()
        {
            object locatorObj = null;
            object servicesObj = null;
            object instancesObj = null;
            object eventSourceObj = null;

            try
            {
                Type locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
                if (locatorType == null) return false;

                locatorObj = Activator.CreateInstance(locatorType);
                if (locatorObj == null) return false;

                dynamic locator = locatorObj;
                servicesObj = locator.ConnectServer(".", "root\\wmi");
                if (servicesObj == null) return false;

                dynamic services = servicesObj;
                instancesObj = services.InstancesOf("WmiMonitorBrightness");

                bool foundActive = false;
                foreach (dynamic item in (dynamic)instancesObj)
                {
                    try
                    {
                        dynamic props = item.Properties_;
                        dynamic activeProp = props.Item("Active");
                        bool active = activeProp != null && Convert.ToBoolean(activeProp.Value);
                        dynamic curProp = props.Item("CurrentBrightness");
                        int curPct = curProp != null ? Math.Clamp(Convert.ToInt32(curProp.Value), 0, 100) : 100;

                        if (active || !foundActive)
                        {
                            _brightnessPercent = curPct;
                            foundActive = true;
                        }

                        ReleaseComSafe(curProp);
                        ReleaseComSafe(activeProp);
                        ReleaseComSafe(props);
                    }
                    finally
                    {
                        ReleaseComSafe(item);
                    }
                }

                ReleaseComSafe(instancesObj);
                instancesObj = null;

                if (!foundActive)
                    return false;

                IsSupported = true;
                eventSourceObj = services.ExecNotificationQuery("SELECT * FROM WmiMonitorBrightnessEvent");
                dynamic eventSource = eventSourceObj;

                WindowsBuddy.Core.LogWriter.WriteNow("brightness", $"WMI brightness watcher active. Initial={_brightnessPercent}%");

                while (!_disposed)
                {
                    object evObj = null;
                    try
                    {
                        evObj = eventSource.NextEvent(1800);
                        if (evObj != null)
                        {
                            dynamic ev = evObj;
                            dynamic props = ev.Properties_;
                            dynamic bProp = props.Item("Brightness");
                            if (bProp != null && bProp.Value != null)
                            {
                                int newPct = Math.Clamp(Convert.ToInt32(bProp.Value), 0, 100);
                                _brightnessPercent = newPct;
                                BrightnessChanged?.Invoke(newPct);
                            }
                            ReleaseComSafe(bProp);
                            ReleaseComSafe(props);
                        }
                    }
                    catch (COMException cex) when (cex.ErrorCode == WbemErrTimedout || cex.HResult == WbemErrTimedout)
                    {
                        // Normal idle wait timeout; continue loop
                    }
                    catch (COMException)
                    {
                        if (_disposed) break;
                        Thread.Sleep(1500);
                        break;
                    }
                    finally
                    {
                        ReleaseComSafe(evObj);
                    }
                }

                return true;
            }
            catch (COMException cex)
            {
                WindowsBuddy.Core.LogWriter.WriteNow("brightness", $"WMI brightness unavailable (0x{cex.HResult:X8})");
                return false;
            }
            catch (Exception ex)
            {
                WindowsBuddy.Core.LogWriter.WriteNow("brightness", $"WMI monitor error: {ex.Message}");
                return false;
            }
            finally
            {
                ReleaseComSafe(eventSourceObj);
                ReleaseComSafe(instancesObj);
                ReleaseComSafe(servicesObj);
                ReleaseComSafe(locatorObj);
            }
        }

        private bool TryRunDdcCiFallbackMonitor()
        {
            if (!TryReadPrimaryMonitorDdcBrightness(out int initialPct))
                return false;

            IsSupported = true;
            _brightnessPercent = initialPct;
            WindowsBuddy.Core.LogWriter.WriteNow("brightness", $"DDC/CI fallback brightness watcher active. Initial={initialPct}%");

            while (!_disposed)
            {
                Thread.Sleep(450);
                if (_disposed) break;

                if (TryReadPrimaryMonitorDdcBrightness(out int pct))
                {
                    if (pct != _brightnessPercent)
                    {
                        _brightnessPercent = pct;
                        BrightnessChanged?.Invoke(pct);
                    }
                }
                else
                {
                    break;
                }
            }

            return true;
        }

        private static bool TryReadPrimaryMonitorDdcBrightness(out int percent)
        {
            percent = 100;
            try
            {
                var pt = new WindowsBuddy.Core.NativeMethods.POINT { X = 1, Y = 1 };
                IntPtr hMonitor = WindowsBuddy.Core.NativeMethods.MonitorFromPoint(pt, 1 /* MONITOR_DEFAULTTOPRIMARY */);
                if (hMonitor == IntPtr.Zero) return false;

                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                    return false;

                var monitors = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                    return false;

                try
                {
                    for (int i = 0; i < monitors.Length; i++)
                    {
                        if (GetMonitorBrightness(monitors[i].hPhysicalMonitor, out uint min, out uint cur, out uint max) && max > min)
                        {
                            percent = (int)Math.Clamp(Math.Round(((double)(cur - min) / (max - min)) * 100.0), 0, 100);
                            return true;
                        }
                    }
                }
                finally
                {
                    DestroyPhysicalMonitors(count, monitors);
                }
            }
            catch { }

            return false;
        }

        private static void ReleaseComSafe(object obj)
        {
            if (obj != null && Marshal.IsComObject(obj))
            {
                try { Marshal.ReleaseComObject(obj); }
                catch { }
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }

        #region DDC/CI Native Methods

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        #endregion
    }
}
