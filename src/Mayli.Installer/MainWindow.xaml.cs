using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace Mayli.Installer
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private string _targetDirectory;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string userDataFolder = Path.Combine(Path.GetTempPath(), "Mayli_Installer_WV2");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await InstallerWebView.EnsureCoreWebView2Async(env);

                InstallerWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                InstallerWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                InstallerWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                InstallerWebView.WebMessageReceived += OnWebMessageReceived;

                string htmlContent = LoadEmbeddedHtml();
                InstallerWebView.NavigateToString(htmlContent);

                InstallerWebView.NavigationCompleted += (s, args) =>
                {
                    _targetDirectory = InstallerService.GetDefaultInstallPath();
                    PostToJs(new
                    {
                        type = "init:path",
                        path = _targetDirectory
                    });
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Setup initialization error: {ex.Message}", "Mayli Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private string LoadEmbeddedHtml()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resName = null;
            foreach (var n in assembly.GetManifestResourceNames())
            {
                if (n.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
                {
                    resName = n;
                    break;
                }
            }

            if (resName != null)
            {
                using Stream stream = assembly.GetManifestResourceStream(resName);
                if (stream != null)
                {
                    using StreamReader reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }

            string devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\UI\index.html");
            if (File.Exists(devPath)) return File.ReadAllText(devPath);

            throw new FileNotFoundException("Could not find embedded index.html UI resource.");
        }

        private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawJson = e.WebMessageAsJson;
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                string type = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "window:drag":
                        Dispatcher.Invoke(() =>
                        {
                            var helper = new WindowInteropHelper(this);
                            ReleaseCapture();
                            SendMessage(helper.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        });
                        break;

                    case "window:minimize":
                        Dispatcher.Invoke(() => WindowState = WindowState.Minimized);
                        break;

                    case "window:close":
                        Dispatcher.Invoke(Close);
                        break;

                    case "folder:browse":
                        string current = root.TryGetProperty("current", out var cp) ? cp.GetString() : _targetDirectory;
                        Dispatcher.Invoke(() =>
                        {
                            using var fbd = new System.Windows.Forms.FolderBrowserDialog
                            {
                                Description = "Select Mayli Installation Folder",
                                UseDescriptionForTitle = true,
                                SelectedPath = Directory.Exists(current) ? current : InstallerService.GetDefaultInstallPath()
                            };

                            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                _targetDirectory = fbd.SelectedPath;
                                PostToJs(new { type = "folder:selected", path = fbd.SelectedPath });
                            }
                        });
                        break;

                    case "install:start":
                        var data = root.GetProperty("data");
                        var options = new InstallOptions
                        {
                            Path = data.GetProperty("path").GetString(),
                            City = data.GetProperty("city").GetString(),
                            Country = data.TryGetProperty("country", out var ctry) ? ctry.GetString() : "Iran",
                            Latitude = data.TryGetProperty("lat", out var latp) ? latp.GetDouble() : 35.6892,
                            Longitude = data.TryGetProperty("lon", out var lonp) ? lonp.GetDouble() : 51.3890,
                            CreateShortcut = data.GetProperty("createShortcut").GetBoolean(),
                            AutoStartup = data.GetProperty("autoStartup").GetBoolean(),
                            EnableMica = data.GetProperty("enableMica").GetBoolean()
                        };

                        _targetDirectory = options.Path;

                        try
                        {
                            await InstallerService.ExecuteInstallAsync(options, (percent, status) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    PostToJs(new
                                    {
                                        type = "install:progress",
                                        percent = percent,
                                        status = status
                                    });
                                });
                            });

                            Dispatcher.Invoke(() =>
                            {
                                PostToJs(new { type = "install:done" });
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PostToJs(new
                                {
                                    type = "install:error",
                                    error = ex.Message
                                });
                            });
                        }
                        break;

                    case "app:launch":
                        string launchDir = root.TryGetProperty("path", out var lp) ? lp.GetString() : _targetDirectory;
                        InstallerService.LaunchInstalledApp(launchDir);
                        Dispatcher.Invoke(Close);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error processing web message: " + ex);
            }
        }

        private void PostToJs(object obj)
        {
            try
            {
                string json = JsonSerializer.Serialize(obj);
                InstallerWebView.CoreWebView2?.PostWebMessageAsJson(json);
            }
            catch { }
        }
    }
}
