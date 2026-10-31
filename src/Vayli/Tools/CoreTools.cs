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

            // 18 tools matching the exact dock sequence in Screenshot 1
            _tools.Add(new HomeTool());         // 1. Home
            _tools.Add(new SystemTool());       // 2. System
            _tools.Add(new ClockTool());        // 3. Clock
            _tools.Add(new TimerClockTool());   // 4. Focus
            _tools.Add(new CalendarTool());     // 5. Calendar
            _tools.Add(new WeatherTool());      // 6. Weather
            _tools.Add(new ClipboardTool());    // 7. Clipboard
            _tools.Add(new NotesTool());        // 8. Notes
            _tools.Add(new FileShelfTool());    // 9. File Shelf
            _tools.Add(new DropTool());         // 10. Drop
            _tools.Add(new TransferTool());     // 11. Transfer
            _tools.Add(new QuickToolsTool());   // 12. Tools
            _tools.Add(new LinksTool());        // 13. Links
            _tools.Add(new CardsTool());        // 14. Cards
            _tools.Add(new AudioTool());        // 15. Audio
            _tools.Add(new ScreenTimeTool());   // 16. Screen Time
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
    /// FileShelfTool: File Shelf shown in Screenshot 4 Left ("Your files and links, one hover away").
    /// </summary>
    public sealed class FileShelfTool : IBuddyTool
    {
        public string Id => "fileshelf";
        public string Title => "File Shelf";
        public string IconGlyph => "📁";

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var root = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. Left Sidebar: SHELVES
            var sidebar = new BuddyCard { Margin = new Thickness(2), Padding = new Thickness(8) };
            var sbDock = new DockPanel();

            var sbHeader = new TextBlock
            {
                Text = "SHELVES",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text3),
                Margin = new Thickness(4, 2, 0, 8)
            };
            DockPanel.SetDock(sbHeader, Dock.Top);
            sbDock.Children.Add(sbHeader);

            var newShelfBtn = new Border
            {
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 4, 6, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 6, 0, 0),
                Child = new TextBlock
                {
                    Text = "+ New shelf",
                    FontSize = 10.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            DockPanel.SetDock(newShelfBtn, Dock.Bottom);
            sbDock.Children.Add(newShelfBtn);

            var shelvesList = new StackPanel();
            var deskItem = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 6, 8, 6),
                Cursor = Cursors.Hand
            };
            var itemDock = new DockPanel();
            var badge = new Border
            {
                Background = CapsuleTheme.Brush(p.ControlFill),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(5, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = "5",
                    FontSize = 10,
                    Foreground = CapsuleTheme.Brush(p.Text2)
                }
            };
            DockPanel.SetDock(badge, Dock.Right);
            itemDock.Children.Add(badge);
            itemDock.Children.Add(new TextBlock
            {
                Text = "📁 Desk",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            deskItem.Child = itemDock;
            shelvesList.Children.Add(deskItem);

            sbDock.Children.Add(shelvesList);
            sidebar.Child = sbDock;
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);

            // 2. Main Content Area
            var mainCard = new BuddyCard { Margin = new Thickness(2), Padding = new Thickness(12, 10, 12, 10) };
            var mainDock = new DockPanel();

            // Header bar
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(CreateTextButton("Recent ⌵"));
            actions.Children.Add(new TextBlock { Text = " · ", Foreground = CapsuleTheme.Brush(p.Text3), VerticalAlignment = VerticalAlignment.Center });
            actions.Children.Add(CreateTextButton("Select"));
            actions.Children.Add(new TextBlock { Text = " · ", Foreground = CapsuleTheme.Brush(p.Text3), VerticalAlignment = VerticalAlignment.Center });
            actions.Children.Add(CreateTextButton("Copy all"));
            actions.Children.Add(new TextBlock { Text = " · ", Foreground = CapsuleTheme.Brush(p.Text3), VerticalAlignment = VerticalAlignment.Center });
            actions.Children.Add(CreateTextButton("Clear"));
            DockPanel.SetDock(actions, Dock.Right);
            topBar.Children.Add(actions);

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Desk",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "  5 items · 39.7 MB",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            topBar.Children.Add(titleStack);
            DockPanel.SetDock(topBar, Dock.Top);
            mainDock.Children.Add(topBar);

            // Drop zone at bottom
            var dropZone = new Border
            {
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 8, 0, 0)
            };
            var dropDock = new DockPanel();
            var chooseBtn = new BuddyPillButton(null, "Choose files") { Padding = new Thickness(9, 4, 9, 4) };
            DockPanel.SetDock(chooseBtn, Dock.Right);
            dropDock.Children.Add(chooseBtn);

            var dropLabel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            dropLabel.Children.Add(new TextBlock
            {
                Text = "⇪ ",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text3)
            });
            dropLabel.Children.Add(new TextBlock
            {
                Text = "Drop files here - drag out to use anywhere",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text3)
            });
            dropDock.Children.Add(dropLabel);
            dropZone.Child = dropDock;
            DockPanel.SetDock(dropZone, Dock.Bottom);
            mainDock.Children.Add(dropZone);

            // Parked files horizontal/grid list
            var filesWrap = new WrapPanel { Orientation = Orientation.Horizontal };
            filesWrap.Children.Add(CreateFileItem("🗜", Color.FromRgb(242, 196, 68), "Site photos...", "37.1 MB"));
            filesWrap.Children.Add(CreateFileItem("W", Color.FromRgb(43, 114, 219), "Interview no...", "48 KB"));
            filesWrap.Children.Add(CreateFileItem("PDF", Color.FromRgb(235, 60, 60), "Invoice 2026...", "180 KB"));
            filesWrap.Children.Add(CreateFileItem("PDF", Color.FromRgb(235, 60, 60), "Q4 roadmap...", "2.3 MB"));
            filesWrap.Children.Add(CreateFileItem("🌄", Color.FromRgb(165, 85, 230), "Moodboard -...", "127 KB"));

            mainDock.Children.Add(filesWrap);
            mainCard.Child = mainDock;
            Grid.SetColumn(mainCard, 1);
            root.Children.Add(mainCard);

            return root;
        }

        private static Border CreateFileItem(string badgeText, Color badgeColor, string title, string size)
        {
            var p = CapsuleTheme.Current;
            var box = new Border
            {
                Width = 86,
                Margin = new Thickness(4, 2, 4, 2),
                Padding = new Thickness(4),
                Cursor = Cursors.Hand
            };

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            // Badge Icon
            var iconBox = new Border
            {
                Width = 42,
                Height = 44,
                CornerRadius = new CornerRadius(7),
                Background = CapsuleTheme.Brush(badgeColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            iconBox.Child = new TextBlock
            {
                Text = badgeText,
                FontSize = badgeText.Length > 2 ? 10 : 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(iconBox);

            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 80
            });

            sp.Children.Add(new TextBlock
            {
                Text = size,
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            box.Child = sp;
            return box;
        }

        private static TextBlock CreateTextButton(string text)
        {
            var p = CapsuleTheme.Current;
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            return tb;
        }

        public void OnActivated() { }
    }

    /// <summary>
    /// LinksTool: Quick Bookmarks shown in Screenshot 4 Right ("Your files and links, one hover away").
    /// </summary>
    public sealed class LinksTool : IBuddyTool
    {
        public string Id => "links";
        public string Title => "Links";
        public string IconGlyph => "🔗";

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4, 2, 4, 2) };
            var dock = new DockPanel();

            // Search Bar at Top
            var searchBox = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 5, 6, 5),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var sDock = new DockPanel();

            var searchBtn = new BuddyPillButton(null, "Search ↗") { Padding = new Thickness(10, 4, 10, 4) };
            searchBtn.Clicked += () =>
            {
                try { Process.Start(new ProcessStartInfo("https://www.google.com") { UseShellExecute = true }); } catch { }
            };
            DockPanel.SetDock(searchBtn, Dock.Right);
            sDock.Children.Add(searchBtn);

            var input = new TextBox
            {
                Text = "Search or paste a link",
                Background = Brushes.Transparent,
                Foreground = CapsuleTheme.Brush(p.Text2),
                BorderThickness = new Thickness(0),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            input.GotFocus += (_, _) => { if (input.Text == "Search or paste a link") input.Text = ""; };
            sDock.Children.Add(input);
            searchBox.Child = sDock;
            DockPanel.SetDock(searchBox, Dock.Top);
            dock.Children.Add(searchBox);

            // Quick suggestion chips
            var chipsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            string[] chips = { "Google", "YouTube", "GitHub", "Stack Overflow", "ChatGPT", "Perplexity", "+" };
            foreach (var ch in chips)
            {
                string query = ch;
                var chip = new BuddyPillButton(null, ch) { Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2) };
                chip.Clicked += () =>
                {
                    string url = query switch
                    {
                        "Google" => "https://google.com",
                        "YouTube" => "https://youtube.com",
                        "GitHub" => "https://github.com",
                        "Stack Overflow" => "https://stackoverflow.com",
                        "ChatGPT" => "https://chatgpt.com",
                        "Perplexity" => "https://perplexity.ai",
                        _ => "https://google.com"
                    };
                    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
                };
                chipsWrap.Children.Add(chip);
            }
            DockPanel.SetDock(chipsWrap, Dock.Top);
            dock.Children.Add(chipsWrap);

            // Saved Links Header
            var savedHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var addBtn = new BuddyPillButton(null, "+ Add Link") { Padding = new Thickness(8, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(addBtn, Dock.Right);
            savedHeader.Children.Add(addBtn);

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Saved links",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "  8 links",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                VerticalAlignment = VerticalAlignment.Center
            });
            savedHeader.Children.Add(titleStack);
            DockPanel.SetDock(savedHeader, Dock.Top);
            dock.Children.Add(savedHeader);

            // Saved Links Cards
            var linksGrid = new UniformGrid { Columns = 4, Rows = 1 };
            linksGrid.Children.Add(CreateLinkCard("F", Color.FromRgb(150, 80, 220), "Figma — Launch...", "figma.com", "https://figma.com"));
            linksGrid.Children.Add(CreateLinkCard("G", Color.FromRgb(40, 160, 120), "GitHub", "github.com", "https://github.com"));
            linksGrid.Children.Add(CreateLinkCard("Y", Color.FromRgb(200, 40, 40), "YouTube — La...", "youtube.com", "https://youtube.com"));
            linksGrid.Children.Add(CreateLinkCard("C", Color.FromRgb(60, 120, 230), "Google Calen...", "calendar.google...", "https://calendar.google.com"));

            dock.Children.Add(linksGrid);
            card.Child = dock;
            return card;
        }

        private static Border CreateLinkCard(string initial, Color iconBg, string title, string domain, string url)
        {
            var p = CapsuleTheme.Current;
            var box = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 8, 8, 8),
                Margin = new Thickness(3),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();

            var menu = new TextBlock
            {
                Text = "⋮",
                FontSize = 13,
                Foreground = CapsuleTheme.Brush(p.Text3),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(menu, Dock.Right);
            dock.Children.Add(menu);

            var avatar = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Background = CapsuleTheme.Brush(iconBg),
                Margin = new Thickness(0, 0, 7, 0),
                Child = new TextBlock
                {
                    Text = initial,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            DockPanel.SetDock(avatar, Dock.Left);
            dock.Children.Add(avatar);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            textStack.Children.Add(new TextBlock
            {
                Text = domain,
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            dock.Children.Add(textStack);

            box.Child = dock;
            box.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            };
            return box;
        }

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
    /// TimerClockTool: Focus countdown timer.
    /// </summary>
    public sealed class TimerClockTool : IBuddyTool
    {
        public string Id => "focus";
        public string Title => "Focus";
        public string IconGlyph => "⏱";

        public static int RemainingSeconds { get; private set; } = 18 * 60 + 37; // 18:37 matching Screenshot 1
        public static bool IsRunning { get; private set; } = true;
        private static DispatcherTimer _countdown;

        static TimerClockTool()
        {
            _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdown.Tick += (_, _) =>
            {
                if (IsRunning && RemainingSeconds > 0)
                {
                    RemainingSeconds--;
                    if (RemainingSeconds == 0) IsRunning = false;
                }
            };
            _countdown.Start();
        }

        public static void ToggleRunning() => IsRunning = !IsRunning;

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var timerCard = new BuddyCard { Margin = new Thickness(3) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

            var display = new TextBlock
            {
                Text = FormatTime(RemainingSeconds),
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(display);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var startStopBtn = new BuddyPillButton(null, IsRunning ? "Pause" : "Start Focus");
            startStopBtn.Clicked += () =>
            {
                IsRunning = !IsRunning;
                display.Text = FormatTime(RemainingSeconds);
            };
            var reset25 = new BuddyPillButton(null, "25m");
            reset25.Clicked += () => { IsRunning = false; RemainingSeconds = 25 * 60; display.Text = FormatTime(RemainingSeconds); };
            var reset18 = new BuddyPillButton(null, "18m");
            reset18.Clicked += () => { IsRunning = true; RemainingSeconds = 18 * 60 + 37; display.Text = FormatTime(RemainingSeconds); };

            controls.Children.Add(startStopBtn);
            controls.Children.Add(reset25);
            controls.Children.Add(reset18);
            stack.Children.Add(controls);
            timerCard.Child = stack;

            var uiTick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            uiTick.Tick += (_, _) => display.Text = FormatTime(RemainingSeconds);
            uiTick.Start();

            Grid.SetColumn(timerCard, 0);
            grid.Children.Add(timerCard);

            var worldCard = new BuddyCard { Margin = new Thickness(3) };
            var worldStack = new StackPanel();
            worldStack.Children.Add(new TextBlock
            {
                Text = "WORLD CLOCK",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var utcNow = DateTime.UtcNow;
            AddCityRow(worldStack, "San Francisco (PDT)", utcNow.AddHours(-7).ToString("HH:mm"));
            AddCityRow(worldStack, "Local Time", DateTime.Now.ToString("HH:mm"));
            AddCityRow(worldStack, "London (UTC)", utcNow.ToString("HH:mm"));
            AddCityRow(worldStack, "Tokyo (JST)", utcNow.AddHours(9).ToString("HH:mm"));

            worldCard.Child = worldStack;
            Grid.SetColumn(worldCard, 1);
            grid.Children.Add(worldCard);

            return grid;
        }

        private static void AddCityRow(StackPanel parent, string city, string time)
        {
            var p = CapsuleTheme.Current;
            var row = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var dp = new DockPanel();
            var tBlock = new TextBlock { Text = time, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text), HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(tBlock, Dock.Right);
            dp.Children.Add(tBlock);
            dp.Children.Add(new TextBlock { Text = city, FontSize = 11, Foreground = CapsuleTheme.Brush(p.Text2) });
            row.Child = dp;
            parent.Children.Add(row);
        }

        public static string FormatTime(int totalSec) => $"{totalSec / 60:00}:{totalSec % 60:00}";
        public void OnActivated() { }
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
        public string IconGlyph => "🕒";
        public FrameworkElement BuildView() => new TimerClockTool().BuildView();
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
        public string Id => "tools";
        public string Title => "Tools";
        public string IconGlyph => "📏";
        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "DESKTOP COMPANION TOOLS", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Accent), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = "Quick calculations, color picker, screenshot tools and notch triggers.", FontSize = 11.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            card.Child = stack;
            return card;
        }
        public void OnActivated() { }
    }

    public sealed class CardsTool : IBuddyTool
    {
        public string Id => "cards";
        public string Title => "Cards";
        public string IconGlyph => "🗂";
        public FrameworkElement BuildView() => new LinksTool().BuildView();
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
