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
using WindowsBuddy.Services;

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
        private StackPanel _notchPreviewStack;
        private TextBlock _notchPreviewText;
        private DispatcherTimer _typeOnTimer;
        private DispatcherTimer _previewPulseTimer;
        private DispatcherTimer _previewLoopTimer;

        // Compose Controls
        private TextBox _inputBox;
        private TextBlock _placeholderText;
        private TextBlock _charCountText;
        private Border _flashButton;
        private TextBlock _flashBtnText;
        private TextBlock _secondaryBtn;

        // Animation Card Controls
        private readonly Dictionary<MessageAnimationMode, Border> _animPillBorders = new();
        private Canvas _speedSliderCanvas;
        private Border _speedTrack;
        private Border _speedFill;
        private Border _speedThumb;
        private bool _isDraggingSpeed = false;
        private TextBlock _speedLabel;

        // Colour Card Controls
        private readonly List<Border> _swatchBorders = new();
        private Border _plusBorder;
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
        private StackPanel _customStepperPanel;
        private TextBlock _customStepperText;
        private Border _repeatToggleTrack;
        private Border _repeatToggleThumb;

        public MessageToolView()
        {
            FlowDirection = LocalizationService.CurrentFlowDirection;
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
                _previewLoopTimer?.Stop();
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
            var p = CapsuleTheme.Current;
            _previewCard = new Border
            {
                Height = 66,
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 0, 5)
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
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            leftStack.Children.Add(_liveDotText);

            var previewLabel = new TextBlock
            {
                Text = LocalizationService.T("message.live_preview", "Live preview"),
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(previewLabel);
            DockPanel.SetDock(leftStack, Dock.Left);
            topBar.Children.Add(leftStack);

            _durationText = new TextBlock
            {
                Text = _settings.DurationLabel,
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                Width = 240,
                Height = 32,
                Background = CapsuleTheme.Brush(p.Bg),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true
            };
            _notchPreviewContainer.SizeChanged += (s, e) =>
            {
                if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
                {
                    double radius = e.NewSize.Height / 2.0;
                    _notchPreviewContainer.Clip = new RectangleGeometry(
                        new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
                        radius,
                        radius);
                }
            };

            _notchPreviewStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            var previewIcon = new WindowsBuddy.UI.AnimateIcons.AnimateIcon(
                WindowsBuddy.UI.AnimateIcons.AnimateIconType.Sparkles,
                13,
                CapsuleTheme.Brush(p.Accent))
            {
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                AutoAnimateOnHover = false
            };
            _notchPreviewStack.Children.Add(previewIcon);

            _notchPreviewText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_settings.Text) ? LocalizationService.T("message.placeholder", "Type a message") : _settings.Text,
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            };
            _notchPreviewStack.Children.Add(_notchPreviewText);
            _notchPreviewContainer.Child = _notchPreviewStack;

            Grid.SetRow(_notchPreviewContainer, 1);
            grid.Children.Add(_notchPreviewContainer);

            _previewCard.Child = grid;
            return _previewCard;
        }

        private Border BuildComposeRow()
        {
            var p = CapsuleTheme.Current;
            var rowBorder = new Border
            {
                Height = 38,
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 2, 8, 2),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var dock = new DockPanel();

            // Right side buttons: Flash & Clear
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _charCountText = new TextBlock
            {
                Text = $"{(_settings.Text?.Length ?? 0)}/60",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            rightStack.Children.Add(_charCountText);

            // Flash button
            _flashButton = new Border
            {
                Background = CapsuleTheme.Brush(p.Accent),
                BorderBrush = CapsuleTheme.Brush(p.Accent),
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
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            _flashBtnText = new TextBlock
            {
                Text = LocalizationService.T("message.flash", "Flash"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                VerticalAlignment = VerticalAlignment.Center
            };
            flashStack.Children.Add(_flashBtnText);
            _flashButton.Child = flashStack;

            _flashButton.MouseEnter += (_, _) => _flashButton.Opacity = 0.88;
            _flashButton.MouseLeave += (_, _) => _flashButton.Opacity = 1.0;
            _flashButton.MouseLeftButtonUp += (_, _) =>
            {
                _settings.Text = _inputBox.Text?.Trim() ?? "";
                _service.Flash(_settings);
            };
            rightStack.Children.Add(_flashButton);

            // Clear / Take Down button
            _secondaryBtn = new TextBlock
            {
                Text = LocalizationService.T("message.clear", "Clear"),
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            _secondaryBtn.MouseEnter += (_, _) => _secondaryBtn.Foreground = CapsuleTheme.Brush(p.Text);
            _secondaryBtn.MouseLeave += (_, _) => _secondaryBtn.Foreground = CapsuleTheme.Brush(p.Text2);
            _secondaryBtn.MouseLeftButtonUp += (_, _) =>
            {
                if (_service.IsLive)
                {
                    _service.TakeDown();
                    UpdateLiveStatus();
                }
                else
                {
                    _inputBox.Text = "";
                    _settings.Text = "";
                    SyncAllToSettings();
                    TriggerPreviewAnimation();
                }
            };
            rightStack.Children.Add(_secondaryBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            // Left side message icon
            var msgIcon = new WindowsBuddy.UI.AnimateIcons.AnimateIcon(
                WindowsBuddy.UI.AnimateIcons.AnimateIconType.Message,
                16,
                CapsuleTheme.Brush(p.Text2))
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(msgIcon, Dock.Left);
            dock.Children.Add(msgIcon);

            // Center input with placeholder overlay
            var inputGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            _placeholderText = new TextBlock
            {
                Text = LocalizationService.T("message.placeholder", "Type a message"),
                FontSize = 12.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            _inputBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    _settings.Text = _inputBox.Text?.Trim() ?? "";
                    _service.Flash(_settings);
                }
            };
            _inputBox.TextChanged += (_, _) =>
            {
                string txt = _inputBox.Text ?? "";
                _settings.Text = txt;
                _charCountText.Text = $"{txt.Length}/60";
                _placeholderText.Visibility = string.IsNullOrEmpty(txt) ? Visibility.Visible : Visibility.Collapsed;
                _notchPreviewText.Text = string.IsNullOrWhiteSpace(txt) ? LocalizationService.T("message.placeholder", "Type a message") : txt;
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
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var dock = new DockPanel();

            // Header
            var header = new TextBlock
            {
                Text = "▶  " + LocalizationService.T("message.animation", "Animation"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Speed Slider Row at Bottom
            var speedDock = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(speedDock, Dock.Bottom);

            var speedTitle = new TextBlock
            {
                Text = LocalizationService.T("message.speed", "Speed"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            DockPanel.SetDock(speedTitle, Dock.Left);
            speedDock.Children.Add(speedTitle);

            _speedLabel = new TextBlock
            {
                Text = LocalizationService.T("message.speed.normal", "Normal"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(6, 0, 0, 0),
                MinWidth = 42,
                TextAlignment = TextAlignment.Right
            };
            DockPanel.SetDock(_speedLabel, Dock.Right);
            speedDock.Children.Add(_speedLabel);

            // Sleek custom interactive slider Canvas (click and drag supported reliably)
            _speedSliderCanvas = new Canvas
            {
                Height = 18.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(6, 0, 6, 0),
                ClipToBounds = false
            };

            _speedTrack = new Border
            {
                Height = 4.0,
                CornerRadius = new CornerRadius(2.0),
                Background = CapsuleTheme.Brush(p.Surface2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = false
            };
            _speedFill = new Border
            {
                Height = 4.0,
                CornerRadius = new CornerRadius(2.0),
                Background = CapsuleTheme.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = false
            };
            _speedThumb = new Border
            {
                Width = 14.0,
                Height = 14.0,
                CornerRadius = new CornerRadius(7.0),
                Background = CapsuleTheme.Brush(p.AccentFg),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.35,
                    Color = Colors.Black
                }
            };

            _speedSliderCanvas.Children.Add(_speedTrack);
            _speedSliderCanvas.Children.Add(_speedFill);
            _speedSliderCanvas.Children.Add(_speedThumb);

            Canvas.SetTop(_speedTrack, 7.0);
            Canvas.SetTop(_speedFill, 7.0);
            Canvas.SetTop(_speedThumb, 2.0);

            _speedSliderCanvas.SizeChanged += (_, _) => UpdateSpeedSliderVisual();
            _speedSliderCanvas.MouseLeftButtonDown += OnSpeedSliderMouseDown;
            _speedSliderCanvas.MouseMove += OnSpeedSliderMouseMove;
            _speedSliderCanvas.MouseLeftButtonUp += OnSpeedSliderMouseUp;

            speedDock.Children.Add(_speedSliderCanvas);
            dock.Children.Add(speedDock);

            // Pills container (UniformGrid 2x3 for responsive fit)
            var pGrid = new UniformGrid
            {
                Rows = 2,
                Columns = 3,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 2, 0, 2)
            };
            pGrid.Children.Add(CreateAnimPill(LocalizationService.T("message.anim.slide", "Slide"), MessageAnimationMode.Slide));
            pGrid.Children.Add(CreateAnimPill(LocalizationService.T("message.anim.fade", "Fade"), MessageAnimationMode.Fade));
            pGrid.Children.Add(CreateAnimPill(LocalizationService.T("message.anim.typeon", "Type-on"), MessageAnimationMode.TypeOn));
            pGrid.Children.Add(CreateAnimPill(LocalizationService.T("message.anim.scroll", "Scroll"), MessageAnimationMode.Scroll));
            pGrid.Children.Add(CreateAnimPill(LocalizationService.T("message.anim.blur", "Blur"), MessageAnimationMode.Blur));
            pGrid.Children.Add(CreateAnimPill(LocalizationService.T("message.anim.pulse", "Pulse"), MessageAnimationMode.Pulse));
            dock.Children.Add(pGrid);
            card.Child = dock;
            return card;
        }

        private void UpdateSpeedSliderVisual()
        {
            if (_speedSliderCanvas == null) return;
            double trackW = _speedSliderCanvas.ActualWidth;
            if (trackW <= 5) return;
            _speedTrack.Width = trackW;

            double ratio = Math.Clamp((_settings.Speed - 0.5) / 1.0, 0.0, 1.0);
            double fillW = Math.Clamp(trackW * ratio, 0.0, trackW);
            _speedFill.Width = fillW;

            double thumbX = Math.Clamp(fillW - 7.0, 0.0, Math.Max(0.0, trackW - 14.0));
            Canvas.SetLeft(_speedThumb, thumbX);
        }

        private void OnSpeedSliderMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSpeed = true;
            _speedSliderCanvas.CaptureMouse();
            ApplySpeedFromPointer(e.GetPosition(_speedSliderCanvas).X);
            e.Handled = true;
        }

        private void OnSpeedSliderMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSpeed)
            {
                ApplySpeedFromPointer(e.GetPosition(_speedSliderCanvas).X);
                e.Handled = true;
            }
        }

        private void OnSpeedSliderMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSpeed)
            {
                _isDraggingSpeed = false;
                _speedSliderCanvas.ReleaseMouseCapture();
                ApplySpeedFromPointer(e.GetPosition(_speedSliderCanvas).X);
                e.Handled = true;
            }
        }

        private void ApplySpeedFromPointer(double pointerX)
        {
            double trackW = _speedSliderCanvas.ActualWidth > 10 ? _speedSliderCanvas.ActualWidth : 120.0;
            double ratio = Math.Clamp(pointerX / trackW, 0.0, 1.0);
            double newSpeed = Math.Round(0.5 + ratio * 1.0, 2);
            _settings.Speed = newSpeed;

            UpdateSpeedSliderVisual();
            UpdateSpeedLabel();
            TriggerPreviewAnimation();

            if (_service.IsLive)
            {
                _service.Flash(_settings);
            }
        }

        private void UpdateSpeedLabel()
        {
            if (_speedLabel == null) return;
            if (_settings.Speed < 0.8)
                _speedLabel.Text = LocalizationService.T("message.speed.slow", "Slow");
            else if (_settings.Speed > 1.2)
                _speedLabel.Text = LocalizationService.T("message.speed.fast", "Fast");
            else
                _speedLabel.Text = LocalizationService.T("message.speed.normal", "Normal");
        }

        private Border CreateAnimPill(string label, MessageAnimationMode mode)
        {
            var p = CapsuleTheme.Current;
            var pill = new Border
            {
                Height = 25,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(3, 0, 3, 0),
                Margin = new Thickness(2, 2, 2, 2),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1)
            };

            var text = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                if (_service.IsLive)
                {
                    _service.UpdateLive(_settings);
                }
            };

            _animPillBorders[mode] = pill;
            return pill;
        }

        private void UpdateAnimPillVisuals()
        {
            var p = CapsuleTheme.Current;
            foreach (var kvp in _animPillBorders)
            {
                bool active = kvp.Key == _settings.Animation;
                kvp.Value.BorderBrush = active
                    ? CapsuleTheme.Brush(p.Accent)
                    : CapsuleTheme.Brush(p.Border);
                kvp.Value.Background = active
                    ? new SolidColorBrush(Color.FromArgb(40, p.Accent.R, p.Accent.G, p.Accent.B))
                    : Brushes.Transparent;

                if (kvp.Value.Child is TextBlock tb)
                {
                    tb.Foreground = active ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Text2);
                    tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        private Border BuildColourCard()
        {
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(4, 0, 4, 0)
            };

            var dock = new DockPanel();

            // Header
            var header = new TextBlock
            {
                Text = "🖌  " + LocalizationService.T("message.color", "Colour"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 0, 0, 6)
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
            _plusBorder = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                ToolTip = LocalizationService.T("message.custom", "Custom")
            };
            _plusBorder.Child = new TextBlock
            {
                Text = "+",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _plusBorder.MouseLeftButtonUp += (_, _) =>
            {
                using var dlg = new System.Windows.Forms.ColorDialog();
                try
                {
                    var cur = (Color)ColorConverter.ConvertFromString(_settings.ColorHex ?? "#FFFFFF");
                    dlg.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                }
                catch { }
                dlg.FullOpen = true;
                if (WindowsBuddy.BuddyCapsule.Core.NotchInteractionGuard.RunModal(() => dlg.ShowDialog()) == System.Windows.Forms.DialogResult.OK)
                {
                    string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    _settings.ColorHex = hex;
                    _settings.CustomColorHex = hex;
                    _settings.Finish = MessageFinish.None;
                    UpdateColorSwatchVisuals();
                    UpdateFinishVisuals();
                    TriggerPreviewAnimation();
                    if (_service.IsLive)
                    {
                        _service.UpdateLive(_settings);
                    }
                }
            };
            swatchesRow.Children.Add(_plusBorder);

            DockPanel.SetDock(swatchesRow, Dock.Top);
            dock.Children.Add(swatchesRow);

            // Row 2: Finish options
            var finishDock = new DockPanel { Margin = new Thickness(0, 4, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var finishLabel = new TextBlock
            {
                Text = LocalizationService.T("message.finish", "Finish"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(finishLabel, Dock.Left);
            finishDock.Children.Add(finishLabel);

            var finishStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            finishStack.Children.Add(CreateFinishItem(MessageFinish.None, LocalizationService.T("message.finish.solid", "Solid")));
            finishStack.Children.Add(CreateFinishItem(MessageFinish.Silver, LocalizationService.T("message.finish.silver", "Silver")));
            finishStack.Children.Add(CreateFinishItem(MessageFinish.Ocean, LocalizationService.T("message.finish.ocean", "Ocean")));
            finishStack.Children.Add(CreateFinishItem(MessageFinish.Aurora, LocalizationService.T("message.finish.aurora", "Aurora")));
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
                _settings.Finish = MessageFinish.None;
                UpdateColorSwatchVisuals();
                UpdateFinishVisuals();
                TriggerPreviewAnimation();
                if (_service.IsLive)
                {
                    _service.UpdateLive(_settings);
                }
            };

            return outer;
        }

        private void UpdateColorSwatchVisuals()
        {
            var p = CapsuleTheme.Current;
            bool isSolid = _settings.Finish == MessageFinish.None;
            bool matchedPreset = false;

            for (int i = 0; i < _swatchColors.Length; i++)
            {
                bool selected = isSolid && string.Equals(_swatchColors[i], _settings.ColorHex, StringComparison.OrdinalIgnoreCase);
                if (selected) matchedPreset = true;
                _swatchBorders[i].BorderBrush = selected ? CapsuleTheme.Brush(p.Accent) : Brushes.Transparent;
            }

            if (_plusBorder != null)
            {
                bool customSelected = isSolid && !matchedPreset && !string.IsNullOrEmpty(_settings.ColorHex);
                _plusBorder.BorderBrush = customSelected ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Border);
                if (customSelected)
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(_settings.ColorHex);
                        _plusBorder.Background = new SolidColorBrush(c);
                    }
                    catch { }
                }
                else
                {
                    _plusBorder.Background = CapsuleTheme.Brush(p.Surface2);
                }
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
                UpdateColorSwatchVisuals();
                UpdateFinishVisuals();
                TriggerPreviewAnimation();
                if (_service.IsLive)
                {
                    _service.UpdateLive(_settings);
                }
            };

            _finishBorders[finish] = outer;
            return outer;
        }

        private void UpdateFinishVisuals()
        {
            var p = CapsuleTheme.Current;
            foreach (var kvp in _finishBorders)
            {
                bool active = kvp.Key == _settings.Finish;
                kvp.Value.BorderBrush = active ? CapsuleTheme.Brush(p.Accent) : Brushes.Transparent;
            }
        }

        private Border BuildDurationCard()
        {
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(4, 0, 0, 0)
            };

            var dock = new DockPanel();

            // Header
            var header = new TextBlock
            {
                Text = "🕒  " + LocalizationService.T("message.duration", "Duration"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Repeat toggle at bottom
            var repeatDock = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(repeatDock, Dock.Bottom);

            var repeatText = new TextBlock
            {
                Text = LocalizationService.T("message.repeat", "Repeat until taken down"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                Background = _settings.RepeatEnabled ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Padding = new Thickness(2)
            };
            _repeatToggleThumb = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = CapsuleTheme.Brush(p.AccentFg),
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
            durGrid.Children.Add(CreateDurPill(LocalizationService.T("message.custom", "Custom"), MessageDurationMode.Custom));

            // Custom duration stepper: [-] {15}s [+]
            _customStepperPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = _settings.Duration == MessageDurationMode.Custom ? Visibility.Visible : Visibility.Collapsed
            };

            var minusBtn = new Border
            {
                Width = 22,
                Height = 20,
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            minusBtn.Child = new TextBlock
            {
                Text = "−",
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            minusBtn.MouseLeftButtonUp += (_, _) =>
            {
                _settings.CustomDurationSeconds = Math.Clamp(_settings.CustomDurationSeconds - 5, 1, 300);
                UpdateCustomStepperLabel();
                _durationText.Text = _settings.DurationLabel;
            };
            _customStepperPanel.Children.Add(minusBtn);

            _customStepperText = new TextBlock
            {
                Text = $"{_settings.CustomDurationSeconds}s",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                MinWidth = 36,
                TextAlignment = TextAlignment.Center
            };
            _customStepperPanel.Children.Add(_customStepperText);

            var plusBtn = new Border
            {
                Width = 22,
                Height = 20,
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            plusBtn.Child = new TextBlock
            {
                Text = "+",
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            plusBtn.MouseLeftButtonUp += (_, _) =>
            {
                _settings.CustomDurationSeconds = Math.Clamp(_settings.CustomDurationSeconds + 5, 1, 300);
                UpdateCustomStepperLabel();
                _durationText.Text = _settings.DurationLabel;
            };
            _customStepperPanel.Children.Add(plusBtn);

            var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            centerStack.Children.Add(durGrid);
            centerStack.Children.Add(_customStepperPanel);
            dock.Children.Add(centerStack);

            card.Child = dock;
            return card;
        }

        private void UpdateCustomStepperLabel()
        {
            if (_customStepperText != null)
            {
                _customStepperText.Text = $"{_settings.CustomDurationSeconds}s";
            }
        }

        private Border CreateDurPill(string label, MessageDurationMode mode)
        {
            var p = CapsuleTheme.Current;
            var pill = new Border
            {
                Height = 25,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(3, 0, 3, 0),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1)
            };

            var text = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
            var p = CapsuleTheme.Current;
            foreach (var kvp in _durPillBorders)
            {
                bool active = kvp.Key == _settings.Duration;
                kvp.Value.BorderBrush = active
                    ? CapsuleTheme.Brush(p.Accent)
                    : CapsuleTheme.Brush(p.Border);
                kvp.Value.Background = active
                    ? new SolidColorBrush(Color.FromArgb(40, p.Accent.R, p.Accent.G, p.Accent.B))
                    : Brushes.Transparent;

                if (kvp.Value.Child is TextBlock tb)
                {
                    tb.Foreground = active ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Text2);
                    tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }

            if (_customStepperPanel != null)
            {
                _customStepperPanel.Visibility = _settings.Duration == MessageDurationMode.Custom ? Visibility.Visible : Visibility.Collapsed;
                UpdateCustomStepperLabel();
            }
        }

        private void UpdateRepeatVisuals()
        {
            var p = CapsuleTheme.Current;
            _repeatToggleTrack.Background = _settings.RepeatEnabled
                ? CapsuleTheme.Brush(p.Accent)
                : CapsuleTheme.Brush(p.Surface2);
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
            UpdateSpeedSliderVisual();
            UpdateSpeedLabel();
            UpdateColorSwatchVisuals();
            UpdateFinishVisuals();
            UpdateDurPillVisuals();
            UpdateRepeatVisuals();
            UpdateLiveStatus();
        }

        private void UpdateLiveStatus()
        {
            var p = CapsuleTheme.Current;
            bool live = _service.IsLive;
            _liveDotText.Foreground = live
                ? CapsuleTheme.Brush(p.Accent)
                : CapsuleTheme.Brush(p.Text2);

            _flashBtnText.Text = LocalizationService.T("message.flash", "Flash");
            if (live)
            {
                _secondaryBtn.Text = LocalizationService.T("message.take_down", "Take Down");
            }
            else
            {
                _secondaryBtn.Text = LocalizationService.T("message.clear", "Clear");
            }
        }

        private void TriggerPreviewAnimation()
        {
            _previewLoopTimer?.Stop();
            RunPreviewAnimationOnce();

            if (_settings.Animation != MessageAnimationMode.Scroll)
            {
                if (_previewLoopTimer == null)
                {
                    _previewLoopTimer = new DispatcherTimer();
                    _previewLoopTimer.Tick += (_, _) =>
                    {
                        RunPreviewAnimationOnce();
                    };
                }
                double cycleSec = Math.Max(2.2, (0.55 / _settings.Speed) + 1.5);
                _previewLoopTimer.Interval = TimeSpan.FromSeconds(cycleSec);
                _previewLoopTimer.Start();
            }
        }

        private void RunPreviewAnimationOnce()
        {
            string txt = string.IsNullOrWhiteSpace(_settings.Text)
                ? LocalizationService.T("message.placeholder", "Type a message")
                : _settings.Text;
            _notchPreviewText.Foreground = MessageRenderer.GetForegroundBrush(_settings.ColorHex, _settings.Finish);

            bool isScroll = _settings.Animation == MessageAnimationMode.Scroll;
            _notchPreviewText.TextTrimming = isScroll ? TextTrimming.None : TextTrimming.CharacterEllipsis;
            _notchPreviewText.MaxWidth = isScroll ? 2000.0 : 200.0;

            bool isRtl = LocalizationService.IsRtl(txt) || AppPreferences.Instance.IsPersian;
            double w = _notchPreviewContainer?.ActualWidth > 80.0 ? _notchPreviewContainer.ActualWidth : 220.0;

            MessageRenderer.AnimateElement(
                _notchPreviewStack ?? (FrameworkElement)_notchPreviewText,
                _notchPreviewText,
                txt,
                _settings.Animation,
                _settings.Speed,
                _typeOnTimer,
                out _typeOnTimer,
                containerWidth: w,
                isRtl: isRtl);
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
