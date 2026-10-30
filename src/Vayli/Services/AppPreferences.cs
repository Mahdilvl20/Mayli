using System;
using System.Windows.Interop;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services
{
    public class AppPreferencesModel
    {
        public string Theme { get; set; } = "Dark";
        public string ColorStyle { get; set; } = "Obsidian";
        public string Accent { get; set; } = "#30D158";
        public string NotchSilhouette { get; set; } = "Shouldered";
        public string EdgeLightMode { get; set; } = "Aura";
        public string Language { get; set; } = "en-US";
        public int HotkeyModifiers { get; set; } = 3; // Ctrl + Alt
        public int HotkeyKey { get; set; } = 0x4E;    // N
        public int HoverDelayMs { get; set; } = 120;
        public bool OpensOnClick { get; set; } = false;
        public bool EnablePet { get; set; } = true;
        public bool ClipboardRecording { get; set; } = true;
        public int ClipboardHistoryLimit { get; set; } = 30;
        public int ClipboardRetentionDays { get; set; } = 7;
        public string HomeWidgetOrder { get; set; } = "media,pet,clock,timer,weather,system";
        public string DropSaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
        public bool DropRequirePin { get; set; } = false;
    }

    public sealed class AppPreferences
    {
        private const string FileName = "preferences.json";
        private static readonly Lazy<AppPreferences> _lazy = new(() => new AppPreferences());
        public static AppPreferences Instance => _lazy.Value;

        public AppPreferencesModel Raw { get; private set; }
        public event Action Changed;

        private AppPreferences()
        {
            Raw = PersistenceHelper.SafeReadJson<AppPreferencesModel>(FileName);
        }

        public void Save()
        {
            PersistenceHelper.SafeWriteJson(FileName, Raw);
            Changed?.Invoke();
        }

        public string Theme
        {
            get => Raw.Theme;
            set { Raw.Theme = value; Save(); }
        }

        public string Accent
        {
            get => Raw.Accent;
            set { Raw.Accent = value; Save(); }
        }

        public string NotchSilhouette
        {
            get => Raw.NotchSilhouette;
            set { Raw.NotchSilhouette = value; Save(); }
        }

        public string EdgeLightMode
        {
            get => Raw.EdgeLightMode;
            set { Raw.EdgeLightMode = value; Save(); }
        }

        public int HoverDelayMs
        {
            get => Raw.HoverDelayMs;
            set { Raw.HoverDelayMs = value; Save(); }
        }

        public bool OpensOnClick
        {
            get => Raw.OpensOnClick;
            set { Raw.OpensOnClick = value; Save(); }
        }

        public bool EnablePet
        {
            get => Raw.EnablePet;
            set { Raw.EnablePet = value; Save(); }
        }
    }

    public sealed class HotkeyService : IDisposable
    {
        private const int HotkeyId = 9001;
        private const uint MOD_NOREPEAT = 0x4000;
        public static HotkeyService Instance { get; } = new HotkeyService();

        private HwndSource _source;
        private bool _registered;

        public event Action Pressed;
        public bool IsRegistered => _registered;
        public string LastError { get; private set; }

        public void AttachWindow(IntPtr hwnd)
        {
            _source = HwndSource.FromHwnd(hwnd);
            _source?.AddHook(WndProc);
            var prefs = AppPreferences.Instance.Raw;
            Apply(prefs.HotkeyModifiers, prefs.HotkeyKey);
        }

        public bool Apply(int modifiers, int virtualKey)
        {
            if (_source == null) return false;
            Release();
            _registered = NativeMethods.RegisterHotKey(
                _source.Handle,
                HotkeyId,
                (uint)modifiers | MOD_NOREPEAT,
                (uint)virtualKey);
            if (!_registered)
                LastError = "Could not bind global hotkey.";
            return _registered;
        }

        public void Release()
        {
            if (_registered && _source != null)
            {
                NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
                _registered = false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                Pressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Release();
            _source?.RemoveHook(WndProc);
        }
    }
}
