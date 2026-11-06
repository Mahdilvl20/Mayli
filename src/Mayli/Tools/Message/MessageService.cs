using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace WindowsBuddy.Tools.Message
{
    public sealed class MessageService
    {
        private static readonly Lazy<MessageService> _lazy = new(() => new MessageService());
        public static MessageService Instance => _lazy.Value;

        private readonly string _settingsFilePath;
        private readonly object _lock = new();

        public MessageSettings Settings { get; private set; } = new();
        public bool IsLive { get; private set; } = false;
        public bool IsPinned { get; private set; } = false;

        public event Action<MessageSettings> FlashRequested;
        public event Action TakeDownRequested;
        public event Action StateChanged;

        private MessageService()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "Mayli", "message");
                Directory.CreateDirectory(dir);
                _settingsFilePath = Path.Combine(dir, "settings.json");
                Load();
            }
            catch
            {
                _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "message_settings.json");
            }
        }

        public void Flash(MessageSettings newSettings = null)
        {
            lock (_lock)
            {
                if (newSettings != null)
                {
                    Settings = newSettings.Clone();
                }

                IsLive = true;
                IsPinned = Settings.RepeatEnabled;
                Save();
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                FlashRequested?.Invoke(Settings);
                StateChanged?.Invoke();
            });
        }

        public void TakeDown()
        {
            lock (_lock)
            {
                IsLive = false;
                IsPinned = false;
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                TakeDownRequested?.Invoke();
                StateChanged?.Invoke();
            });
        }

        public void UpdateLive(MessageSettings settings)
        {
            lock (_lock)
            {
                Settings = settings.Clone();
                IsPinned = Settings.RepeatEnabled;
                Save();
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (IsLive)
                {
                    FlashRequested?.Invoke(Settings);
                }
                StateChanged?.Invoke();
            });
        }

        public void UpdateSettings(MessageSettings settings)
        {
            lock (_lock)
            {
                Settings = settings.Clone();
                Save();
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                StateChanged?.Invoke();
            });
        }

        public void NotifyNotchExpired()
        {
            lock (_lock)
            {
                if (!IsPinned)
                {
                    IsLive = false;
                }
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                StateChanged?.Invoke();
            });
        }

        private void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<MessageSettings>(json);
                    if (loaded != null)
                    {
                        Settings = loaded;
                    }
                }
            }
            catch { }
        }
    }
}
