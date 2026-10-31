using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools
{
    public interface IBuddyTool
    {
        string Id { get; }
        string Title { get; }
        string IconGlyph { get; }
        FrameworkElement BuildView();
        void OnActivated();
    }

    public static class ToolRegistry
    {
        private static readonly List<IBuddyTool> _tools = new();
        public static IReadOnlyList<IBuddyTool> All => _tools;
        public static Action<string> OnToolNavigationRequested;

        public static void RegisterDefaults()
        {
            if (_tools.Count > 0) return;

            // 18 tools matching the exact dock sequence
            _tools.Add(new HomeTool());         // 1. Home
            _tools.Add(new SystemTool());       // 2. System
            _tools.Add(new ClipboardTool());    // 3. Clipboard
            _tools.Add(new NotesTool());        // 4. Notes
            _tools.Add(new TimerClockTool());   // 5. Timers
            _tools.Add(new FileShelfTool());    // 6. File Shelf
            _tools.Add(new DropTool());         // 7. Drop
            _tools.Add(new WeatherTool());      // 8. Weather
            _tools.Add(new ClockTool());        // 9. Clock
            _tools.Add(new QuickToolsTool());   // 10. Quick Tools
            _tools.Add(new LinksTool());         // 11. Links
            _tools.Add(new MessageBannerTool()); // 12. Message (Notch banner)
            _tools.Add(new ScreenTimeTool());    // 13. Screen Time
            _tools.Add(new AudioTool());         // 14. Audio
            _tools.Add(new CalendarTool());     // 15. Calendar
            _tools.Add(new TransferTool());     // 16. Transfer
            _tools.Add(new PetCompanionTool()); // 17. Buddy / Pet
            _tools.Add(new AICodingTool());     // 18. Media / Coding
        }

        public static IBuddyTool GetById(string id) =>
            _tools.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? _tools.FirstOrDefault();
    }

    /// <summary>
    /// HomeTool: The exact 6-Card Dashboard shown in Screenshot 1 ("Everything at a glance").
    /// </summary>
    public sealed class HomeTool : IBuddyTool
    {
        public string Id => "home";
        public string Title => "Home";
        public string IconGlyph => "⌂";

        private CircularGauge _cpuGauge;
        private CircularGauge _ramGauge;
        private CircularGauge _diskGauge;
        private TextBlock _focusTimeText;
        private TextBlock _focusStatusText;
        private Border _focusToggleBtn;
        private TextBlock _focusToggleIcon;
        private DispatcherTimer _tickTimer;

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;

            var rootGrid = new Grid
            {
                Margin = new Thickness(2, 0, 2, 2)
            };

            // 2 Rows, 3 Columns
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // CARD 1: SCREEN TIME (Row 0, Col 0)
            // -------------------------------------------------------------
            var screenTimeCard = new BuddyCard { Margin = new Thickness(3) };
            var stDock = new DockPanel();

            var stHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            stHeader.Children.Add(new TextBlock
            {
                Text = "☵ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            stHeader.Children.Add(new TextBlock
            {
                Text = "SCREEN TIME",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(stHeader, Dock.Top);
            stDock.Children.Add(stHeader);

            var stBottom = new WeeklyBarChart(2); // Wednesday active
            DockPanel.SetDock(stBottom, Dock.Bottom);
            stDock.Children.Add(stBottom);

            var stCenter = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) };
            stCenter.Children.Add(new TextBlock
            {
                Text = "4h 24m",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });

            var stAppLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var appIconBadge = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = CapsuleTheme.Brush(Color.FromRgb(0, 122, 204)), // VS Code blue
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "⟨⟩",
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            stAppLine.Children.Add(appIconBadge);
            stAppLine.Children.Add(new TextBlock
            {
                Text = "Visual Studio Code · 1h 42m",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stCenter.Children.Add(stAppLine);
            stDock.Children.Add(stCenter);

            screenTimeCard.Child = stDock;
            Grid.SetRow(screenTimeCard, 0);
            Grid.SetColumn(screenTimeCard, 0);
            rootGrid.Children.Add(screenTimeCard);

            // -------------------------------------------------------------
            // CARD 2: SYSTEM (Row 0, Col 1)
            // -------------------------------------------------------------
            var systemCard = new BuddyCard { Margin = new Thickness(3) };
            var sysDock = new DockPanel();

            var sysHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            sysHeader.Children.Add(new TextBlock
            {
                Text = "⚙ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            sysHeader.Children.Add(new TextBlock
            {
                Text = "SYSTEM",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(sysHeader, Dock.Top);
            sysDock.Children.Add(sysHeader);

            var gaugesRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _cpuGauge = new CircularGauge("CPU", 1, radius: 21, thickness: 4.5);
            _ramGauge = new CircularGauge("RAM", 83, radius: 21, thickness: 5.5);
            _diskGauge = new CircularGauge("DISK", 0, radius: 21, thickness: 4.5);
            gaugesRow.Children.Add(_cpuGauge);
            gaugesRow.Children.Add(_ramGauge);
            gaugesRow.Children.Add(_diskGauge);
            sysDock.Children.Add(gaugesRow);

            systemCard.Child = sysDock;
            Grid.SetRow(systemCard, 0);
            Grid.SetColumn(systemCard, 1);
            rootGrid.Children.Add(systemCard);

            // -------------------------------------------------------------
            // CARD 3: WEATHER (Row 0, Col 2)
            // -------------------------------------------------------------
            var weatherCard = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            weatherCard.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("weather");

            var wDock = new DockPanel();
            var wHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            wHeader.Children.Add(new TextBlock
            {
                Text = "⛅ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            wHeader.Children.Add(new TextBlock
            {
                Text = "WEATHER",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(wHeader, Dock.Top);
            wDock.Children.Add(wHeader);

            var wMain = new Grid { VerticalAlignment = VerticalAlignment.Center };
            wMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wMain.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var wLeft = new StackPanel();
            wLeft.Children.Add(new TextBlock
            {
                Text = "18°",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            wLeft.Children.Add(new TextBlock
            {
                Text = "Sunny · San Francisco",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 1)
            });
            wLeft.Children.Add(new TextBlock
            {
                Text = "H 20°  L 12°",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text3)
            });
            Grid.SetColumn(wLeft, 0);
            wMain.Children.Add(wLeft);

            var wIcon = new TextBlock
            {
                Text = "☼",
                FontSize = 32,
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };
            Grid.SetColumn(wIcon, 1);
            wMain.Children.Add(wIcon);

            wDock.Children.Add(wMain);
            weatherCard.Child = wDock;
            Grid.SetRow(weatherCard, 0);
            Grid.SetColumn(weatherCard, 2);
            rootGrid.Children.Add(weatherCard);

            // -------------------------------------------------------------
            // CARD 4: FOCUS (Row 1, Col 0)
            // -------------------------------------------------------------
            var focusCard = new BuddyCard { Margin = new Thickness(3) };
            var fDock = new DockPanel();

            var fHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            fHeader.Children.Add(new TextBlock
            {
                Text = "⏱ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            fHeader.Children.Add(new TextBlock
            {
                Text = "FOCUS",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(fHeader, Dock.Top);
            fDock.Children.Add(fHeader);

            var fContent = new Grid { VerticalAlignment = VerticalAlignment.Center };
            fContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var fLeft = new StackPanel();
            _focusTimeText = new TextBlock
            {
                Text = "18:37",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            _focusStatusText = new TextBlock
            {
                Text = "Focusing",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 0)
            };
            fLeft.Children.Add(_focusTimeText);
            fLeft.Children.Add(_focusStatusText);
            Grid.SetColumn(fLeft, 0);
            fContent.Children.Add(fLeft);

            _focusToggleBtn = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            _focusToggleIcon = new TextBlock
            {
                Text = "⏸",
                FontSize = 13,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _focusToggleBtn.Child = _focusToggleIcon;
            _focusToggleBtn.MouseLeftButtonUp += (_, _) =>
            {
                TimerClockTool.ToggleRunning();
                UpdateFocusState();
            };
            Grid.SetColumn(_focusToggleBtn, 1);
            fContent.Children.Add(_focusToggleBtn);

            fDock.Children.Add(fContent);
            focusCard.Child = fDock;
            Grid.SetRow(focusCard, 1);
            Grid.SetColumn(focusCard, 0);
            rootGrid.Children.Add(focusCard);

            // -------------------------------------------------------------
            // CARD 5: CLIPBOARD (Row 1, Col 1)
            // -------------------------------------------------------------
            var clipCard = new BuddyCard { Margin = new Thickness(3) };
            var cDock = new DockPanel();

            var cHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            cHeader.Children.Add(new TextBlock
            {
                Text = "📄 ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            cHeader.Children.Add(new TextBlock
            {
                Text = "CLIPBOARD",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(cHeader, Dock.Top);
            cDock.Children.Add(cHeader);

            var cStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            cStack.Children.Add(CreateClipRow("Studio hours: Mon...", "23h"));
            cStack.Children.Add(CreateClipRow("Linen tote — natur...", "4m"));
            cDock.Children.Add(cStack);

            clipCard.Child = cDock;
            Grid.SetRow(clipCard, 1);
            Grid.SetColumn(clipCard, 1);
            rootGrid.Children.Add(clipCard);

            // -------------------------------------------------------------
            // CARD 6: NOTES (Row 1, Col 2)
            // -------------------------------------------------------------
            var notesCard = new BuddyCard { Margin = new Thickness(3) };
            var nDock = new DockPanel();

            var nHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            nHeader.Children.Add(new TextBlock
            {
                Text = "📋 ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            nHeader.Children.Add(new TextBlock
            {
                Text = "NOTES",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(nHeader, Dock.Top);
            nDock.Children.Add(nHeader);

            var nBottom = new DockPanel();
            var nEdited = new TextBlock
            {
                Text = "Edited just now",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                VerticalAlignment = VerticalAlignment.Center
            };
            var nAddBtn = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = "+",
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            nAddBtn.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("notes");
            DockPanel.SetDock(nAddBtn, Dock.Right);
            nBottom.Children.Add(nAddBtn);
            nBottom.Children.Add(nEdited);
            DockPanel.SetDock(nBottom, Dock.Bottom);
            nDock.Children.Add(nBottom);

            var nCenter = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) };
            nCenter.Children.Add(new TextBlock
            {
                Text = "Launch checklist",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            nCenter.Children.Add(new TextBlock
            {
                Text = "2/5 done",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 3, 0, 0)
            });
            nDock.Children.Add(nCenter);

            notesCard.Child = nDock;
            Grid.SetRow(notesCard, 1);
            Grid.SetColumn(notesCard, 2);
            rootGrid.Children.Add(notesCard);

            // Timer update
            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _tickTimer.Tick += (_, _) =>
            {
                var t = SystemMonitorService.Current;
                _cpuGauge.SetValue(Math.Max(1, t.CpuUsagePercent));
                _ramGauge.SetValue(t.MemoryUsagePercent > 0 ? t.MemoryUsagePercent : 83);
                _diskGauge.SetValue(t.DiskUsagePercent);
                UpdateFocusState();
            };
            _tickTimer.Start();

            return rootGrid;
        }

        private void UpdateFocusState()
        {
            if (_focusTimeText == null) return;
            _focusTimeText.Text = TimerClockTool.FormatTime(TimerClockTool.RemainingSeconds);
            _focusStatusText.Text = TimerClockTool.IsRunning ? "Focusing" : "Paused";
            _focusToggleIcon.Text = TimerClockTool.IsRunning ? "⏸" : "▶";
        }

        private static Border CreateClipRow(string title, string time)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();
            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(new TextBlock
            {
                Text = time,
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            right.Children.Add(new TextBlock
            {
                Text = "⧉",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(right, Dock.Right);
            dock.Children.Add(right);

            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(new TextBlock
            {
                Text = "📄 ",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            left.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            dock.Children.Add(left);

            border.Child = dock;
            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                try { System.Windows.Clipboard.SetText(title); } catch { }
            };
            return border;
        }

        public void OnActivated()
        {
            UpdateFocusState();
        }
    }

    /// <summary>
    /// FileShelfTool: File Shelf parked files tool matching 1:1 original Vayli.
    /// </summary>
    public sealed class FileShelfTool : IBuddyTool
    {
        public string Id => "fileshelf";
        public string Title => "File Shelf";
        public string IconGlyph => "📁";

        public FrameworkElement BuildView() => new WindowsBuddy.Tools.FileShelf.FileShelfToolView();

        public void OnActivated() { }
    }

    /// <summary>
    /// LinksTool: Quick Bookmarks matching Vayli 1:1 with search engines and saved bookmark cards.
    /// </summary>
    public sealed class LinksTool : IBuddyTool
    {
        public string Id => "links";
        public string Title => "Links";
        public string IconGlyph => "🔗";

        public FrameworkElement BuildView() => new WindowsBuddy.Tools.Links.LinksToolView();

        public void OnActivated() { }
    }

    /// <summary>
    /// ClipboardTool: Live clipboard history matching the exact Vayli split-column UI with code, image & link previews.
    /// </summary>
    public sealed class ClipboardTool : IBuddyTool
    {
        public string Id => "clipboard";
        public string Title => "Clipboard";
        public string IconGlyph => "📋";

        private WindowsBuddy.Tools.Clipboard.ClipboardToolView _view;

        public static void CaptureCurrentClipboard()
        {
            WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.CaptureCurrentClipboard();
        }

        public FrameworkElement BuildView()
        {
            WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.CaptureCurrentClipboard();
            _view = new WindowsBuddy.Tools.Clipboard.ClipboardToolView();
            return _view;
        }

        public void OnActivated()
        {
            WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.CaptureCurrentClipboard();
            _view?.RefreshList();
        }
    }

    /// <summary>
    /// NotesTool: Live notes & scratchpad matching the exact Vayli split-column UI.
    /// </summary>
    public sealed class NotesTool : IBuddyTool
    {
        public string Id => "notes";
        public string Title => "Notes";
        public string IconGlyph => "📝";

        private WindowsBuddy.BuddyCapsule.UI.NotesToolView _view;

        public FrameworkElement BuildView()
        {
            _view = new WindowsBuddy.BuddyCapsule.UI.NotesToolView();
            return _view;
        }

        public void OnActivated()
        {
            _view?.RefreshList();
        }
    }

    /// <summary>
    /// TimerClockTool: Full Timers tool matching Vayli (Pomodoro, Countdown, Stopwatch, Hydration Water).
    /// </summary>
    public sealed class TimerClockTool : IBuddyTool
    {
        public string Id => "focus";
        public string Title => "Timers";
        public string IconGlyph => "⏱";

        public static int RemainingSeconds => WindowsBuddy.Tools.Timer.TimerService.Instance.PomoRemainingSeconds;
        public static bool IsRunning => WindowsBuddy.Tools.Timer.TimerService.Instance.PomoIsRunning;
        public static void ToggleRunning() => WindowsBuddy.Tools.Timer.TimerService.Instance.TogglePomo();
        public static string FormatTime(int totalSec) => $"{totalSec / 60:D2}:{totalSec % 60:D2}";

        private WindowsBuddy.BuddyCapsule.UI.TimerToolView _view;

        public FrameworkElement BuildView()
        {
            _view = new WindowsBuddy.BuddyCapsule.UI.TimerToolView();
            return _view;
        }

        public void OnActivated()
        {
            _view?.UpdateVisuals();
        }
    }

    #region Supplementary Dock Tools for the 18 Dock Icons

    /// <summary>
    /// SystemTool: Real-time 4-Card hardware telemetry (Battery, Network rates, CPU/RAM/Disk, Volume & Audio Endpoint).
    /// </summary>
    public sealed class SystemTool : IBuddyTool
    {
        public string Id => "system";
        public string Title => "System";
        public string IconGlyph => "⚙";

        private WindowsBuddy.BuddyCapsule.UI.SystemToolView _view;

        public FrameworkElement BuildView()
        {
            _view = new WindowsBuddy.BuddyCapsule.UI.SystemToolView();
            return _view;
        }

        public void OnActivated()
        {
            _view?.RefreshAllMetrics();
        }
    }

    public sealed class ClockTool : IBuddyTool
    {
        public string Id => "clock";
        public string Title => "Clock";
        public string IconGlyph => "🕐";
        public FrameworkElement BuildView() => new WindowsBuddy.Tools.Clock.ClockToolView();
        public void OnActivated() { }
    }

    public sealed class CalendarTool : IBuddyTool
    {
        public string Id => "calendar";
        public string Title => "Calendar";
        public string IconGlyph => "📅";
        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = DateTime.Now.ToString("dddd, MMMM d, yyyy"), FontSize = 18, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = "No upcoming events today. Your calendar is clear!", FontSize = 12, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            card.Child = stack;
            return card;
        }
        public void OnActivated() { }
    }

    public sealed class TransferTool : IBuddyTool
    {
        public string Id => "transfer";
        public string Title => "Transfer";
        public string IconGlyph => "⇄";
        public FrameworkElement BuildView() => new FileShelfTool().BuildView();
        public void OnActivated() { }
    }

    public sealed class QuickToolsTool : IBuddyTool
    {
        public string Id => "units";
        public string Title => "Units";
        public string IconGlyph => "📐";
        public FrameworkElement BuildView() => new WindowsBuddy.Tools.Units.UnitsToolView();
        public void OnActivated() { }
    }

    /// <summary>
    /// MessageBannerTool: Notch Banner tool matching Vayli 1:1 with live preview, animations, colors and notch flash.
    /// </summary>
    public sealed class MessageBannerTool : IBuddyTool
    {
        public string Id => "message";
        public string Title => "Message";
        public string IconGlyph => "🪪";
        public FrameworkElement BuildView() => new WindowsBuddy.Tools.Message.MessageToolView();
        public void OnActivated() { }
    }

    public sealed class AudioTool : IBuddyTool
    {
        public string Id => "audio";
        public string Title => "Audio";
        public string IconGlyph => "🎚";
        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "MEDIA & SOUND", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Accent), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new StaticBeatBars(8, 24) { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 12) });
            stack.Children.Add(new TextBlock { Text = "AirPods Pro Connected · Volume 74%", FontSize = 11.5, Foreground = CapsuleTheme.Brush(p.Text2), HorizontalAlignment = HorizontalAlignment.Center });
            card.Child = stack;
            return card;
        }
        public void OnActivated() { }
    }

    public sealed class PetCompanionTool : IBuddyTool
    {
        public string Id => "pet";
        public string Title => "Buddy";
        public string IconGlyph => "✨";
        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(16) };

            var petView = new PetView("happy", 88)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                ToolTip = "Click me! / کلیک کنید تا واکنش نشون بدم"
            };
            stack.Children.Add(petView);

            stack.Children.Add(new TextBlock
            {
                Text = "Vayli Orb Companion",
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 2)
            });

            var quoteText = new TextBlock
            {
                Text = PetEngine.Instance.CurrentStatusLine,
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 10),
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(quoteText);

            PetEngine.Instance.MoodChanged += _ =>
            {
                quoteText.Text = PetEngine.Instance.CurrentStatusLine;
            };

            var moodRow = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            var quickMoods = new (string Glyph, string Label, OrbMood Mood)[]
            {
                ("😊", "شاد", OrbMood.Happy),
                ("😠", "اخم", OrbMood.Angry),
                ("💖", "دلبر", OrbMood.Love),
                ("😉", "چشمک", OrbMood.Wink),
                ("😴", "خواب", OrbMood.Sleepy),
                ("🌟", "عادی", OrbMood.Idle)
            };

            foreach (var qm in quickMoods)
            {
                var captured = qm.Mood;
                var btn = new BuddyPillButton(qm.Glyph, qm.Label) { Margin = new Thickness(2) };
                btn.Clicked += () =>
                {
                    PetEngine.Instance.SetMood(captured);
                    petView.SetMood(captured);
                };
                moodRow.Children.Add(btn);
            }

            var pokeBtn = new BuddyPillButton("🐾", "قلقلک") { Margin = new Thickness(2) };
            pokeBtn.Clicked += () =>
            {
                petView.Orb.Poke();
                petView.Orb.SpawnSparkleBurst();
            };
            moodRow.Children.Add(pokeBtn);

            stack.Children.Add(moodRow);
            card.Child = stack;
            return card;
        }
        public void OnActivated() { }
    }

    #endregion
}
