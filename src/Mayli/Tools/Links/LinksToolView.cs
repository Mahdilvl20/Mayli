using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Links
{
    public sealed class LinksToolView : Grid
    {
        private readonly LinksService _service = LinksService.Instance;

        // Card 1: Search & Engine chips
        private TextBox _searchBox;
        private TextBlock _searchPlaceholder;
        private Border _searchClearBtn;
        private WrapPanel _chipsPanel;

        // Card 2: Header, Categories & Bookmarks Grid
        private TextBlock _countText;
        private StackPanel _categoryTabsPanel;
        private WrapPanel _bookmarksWrapPanel;
        private TextBlock _emptyStateText;

        // Add / Edit Modal Overlay
        private Grid _modalOverlay;
        private TextBlock _modalTitle;
        private TextBox _modalUrlBox;
        private TextBox _modalNameBox;
        private StackPanel _modalCategoryPillsPanel;
        private Border _modalPinToggle;
        private TextBlock _modalPinText;
        private Border _modalSaveBtn;
        private TextBlock _modalSaveText;

        private string _editingBookmarkId;
        private string _modalSelectedCategory = "🌐 General";
        private bool _modalIsPinned;

        public LinksToolView()
        {
            FlowDirection = LocalizationService.CurrentFlowDirection;
            BuildUI();

            Loaded += (_, _) =>
            {
                _service.StateChanged -= OnStateChanged;
                _service.StateChanged += OnStateChanged;
                RefreshUI();
            };

            Unloaded += (_, _) =>
            {
                _service.StateChanged -= OnStateChanged;
            };
        }

        public void OnActivated()
        {
            _service.StateChanged -= OnStateChanged;
            _service.StateChanged += OnStateChanged;
            RefreshUI();
        }

        private void BuildUI()
        {
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) }); // Search & Engines
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Bookmarks

            BuildSearchCard();
            BuildBookmarksCard();
            BuildModalOverlay();
        }

        #region Card 1: Search & Engine Chips
        private void BuildSearchCard()
        {
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                CornerRadius = new CornerRadius(13),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 7)
            };

            var dock = new DockPanel();

            // --- Top Row: Search Input & Modern Action Button ---
            var searchRow = new DockPanel();

            // Sleek Modern Search Button
            var searchBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Accent),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var searchBtnText = new TextBlock
            {
                Text = LocalizationService.T("links.search", "Search") + " ↗",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            searchBtn.Child = searchBtnText;
            searchBtn.MouseEnter += (_, _) => searchBtn.Opacity = 0.88;
            searchBtn.MouseLeave += (_, _) => searchBtn.Opacity = 1.0;
            searchBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _service.PerformSearch(_searchBox.Text);
            };

            DockPanel.SetDock(searchBtn, Dock.Right);
            searchRow.Children.Add(searchBtn);

            // Search Bar Container
            var searchBoxContainer = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(9),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };

            var searchDock = new DockPanel();

            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(searchIcon, Dock.Left);
            searchDock.Children.Add(searchIcon);

            // Clear Button (fades in when text is present)
            _searchClearBtn = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = CapsuleTheme.Brush(p.ControlFill),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 2, 0)
            };
            var clearTxt = new TextBlock
            {
                Text = "✕",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchClearBtn.Child = clearTxt;
            _searchClearBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _searchBox.Text = "";
                _searchBox.Focus();
            };
            DockPanel.SetDock(_searchClearBtn, Dock.Right);
            searchDock.Children.Add(_searchClearBtn);

            var searchGrid = new Grid();
            _searchPlaceholder = new TextBlock
            {
                Text = LocalizationService.T("links.search_placeholder", "Search the web or filter quick bookmarks..."),
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchGrid.Children.Add(_searchPlaceholder);

            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                FontSize = 12,
                CaretBrush = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += (s, e) =>
            {
                bool hasText = !string.IsNullOrEmpty(_searchBox.Text);
                _searchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
                _searchClearBtn.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
                RebuildBookmarksGrid();
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    _service.PerformSearch(_searchBox.Text);
                }
                else if (e.Key == Key.Escape)
                {
                    _searchBox.Text = "";
                }
            };
            searchGrid.Children.Add(_searchBox);
            searchDock.Children.Add(searchGrid);

            searchBoxContainer.Child = searchDock;
            searchRow.Children.Add(searchBoxContainer);

            DockPanel.SetDock(searchRow, Dock.Top);
            dock.Children.Add(searchRow);

            // --- Bottom Row: Search Engine Chips ---
            _chipsPanel = new WrapPanel
            {
                Margin = new Thickness(0, 6, 0, 0),
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
            var p = CapsuleTheme.Current;
            string selected = _service.SelectedEngine;

            foreach (var eng in _service.Engines)
            {
                bool isSelected = string.Equals(eng.Name, selected, StringComparison.OrdinalIgnoreCase);

                var chip = new Border
                {
                    Background = isSelected ? CapsuleTheme.Brush(p.AccentSoft) : CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = isSelected ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(7, 2, 8, 2),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = eng.IconGlyph,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = isSelected ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = eng.Name,
                    FontSize = 11,
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isSelected ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center
                });
                chip.Child = sp;

                string captured = eng.Name;
                chip.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    _service.SetSelectedEngine(captured);
                    if (!string.IsNullOrWhiteSpace(_searchBox?.Text))
                    {
                        _service.PerformSearch(_searchBox.Text);
                    }
                };

                if (!isSelected)
                {
                    chip.MouseEnter += (_, _) => chip.Background = CapsuleTheme.Brush(p.Surface3);
                    chip.MouseLeave += (_, _) => chip.Background = CapsuleTheme.Brush(p.Surface2);
                }

                _chipsPanel.Children.Add(chip);
            }
        }
        #endregion

        #region Card 2: Saved Bookmarks & Category Tabs
        private void BuildBookmarksCard()
        {
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                CornerRadius = new CornerRadius(13),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9)
            };

            var dock = new DockPanel();

            // Header Row: [🔖 Quick Bookmarks (5)]  ---  [+ Add Link Button]
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };

            // Beautiful Add Link Pill Button
            var addBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Accent),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 4, 11, 4),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var addText = new TextBlock
            {
                Text = LocalizationService.T("links.add_link", "+ Add Link"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            addBtn.Child = addText;
            addBtn.MouseEnter += (_, _) => addBtn.Opacity = 0.88;
            addBtn.MouseLeave += (_, _) => addBtn.Opacity = 1.0;
            addBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                ShowAddModal();
            };
            DockPanel.SetDock(addBtn, Dock.Right);
            header.Children.Add(addBtn);

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(new TextBlock
            {
                Text = "🔖 ",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("links.quick_bookmarks", "Quick Bookmarks"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            });

            _countText = new TextBlock
            {
                Text = "0",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleSp.Children.Add(_countText);
            header.Children.Add(titleSp);

            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Category Filter Segment Row
            var catScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _categoryTabsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            catScroll.Content = _categoryTabsPanel;
            DockPanel.SetDock(catScroll, Dock.Top);
            dock.Children.Add(catScroll);

            // Empty state placeholder
            _emptyStateText = new TextBlock
            {
                Text = LocalizationService.T("links.empty", "No bookmarks found. Click '+ Add Link' to add one."),
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
                Visibility = Visibility.Collapsed
            };

            // Bookmarks Grid (WrapPanel inside ScrollViewer)
            _bookmarksWrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _bookmarksWrapPanel
            };

            var contentGrid = new Grid();
            contentGrid.Children.Add(scroll);
            contentGrid.Children.Add(_emptyStateText);
            dock.Children.Add(contentGrid);

            card.Child = dock;
            Grid.SetRow(card, 1);
            Children.Add(card);
        }

        private void RebuildCategoryTabs()
        {
            _categoryTabsPanel.Children.Clear();
            var p = CapsuleTheme.Current;
            string active = _service.ActiveCategory;

            foreach (var cat in LinksService.Categories)
            {
                bool isSelected = string.Equals(cat, active, StringComparison.OrdinalIgnoreCase);

                var tab = new Border
                {
                    Background = isSelected ? CapsuleTheme.Brush(p.AccentSoft) : CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = isSelected ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(0, 0, 5, 0),
                    Cursor = Cursors.Hand
                };

                var txt = new TextBlock
                {
                    Text = cat,
                    FontSize = 10.5,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isSelected ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                tab.Child = txt;

                string captured = cat;
                tab.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    _service.SetActiveCategory(captured);
                };

                if (!isSelected)
                {
                    tab.MouseEnter += (_, _) => tab.Background = CapsuleTheme.Brush(p.Surface3);
                    tab.MouseLeave += (_, _) => tab.Background = CapsuleTheme.Brush(p.Surface2);
                }

                _categoryTabsPanel.Children.Add(tab);
            }
        }

        private void RebuildBookmarksGrid()
        {
            _bookmarksWrapPanel.Children.Clear();

            string query = _searchBox?.Text;
            string cat = _service.ActiveCategory;
            var list = _service.GetFilteredBookmarks(query, cat);

            _countText.Text = $"({list.Count})";
            _emptyStateText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (var bm in list)
            {
                _bookmarksWrapPanel.Children.Add(CreateBookmarkCard(bm));
            }
        }

        private UIElement CreateBookmarkCard(LinksBookmark bm)
        {
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Width = 186,
                Height = 52,
                CornerRadius = new CornerRadius(10),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 5, 7, 5),
                Margin = new Thickness(0, 0, 8, 7),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();

            // Right side: Actions & Pin indicator
            var rightActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Pin badge (amber star/pin)
            var pinBadge = new TextBlock
            {
                Text = "📌",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = bm.Pinned ? Visibility.Visible : Visibility.Collapsed
            };
            rightActions.Children.Add(pinBadge);

            // Copy Link Hover Button
            var copyBtn = new Border
            {
                Width = 20,
                Height = 22,
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var copyTxt = new TextBlock
            {
                Text = "⎘",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            copyBtn.Child = copyTxt;
            copyBtn.MouseEnter += (_, _) => copyTxt.Foreground = CapsuleTheme.Brush(p.Text);
            copyBtn.MouseLeave += (_, _) => copyTxt.Foreground = CapsuleTheme.Brush(p.Text2);
            copyBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                try
                {
                    System.Windows.Clipboard.SetText(bm.Url);
                    copyTxt.Text = "✓";
                    copyTxt.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
                    t.Tick += (s, _) =>
                    {
                        t.Stop();
                        copyTxt.Text = "⎘";
                        copyTxt.Foreground = CapsuleTheme.Brush(p.Text2);
                    };
                    t.Start();
                }
                catch { }
            };
            rightActions.Children.Add(copyBtn);

            // Three dots Context Menu Button
            var menuBtn = new Border
            {
                Width = 20,
                Height = 22,
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dotsText = new TextBlock
            {
                Text = "⋮",
                FontSize = 13,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            menuBtn.Child = dotsText;
            menuBtn.MouseEnter += (_, _) => dotsText.Foreground = CapsuleTheme.Brush(p.Text);
            menuBtn.MouseLeave += (_, _) => dotsText.Foreground = CapsuleTheme.Brush(p.Text2);

            // Modern Themed Context Menu
            var cm = new ContextMenu
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.BorderStrong),
                BorderThickness = new Thickness(1),
                Placement = PlacementMode.Bottom
            };

            var miOpen = new MenuItem { Header = "🌐 Open in Default Browser", Foreground = CapsuleTheme.Brush(p.Text), FontSize = 11.5 };
            miOpen.Click += (_, _) => _service.RecordVisit(bm.Id);
            cm.Items.Add(miOpen);

            var miCopy = new MenuItem { Header = "⎘ Copy URL", Foreground = CapsuleTheme.Brush(p.Text), FontSize = 11.5 };
            miCopy.Click += (_, _) =>
            {
                try { System.Windows.Clipboard.SetText(bm.Url); } catch { }
            };
            cm.Items.Add(miCopy);

            var miPin = new MenuItem
            {
                Header = bm.Pinned ? "📌 Unpin Bookmark" : "📌 Pin to Top",
                Foreground = CapsuleTheme.Brush(p.Text),
                FontSize = 11.5
            };
            string bmId = bm.Id;
            miPin.Click += (_, _) => _service.TogglePin(bmId);
            cm.Items.Add(miPin);

            var miEdit = new MenuItem { Header = "✏ Edit Bookmark...", Foreground = CapsuleTheme.Brush(p.Text), FontSize = 11.5 };
            miEdit.Click += (_, _) => ShowEditModal(bm);
            cm.Items.Add(miEdit);

            cm.Items.Add(new Separator());

            var miDelete = new MenuItem { Header = "🗑 Delete Bookmark", Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)), FontSize = 11.5 };
            miDelete.Click += (_, _) => _service.RemoveBookmark(bmId);
            cm.Items.Add(miDelete);

            menuBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                cm.PlacementTarget = menuBtn;
                cm.IsOpen = true;
            };

            rightActions.Children.Add(menuBtn);
            DockPanel.SetDock(rightActions, Dock.Right);
            dock.Children.Add(rightActions);

            // Left side: Favicon / Brand Avatar Badge
            var avatar = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(9),
                Background = GetAvatarBrush(bm.Domain),
                VerticalAlignment = VerticalAlignment.Center
            };
            var initialText = new TextBlock
            {
                Text = GetAvatarInitial(bm),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatar.Child = initialText;
            DockPanel.SetDock(avatar, Dock.Left);
            dock.Children.Add(avatar);

            // Center: Title & Domain Subtitle
            var titleStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };

            var nameText = new TextBlock
            {
                Text = bm.Name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleStack.Children.Add(nameText);

            var domainText = new TextBlock
            {
                Text = bm.Domain,
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            titleStack.Children.Add(domainText);

            dock.Children.Add(titleStack);
            card.Child = dock;

            card.MouseEnter += (_, _) =>
            {
                card.Background = CapsuleTheme.Brush(p.Surface3);
                copyBtn.Visibility = Visibility.Visible;
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = CapsuleTheme.Brush(p.Surface2);
                copyBtn.Visibility = Visibility.Collapsed;
            };

            card.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _service.RecordVisit(bm.Id);
            };

            return card;
        }

        private static Brush GetAvatarBrush(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return new SolidColorBrush(Color.FromRgb(40, 40, 50));
            domain = domain.ToLowerInvariant();

            if (domain.Contains("mayli") || domain.Contains("vayli"))
                return new LinearGradientBrush(Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), 45);
            if (domain.Contains("github"))
                return new LinearGradientBrush(Color.FromRgb(30, 41, 59), Color.FromRgb(15, 23, 42), 45);
            if (domain.Contains("google"))
                return new LinearGradientBrush(Color.FromRgb(59, 130, 246), Color.FromRgb(29, 78, 216), 45);
            if (domain.Contains("youtube"))
                return new LinearGradientBrush(Color.FromRgb(239, 68, 68), Color.FromRgb(185, 28, 28), 45);
            if (domain.Contains("chatgpt") || domain.Contains("openai"))
                return new LinearGradientBrush(Color.FromRgb(16, 163, 127), Color.FromRgb(13, 110, 86), 45);
            if (domain.Contains("duckduckgo"))
                return new LinearGradientBrush(Color.FromRgb(222, 88, 51), Color.FromRgb(180, 60, 30), 45);
            if (domain.Contains("bing"))
                return new LinearGradientBrush(Color.FromRgb(14, 165, 233), Color.FromRgb(2, 132, 199), 45);
            if (domain.Contains("stackoverflow"))
                return new LinearGradientBrush(Color.FromRgb(249, 115, 22), Color.FromRgb(194, 65, 12), 45);
            if (domain.Contains("wikipedia"))
                return new LinearGradientBrush(Color.FromRgb(71, 85, 105), Color.FromRgb(51, 65, 85), 45);

            int hash = Math.Abs(domain.GetHashCode());
            byte r = (byte)(35 + (hash % 45));
            byte g = (byte)(35 + ((hash / 10) % 45));
            byte b = (byte)(45 + ((hash / 100) % 45));
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static string GetAvatarInitial(LinksBookmark bm)
        {
            string domain = bm.Domain.ToLowerInvariant();
            if (domain.Contains("github")) return "🐈";
            if (domain.Contains("youtube")) return "▶";
            if (domain.Contains("chatgpt") || domain.Contains("openai")) return "✻";
            if (domain.Contains("duckduckgo")) return "🦆";
            if (domain.Contains("mayli") || domain.Contains("vayli")) return "🐾";
            if (domain.Contains("wikipedia")) return "W";

            if (!string.IsNullOrEmpty(bm.Name))
            {
                char c = bm.Name.TrimStart()[0];
                return char.ToUpperInvariant(c).ToString();
            }
            return "🔗";
        }
        #endregion

        #region Add / Edit Modal Overlay
        private void BuildModalOverlay()
        {
            var p = CapsuleTheme.Current;
            _modalOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRowSpan(_modalOverlay, 2);

            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                CornerRadius = new CornerRadius(14),
                BorderBrush = CapsuleTheme.Brush(p.BorderStrong),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Width = 410,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var dock = new DockPanel();

            // Header
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var closeBtn = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = CapsuleTheme.Brush(p.ControlFill),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeTxt = new TextBlock
            {
                Text = "✕",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Child = closeTxt;
            closeBtn.MouseEnter += (_, _) => closeTxt.Foreground = CapsuleTheme.Brush(p.Text);
            closeBtn.MouseLeave += (_, _) => closeTxt.Foreground = CapsuleTheme.Brush(p.Text2);
            closeBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _modalOverlay.Visibility = Visibility.Collapsed;
            };
            DockPanel.SetDock(closeBtn, Dock.Right);
            top.Children.Add(closeBtn);

            _modalTitle = new TextBlock
            {
                Text = LocalizationService.T("links.add_bookmark", "Add Quick Bookmark"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            top.Children.Add(_modalTitle);
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);

            // Bottom Action Buttons
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var cancelBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelBtn.Child = new TextBlock
            {
                Text = LocalizationService.T("common.cancel", "Cancel"),
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2)
            };
            cancelBtn.MouseEnter += (_, _) => cancelBtn.Background = CapsuleTheme.Brush(p.Surface3);
            cancelBtn.MouseLeave += (_, _) => cancelBtn.Background = CapsuleTheme.Brush(p.Surface2);
            cancelBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _modalOverlay.Visibility = Visibility.Collapsed;
            };
            actions.Children.Add(cancelBtn);

            _modalSaveBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Accent),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 5, 16, 5),
                Cursor = Cursors.Hand
            };
            _modalSaveText = new TextBlock
            {
                Text = "Save Bookmark",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.AccentFg)
            };
            _modalSaveBtn.Child = _modalSaveText;
            _modalSaveBtn.MouseEnter += (_, _) => _modalSaveBtn.Opacity = 0.88;
            _modalSaveBtn.MouseLeave += (_, _) => _modalSaveBtn.Opacity = 1.0;
            _modalSaveBtn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                CommitSaveModal();
            };
            actions.Children.Add(_modalSaveBtn);

            DockPanel.SetDock(actions, Dock.Bottom);
            dock.Children.Add(actions);

            // Form Fields
            var fields = new StackPanel();

            _modalUrlBox = CreateInputRow("URL", "https://example.com", fields);
            _modalNameBox = CreateInputRow("Title (optional)", "My Website", fields);

            // Category Selection Row
            var catLabel = new TextBlock
            {
                Text = "Category",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 4, 0, 3)
            };
            fields.Children.Add(catLabel);

            _modalCategoryPillsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            fields.Children.Add(_modalCategoryPillsPanel);

            // Pin to top toggle
            _modalPinToggle = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _modalPinText = new TextBlock
            {
                Text = "📌 Pin Bookmark to Top: OFF",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2)
            };
            _modalPinToggle.Child = _modalPinText;
            _modalPinToggle.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _modalIsPinned = !_modalIsPinned;
                UpdatePinToggleUI();
            };
            fields.Children.Add(_modalPinToggle);

            dock.Children.Add(fields);
            card.Child = dock;
            _modalOverlay.Children.Add(card);

            Children.Add(_modalOverlay);
        }

        private TextBox CreateInputRow(string label, string placeholder, StackPanel parent)
        {
            var p = CapsuleTheme.Current;
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 2)
            });

            var b = new Border
            {
                Height = 30,
                CornerRadius = new CornerRadius(7),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 8, 0)
            };
            var tb = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                FontSize = 11.5,
                CaretBrush = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            b.Child = tb;
            sp.Children.Add(b);
            parent.Children.Add(sp);
            return tb;
        }

        private void RebuildModalCategoryPills()
        {
            _modalCategoryPillsPanel.Children.Clear();
            var p = CapsuleTheme.Current;
            var cats = LinksService.Categories.Where(c => c != "All" && !c.Contains("Pinned")).ToArray();

            foreach (var c in cats)
            {
                bool isSelected = string.Equals(c, _modalSelectedCategory, StringComparison.OrdinalIgnoreCase);

                var pill = new Border
                {
                    Background = isSelected ? CapsuleTheme.Brush(p.AccentSoft) : CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = isSelected ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(7, 3, 7, 3),
                    Margin = new Thickness(0, 0, 5, 0),
                    Cursor = Cursors.Hand
                };
                var txt = new TextBlock
                {
                    Text = c,
                    FontSize = 10,
                    Foreground = isSelected ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2)
                };
                pill.Child = txt;

                string captured = c;
                pill.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    _modalSelectedCategory = captured;
                    RebuildModalCategoryPills();
                };

                _modalCategoryPillsPanel.Children.Add(pill);
            }
        }

        private void UpdatePinToggleUI()
        {
            var p = CapsuleTheme.Current;
            if (_modalIsPinned)
            {
                _modalPinToggle.Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
                _modalPinToggle.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                _modalPinText.Text = "📌 Pin Bookmark to Top: ON";
                _modalPinText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            }
            else
            {
                _modalPinToggle.Background = CapsuleTheme.Brush(p.Surface2);
                _modalPinToggle.BorderBrush = CapsuleTheme.Brush(p.Border);
                _modalPinText.Text = "📌 Pin Bookmark to Top: OFF";
                _modalPinText.Foreground = CapsuleTheme.Brush(p.Text2);
            }
        }

        private void ShowAddModal()
        {
            _editingBookmarkId = null;
            _modalTitle.Text = LocalizationService.T("links.add_bookmark", "Add Quick Bookmark");
            _modalSaveText.Text = "Save Bookmark";
            _modalUrlBox.Text = "";
            _modalNameBox.Text = "";
            _modalSelectedCategory = "🌐 General";
            _modalIsPinned = false;

            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string clip = System.Windows.Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrEmpty(clip) && (clip.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || clip.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        _modalUrlBox.Text = clip;
                    }
                }
            }
            catch { }

            RebuildModalCategoryPills();
            UpdatePinToggleUI();
            _modalOverlay.Visibility = Visibility.Visible;
            _modalUrlBox.Focus();
        }

        private void ShowEditModal(LinksBookmark bm)
        {
            _editingBookmarkId = bm.Id;
            _modalTitle.Text = "Edit Quick Bookmark";
            _modalSaveText.Text = "Update Bookmark";
            _modalUrlBox.Text = bm.Url;
            _modalNameBox.Text = bm.Name;
            _modalSelectedCategory = string.IsNullOrWhiteSpace(bm.Category) ? "🌐 General" : bm.Category;
            _modalIsPinned = bm.Pinned;

            RebuildModalCategoryPills();
            UpdatePinToggleUI();
            _modalOverlay.Visibility = Visibility.Visible;
            _modalNameBox.Focus();
        }

        private void CommitSaveModal()
        {
            string url = _modalUrlBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            string name = _modalNameBox.Text?.Trim();

            if (string.IsNullOrEmpty(_editingBookmarkId))
            {
                _service.AddBookmark(name, url, _modalSelectedCategory, _modalIsPinned);
            }
            else
            {
                _service.UpdateBookmark(_editingBookmarkId, name, url, _modalSelectedCategory, _modalIsPinned);
            }

            _modalOverlay.Visibility = Visibility.Collapsed;
        }
        #endregion

        private void OnStateChanged()
        {
            Dispatcher.Invoke(RefreshUI);
        }

        private void RefreshUI()
        {
            RebuildEngineChips();
            RebuildCategoryTabs();
            RebuildBookmarksGrid();
        }
    }
}
