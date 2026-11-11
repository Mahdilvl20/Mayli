using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace Mayli.Installer
{
    public partial class MainWindow : Window
    {
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
                // Dedicated temporary data folder for installer WebView2
                string userDataFolder = Path.Combine(Path.GetTempPath(), "Mayli_Installer_WV2");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await InstallerWebView.EnsureCoreWebView2Async(env);

                // Configure WebView2 settings
                InstallerWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                InstallerWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                InstallerWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Handle communication from JS
                InstallerWebView.WebMessageReceived += OnWebMessageReceived;

                // Load embedded HTML
                string htmlContent = LoadEmbeddedHtml();
                InstallerWebView.NavigateToString(htmlContent);

                // Wait for navigation and send default path
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
            using Stream stream = assembly.GetManifestResourceStream("Mayli.Installer.UI.index.html");
            if (stream == null)
            {
                // Fallback for local development
                string devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\UI\index.html");
                if (File.Exists(devPath)) return File.ReadAllText(devPath);
                throw new FileNotFoundException("Could not find embedded index.html UI resource.");
            }

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
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
                            if (Mouse.LeftButton == MouseButtonState.Pressed)
                            {
                                DragMove();
                            }
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
