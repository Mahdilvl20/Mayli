using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using WindowsBuddy.Services;

namespace WindowsBuddy.Core
{
    public static class LogWriter
    {
        private const long MaxBytes = 5 * 1024 * 1024;
        private static readonly object _fileLock = new object();
        private static readonly ConcurrentQueue<KeyValuePair<string, string>> _queue = new();

        public static string PathFor(string category)
        {
            string logsDir = Path.Combine(AppPaths.DataDir, "logs");
            Directory.CreateDirectory(logsDir);
            return Path.Combine(logsDir, $"{category}.log");
        }

        public static void Enqueue(string category, string message)
        {
            _queue.Enqueue(new KeyValuePair<string, string>(category, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}"));
            Pump();
        }

        public static void WriteNow(string category, string message)
        {
            Append(category, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        public static void Pump()
        {
            while (_queue.TryDequeue(out var entry))
            {
                Append(entry.Key, entry.Value);
            }
        }

        private static void Append(string category, string line)
        {
            try
            {
                lock (_fileLock)
                {
                    string path = PathFor(category);
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxBytes)
                    {
                        File.WriteAllText(path, line + Environment.NewLine);
                    }
                    else
                    {
                        File.AppendAllText(path, line + Environment.NewLine);
                    }
                }
            }
            catch
            {
                // Ignore logging IO errors
            }
        }
    }

    public static class EmbeddedAssetLoader
    {
        private static readonly ConcurrentDictionary<string, BitmapImage> _bitmapCache = new();

        public static Stream OpenResourceStream(string logicalSuffix)
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(logicalSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return asm.GetManifestResourceStream(name);
                }
            }
            string diskFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", logicalSuffix);
            if (File.Exists(diskFallback))
            {
                return File.OpenRead(diskFallback);
            }
            return null;
        }

        public static BitmapImage LoadBitmap(string logicalSuffix)
        {
            return _bitmapCache.GetOrAdd(logicalSuffix, key =>
            {
                using var stream = OpenResourceStream(key);
                if (stream == null) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            });
        }

        public static string ReadResourceText(string logicalSuffix)
        {
            using var stream = OpenResourceStream(logicalSuffix);
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
