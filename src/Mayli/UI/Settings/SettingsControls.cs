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
}
