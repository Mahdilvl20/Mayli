using System;
using System.Drawing;
using System.IO;
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
        public const string SingleInstanceMutexName = "Local\\Mayli_SingleInstance_Mutex";
        private const string PipeName = "Mayli_Ipc_Pipe";
        private static Mutex _mutex;
        private static Forms.NotifyIcon _trayIcon;
        private static CapsuleWindow _capsuleWindow;

        [STAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    string path = Path.Combine(AppPaths.DataDir, "logs", "crash_domain.log");
                    File.AppendAllText(path, $"[{DateTime.Now}] Domain Unhandled: {e.ExceptionObject}\n");
                }
                catch { }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try
                {
                    string path = Path.Combine(AppPaths.DataDir, "logs", "crash_domain.log");
                    File.AppendAllText(path, $"[{DateTime.Now}] Unobserved Task: {e.Exception}\n");
                }
                catch { }
                e.SetObserved();
            };

            _mutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                string cmd = (args != null && System.Linq.Enumerable.Any(args, a => a.Contains("setting", StringComparison.OrdinalIgnoreCase)))
                    ? "settings"
                    : "notch";
                SendIpcCommand(cmd);
                return 0;
            }

            try
            {
                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                app.DispatcherUnhandledException += (s, e) =>
                {
                    LogWriter.WriteNow("dispatcher_error", e.Exception.ToString());
                    e.Handled = true;
                };

                WindowsBuddy.BuddyCapsule.Theme.ModernScrollbarStyles.ApplyToApplication(app);
                LocalizationService.Initialize();
                LogWriter.WriteNow("boot", "Step 0: Pre-CoreAudio");
                _ = CoreAudioService.Instance;
                LogWriter.WriteNow("boot", "Step 1: CoreAudio done");

                StartIpcServer(app);
                LogWriter.WriteNow("boot", "Step 2: StartIpcServer done");
                InitializeTrayIcon(app);
                LogWriter.WriteNow("boot", "Step 3: InitializeTrayIcon done");

                _capsuleWindow = new CapsuleWindow();
                LogWriter.WriteNow("boot", "Step 4: CapsuleWindow constructor done");
                _capsuleWindow.Show();
                LogWriter.WriteNow("boot", "Step 5: CapsuleWindow Show done");

                UpdateService.CheckOnStartup();
                LogWriter.WriteNow("boot", "Step 6: UpdateService done");

                LogWriter.WriteNow("boot", "Mayli CapsuleWindow started successfully. Entering app.Run()...");
                int exitCode = app.Run();
                LogWriter.WriteNow("boot", $"Exited app.Run() with code {exitCode}.");
                return exitCode;
            }
            catch (Exception ex)
            {
                LogWriter.WriteNow("boot_crash", ex.ToString());
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mayli_crash.log");
                System.IO.File.WriteAllText(logPath, ex.ToString());
                Forms.MessageBox.Show(ex.ToString(), "Mayli Startup Error");
                return 1;
            }
            finally
            {
                LogWriter.WriteNow("boot", "Main finally block entered.");
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                HotkeyService.Instance.Dispose();
            }
        }

        private static void SendIpcCommand(string cmd)
        {
            try
            {
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", PipeName, System.IO.Pipes.PipeDirection.Out);
                client.Connect(500);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(cmd);
            }
            catch { }
        }

        private static void StartIpcServer(Application app)
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new System.IO.Pipes.NamedPipeServerStream(PipeName, System.IO.Pipes.PipeDirection.In);
                        server.WaitForConnection();
                        using var reader = new StreamReader(server);
                        string line = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            string cmd = line.Trim().ToLowerInvariant();
                            app.Dispatcher.InvokeAsync(() =>
                            {
                                if (cmd == "settings")
                                {
                                    SettingsWindow.ShowSingleton();
                                }
                                else if (cmd == "notch" || cmd == "toggle")
                                {
                                    _capsuleWindow?.ToggleFromExternal();
                                }
                                else if (cmd.StartsWith("tool:"))
                                {
                                    string toolId = cmd.Substring(5).Trim();
                                    _capsuleWindow?.NavigateToTool(toolId);
                                    if (_capsuleWindow?.IsTucked ?? false) _capsuleWindow?.SetTucked(false);
                                }
                                else if (System.Linq.Enumerable.Any(WindowsBuddy.Tools.ToolRegistry.All, t => string.Equals(t.Id, cmd, StringComparison.OrdinalIgnoreCase)))
                                {
                                    _capsuleWindow?.NavigateToTool(cmd);
                                    if (_capsuleWindow?.IsTucked ?? false) _capsuleWindow?.SetTucked(false);
                                }
                            });
                        }
                    }
                    catch { break; }
                }
            })
            {
                IsBackground = true
            };
            thread.Start();
        }

        private static void InitializeTrayIcon(Application app)
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Text = "Mayli — The Notch for Windows (Ctrl+Alt+N)",
                Visible = true
            };

            using (var icoStream = EmbeddedAssetLoader.OpenResourceStream("Mayli.Brand.tray-dark.ico"))
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

            RebuildTrayMenu(app);
            _trayIcon.DoubleClick += (_, _) => SettingsWindow.ShowSingleton();

            LocalizationService.LanguageChanged += () => app.Dispatcher.InvokeAsync(() => RebuildTrayMenu(app));
        }

        private static void RebuildTrayMenu(Application app)
        {
            if (_trayIcon == null) return;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add(LocalizationService.T("tray.toggle", "Toggle Notch (Ctrl+Alt+N)"), null, (_, _) => _capsuleWindow?.ToggleFromExternal());
            menu.Items.Add(LocalizationService.T("tray.settings", "Settings..."), null, (_, _) => SettingsWindow.ShowSingleton());
            menu.Items.Add(new Forms.ToolStripSeparator());

            var moodMenu = new Forms.ToolStripMenuItem(LocalizationService.T("tray.mood", "Mayli Mood / Companion 🐾"));
            bool isFa = AppPreferences.Instance.IsPersian;
            moodMenu.DropDownItems.Add(isFa ? "🌟 عادی" : "🌟 Idle", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Idle));
            moodMenu.DropDownItems.Add(isFa ? "😊 شاد" : "😊 Happy", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Happy));
            moodMenu.DropDownItems.Add(isFa ? "😠 اخم" : "😠 Grumpy", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Angry));
            moodMenu.DropDownItems.Add(isFa ? "💖 دلبر" : "💖 Love", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Love));
            moodMenu.DropDownItems.Add(isFa ? "😉 چشمک" : "😉 Wink", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Wink));
            moodMenu.DropDownItems.Add(isFa ? "😲 تعجب" : "😲 Surprised", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Surprised));
            moodMenu.DropDownItems.Add(isFa ? "😴 خواب‌آلود" : "😴 Sleepy", null, (_, _) => PetEngine.Instance.SetMood(OrbMood.Sleepy));
            menu.Items.Add(moodMenu);

            menu.Items.Add(LocalizationService.T("tray.cycle_mood", "Cycle Mood"), null, (_, _) => PetEngine.Instance.CycleNextMood());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(LocalizationService.T("tray.exit", "Exit Mayli"), null, (_, _) => app.Shutdown());

            _trayIcon.ContextMenuStrip = menu;
        }
    }
}
