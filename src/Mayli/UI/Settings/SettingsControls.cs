using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WindowsBuddy.UI.Settings
{
    /// <summary>
    /// Modern iOS/macOS styled Toggle Switch with smooth vibrant orange (#FF5500) active state.
    /// </summary>
    public sealed class MayliToggleSwitch : Border
    {
        private readonly Ellipse _thumb;
        private bool _isChecked;

        public event Action<bool> CheckedChanged;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    UpdateVisualState();
                    CheckedChanged?.Invoke(_isChecked);
                }
            }
        }

        public MayliToggleSwitch(bool isChecked = false)
        {
            Width = 46;
            Height = 26;
            CornerRadius = new CornerRadius(13);
            Cursor = Cursors.Hand;
            VerticalAlignment = VerticalAlignment.Center;
            SnapsToDevicePixels = true;

            _thumb = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = Brushes.White,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.3,
                    BlurRadius = 4,
                    ShadowDepth = 1
                }
            };

            Child = _thumb;
            _isChecked = isChecked;
            UpdateVisualState();

            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                IsChecked = !IsChecked;
            };
        }

        private void UpdateVisualState()
        {
            if (_isChecked)
            {
                // Vibrant orange matching exact screenshot
                Background = new SolidColorBrush(Color.FromRgb(255, 85, 0));
                _thumb.HorizontalAlignment = HorizontalAlignment.Right;
                _thumb.Margin = new Thickness(0, 0, 3, 0);
            }
            else
            {
                Background = new SolidColorBrush(Color.FromRgb(46, 46, 52));
                _thumb.HorizontalAlignment = HorizontalAlignment.Left;
                _thumb.Margin = new Thickness(3, 0, 0, 0);
            }
        }
    }

    /// <summary>
    /// Modern Segmented Picker / Toggle Group matching the exact screenshot design.
    /// </summary>
    public sealed class MayliSegmentedControl : Border
    {
        private readonly List<Border> _buttons = new();
        private readonly List<string> _options;
        private string _selectedValue;

        public event Action<string> SelectionChanged;

        public string SelectedValue
        {
            get => _selectedValue;
            set
            {
                _selectedValue = value;
                UpdateSelection();
                SelectionChanged?.Invoke(_selectedValue);
            }
        }

        public MayliSegmentedControl(IEnumerable<string> options, string defaultValue = null)
        {
            _options = options.ToList();
            _selectedValue = defaultValue ?? _options.FirstOrDefault() ?? "";

            Background = new SolidColorBrush(Color.FromRgb(26, 26, 30));
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 48));
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            Padding = new Thickness(2);
            VerticalAlignment = VerticalAlignment.Center;

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var opt in _options)
            {
                string captured = opt;
                var btn = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(13, 5, 13, 5),
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent
                };

                var txt = new TextBlock
                {
                    Text = opt,
                    FontSize = 12.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                btn.Child = txt;

                btn.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    SelectedValue = captured;
                };

                _buttons.Add(btn);
                stack.Children.Add(btn);
            }

            Child = stack;
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < _options.Count; i++)
            {
                bool isSelected = string.Equals(_options[i], _selectedValue, StringComparison.OrdinalIgnoreCase);
                var btn = _buttons[i];
                if (btn.Child is TextBlock txt)
                {
                    if (isSelected)
                    {
                        btn.Background = new SolidColorBrush(Color.FromRgb(52, 52, 60));
                        txt.Foreground = Brushes.White;
                        txt.FontWeight = FontWeights.SemiBold;
                    }
                    else
                    {
                        btn.Background = Brushes.Transparent;
                        txt.Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150));
                        txt.FontWeight = FontWeights.Normal;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Standard settings row layout matching the screenshot (Title + Description on Left, Control on Right).
    /// </summary>
    public sealed class MayliSettingRow : Grid
    {
        public string Title { get; }
        public string Description { get; }

        public MayliSettingRow(string title, string description, FrameworkElement control)
        {
            Margin = new Thickness(0, 0, 0, 24);
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Title = title;
            Description = description;

            var leftStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 24, 0),
                MaxWidth = 490
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            leftStack.Children.Add(titleBlock);

            if (!string.IsNullOrEmpty(description))
            {
                var descBlock = new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 138)),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 17,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                leftStack.Children.Add(descBlock);
            }

            Grid.SetColumn(leftStack, 0);
            Children.Add(leftStack);

            if (control != null)
            {
                control.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 1);
                Children.Add(control);
            }
        }
    }

    /// <summary>
    /// Left Sidebar navigation item with squircle icon and selection state.
    /// </summary>
    public sealed class MayliSidebarItem : Border
    {
        public string Key { get; }
        public string Label { get; }
        private readonly TextBlock _labelBlock;
        private bool _isSelected;

        public event Action<string> Clicked;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                UpdateState();
            }
        }

        public MayliSidebarItem(string key, string label, string glyph, Color iconBg)
        {
            Key = key;
            Label = label;
            Height = 38;
            CornerRadius = new CornerRadius(8);
            Cursor = Cursors.Hand;
            Margin = new Thickness(10, 2, 10, 2);
            Padding = new Thickness(8, 0, 8, 0);
            SnapsToDevicePixels = true;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Squircle icon container
            var iconBox = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(iconBg),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var iconGlyph = new TextBlock
            {
                Text = glyph,
                FontSize = 13,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.Child = iconGlyph;
            Grid.SetColumn(iconBox, 0);
            grid.Children.Add(iconBox);

            // Label text
            _labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 218)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_labelBlock, 1);
            grid.Children.Add(_labelBlock);

            Child = grid;

            MouseEnter += (_, _) =>
            {
                if (!_isSelected)
                {
                    Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
                    _labelBlock.Foreground = Brushes.White;
                }
            };

            MouseLeave += (_, _) =>
            {
                if (!_isSelected)
                {
                    Background = Brushes.Transparent;
                    _labelBlock.Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 218));
                }
            };

            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Clicked?.Invoke(Key);
            };

            UpdateState();
        }

        private void UpdateState()
        {
            if (_isSelected)
            {
                // Warm dark highlight matching screenshot
                Background = new SolidColorBrush(Color.FromRgb(46, 34, 28));
                BorderBrush = new SolidColorBrush(Color.FromRgb(72, 52, 40));
                BorderThickness = new Thickness(1);
                _labelBlock.Foreground = Brushes.White;
                _labelBlock.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                Background = Brushes.Transparent;
                BorderBrush = Brushes.Transparent;
                BorderThickness = new Thickness(0);
                _labelBlock.Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 218));
                _labelBlock.FontWeight = FontWeights.Normal;
            }
        }
    }

    /// <summary>
    /// Section header label (VISIBILITY, STYLE, BEHAVIOR, etc.) in uppercase dim gray.
    /// </summary>
    public sealed class MayliSectionHeader : TextBlock
    {
        public MayliSectionHeader(string text)
        {
            Text = text.ToUpperInvariant();
            FontSize = 11.5;
            FontWeight = FontWeights.Bold;
            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 138));
            Margin = new Thickness(0, 22, 0, 12);
        }
    }

    /// <summary>
    /// Visual selection card with preview illustration, title, and description (used in VISIBILITY, STYLE, EDGE LIGHT).
    /// </summary>
    public sealed class MayliVisualCard : Border
    {
        public string Key { get; }
        public string Title { get; }
        private bool _isSelected;

        public event Action<string> Selected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                UpdateCardBorder();
            }
        }

        public MayliVisualCard(string key, string title, string description, FrameworkElement illustration, bool isSelected = false)
        {
            Key = key;
            Title = title;
            _isSelected = isSelected;

            CornerRadius = new CornerRadius(10);
            Background = new SolidColorBrush(Color.FromRgb(22, 22, 24));
            Cursor = Cursors.Hand;
            Margin = new Thickness(4);
            SnapsToDevicePixels = true;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Top Illustration Area
            var illusBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 30)),
                CornerRadius = new CornerRadius(9, 9, 0, 0),
                ClipToBounds = true
            };
            if (illustration != null)
            {
                illustration.HorizontalAlignment = HorizontalAlignment.Center;
                illustration.VerticalAlignment = VerticalAlignment.Center;
                illusBox.Child = illustration;
            }
            Grid.SetRow(illusBox, 0);
            grid.Children.Add(illusBox);

            // 2. Bottom Text Area
            var textStack = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            textStack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetRow(textStack, 1);
            grid.Children.Add(textStack);

            Child = grid;

            MouseEnter += (_, _) =>
            {
                if (!_isSelected)
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 70));
            };

            MouseLeave += (_, _) =>
            {
                if (!_isSelected)
                    UpdateCardBorder();
            };

            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Selected?.Invoke(Key);
            };

            UpdateCardBorder();
        }

        private void UpdateCardBorder()
        {
            if (_isSelected)
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 0));
                BorderThickness = new Thickness(2);
            }
            else
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 46));
                BorderThickness = new Thickness(1);
            }
        }
    }

    /// <summary>
    /// Sleek Slider with a dynamic percentage label (e.g. 90%, 80%).
    /// </summary>
    public sealed class MayliSliderRow : StackPanel
    {
        private readonly TextBlock _label;
        private readonly Slider _slider;

        public event Action<double> ValueChanged;

        public double Value
        {
            get => _slider.Value;
            set
            {
                _slider.Value = value;
                _label.Text = $"{(int)value}%";
            }
        }

        public MayliSliderRow(double min, double max, double current, string unit = "%")
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;

            _label = new TextBlock
            {
                Text = $"{(int)current}{unit}",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 180)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Width = 46,
                TextAlignment = TextAlignment.Right
            };
            Children.Add(_label);

            _slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = current,
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };

            _slider.ValueChanged += (_, e) =>
            {
                _label.Text = $"{(int)e.NewValue}{unit}";
                ValueChanged?.Invoke(e.NewValue);
            };

            Children.Add(_slider);
        }
    }

    /// <summary>
    /// Modern dark Dropdown / ComboBox with sleek chevron and selection popup.
    /// </summary>
    public sealed class MayliDropdown : Border
    {
        private readonly TextBlock _textBlock;
        private readonly List<string> _options;
        private string _selected;

        public event Action<string> SelectionChanged;

        public string SelectedValue
        {
            get => _selected;
            set
            {
                _selected = value;
                _textBlock.Text = value;
                SelectionChanged?.Invoke(value);
            }
        }

        public MayliDropdown(IEnumerable<string> options, string defaultOption = null)
        {
            _options = options.ToList();
            _selected = defaultOption ?? _options.FirstOrDefault() ?? "";

            Background = new SolidColorBrush(Color.FromRgb(28, 28, 32));
            BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 54));
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(14, 6, 12, 6);
            Cursor = Cursors.Hand;
            VerticalAlignment = VerticalAlignment.Center;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _textBlock = new TextBlock
            {
                Text = _selected,
                FontSize = 12.5,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_textBlock, 0);
            grid.Children.Add(_textBlock);

            var chevron = new TextBlock
            {
                Text = "˅",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(chevron, 1);
            grid.Children.Add(chevron);

            Child = grid;

            MouseEnter += (_, _) => Background = new SolidColorBrush(Color.FromRgb(36, 36, 42));
            MouseLeave += (_, _) => Background = new SolidColorBrush(Color.FromRgb(28, 28, 32));

            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                // Cycle through options or show context menu
                var menu = new ContextMenu();
                foreach (var opt in _options)
                {
                    string captured = opt;
                    var item = new MenuItem { Header = opt };
                    item.Click += (_, _) => SelectedValue = captured;
                    menu.Items.Add(item);
                }
                menu.PlacementTarget = this;
                menu.IsOpen = true;
            };
        }
    }

    /// <summary>
    /// Rich Module Item row for DOCK categories (ESSENTIALS, PRODUCTIVITY, INSIGHTS).
    /// Features colorful rounded icon box, Title, Subtitle, vibrant orange toggle switch, and chevron.
    /// </summary>
    public sealed class MayliModuleRow : Border
    {
        public string ModuleId { get; }
        public MayliToggleSwitch Toggle { get; }

        public MayliModuleRow(string moduleId, string title, string description, Color iconBg, string glyph, bool isEnabled)
        {
            ModuleId = moduleId;
            Background = Brushes.Transparent;
            CornerRadius = new CornerRadius(8);
            Padding = new Thickness(6, 6, 6, 6);
            Margin = new Thickness(0, 0, 0, 6);
            SnapsToDevicePixels = true;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Toggle & Chevron

            // 1. Icon Box (36x36 rounded square with vibrant app color)
            var iconBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(iconBg),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var glyphBlock = new TextBlock
            {
                Text = glyph,
                FontSize = 17,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            iconBox.Child = glyphBlock;
            Grid.SetColumn(iconBox, 0);
            grid.Children.Add(iconBox);

            // 2. Text Stack (Title and Subtitle)
            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            var descBlock = new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(titleBlock);
            textStack.Children.Add(descBlock);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // 3. Right Controls (Toggle & Chevron)
            var rightStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Toggle = new MayliToggleSwitch(isEnabled);
            rightStack.Children.Add(Toggle);

            var chevron = new TextBlock
            {
                Text = "›",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 80)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 6, 2)
            };
            rightStack.Children.Add(chevron);

            Grid.SetColumn(rightStack, 2);
            grid.Children.Add(rightStack);

            Child = grid;

            MouseEnter += (_, _) => Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            MouseLeave += (_, _) => Background = Brushes.Transparent;
        }
    }

    /// <summary>
    /// Home Widget item row for HOME WIDGETS section.
    /// Features Title, dynamic subtitle ("Show on Home" or "Remove a widget to make room"), and toggle switch.
    /// </summary>
    public sealed class MayliHomeWidgetRow : Grid
    {
        public string WidgetId { get; }
        public MayliToggleSwitch Toggle { get; }
        private readonly TextBlock _descBlock;

        public MayliHomeWidgetRow(string widgetId, string title, bool isEnabled, string subtitle)
        {
            WidgetId = widgetId;
            Margin = new Thickness(0, 0, 0, 20);
            SnapsToDevicePixels = true;

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            _descBlock = new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            textStack.Children.Add(titleBlock);
            textStack.Children.Add(_descBlock);
            Grid.SetColumn(textStack, 0);
            Children.Add(textStack);

            Toggle = new MayliToggleSwitch(isEnabled);
            Grid.SetColumn(Toggle, 1);
            Children.Add(Toggle);
        }

        public void UpdateSubtitle(string newSubtitle)
        {
            _descBlock.Text = newSubtitle;
        }
    }
}
