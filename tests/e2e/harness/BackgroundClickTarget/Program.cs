using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Mayli.TestHarness.BackgroundTarget
{
    internal static class Program
    {
        private static string _logFilePath = Path.Combine(Path.GetTempPath(), "Mayli_BgTarget_Clicks.log");
        private static int _clickCount = 0;
        private static readonly object _lock = new object();

        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                _logFilePath = args[0];
            }

            // Clean previous log
            try { if (File.Exists(_logFilePath)) File.Delete(_logFilePath); } catch { }

            var form = new Form
            {
                Text = "Mayli_Test_BackgroundTarget",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(Screen.PrimaryScreen?.Bounds.Width ?? 1920, 350),
                BackColor = System.Drawing.Color.FromArgb(24, 28, 36),
                TopMost = false,
                ShowInTaskbar = false
            };

            var label = new Label
            {
                Text = "Mayli Background Click Target (E2E Test Receiver)",
                ForeColor = System.Drawing.Color.FromArgb(120, 160, 240),
                Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            void RecordClick(object? sender, MouseEventArgs e)
            {
                lock (_lock)
                {
                    _clickCount++;
                    string entry = $"[CLICK] #{_clickCount} at X={e.X}, Y={e.Y}, Button={e.Button}, Time={DateTime.UtcNow:O}{Environment.NewLine}";
                    try
                    {
                        File.AppendAllText(_logFilePath, entry);
                    }
                    catch { }
                }
            }

            form.MouseDown += RecordClick;
            label.MouseDown += RecordClick;

            form.Controls.Add(label);

            // Background named pipe or file watcher for shutdown signal
            var exitFlagPath = Path.Combine(Path.GetTempPath(), "Mayli_BgTarget_Exit.signal");
            try { if (File.Exists(exitFlagPath)) File.Delete(exitFlagPath); } catch { }

            var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (s, e) =>
            {
                if (File.Exists(exitFlagPath))
                {
                    timer.Stop();
                    form.Close();
                    Application.Exit();
                }
            };
            timer.Start();

            Application.Run(form);
        }
    }
}
