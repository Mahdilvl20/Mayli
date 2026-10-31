using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.Tools.Message
{
    public sealed class MessageToolView : UserControl
    {
        private readonly MessageService _service = MessageService.Instance;
        private MessageSettings _settings;

        // Top Preview Controls
        private Border _previewCard;
        private TextBlock _liveDotText;
        private TextBlock _durationText;
        private Border _notchPreviewContainer;
        private TextBlock _notchPreviewText;
        private DispatcherTimer _typeOnTimer;
        private DispatcherTimer _previewPulseTimer;

        // Compose Controls
        private TextBox _inputBox;
        private TextBlock _placeholderText;
        private TextBlock _charCountText;
        private Border _flashButton;
        private TextBlock _flashBtnText;
        private TextBlock _secondaryBtn;

        // Animation Card Controls
        private readonly Dictionary<MessageAnimationMode, Border> _animPillBorders = new();
        private Slider _speedSlider;
        private TextBlock _speedLabel;

        // Colour Card Controls
        private readonly List<Border> _swatchBorders = new();
        private readonly string[] _swatchColors = new[]
        {
            "#FFFFFF", // White
            "#F5E6CC", // Cream
            "#F5A623", // Amber / Orange
            "#F26C59", // Coral / Salmon
            "#E91E63", // Hot Pink
            "#9C27B0", // Purple
            "#29B6F6"  // Sky Blue
        };
        private readonly Dictionary<MessageFinish, Border> _finishBorders = new();

        // Duration Card Controls
        private readonly Dictionary<MessageDurationMode, Border> _durPillBorders = new();
        private Border _repeatToggleTrack;
        private Border _repeatToggleThumb;

        public MessageToolView()
        {
            _settings = _service.Settings.Clone();

            var mainDock = new DockPanel { Margin = new Thickness(4, 2, 4, 4) };

            // 1. Top Live Preview Card (Height ~72)
            var previewSection = BuildLivePreviewSection();
            DockPanel.SetDock(previewSection, Dock.Top);
            mainDock.Children.Add(previewSection);

            // 2. Compose Row (Height ~42)
            var composeRow = BuildComposeRow();
            DockPanel.SetDock(composeRow, Dock.Top);
            mainDock.Children.Add(composeRow);

            // 3. Bottom 3 Cards (Grid 1x3)
            var bottomGrid = BuildBottomThreeCards();
            mainDock.Children.Add(bottomGrid);

            Content = mainDock;

            // Live state synchronization
            _service.StateChanged += OnServiceStateChanged;
            Loaded += (_, _) =>
            {
                SyncAllToSettings();
                TriggerPreviewAnimation();
            };
            Unloaded += (_, _) =>
            {
                _service.StateChanged -= OnServiceStateChanged;
                _typeOnTimer?.Stop();
                _previewPulseTimer?.Stop();
            };

            // Setup preview live dot pulse
            _previewPulseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _previewPulseTimer.Tick += (_, _) =>
            {
                if (_service.IsLive && _liveDotText != null)
                {
                    _liveDotText.Opacity = _liveDotText.Opacity > 0.5 ? 0.3 : 1.0;
                }
            };
            _previewPulseTimer.Start();
        }

        private Border BuildLivePreviewSection()
        {
            _previewCard = new Border
            {
                Height = 72,
                Background = new SolidColorBrush(Color.FromRgb(16, 16, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 0, 7)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top Status Bar
            var topBar = new DockPanel();

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal };
            _liveDotText = new TextBlock
            {
                Text = "■",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 74, 25)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            leftStack.Children.Add(_liveDotText);

            var previewLabel = new TextBlock
            {
                Text = "Live preview",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(previewLabel);
            DockPanel.SetDock(leftStack, Dock.Left);
            topBar.Children.Add(leftStack);

            _durationText = new TextBlock
            {
                Text = _settings.DurationLabel,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_durationText, Dock.Right);
            topBar.Children.Add(_durationText);

            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);

            // Center Notch Mockup container
            _notchPreviewContainer = new Border
            {
                MinWidth = 180,
                MaxWidth = 260,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(8, 8, 10)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 24, 28)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true
            };

            _notchPreviewText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_settings.Text) ? "Type a message" : _settings.Text,
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 210
            };
            _notchPreviewContainer.Child = _notchPreviewText;

            Grid.SetRow(_notchPreviewContainer, 1);
            grid.Children.Add(_notchPreviewContainer);

            _previewCard.Child = grid;
            return _previewCard;
        }

        private Border BuildComposeRow()
        {
            var rowBorder = new Border
            {
                Height = 42,
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 32, 38)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var dock = new DockPanel();

            // Right side buttons: Flash & Clear
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _charCountText = new TextBlock
            {
                Text = $"{(_settings.Text?.Length ?? 0)}/60",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 120)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            rightStack.Children.Add(_charCountText);

            // Flash button
            _flashButton = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(110, 36, 16)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(230, 74, 25)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand
            };
            var flashStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            flashStack.Children.Add(new TextBlock
            {
                Text = "⚡",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 166, 35)),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            _flashBtnText = new TextBlock
            {
                Text = "Flash",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            flashStack.Children.Add(_flashBtnText);
            _flashButton.Child = flashStack;

            _flashButton.MouseEnter += (_, _) => _flashButton.Background = new SolidColorBrush(Color.FromRgb(230, 74, 25));
            _flashButton.MouseLeave += (_, _) => _flashButton.Background = new SolidColorBrush(Color.FromRgb(110, 36, 16));
            _flashButton.MouseLeftButtonUp += (_, _) =>
            {
                _settings.Text = _inputBox.Text?.Trim() ?? "";
                _service.Flash(_settings);
            };
            rightStack.Children.Add(_flashButton);

            // Clear button
            _secondaryBtn = new TextBlock
            {
                Text = "Clear",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            _secondaryBtn.MouseEnter += (_, _) => _secondaryBtn.Foreground = Brushes.White;
            _secondaryBtn.MouseLeave += (_, _) => _secondaryBtn.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
            _secondaryBtn.MouseLeftButtonUp += (_, _) =>
            {
                if (_service.IsLive)
                {
                    _service.TakeDown();
                }
                _inputBox.Text = "";
                _settings.Text = "";
                SyncAllToSettings();
                TriggerPreviewAnimation();
            };
            rightStack.Children.Add(_secondaryBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            // Left side message icon
            var msgIcon = new TextBlock
            {
                Text = "🪪",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(msgIcon, Dock.Left);
            dock.Children.Add(msgIcon);

            // Center input with placeholder overlay
            var inputGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            _placeholderText = new TextBlock
            {
                Text = "Type a message",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 100)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(_settings.Text) ? Visibility.Visible : Visibility.Collapsed
            };
            inputGrid.Children.Add(_placeholderText);

            _inputBox = new TextBox
            {
                Text = _settings.Text ?? "",
                MaxLength = 60,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            _inputBox.TextChanged += (_, _) =>
            {
                string txt = _inputBox.Text ?? "";
                _settings.Text = txt;
                _charCountText.Text = $"{txt.Length}/60";
                _placeholderText.Visibility = string.IsNullOrEmpty(txt) ? Visibility.Visible : Visibility.Collapsed;
                _notchPreviewText.Text = string.IsNullOrWhiteSpace(txt) ? "Type a message" : txt;
                TriggerPreviewAnimation();
            };
            inputGrid.Children.Add(_inputBox);

            dock.Children.Add(inputGrid);

            rowBorder.Child = dock;
            return rowBorder;
        }

        private Grid BuildBottomThreeCards()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Card 1: Animation
            var animCard = BuildAnimationCard();
            Grid.SetColumn(animCard, 0);
            grid.Children.Add(animCard);

            // Card 2: Colour
            var colourCard = BuildColourCard();
            Grid.SetColumn(colourCard, 1);
            grid.Children.Add(colourCard);

            // Card 3: Duration
            var durationCard = BuildDurationCard();
            Grid.SetColumn(durationCard, 2);
            grid.Children.Add(durationCard);

            return grid;
        }

        private Border BuildAnimationCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 5, 0)
            };

            var dock = new DockPanel();

            // Header
            var header = new TextBlock
            {
                Text = "▶  Animation",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Speed Slider Row at Bottom
            var speedDock = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(speedDock, Dock.Bottom);

            var speedTitle = new TextBlock
            {
                Text = "Speed",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            DockPanel.SetDock(speedTitle, Dock.Left);
            speedDock.Children.Add(speedTitle);

            _speedLabel = new TextBlock
            {
                Text = "Normal",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(6, 0, 0, 0)
            };
            DockPanel.SetDock(_speedLabel, Dock.Right);
            speedDock.Children.Add(_speedLabel);

            _speedSlider = new Slider
            {
                Minimum = 0.5,
                Maximum = 1.5,
                Value = _settings.Speed,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 74, 25))
            };
            _speedSlider.ValueChanged += (_, _) =>
            {
                _settings.Speed = _speedSlider.Value;
                _speedLabel.Text = _settings.Speed < 0.8 ? "Slow" : (_settings.Speed > 1.2 ? "Fast" : "Normal");
                TriggerPreviewAnimation();
            };
            speedDock.Children.Add(_speedSlider);
            dock.Children.Add(speedDock);

            // Pills container (UniformGrid 2x3 for responsive fit)
            var pGrid = new UniformGrid { Rows = 2, Columns = 3, VerticalAlignment = VerticalAlignment.Center };
            pGrid.Children.Add(CreateAnimPill("Slide", MessageAnimationMode.Slide));
            pGrid.Children.Add(CreateAnimPill("Fade", MessageAnimationMode.Fade));
            pGrid.Children.Add(CreateAnimPill("Type-on", MessageAnimationMode.TypeOn));
            pGrid.Children.Add(CreateAnimPill("Scroll", MessageAnimationMode.Scroll));
            pGrid.Children.Add(CreateAnimPill("Blur", MessageAnimationMode.Blur));
            pGrid.Children.Add(CreateAnimPill("Pulse", MessageAnimationMode.Pulse));
            dock.Children.Add(pGrid);
            card.Child = dock;
            return card;
        }

        private Border CreateAnimPill(string label, MessageAnimationMode mode)
        {
            var pill = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                BorderThickness = new Thickness(1)
            };

            var text = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            pill.Child = text;

            pill.MouseLeftButtonUp += (_, _) =>
            {
                _settings.Animation = mode;
                UpdateAnimPillVisuals();
                TriggerPreviewAnimation();
            };

            _animPillBorders[mode] = pill;
            return pill;
        }

        private void UpdateAnimPillVisuals()
        {
            foreach (var kvp in _animPillBorders)
            {
                bool active = kvp.Key == _settings.Animation;
                kvp.Value.BorderBrush = active
                    ? new SolidColorBrush(Color.FromRgb(230, 74, 25))
                    : new SolidColorBrush(Color.FromRgb(40, 40, 48));
                kvp.Value.Background = active
                    ? new SolidColorBrush(Color.FromRgb(46, 28, 22))
                    : Brushes.Transparent;

                if (kvp.Value.Child is TextBlock tb)
                {
                    tb.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(140, 140, 150));
                    tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        private Border BuildColourCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(4, 0, 4, 0)
            };

            var dock = new DockPanel();

            // Header
            var header = new TextBlock
            {
                Text = "🖌  Colour",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Row 1: Swatches
            var swatchesRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 8) };
            _swatchBorders.Clear();

            foreach (var hex in _swatchColors)
            {
                var swatch = CreateColorSwatch(hex);
                _swatchBorders.Add(swatch);
                swatchesRow.Children.Add(swatch);
            }

            // Plus custom color button
            var plusBorder = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(34, 34, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 65)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand
            };
            plusBorder.Child = new TextBlock
            {
                Text = "+",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 160)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            plusBorder.MouseLeftButtonUp += (_, _) =>
            {
                // Toggle between custom green/cyan or cycle
                _settings.ColorHex = "#00E676";
                UpdateColorSwatchVisuals();
                TriggerPreviewAnimation();
            };
            swatchesRow.Children.Add(plusBorder);

            DockPanel.SetDock(swatchesRow, Dock.Top);
            dock.Children.Add(swatchesRow);

            // Row 2: Finish options
            var finishDock = new DockPanel { Margin = new Thickness(0, 4, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var finishLabel = new TextBlock
            {
                Text = "Finish",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(finishLabel, Dock.Left);
            finishDock.Children.Add(finishLabel);

            var finishStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            finishStack.Children.Add(CreateFinishItem(MessageFinish.None, "Solid"));
            finishStack.Children.Add(CreateFinishItem(MessageFinish.Silver, "Silver"));
            finishStack.Children.Add(CreateFinishItem(MessageFinish.Ocean, "Ocean"));
            finishStack.Children.Add(CreateFinishItem(MessageFinish.Aurora, "Aurora"));
            finishDock.Children.Add(finishStack);

            dock.Children.Add(finishDock);
            card.Child = dock;
            return card;
        }

        private Border CreateColorSwatch(string hex)
        {
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            var outer = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(1.5, 0, 1.5, 0),
                Padding = new Thickness(1.5),
                Cursor = Cursors.Hand
            };

            var inner = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(color)
            };
            outer.Child = inner;

            outer.MouseLeftButtonUp += (_, _) =>
            {
                _settings.ColorHex = hex;
                UpdateColorSwatchVisuals();
                TriggerPreviewAnimation();
            };

            return outer;
        }

        private void UpdateColorSwatchVisuals()
        {
            for (int i = 0; i < _swatchColors.Length; i++)
            {
                bool selected = string.Equals(_swatchColors[i], _settings.ColorHex, StringComparison.OrdinalIgnoreCase);
                _swatchBorders[i].BorderBrush = selected ? Brushes.White : Brushes.Transparent;
            }
        }

        private FrameworkElement CreateFinishItem(MessageFinish finish, string tooltip)
        {
            var outer = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(3, 0, 3, 0),
                Padding = new Thickness(1.5),
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };

            var inner = new Border { CornerRadius = new CornerRadius(9) };
            inner.Background = MessageRenderer.GetForegroundBrush("#FFFFFF", finish);
            outer.Child = inner;

            outer.MouseLeftButtonUp += (_, _) =>
            {
                _settings.Finish = finish;
                UpdateFinishVisuals();
                TriggerPreviewAnimation();
            };

            _finishBorders[finish] = outer;
            return outer;
        }

        private void UpdateFinishVisuals()
        {
            foreach (var kvp in _finishBorders)
            {
                bool active = kvp.Key == _settings.Finish;
                kvp.Value.BorderBrush = active ? Brushes.White : Brushes.Transparent;
            }
        }

        private Border BuildDurationCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(5, 0, 0, 0)
            };

            var dock = new DockPanel();

            // Header
            var header = new TextBlock
            {
                Text = "🕒  Duration",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Repeat toggle at bottom
            var repeatDock = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(repeatDock, Dock.Bottom);

            var repeatText = new TextBlock
            {
                Text = "Repeat until taken down",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(repeatText, Dock.Left);
            repeatDock.Children.Add(repeatText);

            // Switch toggle
            _repeatToggleTrack = new Border
            {
                Width = 36,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = _settings.RepeatEnabled ? new SolidColorBrush(Color.FromRgb(230, 74, 25)) : new SolidColorBrush(Color.FromRgb(46, 46, 54)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Padding = new Thickness(2)
            };
            _repeatToggleThumb = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White,
                HorizontalAlignment = _settings.RepeatEnabled ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
            _repeatToggleTrack.Child = _repeatToggleThumb;
            _repeatToggleTrack.MouseLeftButtonUp += (_, _) =>
            {
                _settings.RepeatEnabled = !_settings.RepeatEnabled;
                UpdateRepeatVisuals();
                _durationText.Text = _settings.DurationLabel;
            };
            repeatDock.Children.Add(_repeatToggleTrack);
            dock.Children.Add(repeatDock);

            // Duration pills (UniformGrid 1x4 for responsive fit)
            var durGrid = new UniformGrid { Rows = 1, Columns = 4, VerticalAlignment = VerticalAlignment.Center };
            durGrid.Children.Add(CreateDurPill("3s", MessageDurationMode.Sec3));
            durGrid.Children.Add(CreateDurPill("5s", MessageDurationMode.Sec5));
            durGrid.Children.Add(CreateDurPill("10s", MessageDurationMode.Sec10));
            durGrid.Children.Add(CreateDurPill("Custom", MessageDurationMode.Custom));
            dock.Children.Add(durGrid);

            card.Child = dock;
            return card;
        }

        private Border CreateDurPill(string label, MessageDurationMode mode)
        {
            var pill = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                BorderThickness = new Thickness(1)
            };

            var text = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            pill.Child = text;

            pill.MouseLeftButtonUp += (_, _) =>
            {
                _settings.Duration = mode;
                UpdateDurPillVisuals();
                _durationText.Text = _settings.DurationLabel;
            };

            _durPillBorders[mode] = pill;
            return pill;
        }

        private void UpdateDurPillVisuals()
        {
            foreach (var kvp in _durPillBorders)
            {
                bool active = kvp.Key == _settings.Duration;
                kvp.Value.BorderBrush = active
                    ? new SolidColorBrush(Color.FromRgb(230, 74, 25))
                    : new SolidColorBrush(Color.FromRgb(40, 40, 48));
                kvp.Value.Background = active
                    ? new SolidColorBrush(Color.FromRgb(46, 28, 22))
                    : Brushes.Transparent;

                if (kvp.Value.Child is TextBlock tb)
                {
                    tb.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(140, 140, 150));
                    tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        private void UpdateRepeatVisuals()
        {
            _repeatToggleTrack.Background = _settings.RepeatEnabled
                ? new SolidColorBrush(Color.FromRgb(230, 74, 25))
                : new SolidColorBrush(Color.FromRgb(46, 46, 54));
            _repeatToggleThumb.HorizontalAlignment = _settings.RepeatEnabled
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
        }

        private void SyncAllToSettings()
        {
            _inputBox.Text = _settings.Text ?? "";
            _charCountText.Text = $"{(_settings.Text?.Length ?? 0)}/60";
            _placeholderText.Visibility = string.IsNullOrEmpty(_settings.Text) ? Visibility.Visible : Visibility.Collapsed;
            _durationText.Text = _settings.DurationLabel;

            UpdateAnimPillVisuals();
            UpdateColorSwatchVisuals();
            UpdateFinishVisuals();
            UpdateDurPillVisuals();
            UpdateRepeatVisuals();
            UpdateLiveStatus();
        }

        private void UpdateLiveStatus()
        {
            bool live = _service.IsLive;
            _liveDotText.Foreground = live
                ? new SolidColorBrush(Color.FromRgb(230, 74, 25))
                : new SolidColorBrush(Color.FromRgb(90, 90, 100));

            if (live)
            {
                _flashBtnText.Text = "Flash";
                _secondaryBtn.Text = "Take Down";
            }
            else
            {
                _flashBtnText.Text = "Flash";
                _secondaryBtn.Text = "Clear";
            }
        }

        private void TriggerPreviewAnimation()
        {
            string txt = string.IsNullOrWhiteSpace(_settings.Text) ? "Type a message" : _settings.Text;
            _notchPreviewText.Foreground = MessageRenderer.GetForegroundBrush(_settings.ColorHex, _settings.Finish);

            MessageRenderer.AnimateElement(
                _notchPreviewText,
                _notchPreviewText,
                txt,
                _settings.Animation,
                _settings.Speed,
                _typeOnTimer,
                out _typeOnTimer);
        }

        private void OnServiceStateChanged()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateLiveStatus();
            });
        }
    }
}
