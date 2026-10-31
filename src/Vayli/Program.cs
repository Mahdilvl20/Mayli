using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy
{
    public static class Program
    {
        public const string SingleInstanceMutexName = "Local\\WindowsBuddy_Vayli_SingleInstance_Mutex";
        private static Mutex _mutex;
        private static Forms.NotifyIcon _trayIcon;
        private static CapsuleWindow _capsuleWindow;

        [STAThread]
        public static int Main(string[] args)
        {
            _mutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                return 0;
            }

            try
            {
                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                WindowsBuddy.BuddyCapsule.Theme.ModernScrollbarStyles.ApplyToApplication(app);

                InitializeTrayIcon(app);

                _capsuleWindow = new CapsuleWindow();
                _capsuleWindow.Show();

                LogWriter.WriteNow("boot", "Vayli CapsuleWindow started successfully.");
                return app.Run();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("F:\\Vayli_Project\\crash.log", ex.ToString());
                Forms.MessageBox.Show(ex.ToString(), "Vayli Startup Error");
                return 1;
            }
            finally
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                HotkeyService.Instance.Dispose();
            }
        }

        private static void InitializeTrayIcon(Application app)
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Text = "Vayli — The Notch for Windows (Ctrl+Alt+N)",
                Visible = true
            };

            using (var icoStream = EmbeddedAssetLoader.OpenResourceStream("Vayli.Brand.tray-dark.ico"))
            {
                if (icoStream != null)
                {
                    _trayIcon.Icon = new Icon(icoStream);
                }
                else
                {
                    _trayIcon.Icon = SystemIcons.Application;
                }
            }

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Toggle Notch (Ctrl+Alt+N)", null, (_, _) => _capsuleWindow?.ToggleFromExternal());
            menu.Items.Add("Settings...", null, (_, _) => SettingsWindow.ShowSingleton());
            menu.Items.Add(new Forms.ToolStripSeparator());

            var moodMenu = new Forms.ToolStripMenuItem("Vayli Mood / حالت گوگولی 🐾");
            moodMenu.DropDownItems.Add("🌟 عادی (Idle)", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Idle));
            moodMenu.DropDownItems.Add("😊 شاد (Happy)", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Happy));
            moodMenu.DropDownItems.Add("😠 اخم (Angry / Grumpy)", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Angry));
            moodMenu.DropDownItems.Add("💖 دلبر (Love)", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Love));
            moodMenu.DropDownItems.Add("😉 چشمک (Wink)", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Wink));
            moodMenu.DropDownItems.Add("😲 تعجب (Surprised)", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Surprised));
            moodMenu.DropDownItems.Add("😴 خواب‌آلود (Sleepy)", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Sleepy));
            menu.Items.Add(moodMenu);

            menu.Items.Add("Cycle Mood (تغییر حالت)", null, (_, _) => PetEngine.Instance.CycleNextMood());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit Vayli", null, (_, _) => app.Shutdown());

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, _) => SettingsWindow.ShowSingleton();
        }
    }
}
