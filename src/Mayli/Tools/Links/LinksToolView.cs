using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsBuddy.Tools.Links
{
    public sealed class LinksToolView : Grid
    {
        private readonly LinksService _service = LinksService.Instance;

        // Card 1: Search & Engine chips
        private TextBox _searchBox;
        private TextBlock _searchPlaceholder;
        private WrapPanel _chipsPanel;

        // Card 2: Bookmarks
        private TextBlock _countText;
        private WrapPanel _bookmarksWrapPanel;

        // Add Bookmark Overlay
        private Grid _addOverlay;
        private TextBox _addUrlBox;
        private TextBox _addNameBox;

        public LinksToolView()
        {
            BuildUI();

            _service.StateChanged += OnStateChanged;
            Loaded += (_, _) => RefreshUI();
            Unloaded += (_, _) => _service.StateChanged -= OnStateChanged;
        }

        private void BuildUI()
        {
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(82) }); // Card 1
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Card 2

            BuildSearchCard();
            BuildBookmarksCard();
            BuildAddOverlay();
        }

        #region Card 1: Search & Engine Chips
        private void BuildSearchCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var dock = new DockPanel();

            // Top Search Row
            var searchRow = new DockPanel();

            var searchBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(230, 74, 25)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var searchBtnText = new TextBlock
            {
                Text = "Search ↗",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            searchBtn.Child = searchBtnText;
            searchBtn.MouseEnter += (_, _) => searchBtn.Background = new SolidColorBrush(Color.FromRgb(255, 90, 40));
            searchBtn.MouseLeave += (_, _) => searchBtn.Background = new SolidColorBrush(Color.FromRgb(230, 74, 25));
            searchBtn.MouseLeftButtonUp += (_, _) => _service.PerformSearch(_searchBox.Text);
            DockPanel.SetDock(searchBtn, Dock.Right);
            searchRow.Children.Add(searchBtn);

            var inputDock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(searchIcon, Dock.Left);
            inputDock.Children.Add(searchIcon);

            var searchGrid = new Grid();
            _searchPlaceholder = new TextBlock
            {
                Text = "Search",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchGrid.Children.Add(_searchPlaceholder);

            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontSize = 12.5,
                CaretBrush = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _searchPlaceholder.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    _service.PerformSearch(_searchBox.Text);
                }
            };
            searchGrid.Children.Add(_searchBox);
            inputDock.Children.Add(searchGrid);

            searchRow.Children.Add(inputDock);
            DockPanel.SetDock(searchRow, Dock.Top);
            dock.Children.Add(searchRow);

            // Bottom Engine Chips Row
            _chipsPanel = new WrapPanel
            {
                Margin = new Thickness(0, 8, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            dock.Children.Add(_chipsPanel);

            card.Child = dock;
            Grid.SetRow(card, 0);
            Children.Add(card);
        }

        private void RebuildEngineChips()
        {
            _chipsPanel.Children.Clear();
            string selected = _service.SelectedEngine;

            foreach (var eng in _service.Engines)
            {
                bool isSelected = string.Equals(eng.Name, selected, StringComparison.OrdinalIgnoreCase);

                var chip = new Border
                {
                    Background = new SolidColorBrush(isSelected ? Color.FromRgb(46, 28, 20) : Colors.Transparent),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 3, 10, 3),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = eng.IconGlyph + " ",
                    FontSize = 11,
                    Foreground = isSelected ? new SolidColorBrush(Color.FromRgb(255, 120, 60)) : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = eng.Name,
                    FontSize = 12,
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                chip.Child = sp;

                string captured = eng.Name;
                chip.MouseLeftButtonUp += (_, _) => _service.SetSelectedEngine(captured);

                if (!isSelected)
                {
                    chip.MouseEnter += (_, _) => chip.Background = new SolidColorBrush(Color.FromRgb(30, 30, 38));
                    chip.MouseLeave += (_, _) => chip.Background = Brushes.Transparent;
                }

                _chipsPanel.Children.Add(chip);
            }

            // Plus icon at end
            var plusBtn = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand
            };
            var plusTxt = new TextBlock
            {
                Text = "+",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            plusBtn.Child = plusTxt;
            plusBtn.MouseEnter += (_, _) => plusTxt.Foreground = Brushes.White;
            plusBtn.MouseLeave += (_, _) => plusTxt.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
            _chipsPanel.Children.Add(plusBtn);
        }
        #endregion

        #region Card 2: Saved Bookmarks
        private void BuildBookmarksCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 12, 14, 12)
            };

            var dock = new DockPanel();

            // Header row: Saved links 2 links | + Add Link
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

            var addBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(230, 74, 25)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 5, 12, 5),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var addText = new TextBlock
            {
                Text = "+ Add Link",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            addBtn.Child = addText;
            addBtn.MouseEnter += (_, _) => addBtn.Background = new SolidColorBrush(Color.FromRgb(255, 90, 40));
            addBtn.MouseLeave += (_, _) => addBtn.Background = new SolidColorBrush(Color.FromRgb(230, 74, 25));
            addBtn.MouseLeftButtonUp += (_, _) => ShowAddOverlay();
            DockPanel.SetDock(addBtn, Dock.Right);
            header.Children.Add(addBtn);

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(new TextBlock
            {
                Text = "🔖 ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = "Saved links",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            _countText = new TextBlock
            {
                Text = "2 links",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleSp.Children.Add(_countText);
            header.Children.Add(titleSp);

            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Bookmarks Grid
            _bookmarksWrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _bookmarksWrapPanel
            };
            dock.Children.Add(scroll);

            card.Child = dock;
            Grid.SetRow(card, 1);
            Children.Add(card);
        }

        private void RebuildBookmarksGrid()
        {
            _bookmarksWrapPanel.Children.Clear();
            var bms = _service.Bookmarks;
            _countText.Text = $"{bms.Count} links";

            foreach (var bm in bms)
            {
                _bookmarksWrapPanel.Children.Add(CreateBookmarkCard(bm));
            }
        }

        private UIElement CreateBookmarkCard(LinksBookmark bm)
        {
            var card = new Border
            {
                Width = 184,
                Height = 48,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                Padding = new Thickness(7, 6, 8, 6),
                Margin = new Thickness(0, 0, 10, 8),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();

            // Right: Three dots menu
            var menuBtn = new Border
            {
                Width = 18,
                Height = 24,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dotsText = new TextBlock
            {
                Text = "⋮",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            menuBtn.Child = dotsText;
            menuBtn.MouseEnter += (_, _) => dotsText.Foreground = Brushes.White;
            menuBtn.MouseLeave += (_, _) => dotsText.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));

            var cm = new ContextMenu();
            var miOpen = new MenuItem { Header = "Open in Browser" };
            miOpen.Click += (_, _) => LinksService.OpenUrl(bm.Url);
            cm.Items.Add(miOpen);

            var miCopy = new MenuItem { Header = "Copy URL" };
            miCopy.Click += (_, _) => System.Windows.Clipboard.SetText(bm.Url);
            cm.Items.Add(miCopy);

            cm.Items.Add(new Separator());

            var miDelete = new MenuItem { Header = "Remove Bookmark" };
            string bid = bm.Id;
            miDelete.Click += (_, _) => _service.RemoveBookmark(bid);
            cm.Items.Add(miDelete);

            menuBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                cm.PlacementTarget = menuBtn;
                cm.IsOpen = true;
            };

            DockPanel.SetDock(menuBtn, Dock.Right);
            dock.Children.Add(menuBtn);

            // Left: Favicon / Initial Avatar
            var avatar = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = GetAvatarBrush(bm.Domain),
                VerticalAlignment = VerticalAlignment.Center
            };
            var initialText = new TextBlock
            {
                Text = GetAvatarInitial(bm),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatar.Child = initialText;
            DockPanel.SetDock(avatar, Dock.Left);
            dock.Children.Add(avatar);

            // Center: Title & Domain
            var titleStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };

            var nameText = new TextBlock
            {
                Text = bm.Name,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleStack.Children.Add(nameText);

            var domainText = new TextBlock
            {
                Text = bm.Domain,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            titleStack.Children.Add(domainText);

            dock.Children.Add(titleStack);
            card.Child = dock;

            card.MouseEnter += (_, _) => card.Background = new SolidColorBrush(Color.FromRgb(38, 38, 46));
            card.MouseLeave += (_, _) => card.Background = new SolidColorBrush(Color.FromRgb(28, 28, 34));
            card.MouseLeftButtonUp += (_, _) => LinksService.OpenUrl(bm.Url);

            return card;
        }

        private static Brush GetAvatarBrush(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return new SolidColorBrush(Color.FromRgb(40, 40, 50));
            domain = domain.ToLowerInvariant();
            if (domain.Contains("mayli") || domain.Contains("vayli")) return new SolidColorBrush(Color.FromRgb(27, 43, 38));
            if (domain.Contains("github")) return new SolidColorBrush(Color.FromRgb(30, 36, 44));
            if (domain.Contains("google")) return new SolidColorBrush(Color.FromRgb(35, 45, 65));
            if (domain.Contains("youtube")) return new SolidColorBrush(Color.FromRgb(65, 25, 25));

            int hash = Math.Abs(domain.GetHashCode());
            byte r = (byte)(30 + (hash % 40));
            byte g = (byte)(30 + ((hash / 10) % 40));
            byte b = (byte)(40 + ((hash / 100) % 40));
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static string GetAvatarInitial(LinksBookmark bm)
        {
            if (bm.Domain.Contains("github")) return "🐈";
            if (!string.IsNullOrEmpty(bm.Name))
            {
                char c = bm.Name.TrimStart()[0];
                return char.ToUpperInvariant(c).ToString();
            }
            return "🔗";
        }
        #endregion

        #region Add Bookmark Overlay
        private void BuildAddOverlay()
        {
            _addOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 10, 10, 14)),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRowSpan(_addOverlay, 2);

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 28)),
                CornerRadius = new CornerRadius(14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 56)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
                Width = 420,
                Height = 180,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dock = new DockPanel();

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var closeBtn = new TextBlock
            {
                Text = "✕",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.MouseLeftButtonUp += (_, _) => _addOverlay.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(closeBtn, Dock.Right);
            top.Children.Add(closeBtn);

            top.Children.Add(new TextBlock
            {
                Text = "Add Bookmark",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var saveBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(230, 74, 25)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand
            };
            saveBtn.Child = new TextBlock
            {
                Text = "Add",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            saveBtn.MouseLeftButtonUp += (_, _) => CommitAdd();
            actions.Children.Add(saveBtn);

            DockPanel.SetDock(actions, Dock.Bottom);
            dock.Children.Add(actions);

            var fields = new StackPanel();

            _addUrlBox = CreateInputRow("URL", "https://example.com", fields);
            _addNameBox = CreateInputRow("Title (optional)", "My Website", fields);

            dock.Children.Add(fields);
            card.Child = dock;
            _addOverlay.Children.Add(card);

            Children.Add(_addOverlay);
        }

        private TextBox CreateInputRow(string label, string placeholder, StackPanel parent)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 0, 3)
            });

            var b = new Border
            {
                Height = 30,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 8, 0)
            };
            var tb = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontSize = 12,
                CaretBrush = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.Child = tb;
            sp.Children.Add(b);
            parent.Children.Add(sp);
            return tb;
        }

        private void ShowAddOverlay()
        {
            _addUrlBox.Text = "";
            _addNameBox.Text = "";

            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string clip = System.Windows.Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrEmpty(clip) && (clip.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || clip.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        _addUrlBox.Text = clip;
                    }
                }
            }
            catch { }

            _addOverlay.Visibility = Visibility.Visible;
            _addUrlBox.Focus();
        }

        private void CommitAdd()
        {
            string url = _addUrlBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(url))
            {
                _service.AddBookmark(_addNameBox.Text?.Trim(), url);
                _addOverlay.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        private void OnStateChanged()
        {
            Dispatcher.Invoke(RefreshUI);
        }

        private void RefreshUI()
        {
            RebuildEngineChips();
            RebuildBookmarksGrid();
        }
    }
}
