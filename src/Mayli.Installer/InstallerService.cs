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
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
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
                onProgress?.Invoke(90, $"Saving weather configuration for {options.City}...");
                try
                {
                    ConfigureInitialWeather(options);
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

        public static void ConfigureInitialWeather(InstallOptions options)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string mayliDataDir = System.IO.Path.Combine(appData, "Mayli");
            Directory.CreateDirectory(mayliDataDir);

            string weatherFile = System.IO.Path.Combine(mayliDataDir, "weather_prefs.json");

            string cityName = string.IsNullOrWhiteSpace(options.City) ? "Tehran" : options.City;
            string country = string.IsNullOrWhiteSpace(options.Country) ? "Iran" : options.Country;
            double lat = options.Latitude;
            double lon = options.Longitude;

            // Fallback default coordinates if lat/lon were not provided
            if (lat == 0 && lon == 0)
            {
                lat = 35.6892;
                lon = 51.3890;
            }

            var prefs = new
            {
                City = cityName,
                Country = country,
                Latitude = lat,
                Longitude = lon,
                UseFahrenheit = false,
                IsConfigured = true,
                RecentCities = new object[]
                {
                    new { Name = cityName, Country = country, Latitude = lat, Longitude = lon }
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
