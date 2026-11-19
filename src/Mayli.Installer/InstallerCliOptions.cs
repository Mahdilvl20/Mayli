using System;
using System.IO;

namespace Mayli.Installer
{
    /// <summary>
    /// Parsed command-line and executable-mode options for setup/uninstall invocation.
    /// </summary>
    public sealed class InstallerCliOptions
    {
        public bool IsUninstall { get; init; }
        public bool IsSilent { get; init; }
        public bool RemoveUserData { get; init; }
        public bool SkipProcessKill { get; init; }
        public bool SkipDesktopShortcut { get; init; }
        public bool EnableAutoStartup { get; init; }
        public string InstallDir { get; init; }

        public static InstallerCliOptions Parse(string[] args, string processPath = null)
        {
            bool isUninstall = false;
            bool isSilent = false;
            bool removeUserData = false;
            bool skipProcessKill = false;
            bool skipDesktopShortcut = false;
            bool enableAutoStartup = true;
            string installDir = null;

            string effectiveExe = string.IsNullOrWhiteSpace(processPath)
                ? Environment.ProcessPath
                : processPath;

            if (!string.IsNullOrWhiteSpace(effectiveExe))
            {
                string exeName = Path.GetFileNameWithoutExtension(effectiveExe);
                if (exeName.Contains("Uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    isUninstall = true;
                }
            }

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i]?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(arg)) continue;

                    if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        isUninstall = true;
                    }
                    else if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("-silent", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
                    {
                        isSilent = true;
                    }
                    else if (arg.Equals("--remove-data", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("--purge-data", StringComparison.OrdinalIgnoreCase))
                    {
                        removeUserData = true;
                    }
                    else if (arg.Equals("--skip-process-kill", StringComparison.OrdinalIgnoreCase))
                    {
                        skipProcessKill = true;
                    }
                    else if (arg.Equals("--no-shortcut", StringComparison.OrdinalIgnoreCase))
                    {
                        skipDesktopShortcut = true;
                    }
                    else if (arg.Equals("--no-startup", StringComparison.OrdinalIgnoreCase))
                    {
                        enableAutoStartup = false;
                    }
                    else if (arg.StartsWith("--install-dir=", StringComparison.OrdinalIgnoreCase))
                    {
                        installDir = Unquote(arg.Substring("--install-dir=".Length));
                    }
                    else if (arg.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase))
                    {
                        installDir = Unquote(arg.Substring("--dir=".Length));
                    }
                    else if (arg.StartsWith("/D=", StringComparison.OrdinalIgnoreCase))
                    {
                        installDir = Unquote(arg.Substring("/D=".Length));
                    }
                    else if ((arg.Equals("--install-dir", StringComparison.OrdinalIgnoreCase) ||
                              arg.Equals("--dir", StringComparison.OrdinalIgnoreCase) ||
                              arg.Equals("--path", StringComparison.OrdinalIgnoreCase)) &&
                             i + 1 < args.Length)
                    {
                        installDir = Unquote(args[++i]);
                    }
                }
            }

            return new InstallerCliOptions
            {
                IsUninstall = isUninstall,
                IsSilent = isSilent,
                RemoveUserData = removeUserData,
                SkipProcessKill = skipProcessKill,
                SkipDesktopShortcut = skipDesktopShortcut,
                EnableAutoStartup = enableAutoStartup,
                InstallDir = installDir
            };
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim().Trim('"');
        }
    }
}
