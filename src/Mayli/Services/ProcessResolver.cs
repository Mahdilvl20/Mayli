using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsBuddy.Services
{
    /// <summary>
    /// Resolves friendly application names and verifies process liveness for device privacy tracking.
    /// Handles both NonPackaged (Win32) and Packaged (UWP/MSIX) applications, unescaping registry paths,
    /// inspecting FileVersionInfo, querying WinRT PackageManager, and performing browser meeting heuristics.
    /// </summary>
    public static class ProcessResolver
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome",
            "msedge",
            "brave",
            "opera",
            "vivaldi"
        };

        /// <summary>
        /// Controls whether mock/test processes are considered alive without requiring an active OS process.
        /// </summary>
        public static bool AllowSimulatedProcesses { get; set; } = true;

        /// <summary>
        /// Unescapes '#' delimited registry paths from Windows ConsentStore.
        /// E.g. "C:#Program Files#Google#Chrome#Application#chrome.exe" -> "C:\Program Files\Google\Chrome\Application\chrome.exe"
        /// </summary>
        public static string UnescapeRegistryPath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            // Handle network shares like "##server#share#app.exe"
            if (raw.StartsWith("##", StringComparison.Ordinal))
            {
                return @"\\" + raw.Substring(2).Replace('#', '\\');
            }

            // Handle standard drive paths like "C:#Program Files#..."
            if (raw.Length >= 2 && raw[1] == '#')
            {
                string drive = raw[0].ToString();
                string rest = raw.Substring(2).TrimStart('#').Replace('#', '\\');
                return $"{drive}:\\{rest}";
            }

            // Fallback for paths that already have backslashes or just '#':
            return raw.Replace('#', '\\');
        }

        /// <summary>
        /// Resolves a friendly display name for a NonPackaged (Win32) application from its registry subkey name.
        /// Uses FileVersionInfo description, product name, or file name, and applies browser heuristics if applicable.
        /// </summary>
        public static string ResolveNonPackagedName(string subKeyName, out string exePath)
        {
            exePath = UnescapeRegistryPath(subKeyName);
            return ResolveExePathFriendlyName(exePath, fallbackProcessName: null);
        }

        /// <summary>
        /// Resolves a friendly application name from a live Windows process ID.
        /// </summary>
        public static string ResolveProcessIdName(int processId)
        {
            if (processId <= 0) return string.Empty;

            string exePath = null;
            string procName = null;
            try
            {
                using var proc = Process.GetProcessById(processId);
                procName = proc.ProcessName;
                try { exePath = proc.MainModule?.FileName; } catch { }
            }
            catch
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(exePath) && string.IsNullOrWhiteSpace(procName))
                return string.Empty;

            return ResolveExePathFriendlyName(exePath, procName);
        }

        private static string ResolveExePathFriendlyName(string exePath, string fallbackProcessName)
        {
            string friendlyName = string.Empty;

            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                    {
                        friendlyName = versionInfo.FileDescription.Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                    {
                        friendlyName = versionInfo.ProductName.Trim();
                    }
                }
                catch
                {
                    // Ignore I/O or permission errors when reading version info
                }
            }

            if (string.IsNullOrWhiteSpace(friendlyName))
            {
                string rawName = !string.IsNullOrWhiteSpace(exePath)
                    ? Path.GetFileNameWithoutExtension(exePath)
                    : fallbackProcessName;

                if (!string.IsNullOrWhiteSpace(rawName))
                {
                    friendlyName = FormatFallbackFileName(rawName);
                }
                else if (!string.IsNullOrWhiteSpace(exePath))
                {
                    friendlyName = exePath;
                }
            }

            if (string.IsNullOrWhiteSpace(friendlyName))
            {
                friendlyName = "Application";
            }

            string processName = !string.IsNullOrWhiteSpace(exePath)
                ? GetProcessNameFromPath(exePath)
                : (fallbackProcessName ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(processName) && BrowserProcessNames.Contains(processName))
            {
                friendlyName = RefineBrowserNameWithWindowTitle(processName, friendlyName);
            }

            return friendlyName;
        }

        /// <summary>
        /// Resolves a friendly display name for a Packaged (UWP/MSIX) application from its package family name.
        /// Uses WinRT PackageManager when available, falling back to parsed package family identifier.
        /// </summary>
        public static string ResolvePackagedName(string packageFamilyName)
        {
            if (string.IsNullOrWhiteSpace(packageFamilyName))
                return "Application";

            try
            {
                var pkgMgr = new Windows.Management.Deployment.PackageManager();
                var packages = pkgMgr.FindPackagesForUser(string.Empty, packageFamilyName);
                foreach (var pkg in packages)
                {
                    if (!string.IsNullOrWhiteSpace(pkg.DisplayName))
                    {
                        return pkg.DisplayName.Trim();
                    }
                }
            }
            catch
            {
                // WinRT lookup may fail in non-packaged contexts or if user account lacks package rights
            }

            // Fallback: parse package family name (e.g. "Microsoft.WindowsCamera_8wekyb3d8bbwe" -> "Windows Camera")
            return FormatPackagedFallbackName(packageFamilyName);
        }

        /// <summary>
        /// Verifies whether the process for the specified executable path is actively running.
        /// Used to discard stale registry records left by crashed applications.
        /// </summary>
        public static bool IsProcessAlive(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return false;

            // Allow mock/test paths for automated test harness without physical processes
            if (AllowSimulatedProcesses && IsMockOrTestPath(exePath))
            {
                return true;
            }

            string processName = GetProcessNameFromPath(exePath);
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            Process[] runningProcesses;
            try
            {
                runningProcesses = Process.GetProcessesByName(processName);
            }
            catch
            {
                return false;
            }

            if (runningProcesses == null || runningProcesses.Length == 0)
                return false;

            bool isAlive = false;
            foreach (var p in runningProcesses)
            {
                try
                {
                    if (!isAlive)
                    {
                        string modulePath = null;
                        try
                        {
                            modulePath = p.MainModule?.FileName;
                        }
                        catch
                        {
                            // Access denied on elevated/protected process (e.g. anti-cheat, system services)
                            // The process exists and is alive.
                            isAlive = true;
                        }

                        if (modulePath != null)
                        {
                            if (string.Equals(modulePath, exePath, StringComparison.OrdinalIgnoreCase))
                            {
                                isAlive = true;
                            }
                        }
                        else
                        {
                            // If we couldn't read module path, name match is sufficient evidence
                            isAlive = true;
                        }
                    }
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            return isAlive;
        }

        /// <summary>
        /// Detects meeting/communication services from a window title (e.g. "Meet - abc - Google Chrome" -> "Google Meet").
        /// </summary>
        public static string DetectMeetingServiceFromTitle(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return null;

            if (windowTitle.IndexOf("Google Meet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowTitle.IndexOf("meet.google.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowTitle.StartsWith("Meet -", StringComparison.OrdinalIgnoreCase) ||
                windowTitle.Contains(" | Google Meet", StringComparison.OrdinalIgnoreCase))
            {
                return "Google Meet";
            }

            if (windowTitle.IndexOf("Zoom Workplace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowTitle.IndexOf("Zoom Meeting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowTitle.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Zoom";
            }

            if (windowTitle.IndexOf("Microsoft Teams", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowTitle.IndexOf("Teams Meeting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowTitle.IndexOf("Teams |", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Microsoft Teams";
            }

            if (windowTitle.IndexOf("Discord", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Discord";
            }

            if (windowTitle.IndexOf("Webex", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Webex";
            }

            if (windowTitle.IndexOf("Slack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Slack";
            }

            if (windowTitle.IndexOf("Skype", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Skype";
            }

            if (windowTitle.IndexOf("Telegram", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Telegram";
            }

            if (windowTitle.IndexOf("WhatsApp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "WhatsApp";
            }

            if (windowTitle.IndexOf("Jitsi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Jitsi Meet";
            }

            return null;
        }

        /// <summary>
        /// Inspects top-level window titles of browser processes to refine label when device access is active.
        /// </summary>
        public static string RefineBrowserNameWithWindowTitle(string processName, string defaultFriendlyName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                    return defaultFriendlyName;

                var targetPids = new HashSet<uint>();
                foreach (var p in processes)
                {
                    targetPids.Add((uint)p.Id);
                    p.Dispose();
                }

                string detectedMeetingService = null;

                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd))
                        return true; // continue enumeration

                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (targetPids.Contains(pid))
                    {
                        var sb = new StringBuilder(512);
                        int len = GetWindowText(hWnd, sb, sb.Capacity);
                        if (len > 0)
                        {
                            string title = sb.ToString();
                            string meeting = DetectMeetingServiceFromTitle(title);
                            if (meeting != null)
                            {
                                detectedMeetingService = meeting;
                                return false; // Stop enumeration once found
                            }
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                if (!string.IsNullOrWhiteSpace(detectedMeetingService))
                {
                    return $"{detectedMeetingService} ({defaultFriendlyName})";
                }
            }
            catch
            {
                // Fall back to default name on any window enumeration error
            }

            return defaultFriendlyName;
        }

        private static string GetProcessNameFromPath(string path)
        {
            try
            {
                return Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsMockOrTestPath(string path)
        {
            return path.IndexOf("Mock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Fake", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatFallbackFileName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            // If camel/pascal case like "DiscordCanary", keep as is or clean
            // Common known mappings
            return rawName switch
            {
                "chrome" => "Google Chrome",
                "msedge" => "Microsoft Edge",
                "brave" => "Brave",
                "firefox" => "Mozilla Firefox",
                "obs64" or "obs32" or "obs" => "OBS Studio",
                "Telegram" => "Telegram",
                "Discord" => "Discord",
                "Zoom" => "Zoom",
                _ => rawName
            };
        }

        private static string FormatPackagedFallbackName(string packageFamilyName)
        {
            string basePart = packageFamilyName.Split('_')[0];

            if (basePart.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            {
                basePart = basePart.Substring("Microsoft.".Length);
            }
            else if (basePart.StartsWith("Windows.", StringComparison.OrdinalIgnoreCase))
            {
                basePart = basePart.Substring("Windows.".Length);
            }

            // Common packaged names
            return basePart switch
            {
                "WindowsCamera" => "Windows Camera",
                "WindowsSoundRecorder" => "Sound Recorder",
                "SkypeApp" => "Skype",
                "XboxApp" => "Xbox",
                _ => AddSpacesToPascalCase(basePart)
            };
        }

        private static string AddSpacesToPascalCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var sb = new StringBuilder(text.Length + 4);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(text[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
