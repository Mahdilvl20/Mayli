using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        public static void RegisterDefaults()
        {
            if (_tools.Count > 0) return;
            _tools.Add(new HomeTool());
            _tools.Add(new FileShelfTool());
            _tools.Add(new ClipboardTool());
            _tools.Add(new NotesTool());
            _tools.Add(new TimerClockTool());
            _tools.Add(new WeatherTool());
            _tools.Add(new ScreenTimeTool());
            _tools.Add(new AICodingTool());
            _tools.Add(new DropTool());
        }

        public static IBuddyTool GetById(string id) =>
            _tools.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? _tools.FirstOrDefault();
    }

    /// <summary>
    /// HomeTool (`WindowsBuddy.Tools.HomeTool`) — main multi-card dashboard inside the expanded 752x352 notch.
    /// </summary>
    public sealed class HomeTool : IBuddyTool
    {
        public string Id => "home";
        public string Title => "Home";
        public string IconGlyph => "⌂";

        private TextBlock _mediaTitle;
        private TextBlock _mediaArtist;
        private TextBlock _playBtnLabel;
        private StaticBeatBars _beatBars;
        private TextBlock _petLineText;
        private TextBlock _petPoseBadge;
        private PetView _petView;
        private TextBlock _cpuValue;
        private TextBlock _ramValue;
        private TextBlock _batteryValue;
        private TextBlock _clockBig;
        private TextBlock _dateSub;

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });

            // Card 1: Media & Audio Now Playing
            var mediaCard = new BuddyCard { Margin = new Thickness(4) };
            var mediaStack = new StackPanel();

            var mediaHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var musicBadge = new TextBlock
            {
                Text = "NOW PLAYING",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            _beatBars = new StaticBeatBars(6, 14) { HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(_beatBars, Dock.Right);
            mediaHeader.Children.Add(_beatBars);
            mediaHeader.Children.Add(musicBadge);
            mediaStack.Children.Add(mediaHeader);

            _mediaTitle = new TextBlock
            {
                Text = MediaService.Instance.State.Title,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _mediaArtist = new TextBlock
            {
                Text = MediaService.Instance.State.Artist,
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 12),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            mediaStack.Children.Add(_mediaTitle);
            mediaStack.Children.Add(_mediaArtist);

            var transportRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 8) };
            var prevBtn = CreateTransportButton("⏮", () => MediaService.Instance.PreviousTrack());
            var playBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Accent),
                CornerRadius = new CornerRadius(18),
                Width = 36,
                Height = 36,
                Margin = new Thickness(10, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _playBtnLabel = new TextBlock
            {
                Text = MediaService.Instance.State.IsPlaying ? "⏸" : "▶",
                FontSize = 14,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            playBorder.Child = _playBtnLabel;
            playBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                MediaService.Instance.TogglePlayPause();
                RefreshMedia();
            };
            var nextBtn = CreateTransportButton("⏭", () => MediaService.Instance.NextTrack());

            transportRow.Children.Add(prevBtn);
            transportRow.Children.Add(playBorder);
            transportRow.Children.Add(nextBtn);
            mediaStack.Children.Add(transportRow);

            // Live Clock sub-row inside Column 0 bottom
            var clockPanel = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 4, 0, 0)
            };
            var clockRow = new DockPanel();
            _clockBig = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm:ss"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            _dateSub = new TextBlock
            {
                Text = DateTime.Now.ToString("ddd, MMM d"),
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(_dateSub, Dock.Right);
            clockRow.Children.Add(_dateSub);
            clockRow.Children.Add(_clockBig);
            clockPanel.Child = clockRow;
            mediaStack.Children.Add(clockPanel);

            mediaCard.Child = mediaStack;
            Grid.SetColumn(mediaCard, 0);
            grid.Children.Add(mediaCard);

            // Card 2: Interactive Vayli Pet Companion (`WindowsBuddy.BuddyCapsule.Renderer.PetView`)
            var petCard = new BuddyCard { Margin = new Thickness(4) };
            var petDock = new Grid();
            petDock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            petDock.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            petDock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var petHeader = new DockPanel();
            var petTitle = new TextBlock
            {
                Text = "BUDDY PET",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent)
            };
            _petPoseBadge = new TextBlock
            {
                Text = PetEngine.Instance.CurrentPose.ToUpperInvariant(),
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(_petPoseBadge, Dock.Right);
            petHeader.Children.Add(_petPoseBadge);
            petHeader.Children.Add(petTitle);
            Grid.SetRow(petHeader, 0);
            petDock.Children.Add(petHeader);

            _petView = new PetView(PetEngine.Instance.CurrentPose, 118)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Click to cycle pose • Hover to wave!"
            };
            Grid.SetRow(_petView, 1);
            petDock.Children.Add(_petView);

            _petLineText = new TextBlock
            {
                Text = PetEngine.Instance.CurrentStatusLine,
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(_petLineText, 2);
            petDock.Children.Add(_petLineText);

            PetEngine.Instance.PoseChanged += () =>
            {
                _petPoseBadge.Text = PetEngine.Instance.CurrentPose.ToUpperInvariant();
                _petLineText.Text = PetEngine.Instance.CurrentStatusLine;
                _petView.SetFrame(PetEngine.Instance.CurrentPose);
            };

            petCard.Child = petDock;
            Grid.SetColumn(petCard, 1);
            grid.Children.Add(petCard);

            // Card 3: System Telemetry (`SystemMonitorService`)
            var sysCard = new BuddyCard { Margin = new Thickness(4) };
            var sysStack = new StackPanel();
            sysStack.Children.Add(new TextBlock
            {
                Text = "SYSTEM TELEMETRY",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.CSystem),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var t = SystemMonitorService.Current;
            _cpuValue = AddMetricRow(sysStack, "CPU Load", $"{t.CpuUsagePercent:0}%", p.CSystem);
            _ramValue = AddMetricRow(sysStack, "Memory", $"{t.MemoryUsedGb:0.0} / {t.MemoryTotalGb:0.0} GB ({t.MemoryUsagePercent:0}%)", p.CFocus);
            _batteryValue = AddMetricRow(sysStack, "Battery", $"{t.BatteryPercent}% {(t.IsCharging ? "⚡ Charging" : "On Battery")}", p.Success);

            var poseButtonsPanel = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var pose in new[] { "idle", "happy", "music-01", "coffee-break", "sleepy" })
            {
                string captured = pose;
                var chip = new BuddyPillButton(null, captured) { Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(2) };
                chip.Clicked += () => PetEngine.Instance.SetPose(captured, $"Switched mood to {captured}!");
                poseButtonsPanel.Children.Add(chip);
            }
            sysStack.Children.Add(poseButtonsPanel);

            sysCard.Child = sysStack;
            Grid.SetColumn(sysCard, 2);
            grid.Children.Add(sysCard);

            var uiClock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            uiClock.Tick += (_, _) =>
            {
                _clockBig.Text = DateTime.Now.ToString("HH:mm:ss");
                _dateSub.Text = DateTime.Now.ToString("ddd, MMM d");
                RefreshTelemetry(SystemMonitorService.Current);
                RefreshMedia();
            };
            uiClock.Start();

            return grid;
        }

        private static Border CreateTransportButton(string glyph, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var b = new Border
            {
                Background = CapsuleTheme.Brush(p.ControlFill),
                CornerRadius = new CornerRadius(15),
                Width = 30,
                Height = 30,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = glyph,
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            b.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick?.Invoke(); };
            return b;
        }

        private static TextBlock AddMetricRow(StackPanel parent, string label, string value, Color dotColor)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var dock = new DockPanel();
            var valBlock = new TextBlock
            {
                Text = value,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(valBlock, Dock.Right);
            var lblBlock = new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2)
            };
            dock.Children.Add(valBlock);
            dock.Children.Add(lblBlock);
            border.Child = dock;
            parent.Children.Add(border);
            return valBlock;
        }

        private void RefreshMedia()
        {
            if (_mediaTitle == null) return;
            var s = MediaService.Instance.State;
            _mediaTitle.Text = s.Title;
            _mediaArtist.Text = s.Artist;
            _playBtnLabel.Text = s.IsPlaying ? "⏸" : "▶";
            _beatBars.IsPlaying = s.IsPlaying;
        }

        private void RefreshTelemetry(SystemTelemetryData t)
        {
            if (_cpuValue == null || t == null) return;
            _cpuValue.Text = $"{t.CpuUsagePercent:0}%";
            _ramValue.Text = $"{t.MemoryUsedGb:0.0}/{t.MemoryTotalGb:0.0} GB ({t.MemoryUsagePercent:0}%)";
            _batteryValue.Text = $"{t.BatteryPercent}% {(t.IsCharging ? "⚡" : "")}";
        }

        public void OnActivated() => RefreshTelemetry(SystemMonitorService.Current);
    }

    /// <summary>
    /// FileShelfTool (`WindowsBuddy.Tools.FileShelf.FileShelfTool`) — Drag & Drop file shelf inside the notch.
    /// </summary>
    public sealed class FileShelfTool : IBuddyTool
    {
        public string Id => "shelf";
        public string Title => "File Shelf";
        public string IconGlyph => "📥";

        private static readonly ObservableCollection<string> _parkedFiles = new();
        private StackPanel _itemsPanel;

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var root = new BuddyCard
            {
                Margin = new Thickness(4),
                AllowDrop = true
            };

            var dock = new DockPanel();
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(new TextBlock
            {
                Text = "FILE SHELF — Drag & drop any files here to park them in the notch",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });

            var clearBtn = new BuddyPillButton("🗑", "Clear All") { HorizontalAlignment = HorizontalAlignment.Right };
            clearBtn.Clicked += () =>
            {
                _parkedFiles.Clear();
                RebuildList();
            };
            DockPanel.SetDock(clearBtn, Dock.Right);
            header.Children.Add(clearBtn);
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            _itemsPanel = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _itemsPanel
            };
            dock.Children.Add(scroll);
            root.Child = dock;

            root.DragOver += (_, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };

            root.Drop += (_, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
                {
                    foreach (var f in files)
                    {
                        if (!_parkedFiles.Contains(f))
                            _parkedFiles.Add(f);
                    }
                    RebuildList();
                }
            };

            RebuildList();
            return root;
        }

        private void RebuildList()
        {
            if (_itemsPanel == null) return;
            _itemsPanel.Children.Clear();
            var p = CapsuleTheme.Current;

            if (_parkedFiles.Count == 0)
            {
                _itemsPanel.Children.Add(new TextBlock
                {
                    Text = "Drop files here from Explorer or click + Add Sample File to test.",
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 36, 0, 12)
                });
                return;
            }

            foreach (var filePath in _parkedFiles)
            {
                string captured = filePath;
                var row = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface2),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 0, 0, 5),
                    Cursor = Cursors.Hand
                };
                var dp = new DockPanel();
                var openBtn = new BuddyPillButton("↗", "Open") { HorizontalAlignment = HorizontalAlignment.Right };
                openBtn.Clicked += () =>
                {
                    try { Process.Start(new ProcessStartInfo(captured) { UseShellExecute = true }); } catch { }
                };
                DockPanel.SetDock(openBtn, Dock.Right);
                dp.Children.Add(openBtn);
                dp.Children.Add(new TextBlock
                {
                    Text = $"{Path.GetFileName(captured)}  ({captured})",
                    FontSize = 11.5,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                row.Child = dp;
                _itemsPanel.Children.Add(row);
            }
        }

        public void OnActivated() => RebuildList();
    }

    /// <summary>
    /// ClipboardTool (`WindowsBuddy.Tools.Clipboard.ClipboardTool`) — Live Clipboard History inside the notch.
    /// </summary>
    public sealed class ClipboardTool : IBuddyTool
    {
        public string Id => "clipboard";
        public string Title => "Clipboard";
        public string IconGlyph => "📋";

        public static List<string> History { get; } = new();
        private StackPanel _listPanel;

        public static void CaptureCurrentClipboard()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        History.Remove(text);
                        History.Insert(0, text);
                        if (History.Count > AppPreferences.Instance.Raw.ClipboardHistoryLimit)
                            History.RemoveAt(History.Count - 1);
                    }
                }
            }
            catch
            {
                // Ignore clipboard contention
            }
        }

        public FrameworkElement BuildView()
        {
            CaptureCurrentClipboard();
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var dock = new DockPanel();

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            top.Children.Add(new TextBlock
            {
                Text = "CLIPBOARD HISTORY — Click any item to copy back",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.CClipboard)
            });
            var refreshBtn = new BuddyPillButton("↻", "Capture Now") { HorizontalAlignment = HorizontalAlignment.Right };
            refreshBtn.Clicked += () => { CaptureCurrentClipboard(); RenderItems(); };
            DockPanel.SetDock(refreshBtn, Dock.Right);
            top.Children.Add(refreshBtn);
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);

            _listPanel = new StackPanel();
            dock.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _listPanel });
            card.Child = dock;

            RenderItems();
            return card;
        }

        private void RenderItems()
        {
            if (_listPanel == null) return;
            _listPanel.Children.Clear();
            var p = CapsuleTheme.Current;

            if (History.Count == 0)
            {
                _listPanel.Children.Add(new TextBlock
                {
                    Text = "No clipboard snippets recorded yet. Copy any text to see it here!",
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    Margin = new Thickness(0, 28, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            foreach (var snippet in History.Take(15))
            {
                string captured = snippet;
                var row = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 0, 5),
                    Cursor = Cursors.Hand
                };
                row.Child = new TextBlock
                {
                    Text = captured.Replace("\r", " ").Replace("\n", " ↵ "),
                    FontSize = 11.5,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                row.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    try { Clipboard.SetText(captured); } catch { }
                };
                _listPanel.Children.Add(row);
            }
        }

        public void OnActivated()
        {
            CaptureCurrentClipboard();
            RenderItems();
        }
    }

    /// <summary>
    /// NotesTool (`WindowsBuddy.Tools.Notes.NotesTool`) — Persistent quick scratchpad in the notch.
    /// </summary>
    public sealed class NotesTool : IBuddyTool
    {
        public string Id => "notes";
        public string Title => "Notes";
        public string IconGlyph => "📝";
        private const string NotesFile = "quick_notes.txt";

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var card = new BuddyCard { Margin = new Thickness(4) };
            var dock = new DockPanel();

            var header = new TextBlock
            {
                Text = "QUICK SCRATCHPAD (Auto-saved to AppData)",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.CNotes),
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            string filePath = Path.Combine(AppPaths.DataDir, NotesFile);
            string initial = PersistenceHelper.SafeReadAllText(filePath)
                          ?? "Welcome to Vayli Notes!\nType anything here — your notes persist automatically.";

            var box = new TextBox
            {
                Text = initial,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = CapsuleTheme.Brush(p.Surface2),
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Accent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                FontSize = 12.5
            };
            box.TextChanged += (_, _) => PersistenceHelper.SafeWriteAllText(filePath, box.Text, createBackup: false);

            dock.Children.Add(box);
            card.Child = dock;
            return card;
        }

        public void OnActivated() { }
    }

    /// <summary>
    /// TimerClockTool (`WindowsBuddy.Tools.Timer.TimerTool` & `ClockTool`) — Focus countdown timer & world clocks.
    /// </summary>
    public sealed class TimerClockTool : IBuddyTool
    {
        public string Id => "timer";
        public string Title => "Timer & Clock";
        public string IconGlyph => "⏱";

        public static int RemainingSeconds { get; private set; } = 25 * 60;
        public static bool IsRunning { get; private set; }
        private static DispatcherTimer _countdown;

        static TimerClockTool()
        {
            _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdown.Tick += (_, _) =>
            {
                if (IsRunning && RemainingSeconds > 0)
                {
                    RemainingSeconds--;
                    if (RemainingSeconds == 0)
                        IsRunning = false;
                }
            };
            _countdown.Start();
        }

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { Margin = new Thickness(4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var timerCard = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

            var display = new TextBlock
            {
                Text = FormatTime(RemainingSeconds),
                FontSize = 34,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(display);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var startStopBtn = new BuddyPillButton("▶", IsRunning ? "Pause" : "Start Focus");
            startStopBtn.Clicked += () =>
            {
                IsRunning = !IsRunning;
                display.Text = FormatTime(RemainingSeconds);
            };
            var reset25 = new BuddyPillButton(null, "25m");
            reset25.Clicked += () => { IsRunning = false; RemainingSeconds = 25 * 60; display.Text = FormatTime(RemainingSeconds); };
            var reset5 = new BuddyPillButton(null, "5m");
            reset5.Clicked += () => { IsRunning = false; RemainingSeconds = 5 * 60; display.Text = FormatTime(RemainingSeconds); };

            controls.Children.Add(startStopBtn);
            controls.Children.Add(reset25);
            controls.Children.Add(reset5);
            stack.Children.Add(controls);
            timerCard.Child = stack;

            var uiTick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            uiTick.Tick += (_, _) => display.Text = FormatTime(RemainingSeconds);
            uiTick.Start();

            Grid.SetColumn(timerCard, 0);
            grid.Children.Add(timerCard);

            // World Clocks column
            var worldCard = new BuddyCard { Margin = new Thickness(4) };
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
            AddCityRow(worldStack, "Local Time", DateTime.Now.ToString("HH:mm"));
            AddCityRow(worldStack, "UTC / London", utcNow.ToString("HH:mm"));
            AddCityRow(worldStack, "New York (EDT)", utcNow.AddHours(-4).ToString("HH:mm"));
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
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 5)
            };
            var dp = new DockPanel();
            var tBlock = new TextBlock { Text = time, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text), HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(tBlock, Dock.Right);
            dp.Children.Add(tBlock);
            dp.Children.Add(new TextBlock { Text = city, FontSize = 12, Foreground = CapsuleTheme.Brush(p.Text2) });
            row.Child = dp;
            parent.Children.Add(row);
        }

        public static string FormatTime(int totalSec) =>
            $"{totalSec / 60:00}:{totalSec % 60:00}";

        public void OnActivated() { }
    }
}
