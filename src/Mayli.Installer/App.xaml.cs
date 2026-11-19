using System;
using System.Windows;

namespace Mayli.Installer
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var cli = InstallerCliOptions.Parse(e.Args, Environment.ProcessPath);

            if (cli.IsUninstall && cli.IsSilent)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                int exitCode = 0;
                try
                {
                    string targetDir = InstallerService.ResolveInstalledDirectory(cli.InstallDir);
                    var result = await InstallerService.ExecuteUninstallAsync(
                        targetDir,
                        cli.RemoveUserData,
                        cli.SkipProcessKill,
                        null);
                    if (!result.Success)
                    {
                        exitCode = 1;
                    }
                }
                catch
                {
                    exitCode = 1;
                }
                finally
                {
                    InstallerService.SchedulePendingSelfDelete();
                }

                Shutdown(exitCode);
                return;
            }

            if (!cli.IsUninstall && cli.IsSilent)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                int exitCode = 0;
                try
                {
                    var options = new InstallOptions
                    {
                        Path = string.IsNullOrWhiteSpace(cli.InstallDir)
                            ? InstallerService.GetDefaultInstallPath()
                            : cli.InstallDir,
                        CreateShortcut = !cli.SkipDesktopShortcut,
                        AutoStartup = cli.EnableAutoStartup,
                        EnableMica = true,
                        SkipProcessKill = cli.SkipProcessKill,
                        SkipWeatherConfig = false
                    };
                    await InstallerService.ExecuteInstallAsync(options, null);
                }
                catch
                {
                    exitCode = 1;
                }

                Shutdown(exitCode);
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var window = new MainWindow(cli);
            MainWindow = window;
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            InstallerService.SchedulePendingSelfDelete();
            base.OnExit(e);
        }
    }
}
