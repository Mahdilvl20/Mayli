using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        public bool SkipProcessKill { get; set; }
        public bool SkipWeatherConfig { get; set; }
    }

    public sealed class InstallManifest
    {
        public string InstallPath { get; set; }
        public string Version { get; set; }
        public string InstalledAtUtc { get; set; }
        public string DesktopShortcutPath { get; set; }
        public string StartMenuShortcutPath { get; set; }
        public string UninstallShortcutPath { get; set; }
        public string StartMenuFolderPath { get; set; }
        public string LegacyStartMenuShortcutPath { get; set; }
        public List<string> InstalledFiles { get; set; } = new List<string>();
    }

    public sealed class UninstallResult
    {
        public bool Success { get; init; }
        public string InstallDir { get; init; }
        public bool NeedsDelayedSelfDelete { get; init; }
        public List<string> PendingDeleteFiles { get; init; } = new List<string>();
        public string ErrorMessage { get; init; }
    }

    internal sealed class PendingCleanupPlan
    {
        public string TargetDirectory { get; init; }
        public List<string> FilesToDelete { get; init; } = new List<string>();
        public List<string> DirsToRemove { get; init; } = new List<string>();
    }

    public static class InstallerService
    {
        public const string UninstallRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli";
        public const string StartupRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string StartupValueName = "Mayli";
        public const string UninstallerExeName = "Mayli_Uninstall.exe";
        public const string ManifestFileName = ".mayli-install-manifest.json";
        public const string AppDisplayName = "Mayli";
        public const string AppPublisher = "Modi Studio";
        public const string AppUrl = "https://mayli.in";

        private static readonly object _cleanupLock = new object();
        private static PendingCleanupPlan _pendingCleanup;

        public static string GetDefaultInstallPath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Mayli");
        }

        public static string GetAppVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            if (ver != null)
            {
                return ver.Revision > 0
                    ? $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}"
                    : $"{ver.Major}.{ver.Minor}.{ver.Build}";
            }

            var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoAttr))
            {
                int plusIndex = infoAttr.IndexOf('+');
                return plusIndex > 0 ? infoAttr.Substring(0, plusIndex) : infoAttr;
            }

            return "1.1.67";
        }

        public static string ResolveInstalledDirectory(string explicitDir = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitDir))
            {
                return Path.GetFullPath(explicitDir.Trim().Trim('"'));
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryKeyPath, false);
                string regLocation = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(regLocation))
                {
                    return Path.GetFullPath(regLocation.Trim().Trim('"'));
                }
            }
            catch { }

            try
            {
                string currentExe = GetCurrentInstallerExecutablePath();
                if (!string.IsNullOrWhiteSpace(currentExe))
                {
                    string exeDir = Path.GetDirectoryName(currentExe);
                    if (!string.IsNullOrWhiteSpace(exeDir) &&
                        (File.Exists(Path.Combine(exeDir, ManifestFileName)) ||
                         File.Exists(Path.Combine(exeDir, "Mayli.exe"))))
                    {
                        return Path.GetFullPath(exeDir);
                    }
                }
            }
            catch { }

            return GetDefaultInstallPath();
        }

        public static async Task ExecuteInstallAsync(InstallOptions options, Action<int, string> onProgress)
        {
            await Task.Run(() =>
            {
                onProgress?.Invoke(10, "Preparing installation directory...");
                string rawTarget = string.IsNullOrWhiteSpace(options?.Path) ? GetDefaultInstallPath() : options.Path;
                string targetDir = Path.GetFullPath(rawTarget.Trim().Trim('"'));
                Directory.CreateDirectory(targetDir);

                if (options == null || !options.SkipProcessKill)
                {
                    KillRunningMayliInstances();
                }

                onProgress?.Invoke(25, "Extracting application binaries...");
                var installedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                bool extracted = ExtractEmbeddedPayload(targetDir, installedFiles, onProgress);
                if (!extracted)
                {
                    CopyFromLocalBuild(targetDir, installedFiles, onProgress);
                }

                string exePath = Path.Combine(targetDir, "Mayli.exe");

                onProgress?.Invoke(68, "Registering uninstaller & system integration...");
                string uninstallerExePath = DeployUninstaller(targetDir, installedFiles);

                var manifest = new InstallManifest
                {
                    InstallPath = targetDir,
                    Version = GetAppVersion(),
                    InstalledAtUtc = DateTime.UtcNow.ToString("O"),
                    InstalledFiles = installedFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                };

                // Desktop & Start Menu Shortcuts
                onProgress?.Invoke(74, "Creating shortcuts...");
                try
                {
                    string targetForLaunch = File.Exists(exePath) ? exePath : uninstallerExePath;
                    if (options != null && options.CreateShortcut && File.Exists(exePath))
                    {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string desktopLink = Path.Combine(desktopPath, "Mayli.lnk");
                        CreateShortcut(desktopLink, exePath);
                        manifest.DesktopShortcutPath = desktopLink;
                    }

                    string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    string startMenuFolder = Path.Combine(programsDir, AppDisplayName);
                    Directory.CreateDirectory(startMenuFolder);
                    manifest.StartMenuFolderPath = startMenuFolder;

                    if (File.Exists(targetForLaunch))
                    {
                        string startLink = Path.Combine(startMenuFolder, "Mayli.lnk");
                        CreateShortcut(startLink, targetForLaunch);
                        manifest.StartMenuShortcutPath = startLink;
                    }

                    if (File.Exists(uninstallerExePath))
                    {
                        string uninstallLink = Path.Combine(startMenuFolder, "Uninstall Mayli.lnk");
                        CreateShortcut(
                            uninstallLink,
                            uninstallerExePath,
                            $"--uninstall --install-dir \"{targetDir}\"",
                            uninstallerExePath + ",0",
                            "Uninstall Mayli from your PC");
                        manifest.UninstallShortcutPath = uninstallLink;
                    }

                    manifest.LegacyStartMenuShortcutPath = Path.Combine(programsDir, "Mayli.lnk");
                }
                catch { }

                // Auto Startup Registry Key
                if (options != null && options.AutoStartup && File.Exists(exePath))
                {
                    onProgress?.Invoke(82, "Configuring Windows startup registry...");
                    try
                    {
                        using var rk = Registry.CurrentUser.CreateSubKey(StartupRunKeyPath, true);
                        rk?.SetValue(StartupValueName, $"\"{exePath}\"");
                    }
                    catch { }
                }

                // Initial Weather & Application Preferences
                if (options != null && !options.SkipWeatherConfig)
                {
                    onProgress?.Invoke(88, $"Saving weather configuration for {options.City}...");
                    try
                    {
                        ConfigureInitialWeather(options);
                    }
                    catch { }
                }

                // Persist install manifest before calculating ARP EstimatedSize
                manifest.InstalledFiles.Add(ManifestFileName);
                string manifestPath = Path.Combine(targetDir, ManifestFileName);
                File.WriteAllText(
                    manifestPath,
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                // Register in Windows Settings -> Apps / Control Panel
                onProgress?.Invoke(94, "Registering application in Windows...");
                RegisterAddRemovePrograms(targetDir, exePath, uninstallerExePath, manifest);

                onProgress?.Invoke(100, "Installation complete!");
            });
        }

        public static Task<UninstallResult> ExecuteUninstallAsync(
            string installDir,
            bool removeUserData,
            Action<int, string> onProgress) =>
            ExecuteUninstallAsync(installDir, removeUserData, skipProcessKill: false, onProgress);

        public static async Task<UninstallResult> ExecuteUninstallAsync(
            string installDir,
            bool removeUserData,
            bool skipProcessKill,
            Action<int, string> onProgress)
        {
            return await Task.Run(() =>
            {
                string targetDir = string.IsNullOrWhiteSpace(installDir)
                    ? ResolveInstalledDirectory()
                    : Path.GetFullPath(installDir.Trim().Trim('"'));

                if (!IsSafeUninstallTarget(targetDir))
                {
                    return new UninstallResult
                    {
                        Success = false,
                        InstallDir = targetDir,
                        ErrorMessage = $"Refusing unsafe uninstall path: {targetDir}"
                    };
                }

                try
                {
                    // Move working directory away from targetDir so this process doesn't lock it
                    try { Environment.CurrentDirectory = Path.GetTempPath(); } catch { }

                    onProgress?.Invoke(15, "Closing running Mayli instances...");
                    if (!skipProcessKill)
                    {
                        KillRunningMayliInstances(targetDir);
                    }

                    InstallManifest manifest = TryLoadManifest(targetDir);

                    onProgress?.Invoke(35, "Removing Desktop & Start Menu shortcuts...");
                    RemoveAllShortcuts(manifest);

                    onProgress?.Invoke(55, "Removing Windows startup & Apps registry entries...");
                    RemoveRegistryEntries();

                    onProgress?.Invoke(75, "Removing installed application files...");
                    var pendingFiles = RemoveInstalledFiles(targetDir, manifest);
                    var remainingEmptyDirs = PruneEmptySubdirectories(targetDir);

                    if (pendingFiles.Count == 0 && Directory.Exists(targetDir))
                    {
                        TryDeleteEmptyDirectory(targetDir);
                    }

                    if (removeUserData)
                    {
                        onProgress?.Invoke(90, "Removing saved preferences and cache...");
                        RemoveUserDataDirectory();
                    }

                    lock (_cleanupLock)
                    {
                        if (pendingFiles.Count > 0 || Directory.Exists(targetDir))
                        {
                            // Include targetDir and its leftover parent folders (for rmdir without /s /q)
                            var dirsToRemove = new List<string>(remainingEmptyDirs);
                            foreach (var pendingFile in pendingFiles)
                            {
                                string parent = Path.GetDirectoryName(pendingFile);
                                while (!string.IsNullOrEmpty(parent) &&
                                       parent.Length > targetDir.Length &&
                                       parent.StartsWith(targetDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!dirsToRemove.Contains(parent, StringComparer.OrdinalIgnoreCase))
                                        dirsToRemove.Add(parent);
                                    parent = Path.GetDirectoryName(parent);
                                }
                            }
                            dirsToRemove = dirsToRemove
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderByDescending(d => d.Length)
                                .ToList();

                            if (IsSafeUninstallTarget(targetDir))
                            {
                                dirsToRemove.Add(targetDir);
                            }

                            _pendingCleanup = new PendingCleanupPlan
                            {
                                TargetDirectory = targetDir,
                                FilesToDelete = pendingFiles,
                                DirsToRemove = dirsToRemove
                            };
                        }
                        else
                        {
                            _pendingCleanup = null;
                        }
                    }

                    onProgress?.Invoke(100, "Mayli has been cleanly uninstalled.");
                    return new UninstallResult
                    {
                        Success = true,
                        InstallDir = targetDir,
                        NeedsDelayedSelfDelete = pendingFiles.Count > 0 || Directory.Exists(targetDir),
                        PendingDeleteFiles = pendingFiles
                    };
                }
                catch (Exception ex)
                {
                    return new UninstallResult
                    {
                        Success = false,
                        InstallDir = targetDir,
                        ErrorMessage = ex.Message
                    };
                }
            });
        }

        public static void SchedulePendingSelfDelete()
        {
            PendingCleanupPlan plan;
            lock (_cleanupLock)
            {
                plan = _pendingCleanup;
                _pendingCleanup = null;
            }

            if (plan == null) return;
            if (plan.FilesToDelete.Count == 0 && (!Directory.Exists(plan.TargetDirectory))) return;
            if (!IsSafeUninstallTarget(plan.TargetDirectory)) return;

            try
            {
                string tempDir = Path.GetTempPath();
                string cmdFile = Path.Combine(tempDir, $"mayli_uninst_{Guid.NewGuid():N}.cmd");

                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("chcp 65001 >nul");
                sb.AppendLine("for /L %%i in (1,1,15) do (");
                sb.AppendLine("  ping -n 2 127.0.0.1 >nul");
                foreach (var file in plan.FilesToDelete)
                {
                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        string escaped = file.Replace("%", "%%");
                        sb.AppendLine($"  if exist \"{escaped}\" del /f /q \"{escaped}\" >nul 2>&1");
                    }
                }
                foreach (var dir in plan.DirsToRemove)
                {
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        string escaped = dir.Replace("%", "%%");
                        sb.AppendLine($"  if exist \"{escaped}\" rmdir \"{escaped}\" >nul 2>&1");
                    }
                }
                string escapedTarget = plan.TargetDirectory.Replace("%", "%%");
                sb.AppendLine($"  if not exist \"{escapedTarget}\" goto done");
                sb.AppendLine(")");
                sb.AppendLine(":done");
                sb.AppendLine("del /f /q \"%~f0\" >nul 2>&1");

                File.WriteAllText(cmdFile, sb.ToString(), new UTF8Encoding(false));

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/d /c \"{cmdFile}\"",
                    WorkingDirectory = tempDir,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        private static string GetCurrentInstallerExecutablePath()
        {
            string procPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(procPath) &&
                !Path.GetFileNameWithoutExtension(procPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return procPath;
            }

            string localSetup = Path.Combine(AppContext.BaseDirectory, "Mayli_Setup.exe");
            return File.Exists(localSetup) ? localSetup : null;
        }

        private static string DeployUninstaller(string targetDir, HashSet<string> installedFiles)
        {
            string destExe = Path.Combine(targetDir, UninstallerExeName);
            string currentExe = GetCurrentInstallerExecutablePath();

            if (!string.IsNullOrWhiteSpace(currentExe) && File.Exists(currentExe))
            {
                if (!PathsEqual(currentExe, destExe))
                {
                    File.Copy(currentExe, destExe, overwrite: true);
                }
                installedFiles.Add(UninstallerExeName);

                // In non-single-file dev/test builds, the apphost requires sibling managed runtime files
                string srcDir = Path.GetDirectoryName(currentExe);
                if (!string.IsNullOrWhiteSpace(srcDir) &&
                    File.Exists(Path.Combine(srcDir, "Mayli_Setup.deps.json")))
                {
                    string[] sidecars =
                    {
                        "Mayli_Setup.dll",
                        "Mayli_Setup.deps.json",
                        "Mayli_Setup.runtimeconfig.json",
                        "WebView2Loader.dll",
                        "Microsoft.Web.WebView2.Core.dll",
                        "Microsoft.Web.WebView2.Wpf.dll"
                    };

                    foreach (var sidecar in sidecars)
                    {
                        string srcSidecar = Path.Combine(srcDir, sidecar);
                        string dstSidecar = Path.Combine(targetDir, sidecar);
                        if (File.Exists(srcSidecar) && !PathsEqual(srcSidecar, dstSidecar))
                        {
                            File.Copy(srcSidecar, dstSidecar, overwrite: true);
                            installedFiles.Add(sidecar);
                        }
                    }
                }
            }

            return destExe;
        }

        private static void RegisterAddRemovePrograms(
            string targetDir,
            string exePath,
            string uninstallerExePath,
            InstallManifest manifest)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryKeyPath, true);
                if (key == null) return;

                string iconTarget = File.Exists(exePath) ? exePath : uninstallerExePath;
                long totalBytes = 0;
                try
                {
                    foreach (var rel in manifest.InstalledFiles)
                    {
                        string full = Path.Combine(targetDir, rel);
                        if (File.Exists(full))
                        {
                            totalBytes += new FileInfo(full).Length;
                        }
                    }
                }
                catch { }

                int estimatedKb = (int)Math.Max(1, Math.Min(int.MaxValue, (totalBytes + 1023) / 1024));

                key.SetValue("DisplayName", AppDisplayName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", manifest.Version ?? GetAppVersion(), RegistryValueKind.String);
                key.SetValue("Publisher", AppPublisher, RegistryValueKind.String);
                key.SetValue("URLInfoAbout", AppUrl, RegistryValueKind.String);
                key.SetValue("HelpLink", AppUrl, RegistryValueKind.String);
                key.SetValue("InstallLocation", targetDir, RegistryValueKind.String);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
                key.SetValue("DisplayIcon", $"{iconTarget},0", RegistryValueKind.String);
                key.SetValue(
                    "UninstallString",
                    $"\"{uninstallerExePath}\" --uninstall --install-dir \"{targetDir}\"",
                    RegistryValueKind.String);
                key.SetValue(
                    "QuietUninstallString",
                    $"\"{uninstallerExePath}\" --uninstall --silent --install-dir \"{targetDir}\"",
                    RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", estimatedKb, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void KillRunningMayliInstances(string restrictToDir = null)
        {
            try
            {
                string normDir = string.IsNullOrWhiteSpace(restrictToDir)
                    ? null
                    : Path.GetFullPath(restrictToDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                foreach (var proc in Process.GetProcessesByName("Mayli"))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(normDir))
                        {
                            string exe = null;
                            try { exe = proc.MainModule?.FileName; } catch { }
                            if (!string.IsNullOrEmpty(exe) &&
                                !Path.GetFullPath(exe).StartsWith(normDir, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: false);
                            proc.WaitForExit(3000);
                        }
                    }
                    catch { }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch { }
        }

        private static InstallManifest TryLoadManifest(string targetDir)
        {
            try
            {
                string manifestPath = Path.Combine(targetDir, ManifestFileName);
                if (!File.Exists(manifestPath)) return null;
                return JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
            }
            catch
            {
                return null;
            }
        }

        private static void RemoveAllShortcuts(InstallManifest manifest)
        {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            var candidateShortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(desktopDir, "Mayli.lnk"),
                Path.Combine(programsDir, "Mayli.lnk"),
                Path.Combine(programsDir, AppDisplayName, "Mayli.lnk"),
                Path.Combine(programsDir, AppDisplayName, "Uninstall Mayli.lnk")
            };

            if (manifest != null)
            {
                if (!string.IsNullOrWhiteSpace(manifest.DesktopShortcutPath)) candidateShortcuts.Add(manifest.DesktopShortcutPath);
                if (!string.IsNullOrWhiteSpace(manifest.StartMenuShortcutPath)) candidateShortcuts.Add(manifest.StartMenuShortcutPath);
                if (!string.IsNullOrWhiteSpace(manifest.UninstallShortcutPath)) candidateShortcuts.Add(manifest.UninstallShortcutPath);
                if (!string.IsNullOrWhiteSpace(manifest.LegacyStartMenuShortcutPath)) candidateShortcuts.Add(manifest.LegacyStartMenuShortcutPath);
            }

            foreach (var shortcut in candidateShortcuts)
            {
                try
                {
                    if (File.Exists(shortcut))
                    {
                        File.SetAttributes(shortcut, FileAttributes.Normal);
                        File.Delete(shortcut);
                    }
                }
                catch { }
            }

            string startMenuFolder = !string.IsNullOrWhiteSpace(manifest?.StartMenuFolderPath)
                ? manifest.StartMenuFolderPath
                : Path.Combine(programsDir, AppDisplayName);

            TryDeleteEmptyDirectory(startMenuFolder);
        }

        private static void RemoveRegistryEntries()
        {
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(StartupRunKeyPath, true);
                runKey?.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryKeyPath, throwOnMissingSubKey: false);
            }
            catch { }
        }

        private static List<string> RemoveInstalledFiles(string targetDir, InstallManifest manifest)
        {
            var pending = new List<string>();
            if (!Directory.Exists(targetDir)) return pending;

            string currentExe = GetCurrentInstallerExecutablePath();
            string currentDir = !string.IsNullOrWhiteSpace(currentExe) ? Path.GetDirectoryName(currentExe) : null;
            bool runningInsideTarget = !string.IsNullOrWhiteSpace(currentDir) && PathsEqual(currentDir, targetDir);

            var filesToDelete = new List<string>();
            if (manifest?.InstalledFiles != null && manifest.InstalledFiles.Count > 0)
            {
                foreach (var rel in manifest.InstalledFiles)
                {
                    if (string.IsNullOrWhiteSpace(rel)) continue;
                    string full = Path.GetFullPath(Path.Combine(targetDir, rel));
                    if (full.StartsWith(targetDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        filesToDelete.Add(full);
                    }
                }
            }
            else if (IsDedicatedMayliFolder(targetDir))
            {
                filesToDelete.AddRange(Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories));
            }

            string manifestPath = Path.Combine(targetDir, ManifestFileName);
            if (!filesToDelete.Contains(manifestPath, StringComparer.OrdinalIgnoreCase))
            {
                filesToDelete.Add(manifestPath);
            }

            string uninstallExeInTarget = Path.Combine(targetDir, UninstallerExeName);
            if (!filesToDelete.Contains(uninstallExeInTarget, StringComparer.OrdinalIgnoreCase))
            {
                filesToDelete.Add(uninstallExeInTarget);
            }

            foreach (var file in filesToDelete.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(file)) continue;

                string fileName = Path.GetFileName(file);
                bool isSelfOrLoadedRuntime = runningInsideTarget &&
                    (PathsEqual(file, currentExe) ||
                     fileName.StartsWith("Mayli_Setup.", StringComparison.OrdinalIgnoreCase) ||
                     fileName.Equals("WebView2Loader.dll", StringComparison.OrdinalIgnoreCase));

                if (isSelfOrLoadedRuntime)
                {
                    pending.Add(file);
                    continue;
                }

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                    pending.Add(file);
                }
            }

            return pending;
        }

        private static List<string> PruneEmptySubdirectories(string targetDir)
        {
            var leftover = new List<string>();
            if (!Directory.Exists(targetDir)) return leftover;

            try
            {
                var subDirs = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length);

                foreach (var dir in subDirs)
                {
                    if (!TryDeleteEmptyDirectory(dir) && Directory.Exists(dir))
                    {
                        leftover.Add(dir);
                    }
                }
            }
            catch { }

            return leftover;
        }

        private static void RemoveUserDataDirectory()
        {
            try
            {
                string overrideDir = Environment.GetEnvironmentVariable("MAYLI_DATA_DIR");
                string dataDir = !string.IsNullOrWhiteSpace(overrideDir)
                    ? Path.GetFullPath(overrideDir)
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        AppDisplayName);

                if (IsSafeUninstallTarget(dataDir) && Directory.Exists(dataDir))
                {
                    Directory.Delete(dataDir, recursive: true);
                }
            }
            catch { }
        }

        private static bool TryDeleteEmptyDirectory(string dirPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return true;
                if (Directory.GetFileSystemEntries(dirPath).Length == 0)
                {
                    Directory.Delete(dirPath, recursive: false);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsDedicatedMayliFolder(string targetDir)
        {
            string folderName = Path.GetFileName(targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (folderName.Equals(AppDisplayName, StringComparison.OrdinalIgnoreCase)) return true;
            return File.Exists(Path.Combine(targetDir, "Mayli.exe")) &&
                   File.Exists(Path.Combine(targetDir, UninstallerExeName));
        }

        internal static bool IsSafeUninstallTarget(string fullTargetDir)
        {
            if (string.IsNullOrWhiteSpace(fullTargetDir)) return false;

            string normalized;
            try
            {
                normalized = Path.GetFullPath(fullTargetDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return false;
            }

            string root = Path.GetPathRoot(normalized)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(root) || normalized.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var protectedFolders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Path.GetTempPath()
            };

            foreach (var pf in protectedFolders)
            {
                if (string.IsNullOrWhiteSpace(pf)) continue;
                string normPf = Path.GetFullPath(pf).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (normalized.Equals(normPf, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            try
            {
                string na = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string nb = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return na.Equals(nb, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool ExtractEmbeddedPayload(
            string targetDir,
            HashSet<string> installedFiles,
            Action<int, string> onProgress)
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

            string fullTargetPrefix = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
            int total = archive.Entries.Count;
            int current = 0;

            foreach (var entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                if (!destinationPath.StartsWith(fullTargetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                entry.ExtractToFile(destinationPath, overwrite: true);

                string relPath = Path.GetRelativePath(targetDir, destinationPath);
                installedFiles.Add(relPath);

                current++;
                int pct = 25 + (int)((current / (float)Math.Max(1, total)) * 40);
                onProgress?.Invoke(pct, $"Unpacking {entry.Name}...");
            }

            return true;
        }

        private static void CopyFromLocalBuild(
            string targetDir,
            HashSet<string> installedFiles,
            Action<int, string> onProgress)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] possibleDirs = new[]
            {
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Mayli\bin\Release\net10.0-windows10.0.19041.0\publish")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Mayli\bin\Release\net10.0-windows10.0.19041.0")),
                @"F:\Vayli_Project\src\Mayli\bin\Release\net10.0-windows10.0.19041.0\publish",
                @"F:\Vayli_Project\src\Mayli\bin\Release\net10.0-windows10.0.19041.0"
            };

            string sourceDir = null;
            foreach (var d in possibleDirs)
            {
                if (Directory.Exists(d) && File.Exists(Path.Combine(d, "Mayli.exe")))
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
                string rel = Path.GetRelativePath(sourceDir, file);
                string dest = Path.Combine(targetDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest, overwrite: true);
                installedFiles.Add(rel);

                current++;
                int pct = 25 + (int)((current / (float)Math.Max(1, files.Length)) * 40);
                onProgress?.Invoke(pct, $"Copying {Path.GetFileName(file)}...");
            }
        }

        public static void CreateShortcut(string shortcutPath, string targetPath)
        {
            CreateShortcut(shortcutPath, targetPath, null, null, null);
        }

        public static void CreateShortcut(
            string shortcutPath,
            string targetPath,
            string arguments,
            string iconLocation,
            string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(arguments))
                    {
                        shortcut.Arguments = arguments;
                    }
                    shortcut.IconLocation = !string.IsNullOrWhiteSpace(iconLocation) ? iconLocation : targetPath + ",0";
                    shortcut.Description = !string.IsNullOrWhiteSpace(description)
                        ? description
                        : "Mayli - The Dynamic Notch for Windows";
                    shortcut.Save();
                }
            }
            catch { }
        }

        public static void ConfigureInitialWeather(InstallOptions options)
        {
            string overrideDir = Environment.GetEnvironmentVariable("MAYLI_DATA_DIR");
            string mayliDataDir = !string.IsNullOrWhiteSpace(overrideDir)
                ? Path.GetFullPath(overrideDir)
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Mayli");
            Directory.CreateDirectory(mayliDataDir);

            string weatherFile = Path.Combine(mayliDataDir, "weather_prefs.json");

            string cityName = string.IsNullOrWhiteSpace(options.City) ? "Tehran" : options.City;
            string country = string.IsNullOrWhiteSpace(options.Country) ? "Iran" : options.Country;
            double lat = options.Latitude;
            double lon = options.Longitude;

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
            string exePath = Path.Combine(targetDir, "Mayli.exe");
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
