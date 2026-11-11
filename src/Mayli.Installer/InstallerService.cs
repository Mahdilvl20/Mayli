using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Mayli.Installer
{
    public class InstallOptions
    {
        public string Path { get; set; }
        public string City { get; set; }
        public bool CreateShortcut { get; set; }
        public bool AutoStartup { get; set; }
        public bool EnableMica { get; set; }
    }

    public static class InstallerService
    {
        public static string GetDefaultInstallPath()
        {
            string localPrograms = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Mayli");
            return localPrograms;
        }

        public static async Task ExecuteInstallAsync(InstallOptions options, Action<int, string> onProgress)
        {
            await Task.Run(() =>
            {
                onProgress?.Invoke(10, "Preparing installation directory...");
                string targetDir = string.IsNullOrWhiteSpace(options.Path) ? GetDefaultInstallPath() : options.Path;
                Directory.CreateDirectory(targetDir);

                // Stop any running Mayli instances
                try
                {
                    foreach (var proc in Process.GetProcessesByName("Mayli"))
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                }
                catch { }

                onProgress?.Invoke(25, "Extracting application binaries...");

                // 1. Extract from Embedded payload or copy from local publish dir
                bool extracted = ExtractEmbeddedPayload(targetDir, onProgress);
                if (!extracted)
                {
                    // Fallback to local build path if testing without embedded zip
                    CopyFromLocalBuild(targetDir, onProgress);
                }

                string exePath = System.IO.Path.Combine(targetDir, "Mayli.exe");

                // 2. Desktop & Start Menu Shortcuts
                if (options.CreateShortcut && File.Exists(exePath))
                {
                    onProgress?.Invoke(70, "Creating desktop & start menu shortcuts...");
                    try
                    {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        CreateShortcut(System.IO.Path.Combine(desktopPath, "Mayli.lnk"), exePath);

                        string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        CreateShortcut(System.IO.Path.Combine(startMenu, "Mayli.lnk"), exePath);
                    }
                    catch { }
                }

                // 3. Auto Startup Registry Key
                if (options.AutoStartup && File.Exists(exePath))
                {
                    onProgress?.Invoke(80, "Configuring Windows startup registry...");
                    try
                    {
                        using var rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                        rk?.SetValue("Mayli", $"\"{exePath}\"");
                    }
                    catch { }
                }

                // 4. Save Initial Weather & Application Preferences
                onProgress?.Invoke(90, $"Saving initial weather configuration for {options.City}...");
                try
                {
                    ConfigureInitialWeather(options.City);
                }
                catch { }

                onProgress?.Invoke(100, "Installation complete!");
            });
        }

        private static bool ExtractEmbeddedPayload(string targetDir, Action<int, string> onProgress)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = null;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = name;
                    break;
                }
            }

            if (resourceName == null) return false;

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return false;

            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
            int total = archive.Entries.Count;
            int current = 0;

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    // Directory entry
                    Directory.CreateDirectory(System.IO.Path.Combine(targetDir, entry.FullName));
                    continue;
                }

                string destinationPath = System.IO.Path.Combine(targetDir, entry.FullName);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath));
                entry.ExtractToFile(destinationPath, overwrite: true);

                current++;
                int pct = 25 + (int)((current / (float)total) * 40);
                onProgress?.Invoke(pct, $"Unpacking {entry.Name}...");
            }

            return true;
        }

        private static void CopyFromLocalBuild(string targetDir, Action<int, string> onProgress)
        {
            // Search known release output folders
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] possibleDirs = new[]
            {
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, @"..\..\..\..\Mayli\bin\Release\net10.0-windows10.0.19041.0\publish")),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, @"..\..\..\..\Mayli\bin\Release\net10.0-windows10.0.19041.0")),
                @"F:\Vayli_Project\src\Mayli\bin\Release\net10.0-windows10.0.19041.0\publish",
                @"F:\Vayli_Project\src\Mayli\bin\Release\net10.0-windows10.0.19041.0"
            };

            string sourceDir = null;
            foreach (var d in possibleDirs)
            {
                if (Directory.Exists(d) && File.Exists(System.IO.Path.Combine(d, "Mayli.exe")))
                {
                    sourceDir = d;
                    break;
                }
            }

            if (sourceDir == null) return;

            var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
            int current = 0;
            foreach (var file in files)
            {
                string rel = System.IO.Path.GetRelativePath(sourceDir, file);
                string dest = System.IO.Path.Combine(targetDir, rel);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                File.Copy(file, dest, overwrite: true);

                current++;
                int pct = 25 + (int)((current / (float)files.Length) * 40);
                onProgress?.Invoke(pct, $"Copying {System.IO.Path.GetFileName(file)}...");
            }
        }

        public static void CreateShortcut(string shortcutPath, string targetPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath);
                    shortcut.IconLocation = targetPath + ",0";
                    shortcut.Description = "Mayli - The Dynamic Notch for Windows";
                    shortcut.Save();
                }
            }
            catch { }
        }

        public static void ConfigureInitialWeather(string city)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string mayliDataDir = System.IO.Path.Combine(appData, "Mayli");
            Directory.CreateDirectory(mayliDataDir);

            string weatherFile = System.IO.Path.Combine(mayliDataDir, "weather_prefs.json");

            // Coordinates dictionary for popular cities
            var coords = new Dictionary<string, (double Lat, double Lon, string Country)>(StringComparer.OrdinalIgnoreCase)
            {
                { "London", (51.5074, -0.1278, "United Kingdom") },
                { "New York", (40.7128, -74.0060, "United States") },
                { "Tokyo", (35.6762, 139.6503, "Japan") },
                { "Paris", (48.8566, 2.3522, "France") },
                { "Dubai", (25.2048, 55.2708, "United Arab Emirates") },
                { "Tehran", (35.6892, 51.3890, "Iran") },
                { "Isfahan", (32.6546, 51.6680, "Iran") },
                { "Mashhad", (36.2605, 59.6168, "Iran") },
                { "Shiraz", (29.5918, 52.5837, "Iran") },
                { "Tabriz", (38.0800, 46.2919, "Iran") }
            };

            double lat = 51.5074;
            double lon = -0.1278;
            string country = "";

            if (!string.IsNullOrWhiteSpace(city) && coords.TryGetValue(city, out var c))
            {
                lat = c.Lat;
                lon = c.Lon;
                country = c.Country;
            }

            var prefs = new
            {
                City = string.IsNullOrWhiteSpace(city) ? "London" : city,
                Country = country,
                Latitude = lat,
                Longitude = lon,
                UseFahrenheit = false,
                IsConfigured = true,
                RecentCities = new object[]
                {
                    new { Name = string.IsNullOrWhiteSpace(city) ? "London" : city, Country = country, Latitude = lat, Longitude = lon }
                }
            };

            string json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(weatherFile, json);
        }

        public static void LaunchInstalledApp(string targetDir)
        {
            string exePath = System.IO.Path.Combine(targetDir, "Mayli.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                });
            }
        }
    }
}
