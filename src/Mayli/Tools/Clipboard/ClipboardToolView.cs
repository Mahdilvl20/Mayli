using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.Tools.Clipboard
{
    public sealed class ClipboardToolView : UserControl
    {
        public const double GridW = 728.0;
        public const double GridH = 242.0;
        public const double SplitPad = 16.0;
        public const double LeftW = 319.0;

        private readonly Grid _rootGrid;
        private readonly StackPanel _listItemsPanel;
        private readonly ScrollViewer _listScrollViewer;
        private readonly TextBox _searchBox;
        private readonly Border _underline;
        private readonly TextBlock _detailMetaText;
        private readonly Border _pinButton;
        private readonly TextBlock _pinIcon;
        private readonly Border _deleteButton;
        private readonly Grid _previewHost;
        private readonly TextBlock _footerMetaText;
        private readonly Border _copyButton;
        private readonly TextBlock _copyButtonText;
        private readonly Border _openLinkButton;
        private readonly Border _saveButton;

        private readonly TextBlock _tabAllText;
        private readonly TextBlock _tabTextText;
        private readonly TextBlock _tabImagesText;
        private readonly TextBlock _tabLinksText;

        private ClipboardFilter _currentFilter = ClipboardFilter.All;
        private string _searchQuery = string.Empty;
        private ClipboardItem _selectedItem;
        private readonly DispatcherTimer _feedbackTimer;

        public ClipboardToolView()
        {
            Background = Brushes.Transparent;

            _rootGrid = new Grid();
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LeftW) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SplitPad) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -----------------------------------------------------------------
            // LEFT COLUMN (Filter Tabs + Search + Items List)
            // -----------------------------------------------------------------
            var leftColumn = new Grid();
            leftColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tabs bar
            leftColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search (if open)
            leftColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Section header
            leftColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            // Row 0: Tabs Bar
            var tabsDock = new DockPanel { Height = 28, Margin = new Thickness(0, 0, 0, 2) };

            var searchIconBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Search clipboard history",
                Child = new TextBlock
                {
                    Text = "🔍",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            searchIconBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _searchBox.Visibility = _searchBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                if (_searchBox.Visibility == Visibility.Visible)
                {
                    _searchBox.Focus();
                }
                else
                {
                    _searchBox.Text = string.Empty;
                }
            };
            DockPanel.SetDock(searchIconBtn, Dock.Right);
            tabsDock.Children.Add(searchIconBtn);

            var tabsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _tabAllText = CreateTabLabel("All 0", ClipboardFilter.All);
            _tabTextText = CreateTabLabel("Text 0", ClipboardFilter.Text);
            _tabImagesText = CreateTabLabel("Images 0", ClipboardFilter.Images);
            _tabLinksText = CreateTabLabel("Links 0", ClipboardFilter.Links);

            tabsStack.Children.Add(_tabAllText);
            tabsStack.Children.Add(_tabTextText);
            tabsStack.Children.Add(_tabImagesText);
            tabsStack.Children.Add(_tabLinksText);
            tabsDock.Children.Add(tabsStack);

            var tabsContainer = new Grid();
            tabsContainer.Children.Add(tabsDock);

            // Orange sliding underline
            _underline = new Border
            {
                Height = 2.0,
                Width = 36.0,
                Background = new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Orange #FF5722
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 0, 0),
                CornerRadius = new CornerRadius(1)
            };
            tabsContainer.Children.Add(_underline);

            Grid.SetRow(tabsContainer, 0);
            leftColumn.Children.Add(tabsContainer);

            // Row 1: Search Box (collapsible)
            _searchBox = new TextBox
            {
                Visibility = Visibility.Collapsed,
                Height = 24,
                Margin = new Thickness(0, 2, 0, 4),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                Padding = new Thickness(6, 2, 6, 2)
            };
            _searchBox.TextChanged += (_, _) =>
            {
                _searchQuery = _searchBox.Text.Trim();
                RefreshList();
            };
            Grid.SetRow(_searchBox, 1);
            leftColumn.Children.Add(_searchBox);

            // Row 2: Section header ("Today")
            var sectionHeader = new TextBlock
            {
                Text = "Today",
                FontSize = 11.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(4, 6, 0, 4)
            };
            Grid.SetRow(sectionHeader, 2);
            leftColumn.Children.Add(sectionHeader);

            // Row 3: Items List
            _listItemsPanel = new StackPanel();
            _listScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _listItemsPanel
            };
            Grid.SetRow(_listScrollViewer, 3);
            leftColumn.Children.Add(_listScrollViewer);

            Grid.SetColumn(leftColumn, 0);
            _rootGrid.Children.Add(leftColumn);

            // Vertical subtle separator in split gap
            var splitDivider = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(splitDivider, 1);
            _rootGrid.Children.Add(splitDivider);

            // -----------------------------------------------------------------
            // RIGHT COLUMN (Detail Header + Content Preview + Action Footer)
            // -----------------------------------------------------------------
            var rightColumn = new Grid();
            rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (Type + Pin + Delete)
            rightColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Preview content
            rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer (Meta + Copy + Save)

            // Row 0: Detail Header
            var detailHeader = new DockPanel { Height = 28, Margin = new Thickness(4, 0, 0, 4) };

            var rightActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _pinIcon = new TextBlock
            {
                Text = "📌",
                FontSize = 11.5,
                Opacity = 0.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pinButton = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Pin item",
                Margin = new Thickness(0, 0, 4, 0),
                Child = _pinIcon
            };
            _pinButton.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (_selectedItem != null)
                {
                    ClipboardService.Instance.TogglePin(_selectedItem);
                    UpdatePinVisual();
                }
            };
            rightActions.Children.Add(_pinButton);

            _deleteButton = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Delete item",
                Child = new TextBlock
                {
                    Text = "🗑️",
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _deleteButton.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (_selectedItem != null)
                {
                    ClipboardService.Instance.DeleteItem(_selectedItem);
                }
            };
            rightActions.Children.Add(_deleteButton);

            DockPanel.SetDock(rightActions, Dock.Right);
            detailHeader.Children.Add(rightActions);

            _detailMetaText = new TextBlock
            {
                Text = "Code · 49m ago",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            detailHeader.Children.Add(_detailMetaText);

            Grid.SetRow(detailHeader, 0);
            rightColumn.Children.Add(detailHeader);

            // Row 1: Content Preview Host
            _previewHost = new Grid
            {
                Margin = new Thickness(4, 2, 4, 4),
                ClipToBounds = true
            };
            Grid.SetRow(_previewHost, 1);
            rightColumn.Children.Add(_previewHost);

            // Row 2: Footer (Meta + Copy + Save)
            var footerDock = new DockPanel { Height = 32, Margin = new Thickness(4, 4, 0, 0) };

            var footerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Open Link button (visible only for Link kind)
            _openLinkButton = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Cursor = Cursors.Hand,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 8, 0),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = "↗ Open Link",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _openLinkButton.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenCurrentLink();
            };
            footerActions.Children.Add(_openLinkButton);

            // Orange Copy button matching screenshot
            _copyButton = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromRgb(230, 74, 25)), // #E64A19
                Cursor = Cursors.Hand,
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _copyButtonText = new TextBlock
            {
                Text = "📋 Copy",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            _copyButton.Child = _copyButtonText;
            _copyButton.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CopySelectedItem();
            };
            footerActions.Children.Add(_copyButton);

            // Save icon button
            _saveButton = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Save to file",
                Child = new TextBlock
                {
                    Text = "💾",
                    FontSize = 12.0,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _saveButton.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                SaveCurrentItem();
            };
            footerActions.Children.Add(_saveButton);

            DockPanel.SetDock(footerActions, Dock.Right);
            footerDock.Children.Add(footerActions);

            _footerMetaText = new TextBlock
            {
                Text = "81 lines · 2.3k chars",
                FontSize = 11.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            footerDock.Children.Add(_footerMetaText);

            Grid.SetRow(footerDock, 2);
            rightColumn.Children.Add(footerDock);

            Grid.SetColumn(rightColumn, 2);
            _rootGrid.Children.Add(rightColumn);

            Content = _rootGrid;

            _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _feedbackTimer.Tick += (_, _) =>
            {
                _feedbackTimer.Stop();
                _copyButtonText.Text = "📋 Copy";
                _copyButton.Background = new SolidColorBrush(Color.FromRgb(230, 74, 25));
            };

            ClipboardService.Instance.ItemsChanged += () => Dispatcher.InvokeAsync(RefreshList);
            RefreshList();
        }

        private TextBlock CreateTabLabel(string text, ClipboardFilter filter)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 12.0,
                FontWeight = filter == _currentFilter ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(filter == _currentFilter ? Colors.White : Color.FromRgb(142, 142, 147)),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                SetFilter(filter);
            };
            return tb;
        }

        private void SetFilter(ClipboardFilter filter)
        {
            _currentFilter = filter;

            _tabAllText.FontWeight = _currentFilter == ClipboardFilter.All ? FontWeights.Bold : FontWeights.Normal;
            _tabAllText.Foreground = new SolidColorBrush(_currentFilter == ClipboardFilter.All ? Colors.White : Color.FromRgb(142, 142, 147));

            _tabTextText.FontWeight = _currentFilter == ClipboardFilter.Text ? FontWeights.Bold : FontWeights.Normal;
            _tabTextText.Foreground = new SolidColorBrush(_currentFilter == ClipboardFilter.Text ? Colors.White : Color.FromRgb(142, 142, 147));

            _tabImagesText.FontWeight = _currentFilter == ClipboardFilter.Images ? FontWeights.Bold : FontWeights.Normal;
            _tabImagesText.Foreground = new SolidColorBrush(_currentFilter == ClipboardFilter.Images ? Colors.White : Color.FromRgb(142, 142, 147));

            _tabLinksText.FontWeight = _currentFilter == ClipboardFilter.Links ? FontWeights.Bold : FontWeights.Normal;
            _tabLinksText.Foreground = new SolidColorBrush(_currentFilter == ClipboardFilter.Links ? Colors.White : Color.FromRgb(142, 142, 147));

            // Position underline under active tab
            double offset = _currentFilter switch
            {
                ClipboardFilter.Text => 46,
                ClipboardFilter.Images => 96,
                ClipboardFilter.Links => 166,
                _ => 2
            };
            double w = _currentFilter switch
            {
                ClipboardFilter.Text => 40,
                ClipboardFilter.Images => 58,
                ClipboardFilter.Links => 46,
                _ => 36
            };
            _underline.Margin = new Thickness(offset, 0, 0, 0);
            _underline.Width = w;

            RefreshList();
        }

        public void RefreshList()
        {
            var all = ClipboardService.Instance.Items;

            // Update tab counts
            int countAll = all.Count;
            int countText = all.Count(i => i.Kind == ClipKind.Text || i.Kind == ClipKind.Code || i.Kind == ClipKind.Email);
            int countImages = all.Count(i => i.Kind == ClipKind.Image);
            int countLinks = all.Count(i => i.Kind == ClipKind.Link);

            _tabAllText.Text = $"All {countAll}";
            _tabTextText.Text = $"Text {countText}";
            _tabImagesText.Text = $"Images {countImages}";
            _tabLinksText.Text = $"Links {countLinks}";

            // Filter items
            IEnumerable<ClipboardItem> filtered = all;
            if (_currentFilter == ClipboardFilter.Text)
            {
                filtered = filtered.Where(i => i.Kind == ClipKind.Text || i.Kind == ClipKind.Code || i.Kind == ClipKind.Email);
            }
            else if (_currentFilter == ClipboardFilter.Images)
            {
                filtered = filtered.Where(i => i.Kind == ClipKind.Image);
            }
            else if (_currentFilter == ClipboardFilter.Links)
            {
                filtered = filtered.Where(i => i.Kind == ClipKind.Link);
            }

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                filtered = filtered.Where(i =>
                    (!string.IsNullOrEmpty(i.Text) && i.Text.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(i.SourceApp) && i.SourceApp.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(i.LinkHost) && i.LinkHost.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)));
            }

            var list = filtered.ToList();

            _listItemsPanel.Children.Clear();

            if (list.Count == 0)
            {
                _listItemsPanel.Children.Add(new TextBlock
                {
                    Text = "No items found",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 128)),
                    Margin = new Thickness(10, 20, 10, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                _selectedItem = null;
                RenderPreview();
                return;
            }

            if (_selectedItem == null || !list.Contains(_selectedItem))
            {
                _selectedItem = list[0];
            }

            foreach (var item in list)
            {
                bool isSelected = item == _selectedItem;

                var rowBorder = new Border
                {
                    Height = 40,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(isSelected ? Color.FromRgb(38, 38, 44) : Colors.Transparent),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 1, 0, 1),
                    Cursor = Cursors.Hand
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Left icon or image thumbnail
                var iconContainer = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    ClipToBounds = true,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };

                if (item.Kind == ClipKind.Image && (item.Thumbnail != null || item.Image != null))
                {
                    iconContainer.Child = new Image
                    {
                        Source = item.Thumbnail ?? item.Image,
                        Stretch = Stretch.UniformToFill
                    };
                }
                else if (item.Kind == ClipKind.Code)
                {
                    iconContainer.Child = new TextBlock
                    {
                        Text = "</>",
                        FontSize = 11.0,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88)), // green
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
                else if (item.Kind == ClipKind.Link)
                {
                    iconContainer.Child = new TextBlock
                    {
                        Text = "🔗",
                        FontSize = 11.0,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
                else
                {
                    iconContainer.Child = new TextBlock
                    {
                        Text = "📄",
                        FontSize = 11.0,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                Grid.SetColumn(iconContainer, 0);
                rowGrid.Children.Add(iconContainer);

                // Title + Subtitle
                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
                var titleBlock = new TextBlock
                {
                    Text = item.GetDisplayTitle(),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                textStack.Children.Add(titleBlock);

                var subtitleBlock = new TextBlock
                {
                    Text = item.GetDisplaySubtitle(),
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                textStack.Children.Add(subtitleBlock);

                Grid.SetColumn(textStack, 1);
                rowGrid.Children.Add(textStack);

                rowBorder.Child = rowGrid;

                rowBorder.MouseEnter += (_, _) =>
                {
                    if (item != _selectedItem)
                    {
                        rowBorder.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                    }
                };
                rowBorder.MouseLeave += (_, _) =>
                {
                    if (item != _selectedItem)
                    {
                        rowBorder.Background = Brushes.Transparent;
                    }
                };
                rowBorder.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    _selectedItem = item;
                    RefreshList();
                };

                _listItemsPanel.Children.Add(rowBorder);
            }

            RenderPreview();
        }

        private void RenderPreview()
        {
            _previewHost.Children.Clear();

            if (_selectedItem == null)
            {
                _detailMetaText.Text = string.Empty;
                _footerMetaText.Text = string.Empty;
                _openLinkButton.Visibility = Visibility.Collapsed;
                return;
            }

            _detailMetaText.Text = $"{_selectedItem.GetKindLabel()} · {_selectedItem.GetTimestampFormatted()}";
            _footerMetaText.Text = _selectedItem.GetFooterMeta();
            UpdatePinVisual();

            _openLinkButton.Visibility = _selectedItem.Kind == ClipKind.Link ? Visibility.Visible : Visibility.Collapsed;

            if (_selectedItem.Kind == ClipKind.Image && _selectedItem.Image != null)
            {
                // Full image preview
                var imgBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(4),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                imgBorder.Child = new Image
                {
                    Source = _selectedItem.Image,
                    Stretch = Stretch.Uniform,
                    MaxHeight = 150,
                    MaxWidth = 360
                };
                _previewHost.Children.Add(imgBorder);
            }
            else if (_selectedItem.Kind == ClipKind.Link)
            {
                // Link Preview
                var linkStack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10)
                };

                var globeIcon = new TextBlock
                {
                    Text = "🌐",
                    FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                linkStack.Children.Add(globeIcon);

                var hostText = new TextBlock
                {
                    Text = _selectedItem.LinkHost,
                    FontSize = 14.0,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                linkStack.Children.Add(hostText);

                var urlText = new TextBlock
                {
                    Text = _selectedItem.Text,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 210, 255)), // light blue
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 340,
                    Margin = new Thickness(0, 4, 0, 12)
                };
                linkStack.Children.Add(urlText);

                var openBtn = new Border
                {
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = new SolidColorBrush(Color.FromRgb(10, 132, 255)),
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(16, 0, 16, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "Open in Browser ↗",
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                openBtn.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    OpenCurrentLink();
                };
                linkStack.Children.Add(openBtn);

                _previewHost.Children.Add(linkStack);
            }
            else
            {
                // Monospace Code / Text preview
                var scroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                var textBlock = new TextBlock
                {
                    Text = _selectedItem.Text,
                    FontFamily = new FontFamily("Consolas, Cascadia Code, Segoe UI Mono, monospace"),
                    FontSize = 12.0,
                    LineHeight = 18.0,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2)
                };
                scroll.Content = textBlock;
                _previewHost.Children.Add(scroll);
            }
        }

        private void UpdatePinVisual()
        {
            if (_selectedItem != null && _selectedItem.IsPinned)
            {
                _pinIcon.Opacity = 1.0;
                _pinButton.Background = new SolidColorBrush(Color.FromArgb(60, 255, 214, 10));
            }
            else
            {
                _pinIcon.Opacity = 0.5;
                _pinButton.Background = Brushes.Transparent;
            }
        }

        private void CopySelectedItem()
        {
            if (_selectedItem == null) return;
            if (ClipboardService.Instance.CopyToClipboard(_selectedItem))
            {
                _copyButtonText.Text = "Copied! ✓";
                _copyButton.Background = new SolidColorBrush(Color.FromRgb(48, 209, 88)); // Green
                _feedbackTimer.Stop();
                _feedbackTimer.Start();
            }
        }

        private void OpenCurrentLink()
        {
            if (_selectedItem?.LinkUri != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_selectedItem.LinkUri.AbsoluteUri) { UseShellExecute = true });
                }
                catch { }
            }
        }

        private void SaveCurrentItem()
        {
            if (_selectedItem == null) return;
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog();
                if (_selectedItem.Kind == ClipKind.Image && _selectedItem.Image != null)
                {
                    sfd.FileName = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    sfd.Filter = "PNG Image (*.png)|*.png";
                    if (sfd.ShowDialog() == true)
                    {
                        using var fs = new System.IO.FileStream(sfd.FileName, System.IO.FileMode.Create);
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(_selectedItem.Image));
                        enc.Save(fs);
                    }
                }
                else
                {
                    sfd.FileName = $"snippet_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    sfd.Filter = "Text File (*.txt)|*.txt";
                    if (sfd.ShowDialog() == true)
                    {
                        System.IO.File.WriteAllText(sfd.FileName, _selectedItem.Text);
                    }
                }
            }
            catch { }
        }
    }
}
