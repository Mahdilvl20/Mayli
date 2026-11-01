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
        public int TuckHotkeyModifiers { get; set; } = 6; // Ctrl + Shift
        public int TuckHotkeyKey { get; set; } = 0x48;    // H
        public int HoverDelayMs { get; set; } = 120;
        public bool OpensOnClick { get; set; } = false;
        public bool EnablePet { get; set; } = true;
        public bool ClipboardRecording { get; set; } = true;
        public int ClipboardHistoryLimit { get; set; } = 30;
        public int ClipboardRetentionDays { get; set; } = 7;
        public string HomeWidgetOrder { get; set; } = "media,pet,clock,timer,weather,system";
        public string DropSaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
        public bool DropRequirePin { get; set; } = false;

        // General settings matching native UI
        public string TimeFormat { get; set; } = "System"; // "System", "12-hour", "24-hour"
        public string TemperatureUnit { get; set; } = "°C"; // "°C", "°F"
        public string WindSpeedUnit { get; set; } = "km/h"; // "km/h", "mph"
        public string OpenNotchShortcut { get; set; } = "Ctrl + Alt + N";
        public bool StartWithWindows { get; set; } = false;
        public bool HideOnFullscreen { get; set; } = true;
        public bool ShowMediaActivity { get; set; } = true;
        public bool ShowFileShelfActivity { get; set; } = true;
        public bool ShowBatteryActivity { get; set; } = true;
        public bool ShowTimerActivity { get; set; } = true;

        // Notch Page detailed preferences (matching exact screenshots)
        public string NotchVisibility { get; set; } = "Always"; // "Always", "Smart", "Minimal", "Hidden"
        public string NotchStyle { get; set; } = "Notch"; // "Line", "Notch", "Island"
        public string NotchShape { get; set; } = "Compact"; // "Compact", "Wide", "Classic", "Ultra-Compact"
        public int NotchScalePercent { get; set; } = 90; // 60 - 140
        public string NotchRestingLayout { get; set; } = "Corners"; // "Corners", "Center"
        public string NotchOpenTrigger { get; set; } = "Hover"; // "Hover", "Click"
        public string NotchDisplay { get; set; } = "Primary display"; // "Primary display", "All displays"
        public bool ShowNotchBar { get; set; } = true;

        // Behavior
        public bool ShowEventsOverApps { get; set; } = true;
        public bool TabAnimations { get; set; } = true;
        public bool EnablePetBeta { get; set; } = false;

        // Edge Light
        public bool EnableEdgeLight { get; set; } = true;
        public string EdgeLightPattern { get; set; } = "Twin comets"; // "Comet", "Twin comets", "Breathe"
        public string EdgeLightColor { get; set; } = "#FFFFFF";
        public int EdgeLightIntensity { get; set; } = 80; // 0 - 100
        public string EdgeLightSpeed { get; set; } = "Normal"; // "Slow", "Normal", "Fast"
        public string EdgeLightTrigger { get; set; } = "Always"; // "Always", "On events & hover"

        // When Closed, Show
        public bool ShowClosedTime { get; set; } = true;
        public string ClosedClockStyle { get; set; } = "Digital"; // "Digital", "Analog"
        public bool ShowClosedDate { get; set; } = false;
        public bool ShowClosedBattery { get; set; } = true;
        public bool ShowClosedNowPlaying { get; set; } = false;
        public bool ShowClosedBluetooth { get; set; } = false;
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

        public static bool IsWindowsStartupEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("Mayli") != null;
            }
            catch { return false; }
        }

        public static void SetWindowsStartup(bool enable)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                if (enable)
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("Mayli", $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue("Mayli", false);
                }
            }
            catch { }
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

        public string TimeFormat
        {
            get => Raw.TimeFormat;
            set { Raw.TimeFormat = value; Save(); }
        }

        public string TemperatureUnit
        {
            get => Raw.TemperatureUnit;
            set { Raw.TemperatureUnit = value; Save(); }
        }

        public string WindSpeedUnit
        {
            get => Raw.WindSpeedUnit;
            set { Raw.WindSpeedUnit = value; Save(); }
        }

        public string OpenNotchShortcut
        {
            get => Raw.OpenNotchShortcut;
            set { Raw.OpenNotchShortcut = value; Save(); }
        }

        public bool HideOnFullscreen
        {
            get => Raw.HideOnFullscreen;
            set { Raw.HideOnFullscreen = value; Save(); }
        }

        public bool ShowMediaActivity
        {
            get => Raw.ShowMediaActivity;
            set { Raw.ShowMediaActivity = value; Save(); }
        }

        public bool ShowFileShelfActivity
        {
            get => Raw.ShowFileShelfActivity;
            set { Raw.ShowFileShelfActivity = value; Save(); }
        }

        public bool ShowBatteryActivity
        {
            get => Raw.ShowBatteryActivity;
            set { Raw.ShowBatteryActivity = value; Save(); }
        }

        public bool ShowTimerActivity
        {
            get => Raw.ShowTimerActivity;
            set { Raw.ShowTimerActivity = value; Save(); }
        }

        public string NotchVisibility
        {
            get => Raw.NotchVisibility;
            set { Raw.NotchVisibility = value; Save(); }
        }

        public string NotchStyle
        {
            get => Raw.NotchStyle;
            set { Raw.NotchStyle = value; Save(); }
        }

        public string NotchShape
        {
            get => Raw.NotchShape;
            set { Raw.NotchShape = value; Save(); }
        }

        public int NotchScalePercent
        {
            get => Raw.NotchScalePercent;
            set { Raw.NotchScalePercent = value; Save(); }
        }

        public string NotchRestingLayout
        {
            get => Raw.NotchRestingLayout;
            set { Raw.NotchRestingLayout = value; Save(); }
        }

        public string NotchOpenTrigger
        {
            get => Raw.NotchOpenTrigger;
            set { Raw.NotchOpenTrigger = value; OpensOnClick = (value == "Click"); Save(); }
        }

        public string NotchDisplay
        {
            get => Raw.NotchDisplay;
            set { Raw.NotchDisplay = value; Save(); }
        }

        public bool ShowNotchBar
        {
            get => Raw.ShowNotchBar;
            set { Raw.ShowNotchBar = value; Save(); }
        }

        public bool ShowEventsOverApps
        {
            get => Raw.ShowEventsOverApps;
            set { Raw.ShowEventsOverApps = value; Save(); }
        }

        public bool TabAnimations
        {
            get => Raw.TabAnimations;
            set { Raw.TabAnimations = value; Save(); }
        }

        public bool EnablePetBeta
        {
            get => Raw.EnablePetBeta;
            set { Raw.EnablePetBeta = value; EnablePet = value; Save(); }
        }

        public bool EnableEdgeLight
        {
            get => Raw.EnableEdgeLight;
            set { Raw.EnableEdgeLight = value; Save(); }
        }

        public string EdgeLightPattern
        {
            get => Raw.EdgeLightPattern;
            set { Raw.EdgeLightPattern = value; Save(); }
        }

        public string EdgeLightColor
        {
            get => Raw.EdgeLightColor;
            set { Raw.EdgeLightColor = value; Save(); }
        }

        public int EdgeLightIntensity
        {
            get => Raw.EdgeLightIntensity;
            set { Raw.EdgeLightIntensity = value; Save(); }
        }

        public string EdgeLightSpeed
        {
            get => Raw.EdgeLightSpeed;
            set { Raw.EdgeLightSpeed = value; Save(); }
        }

        public string EdgeLightTrigger
        {
            get => Raw.EdgeLightTrigger;
            set { Raw.EdgeLightTrigger = value; Save(); }
        }

        public bool ShowClosedTime
        {
            get => Raw.ShowClosedTime;
            set { Raw.ShowClosedTime = value; Save(); }
        }

        public string ClosedClockStyle
        {
            get => Raw.ClosedClockStyle;
            set { Raw.ClosedClockStyle = value; Save(); }
        }

        public bool ShowClosedDate
        {
            get => Raw.ShowClosedDate;
            set { Raw.ShowClosedDate = value; Save(); }
        }

        public bool ShowClosedBattery
        {
            get => Raw.ShowClosedBattery;
            set { Raw.ShowClosedBattery = value; Save(); }
        }

        public bool ShowClosedNowPlaying
        {
            get => Raw.ShowClosedNowPlaying;
            set { Raw.ShowClosedNowPlaying = value; Save(); }
        }

        public bool ShowClosedBluetooth
        {
            get => Raw.ShowClosedBluetooth;
            set { Raw.ShowClosedBluetooth = value; Save(); }
        }

        public void ResetNotchDefaults()
        {
            Raw.NotchVisibility = "Always";
            Raw.NotchStyle = "Notch";
            Raw.NotchShape = "Compact";
            Raw.NotchSilhouette = "Shouldered";
            Raw.NotchScalePercent = 90;
            Raw.NotchRestingLayout = "Corners";
            Raw.NotchOpenTrigger = "Hover";
            Raw.OpensOnClick = false;
            Raw.NotchDisplay = "Primary display";
            Raw.ShowNotchBar = true;
            Raw.HideOnFullscreen = true;
            Raw.ShowEventsOverApps = true;
            Raw.TabAnimations = true;
            Raw.EnablePetBeta = false;
            Raw.EnablePet = false;
            Raw.EnableEdgeLight = true;
            Raw.EdgeLightPattern = "Twin comets";
            Raw.EdgeLightColor = "#FFFFFF";
            Raw.EdgeLightIntensity = 80;
            Raw.EdgeLightSpeed = "Normal";
            Raw.EdgeLightTrigger = "Always";
            Raw.ShowClosedTime = true;
            Raw.ClosedClockStyle = "Digital";
            Raw.ShowClosedDate = false;
            Raw.ShowClosedBattery = true;
            Raw.ShowClosedNowPlaying = false;
            Raw.ShowClosedBluetooth = false;
            Save();
        }
    }

    public sealed class HotkeyService : IDisposable
    {
        private const int HotkeyId = 9001;        // Ctrl + Alt + N (Expand / Collapse)
        private const int TuckHotkeyId1 = 9002;   // Primary Tuck Hotkey (Default: Ctrl + Shift + H)
        private const int TuckHotkeyId2 = 9003;   // Fallback Tuck Hotkey (Ctrl + Alt + H)
        private const uint MOD_NOREPEAT = 0x4000;
        public static HotkeyService Instance { get; } = new HotkeyService();

        private HwndSource _source;
        private bool _registered;
        private bool _tuckRegistered;

        public event Action Pressed;
        public event Action TuckPressed;
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

            var prefs = AppPreferences.Instance.Raw;
            // Register primary Tuck hotkey (Ctrl + Shift + H)
            _tuckRegistered = NativeMethods.RegisterHotKey(
                _source.Handle,
                TuckHotkeyId1,
                (uint)prefs.TuckHotkeyModifiers | MOD_NOREPEAT,
                (uint)prefs.TuckHotkeyKey);

            // Register fallback Tuck hotkey (Ctrl + Alt + H: modifiers = 3, key = 0x48)
            NativeMethods.RegisterHotKey(
                _source.Handle,
                TuckHotkeyId2,
                (uint)3 | MOD_NOREPEAT,
                (uint)0x48);

            if (!_registered)
                LastError = "Could not bind global hotkey.";
            return _registered;
        }

        public void Release()
        {
            if (_source != null)
            {
                if (_registered)
                {
                    NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
                    _registered = false;
                }
                NativeMethods.UnregisterHotKey(_source.Handle, TuckHotkeyId1);
                NativeMethods.UnregisterHotKey(_source.Handle, TuckHotkeyId2);
                _tuckRegistered = false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HotkeyId)
                {
                    Pressed?.Invoke();
                    handled = true;
                }
                else if (id == TuckHotkeyId1 || id == TuckHotkeyId2)
                {
                    TuckPressed?.Invoke();
                    handled = true;
                }
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
