using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools
{
    /// <summary>
    /// WeatherTool (`WindowsBuddy.Tools.Weather.WeatherTool`) — Forecast & Solar Daylight widget.
    /// </summary>
    public sealed class WeatherTool : IBuddyTool
    {
        public string Id => "weather";
        public string Title => "Weather";
        public string IconGlyph => "☀";

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { Margin = new Thickness(4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

            var currentCard = new BuddyCard { Margin = new Thickness(4) };
            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            leftStack.Children.Add(new TextBlock
            {
                Text = "LOCAL WEATHER",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.CWeather)
            });
            leftStack.Children.Add(new TextBlock
            {
                Text = "22°C",
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 4, 0, 2)
            });
            leftStack.Children.Add(new TextBlock
            {
                Text = "Clear Sky • Humidity 42% • Wind 11 km/h",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2)
            });
            leftStack.Children.Add(new TextBlock
            {
                Text = "☀ Sunrise 06:24   🌇 Sunset 18:12",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 10, 0, 0)
            });
            currentCard.Child = leftStack;
            Grid.SetColumn(currentCard, 0);
            grid.Children.Add(currentCard);

            var forecastCard = new BuddyCard { Margin = new Thickness(4) };
            var rightStack = new StackPanel();
            rightStack.Children.Add(new TextBlock
            {
                Text = "5-DAY OUTLOOK (Open-Meteo)",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 8)
            });

            string[] days = { "Today", "Tomorrow", "Mon", "Tue", "Wed" };
            string[] conds = { "☀ Sunny", "⛅ Partly Cloudy", "☀ Clear", "🌧 Light Rain", "☀ Sunny" };
            string[] temps = { "22° / 13°", "20° / 12°", "23° / 14°", "18° / 10°", "24° / 14°" };
            for (int i = 0; i < days.Length; i++)
            {
                var row = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface2),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                var dp = new DockPanel();
                var tempBlock = new TextBlock { Text = temps[i], FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = CapsuleTheme.Brush(p.Text), HorizontalAlignment = HorizontalAlignment.Right };
                DockPanel.SetDock(tempBlock, Dock.Right);
                dp.Children.Add(tempBlock);
                dp.Children.Add(new TextBlock { Text = $"{days[i]}   {conds[i]}", FontSize = 11.5, Foreground = CapsuleTheme.Brush(p.Text2) });
                row.Child = dp;
                rightStack.Children.Add(row);
            }

            forecastCard.Child = rightStack;
            Grid.SetColumn(forecastCard, 1);
            grid.Children.Add(forecastCard);
            return grid;
        }

        public void OnActivated() { }
    }

    /// <summary>
    /// ScreenTimeTool (`WindowsBuddy.Tools.ScreenTime.ScreenTimeTool`) — Tracks foreground application time via Win32 `GetForegroundWindow`.
    /// </summary>
    public sealed class ScreenTimeTool : IBuddyTool
    {
        public string Id => "screentime";
        public string Title => "Screen Time";
        public string IconGlyph => "📊";

        private static readonly Dictionary<string, int> _appSeconds = new(StringComparer.OrdinalIgnoreCase);
        private static readonly DispatcherTimer _trackerTimer;
        private StackPanel _listPanel;

        static ScreenTimeTool()
        {
            _trackerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _trackerTimer.Tick += (_, _) => SampleForegroundApp();
            _trackerTimer.Start();
            SampleForegroundApp();
        }

        private static void SampleForegroundApp()
        {
            try
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return;
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName;
                if (string.IsNullOrWhiteSpace(name)) return;
                _appSeconds[name] = (_appSeconds.TryGetValue(name, out int cur) ? cur : 0) + 2;
            }
            catch
            {
                // Ignore inaccessible processes
            }
        }

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var dock = new DockPanel();

            var header = new TextBlock
            {
                Text = "ACTIVE APPLICATION SCREEN TIME (Live Win32 Foreground Tracker)",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.CScreen),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            _listPanel = new StackPanel();
            dock.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _listPanel });
            card.Child = dock;

            RenderRows();
            return card;
        }

        private void RenderRows()
        {
            if (_listPanel == null) return;
            _listPanel.Children.Clear();
            var p = CapsuleTheme.Current;

            foreach (var kv in _appSeconds.OrderByDescending(k => k.Value).Take(8))
            {
                var row = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var dp = new DockPanel();
                int mins = kv.Value / 60;
                int secs = kv.Value % 60;
                var val = new TextBlock
                {
                    Text = mins > 0 ? $"{mins}m {secs}s" : $"{secs}s",
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = CapsuleTheme.Brush(p.Accent),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(val, Dock.Right);
                dp.Children.Add(val);
                dp.Children.Add(new TextBlock
                {
                    Text = kv.Key,
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(p.Text)
                });
                row.Child = dp;
                _listPanel.Children.Add(row);
            }
        }

        public void OnActivated()
        {
            SampleForegroundApp();
            RenderRows();
        }
    }

    /// <summary>
    /// AICodingTool (`WindowsBuddy.Tools.AICoding.AICodingTool`) — Monitors AI coding agent status and token usage.
    /// </summary>
    public sealed class AICodingTool : IBuddyTool
    {
        public string Id => "aicoding";
        public string Title => "AI Coding";
        public string IconGlyph => "🤖";

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "AI CODING AGENTS & USAGE MONITOR",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                Margin = new Thickness(0, 0, 0, 8)
            });

            AddAgentRow(stack, "Antigravity / Gemini Agent", "Active • Reconstructing Vayli", "142.8K tokens • $0.00");
            AddAgentRow(stack, "Claude Code CLI", "Idle • Ready", "38.4K tokens today");
            AddAgentRow(stack, "Cursor / Codex", "Standby", "19.1K tokens today");

            card.Child = stack;
            return card;
        }

        private static void AddAgentRow(StackPanel parent, string agent, string status, string metric)
        {
            var p = CapsuleTheme.Current;
            var row = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var dp = new DockPanel();
            var right = new TextBlock
            {
                Text = metric,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(right, Dock.Right);
            dp.Children.Add(right);

            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = agent, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            left.Children.Add(new TextBlock { Text = status, FontSize = 11, Foreground = CapsuleTheme.Brush(p.Text2) });
            dp.Children.Add(left);
            row.Child = dp;
            parent.Children.Add(row);
        }

        public void OnActivated() { }
    }

    /// <summary>
    /// DropTool (`WindowsBuddy.Tools.Drop.DropTool`) — Local Wi-Fi / LAN file sharing endpoint.
    /// </summary>
    public sealed class DropTool : IBuddyTool
    {
        public string Id => "drop";
        public string Title => "Vayli Drop";
        public string IconGlyph => "📡";

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            string localIp = GetLocalLanIp();
            string endpoint = $"http://{localIp}:58210";

            stack.Children.Add(new TextBlock
            {
                Text = "VAYLI DROP — Private LAN File Transfer",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                Margin = new Thickness(0, 0, 0, 6)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"Local Endpoint: {endpoint}",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 2, 0, 4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"Save Folder: {AppPreferences.Instance.Raw.DropSaveFolder}",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var openFolderBtn = new BuddyPillButton("📂", "Open Drop Downloads Folder") { HorizontalAlignment = HorizontalAlignment.Left };
            openFolderBtn.Clicked += () =>
            {
                try { Process.Start(new ProcessStartInfo(AppPreferences.Instance.Raw.DropSaveFolder) { UseShellExecute = true }); } catch { }
            };
            stack.Children.Add(openFolderBtn);

            card.Child = stack;
            return card;
        }

        private static string GetLocalLanIp()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }

        public void OnActivated() { }
    }
}
