using System;
using System.IO;
using System.Text.Json;

namespace WindowsBuddy.Services
{
    public static class AppPaths
    {
        public const string CurrentFolderName = "Mayli";
        public const string LegacyFolderName = "Vayli";
        public const string OverrideVariable = "MAYLI_DATA_DIR";

        public static string FolderName => CurrentFolderName;

        public static string LocalAppData =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static string RoamingAppData =>
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string DataDir
        {
            get
            {
                string envOverride = Environment.GetEnvironmentVariable(OverrideVariable);
                if (!string.IsNullOrWhiteSpace(envOverride))
                {
                    Directory.CreateDirectory(envOverride);
                    return envOverride;
                }
                string dir = Path.Combine(LocalAppData, CurrentFolderName);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string OpenableDataDir => DataDir;
    }

    public static class PersistenceHelper
    {
        private static readonly object _fileLock = new object();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static event Action<string, string> SaveFailed;

        public static bool SafeWriteAllText(string path, string content, bool createBackup = true)
        {
            try
            {
                lock (_fileLock)
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string tempPath = path + ".tmp";
                    File.WriteAllText(tempPath, content);
                    if (File.Exists(path) && createBackup)
                    {
                        File.Copy(path, path + ".bak", overwrite: true);
                    }
                    File.Move(tempPath, path, overwrite: true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                SaveFailed?.Invoke(path, ex.Message);
                return false;
            }
        }

        public static string SafeReadAllText(string path)
        {
            try
            {
                lock (_fileLock)
                {
                    if (File.Exists(path))
                        return File.ReadAllText(path);
                    if (File.Exists(path + ".bak"))
                        return File.ReadAllText(path + ".bak");
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void SafeWriteJson<T>(string fileName, T model)
        {
            string path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(AppPaths.DataDir, fileName);
            string json = JsonSerializer.Serialize(model, _jsonOptions);
            SafeWriteAllText(path, json);
        }

        public static T SafeReadJson<T>(string fileName) where T : new()
        {
            string path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(AppPaths.DataDir, fileName);
            string raw = SafeReadAllText(path);
            if (string.IsNullOrWhiteSpace(raw))
                return new T();
            try
            {
                return JsonSerializer.Deserialize<T>(raw, _jsonOptions) ?? new T();
            }
            catch
            {
                return new T();
            }
        }
    }
}
