using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.Tools.Units
{
    public sealed class UnitsToolView : Grid
    {
        private readonly UnitsService _service = UnitsService.Instance;

        // Left sidebar
        private StackPanel _categoriesPanel;

        // Right detail
        private TextBox _fromInputBox;
        private TextBlock _fromUnitBtnText;
        private TextBlock _fromUnitSymbolText;
        private Border _fromUnitBtn;

        private TextBlock _toResultText;
        private TextBlock _toUnitBtnText;
        private TextBlock _toUnitSymbolText;
        private Border _toUnitBtn;

        private Border _copyResultBtn;
        private TextBlock _copyGlyph;

        private StackPanel _commonEquivalentsPanel;
        private TextBlock _recentText;

        public UnitsToolView()
        {
            Width = 728;
            Height = 242;

            BuildUI();

            _service.StateChanged += OnStateChanged;
            Loaded += (_, _) => RefreshUI();
            Unloaded += (_, _) => _service.StateChanged -= OnStateChanged;
        }

        private void BuildUI()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(168) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. Left Categories List
            var sidebar = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            _categoriesPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sidebar.Children.Add(_categoriesPanel);

            Grid.SetColumn(sidebar, 0);
            Children.Add(sidebar);

            // 2. Vertical Divider
            var vDivider = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 38)),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(vDivider, 1);
            Children.Add(vDivider);

            // 3. Right Conversion Area
            var rightArea = new Grid { Margin = new Thickness(18, 10, 18, 10) };
            rightArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // From block
            rightArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Divider & Swap button
            rightArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // To block
            rightArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Common equivalents
            rightArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Recent row

            // --- Row 0: From Block ---
            var fromBlock = new StackPanel();
            var fromLabel = new TextBlock
            {
                Text = "From",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            fromBlock.Children.Add(fromLabel);

            var fromRow = new DockPanel();

            _fromUnitBtn = CreateUnitSelector(out _fromUnitBtnText, out _fromUnitSymbolText, () => ShowUnitMenu(isFrom: true));
            DockPanel.SetDock(_fromUnitBtn, Dock.Right);
            fromRow.Children.Add(_fromUnitBtn);

            _fromInputBox = new TextBox
            {
                Text = "1",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CaretBrush = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            _fromInputBox.TextChanged += OnInputTextChanged;
            fromRow.Children.Add(_fromInputBox);

            fromBlock.Children.Add(fromRow);
            Grid.SetRow(fromBlock, 0);
            rightArea.Children.Add(fromBlock);

            // --- Row 1: Divider & Swap Button ---
            var swapGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var hLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 38)),
                VerticalAlignment = VerticalAlignment.Center
            };
            swapGrid.Children.Add(hLine);

            var swapBtn = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 58)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var swapTxt = new TextBlock
            {
                Text = "⇅",
                FontSize = 12,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            swapBtn.Child = swapTxt;
            swapBtn.MouseEnter += (_, _) => swapBtn.Background = new SolidColorBrush(Color.FromRgb(46, 46, 58));
            swapBtn.MouseLeave += (_, _) => swapBtn.Background = new SolidColorBrush(Color.FromRgb(30, 30, 38));
            swapBtn.MouseLeftButtonUp += (_, _) => _service.Swap();

            swapGrid.Children.Add(swapBtn);
            Grid.SetRow(swapGrid, 1);
            rightArea.Children.Add(swapGrid);

            // --- Row 2: To Block ---
            var toBlock = new StackPanel();
            var toLabel = new TextBlock
            {
                Text = "To",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            toBlock.Children.Add(toLabel);

            var toRow = new DockPanel();

            _toUnitBtn = CreateUnitSelector(out _toUnitBtnText, out _toUnitSymbolText, () => ShowUnitMenu(isFrom: false));
            DockPanel.SetDock(_toUnitBtn, Dock.Right);
            toRow.Children.Add(_toUnitBtn);

            var toLeftSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _toResultText = new TextBlock
            {
                Text = "3.28084",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            toLeftSp.Children.Add(_toResultText);

            _copyResultBtn = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _copyGlyph = new TextBlock
            {
                Text = "⎘",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _copyResultBtn.Child = _copyGlyph;
            _copyResultBtn.MouseEnter += (_, _) => _copyGlyph.Foreground = Brushes.White;
            _copyResultBtn.MouseLeave += (_, _) => _copyGlyph.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
            _copyResultBtn.MouseLeftButtonUp += (_, _) => CopyResultToClipboard();

            toLeftSp.Children.Add(_copyResultBtn);
            toRow.Children.Add(toLeftSp);

            toBlock.Children.Add(toRow);
            Grid.SetRow(toBlock, 2);
            rightArea.Children.Add(toBlock);

            // --- Row 3: Common Equivalents ---
            _commonEquivalentsPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetRow(_commonEquivalentsPanel, 3);
            rightArea.Children.Add(_commonEquivalentsPanel);

            // --- Row 4: Recent Row ---
            _recentText = new TextBlock
            {
                Text = "Recent  1 m → 3.28084 ft",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 128)),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(_recentText, 4);
            rightArea.Children.Add(_recentText);

            Grid.SetColumn(rightArea, 2);
            Children.Add(rightArea);
        }

        private Border CreateUnitSelector(out TextBlock nameText, out TextBlock symText, Action onClick)
        {
            var btn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 34)),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 5, 8, 5),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            nameText = new TextBlock
            {
                Text = "Meters",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(nameText);

            symText = new TextBlock
            {
                Text = "  m  ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(symText);

            var arrow = new TextBlock
            {
                Text = "⌵",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(arrow);

            btn.Child = sp;
            btn.MouseEnter += (_, _) => btn.Background = new SolidColorBrush(Color.FromRgb(38, 38, 48));
            btn.MouseLeave += (_, _) => btn.Background = new SolidColorBrush(Color.FromRgb(26, 26, 34));
            btn.MouseLeftButtonUp += (_, _) => onClick?.Invoke();

            return btn;
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            string txt = _fromInputBox.Text?.Trim();
            if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val) && val >= 0)
            {
                _service.SetInputValue(val);
            }
        }

        private void OnStateChanged()
        {
            Dispatcher.Invoke(RefreshUI);
        }

        private void RefreshUI()
        {
            // 1. Rebuild categories sidebar
            _categoriesPanel.Children.Clear();
            var activeCat = _service.ActiveCategory;

            foreach (var cat in UnitCatalog.All)
            {
                bool isActive = cat == activeCat;
                var pill = new Border
                {
                    Height = 25,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(isActive ? Color.FromRgb(34, 34, 42) : Colors.Transparent),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(0, 1, 0, 1),
                    Cursor = Cursors.Hand
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = cat.Icon + " ",
                    FontSize = 11.5,
                    Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = cat.Name,
                    FontSize = 11.5,
                    FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    VerticalAlignment = VerticalAlignment.Center
                });

                pill.Child = sp;

                string cname = cat.Name;
                pill.MouseLeftButtonUp += (_, _) => _service.SelectCategory(cname);

                if (!isActive)
                {
                    pill.MouseEnter += (_, _) => pill.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                    pill.MouseLeave += (_, _) => pill.Background = Brushes.Transparent;
                }

                _categoriesPanel.Children.Add(pill);
            }

            // 2. Update Units labels
            var from = _service.FromUnit;
            var to = _service.ToUnit;

            if (from != null)
            {
                _fromUnitBtnText.Text = from.Name;
                _fromUnitSymbolText.Text = $"  {from.Symbol}  ";
            }
            if (to != null)
            {
                _toUnitBtnText.Text = to.Name;
                _toUnitSymbolText.Text = $"  {to.Symbol}  ";
            }

            // 3. Update result
            decimal res = _service.ConvertedValue;
            _toResultText.Text = UnitCatalog.FormatResult(res);

            // 4. Update Common Equivalents
            _commonEquivalentsPanel.Children.Clear();
            var commons = _service.GetCommonEquivalents();
            foreach (var item in commons)
            {
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };

                var leftName = new TextBlock
                {
                    Text = item.name,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(leftName, Dock.Left);
                row.Children.Add(leftName);

                var rightSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                rightSp.Children.Add(new TextBlock
                {
                    Text = item.value,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                });
                rightSp.Children.Add(new TextBlock
                {
                    Text = "  " + item.symbol,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
                });

                DockPanel.SetDock(rightSp, Dock.Right);
                row.Children.Add(rightSp);

                _commonEquivalentsPanel.Children.Add(row);
            }

            // 5. Update Recent
            if (_service.Recents.Count > 0)
            {
                _recentText.Text = $"Recent  {_service.Recents[0].DisplayText}";
            }
        }

        private void ShowUnitMenu(bool isFrom)
        {
            var cm = new ContextMenu();
            var targetBtn = isFrom ? _fromUnitBtn : _toUnitBtn;
            var currentUnit = isFrom ? _service.FromUnit : _service.ToUnit;

            foreach (var u in _service.ActiveCategory.Units)
            {
                var mi = new MenuItem
                {
                    Header = $"{u.Name}  ({u.Symbol})",
                    IsCheckable = true,
                    IsChecked = u.Symbol == currentUnit?.Symbol
                };
                var captured = u;
                mi.Click += (_, _) =>
                {
                    if (isFrom) _service.SetFromUnit(captured);
                    else _service.SetToUnit(captured);
                };
                cm.Items.Add(mi);
            }

            cm.PlacementTarget = targetBtn;
            cm.IsOpen = true;
        }

        private void CopyResultToClipboard()
        {
            try
            {
                System.Windows.Clipboard.SetText(_toResultText.Text);
                _copyGlyph.Text = "✓";
                _copyGlyph.Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88));

                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    _copyGlyph.Text = "⎘";
                    _copyGlyph.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                };
                timer.Start();
            }
            catch { }
        }
    }
}
