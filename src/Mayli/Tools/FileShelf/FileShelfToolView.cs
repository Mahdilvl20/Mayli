using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.FileShelf
{
    public sealed class FileShelfToolView : Grid
    {
        private readonly FileShelfService _service = FileShelfService.Instance;

        // Left Sidebar
        private StackPanel _shelvesListPanel;
        private string _renamingShelfId = null;

        // Top Action Bar
        private TextBlock _shelfTitleText;
        private TextBlock _shelfStatusText;
        private Border _cleanMissingBtn;
        private TextBlock _cleanMissingText;
        private TextBlock _sortBtnText;
        private TextBlock _viewModeBtnText;
        private TextBlock _clearBtnText;
        private DispatcherTimer _clearConfirmTimer;
        private bool _isClearPending = false;

        // Search / Filter
        private Border _searchBarBorder;
        private TextBox _searchBox;
        private string _filterQuery = string.Empty;

        // Main Drop Zone & Content
        private Grid _dropZoneGrid;
        private Rectangle _dropZoneBg;
        private StackPanel _emptyStatePanel;
        private ScrollViewer _filesScrollViewer;
        private WrapPanel _filesWrapPanel;
        private StackPanel _filesListStack;

        // Drag & Drop State
        private Point _dragStartPoint;
        private bool _isDraggingTile;
        private bool _isListView = false;

        public FileShelfToolView()
        {
            Focusable = true;
            FlowDirection = LocalizationService.CurrentFlowDirection;

            BuildUI();

            _service.ShelfChanged += OnShelfDataChanged;
            Loaded += (_, _) =>
            {
                RefreshUI();
                Focus();
            };
            Unloaded += (_, _) => _service.ShelfChanged -= OnShelfDataChanged;

            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void BuildUI()
        {
            var p = CapsuleTheme.Current;
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // 1. LEFT SIDEBAR (SHELVES)
            // -------------------------------------------------------------
            var sidebar = new Grid { Margin = new Thickness(0) };
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Shelves List
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // + New Shelf Button

            var sidebarHeader = new DockPanel { Margin = new Thickness(12, 10, 8, 6) };

            var newShelfPlusBtn = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Create new shelf",
                Child = new TextBlock
                {
                    Text = "+",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            newShelfPlusBtn.MouseEnter += (_, _) => newShelfPlusBtn.Background = CapsuleTheme.Brush(p.Surface2);
            newShelfPlusBtn.MouseLeave += (_, _) => newShelfPlusBtn.Background = Brushes.Transparent;
            newShelfPlusBtn.MouseLeftButtonUp += (_, _) => CreateNewShelfWithPrompt();
            DockPanel.SetDock(newShelfPlusBtn, Dock.Right);
            sidebarHeader.Children.Add(newShelfPlusBtn);

            var headerTitle = new TextBlock
            {
                Text = LocalizationService.T("shelf.shelves", "SHELVES"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            sidebarHeader.Children.Add(headerTitle);

            Grid.SetRow(sidebarHeader, 0);
            sidebar.Children.Add(sidebarHeader);

            _shelvesListPanel = new StackPanel { Margin = new Thickness(6, 0, 8, 0) };
            var shelvesScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _shelvesListPanel
            };
            Grid.SetRow(shelvesScroll, 1);
            sidebar.Children.Add(shelvesScroll);

            var newShelfBtn = new Border
            {
                Cursor = Cursors.Hand,
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(8, 4, 8, 8),
                CornerRadius = new CornerRadius(6)
            };
            var newShelfText = new TextBlock
            {
                Text = LocalizationService.T("shelf.new_shelf", "+ New shelf"),
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            newShelfBtn.Child = newShelfText;
            newShelfBtn.MouseEnter += (_, _) =>
            {
                newShelfText.Foreground = CapsuleTheme.Brush(p.Text);
                newShelfBtn.Background = CapsuleTheme.Brush(p.Surface2);
            };
            newShelfBtn.MouseLeave += (_, _) =>
            {
                newShelfText.Foreground = CapsuleTheme.Brush(p.Text2);
                newShelfBtn.Background = CapsuleTheme.Brush(p.Surface);
            };
            newShelfBtn.MouseLeftButtonUp += (_, _) => CreateNewShelfWithPrompt();

            Grid.SetRow(newShelfBtn, 2);
            sidebar.Children.Add(newShelfBtn);

            Grid.SetColumn(sidebar, 0);
            Children.Add(sidebar);

            // -------------------------------------------------------------
            // 2. VERTICAL DIVIDER
            // -------------------------------------------------------------
            var divider = new Border
            {
                Width = 1,
                Background = CapsuleTheme.Brush(p.Border),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(divider, 1);
            Children.Add(divider);

            // -------------------------------------------------------------
            // 3. RIGHT CONTENT AREA (Header + Drop Zone Grid/List)
            // -------------------------------------------------------------
            var rightArea = new DockPanel { Margin = new Thickness(0) };

            // Top action bar
            var topBar = new DockPanel { Margin = new Thickness(12, 8, 12, 6), LastChildFill = false };

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _shelfTitleText = new TextBlock
            {
                Text = "Desk",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            titleStack.Children.Add(_shelfTitleText);

            _shelfStatusText = new TextBlock
            {
                Text = "Empty",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(8, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(_shelfStatusText);

            // Missing file warning / clean button
            _cleanMissingText = new TextBlock
            {
                Text = "⚠️ Clean missing",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 149, 0)),
                VerticalAlignment = VerticalAlignment.Center
            };
            _cleanMissingBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 149, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                Child = _cleanMissingText
            };
            _cleanMissingBtn.MouseLeftButtonUp += (_, _) =>
            {
                int count = _service.RemoveMissingFiles();
                FlashStatus($"Cleaned {count} missing files");
            };
            titleStack.Children.Add(_cleanMissingBtn);

            DockPanel.SetDock(titleStack, Dock.Left);
            topBar.Children.Add(titleStack);

            var actionsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Search / Filter Input
            _searchBarBorder = new Border
            {
                Width = 90,
                Height = 24,
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 0, 6, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Search / Filter files"
            };
            _searchBox.TextChanged += (_, _) =>
            {
                _filterQuery = _searchBox.Text.Trim();
                RefreshFilesContent();
            };
            _searchBarBorder.Child = _searchBox;
            actionsStack.Children.Add(_searchBarBorder);

            // View Mode Toggle (Grid vs List)
            _viewModeBtnText = CreateActionLink("⊞ Grid", () =>
            {
                _isListView = !_isListView;
                _viewModeBtnText.Text = _isListView ? "☰ List" : "⊞ Grid";
                RefreshFilesContent();
            });
            actionsStack.Children.Add(_viewModeBtnText);
            actionsStack.Children.Add(CreateSpacer(10));

            // Sort Menu button
            _sortBtnText = CreateActionLink("Recent ⌵", ShowSortMenu);
            actionsStack.Children.Add(_sortBtnText);
            actionsStack.Children.Add(CreateSpacer(10));

            // Add Files
            actionsStack.Children.Add(CreateActionLink("+ Add", ChooseFilesDialog));
            actionsStack.Children.Add(CreateSpacer(10));

            // Paste button
            actionsStack.Children.Add(CreateActionLink("📋 Paste", () =>
            {
                if (_service.PasteClipboardData()) FlashStatus("Pasted into shelf!");
                else FlashStatus("Clipboard empty");
            }));
            actionsStack.Children.Add(CreateSpacer(10));

            // Copy all
            actionsStack.Children.Add(CreateActionLink("Copy all", () =>
            {
                _service.CopyAllActiveShelfFiles();
                FlashStatus("Copied files to clipboard!");
            }));
            actionsStack.Children.Add(CreateSpacer(10));

            // Clear button (with safety confirmation)
            _clearBtnText = CreateActionLink("Clear", HandleClearClick);
            actionsStack.Children.Add(_clearBtnText);

            DockPanel.SetDock(actionsStack, Dock.Right);
            topBar.Children.Add(actionsStack);

            DockPanel.SetDock(topBar, Dock.Top);
            rightArea.Children.Add(topBar);

            // Large Dashed Drop Zone Container
            _dropZoneGrid = new Grid
            {
                Margin = new Thickness(8, 0, 12, 10),
                AllowDrop = true
            };

            _dropZoneBg = new Rectangle
            {
                Fill = CapsuleTheme.Brush(p.Surface),
                Stroke = CapsuleTheme.Brush(p.Border),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                RadiusX = 10,
                RadiusY = 10
            };
            _dropZoneGrid.Children.Add(_dropZoneBg);

            // Drag and Drop events
            _dropZoneGrid.DragEnter += OnDragEnter;
            _dropZoneGrid.DragOver += OnDragOver;
            _dropZoneGrid.DragLeave += OnDragLeave;
            _dropZoneGrid.Drop += OnDrop;

            // Empty State
            _emptyStatePanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var docIcon = new TextBlock
            {
                Text = "📥",
                FontSize = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _emptyStatePanel.Children.Add(docIcon);

            var emptyMsg = new TextBlock
            {
                Text = LocalizationService.T("shelf.empty_desc", "Nothing on Desk yet · drop files or paste with Ctrl+V"),
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _emptyStatePanel.Children.Add(emptyMsg);

            var chooseFilesBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var chooseFilesText = new TextBlock
            {
                Text = LocalizationService.T("shelf.choose_files", "Browse Files..."),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            chooseFilesBtn.Child = chooseFilesText;
            chooseFilesBtn.MouseEnter += (_, _) => chooseFilesBtn.Background = CapsuleTheme.Brush(p.Surface3);
            chooseFilesBtn.MouseLeave += (_, _) => chooseFilesBtn.Background = CapsuleTheme.Brush(p.Surface2);
            chooseFilesBtn.MouseLeftButtonUp += (_, _) => ChooseFilesDialog();
            _emptyStatePanel.Children.Add(chooseFilesBtn);

            _dropZoneGrid.Children.Add(_emptyStatePanel);

            // Populated State Host (ScrollViewer with WrapPanel or StackPanel)
            _filesWrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(6)
            };
            _filesListStack = new StackPanel
            {
                Margin = new Thickness(6)
            };

            _filesScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _filesWrapPanel,
                Visibility = Visibility.Collapsed
            };
            _dropZoneGrid.Children.Add(_filesScrollViewer);

            rightArea.Children.Add(_dropZoneGrid);

            Grid.SetColumn(rightArea, 2);
            Children.Add(rightArea);

            _clearConfirmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _clearConfirmTimer.Tick += (_, _) =>
            {
                _clearConfirmTimer.Stop();
                _isClearPending = false;
                _clearBtnText.Text = "Clear";
                _clearBtnText.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
            };
        }

        private TextBlock CreateActionLink(string text, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.MouseEnter += (_, _) => tb.Foreground = CapsuleTheme.Brush(p.Text);
            tb.MouseLeave += (_, _) =>
            {
                if (!_isClearPending || tb != _clearBtnText)
                {
                    tb.Foreground = CapsuleTheme.Brush(p.Text2);
                }
            };
            tb.MouseLeftButtonUp += (_, _) => onClick?.Invoke();
            return tb;
        }

        private static FrameworkElement CreateSpacer(double width) => new FrameworkElement { Width = width };

        private void HandleClearClick()
        {
            if (_service.ActiveShelf == null || _service.ActiveShelf.Files.Count == 0) return;

            if (!_isClearPending)
            {
                _isClearPending = true;
                _clearBtnText.Text = "Confirm?";
                _clearBtnText.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
                _clearConfirmTimer.Stop();
                _clearConfirmTimer.Start();
            }
            else
            {
                _clearConfirmTimer.Stop();
                _isClearPending = false;
                _clearBtnText.Text = "Clear";
                _clearBtnText.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
                _service.ClearActiveShelf();
                FlashStatus("Shelf cleared");
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_service.PasteClipboardData()) FlashStatus("Pasted into shelf!");
                e.Handled = true;
            }
        }

        private void ChooseFilesDialog()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Multiselect = true,
                    Title = "Park files on Shelf"
                };
                if (WindowsBuddy.BuddyCapsule.Core.NotchInteractionGuard.RunModal(() => dlg.ShowDialog()) == true)
                {
                    _service.AddFilesToActiveShelf(dlg.FileNames);
                    FlashStatus($"Added {dlg.FileNames.Length} files");
                }
            }
            catch { }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            var p = CapsuleTheme.Current;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                _dropZoneBg.Stroke = CapsuleTheme.Brush(p.Accent);
                _dropZoneBg.Fill = CapsuleTheme.Brush(p.Surface2);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            var p = CapsuleTheme.Current;
            _dropZoneBg.Stroke = CapsuleTheme.Brush(p.Border);
            _dropZoneBg.Fill = CapsuleTheme.Brush(p.Surface);
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var p = CapsuleTheme.Current;
            _dropZoneBg.Stroke = CapsuleTheme.Brush(p.Border);
            _dropZoneBg.Fill = CapsuleTheme.Brush(p.Surface);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    _service.AddFilesToActiveShelf(files);
                    FlashStatus($"Added {files.Length} files");
                }
            }
            e.Handled = true;
        }

        private void OnShelfDataChanged()
        {
            Dispatcher.Invoke(RefreshUI);
        }

        private void CreateNewShelfWithPrompt()
        {
            int num = _service.Shelves.Count + 1;
            string newName = $"Shelf {num}";
            _service.CreateShelf(newName);
            _renamingShelfId = _service.ActiveShelf?.Id;
            RefreshUI();
        }

        private void RefreshUI()
        {
            var p = CapsuleTheme.Current;

            // 1. Rebuild shelves sidebar list
            _shelvesListPanel.Children.Clear();
            var active = _service.ActiveShelf;

            foreach (var shelf in _service.Shelves)
            {
                var capturedShelf = shelf;
                bool isActive = capturedShelf == active;
                bool isRenaming = capturedShelf.Id == _renamingShelfId;

                var pill = new Border
                {
                    Height = 34,
                    CornerRadius = new CornerRadius(6),
                    Background = isActive ? CapsuleTheme.Brush(p.Surface) : Brushes.Transparent,
                    BorderBrush = isActive ? CapsuleTheme.Brush(p.Border) : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 1.5, 0, 1.5)
                };

                var dock = new DockPanel { Margin = new Thickness(8, 0, 8, 0) };

                // Right side: delete button (on hover) + count badge
                var rightIcons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                if (_service.Shelves.Count > 1)
                {
                    var delShelfBtn = new TextBlock
                    {
                        Text = "✕",
                        FontSize = 9.5,
                        Foreground = CapsuleTheme.Brush(p.Text2),
                        Margin = new Thickness(0, 0, 6, 0),
                        Cursor = Cursors.Hand,
                        Visibility = Visibility.Collapsed,
                        ToolTip = "Delete shelf"
                    };
                    delShelfBtn.MouseEnter += (_, _) => delShelfBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
                    delShelfBtn.MouseLeave += (_, _) => delShelfBtn.Foreground = CapsuleTheme.Brush(p.Text2);
                    delShelfBtn.MouseLeftButtonUp += (_, e) =>
                    {
                        e.Handled = true;
                        _service.DeleteShelf(capturedShelf.Id);
                    };
                    rightIcons.Children.Add(delShelfBtn);

                    pill.MouseEnter += (_, _) => delShelfBtn.Visibility = Visibility.Visible;
                    pill.MouseLeave += (_, _) => delShelfBtn.Visibility = Visibility.Collapsed;
                }

                var countBadge = new TextBlock
                {
                    Text = capturedShelf.Files.Count.ToString(),
                    FontSize = 11,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                rightIcons.Children.Add(countBadge);

                DockPanel.SetDock(rightIcons, Dock.Right);
                dock.Children.Add(rightIcons);

                // Left: folder glyph
                var icon = new TextBlock
                {
                    Text = "📁",
                    FontSize = 12,
                    Foreground = isActive ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Text),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(icon, Dock.Left);
                dock.Children.Add(icon);

                // Center: shelf name (or TextBox if renaming)
                if (isRenaming)
                {
                    var renameBox = new TextBox
                    {
                        Text = capturedShelf.Name,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = CapsuleTheme.Brush(p.Text),
                        CaretBrush = CapsuleTheme.Brush(p.Text),
                        Background = CapsuleTheme.Brush(p.Surface2),
                        BorderThickness = new Thickness(0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    renameBox.Loaded += (_, _) =>
                    {
                        renameBox.Focus();
                        renameBox.SelectAll();
                    };
                    void CommitRename()
                    {
                        if (_renamingShelfId == capturedShelf.Id)
                        {
                            _renamingShelfId = null;
                            if (!string.IsNullOrWhiteSpace(renameBox.Text))
                            {
                                _service.RenameShelf(capturedShelf.Id, renameBox.Text.Trim());
                            }
                            else
                            {
                                RefreshUI();
                            }
                        }
                    }
                    renameBox.KeyDown += (_, ke) =>
                    {
                        if (ke.Key == Key.Enter) { ke.Handled = true; CommitRename(); }
                        else if (ke.Key == Key.Escape) { ke.Handled = true; _renamingShelfId = null; RefreshUI(); }
                    };
                    renameBox.LostFocus += (_, _) => CommitRename();
                    dock.Children.Add(renameBox);
                }
                else
                {
                    var nameText = new TextBlock
                    {
                        Text = capturedShelf.Name,
                        FontSize = 12,
                        FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isActive ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    dock.Children.Add(nameText);
                }

                pill.Child = dock;

                string sid = capturedShelf.Id;
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    if (!isRenaming) _service.SetActiveShelf(sid);
                };

                // Context Menu for Shelf
                var shelfMenu = new ContextMenu();
                var miRename = new MenuItem { Header = "Rename Shelf", Icon = new TextBlock { Text = "✏️" } };
                miRename.Click += (_, _) =>
                {
                    _renamingShelfId = capturedShelf.Id;
                    RefreshUI();
                };
                shelfMenu.Items.Add(miRename);

                var miCopyAll = new MenuItem { Header = "Copy All Files", Icon = new TextBlock { Text = "📋" } };
                miCopyAll.Click += (_, _) =>
                {
                    _service.SetActiveShelf(capturedShelf.Id);
                    _service.CopyAllActiveShelfFiles();
                    FlashStatus("Copied files to clipboard!");
                };
                shelfMenu.Items.Add(miCopyAll);

                var miClear = new MenuItem { Header = "Clear Shelf", Icon = new TextBlock { Text = "🧹" } };
                miClear.Click += (_, _) =>
                {
                    _service.SetActiveShelf(capturedShelf.Id);
                    _service.ClearActiveShelf();
                    FlashStatus("Shelf cleared");
                };
                shelfMenu.Items.Add(miClear);

                if (_service.Shelves.Count > 1)
                {
                    shelfMenu.Items.Add(new Separator());
                    var miDel = new MenuItem { Header = "Delete Shelf", Icon = new TextBlock { Text = "🗑️" } };
                    miDel.Click += (_, _) => _service.DeleteShelf(capturedShelf.Id);
                    shelfMenu.Items.Add(miDel);
                }
                pill.ContextMenu = shelfMenu;

                if (!isActive)
                {
                    pill.MouseEnter += (_, _) => pill.Background = CapsuleTheme.Brush(p.Surface2);
                    pill.MouseLeave += (_, _) => pill.Background = Brushes.Transparent;
                }

                _shelvesListPanel.Children.Add(pill);
            }

            // 2. Update Top Bar title & status
            if (active != null)
            {
                _shelfTitleText.Text = active.Name;
                if (active.Files.Count == 0)
                {
                    _shelfStatusText.Text = LocalizationService.T("shelf.empty", "Empty");
                }
                else
                {
                    long totalBytes = active.Files.Sum(f => f.FileSize);
                    _shelfStatusText.Text = string.Format(LocalizationService.T("shelf.items_fmt", "{0} items · {1}"), active.Files.Count, ParkedFile.FormatBytes(totalBytes));
                }

                _sortBtnText.Text = active.SortMode switch
                {
                    ShelfSort.Name => "Name ⌵",
                    ShelfSort.Size => "Size ⌵",
                    ShelfSort.Type => "Type ⌵",
                    _ => "Recent ⌵"
                };

                // Check missing files
                int missingCount = active.Files.Count(f => !File.Exists(f.FilePath) && !Directory.Exists(f.FilePath));
                if (missingCount > 0)
                {
                    _cleanMissingText.Text = $"⚠️ Clean {missingCount} missing";
                    _cleanMissingBtn.Visibility = Visibility.Visible;
                }
                else
                {
                    _cleanMissingBtn.Visibility = Visibility.Collapsed;
                }
            }

            // 3. Update Drop Zone Content (Empty vs Populated)
            RefreshFilesContent();
        }

        private void RefreshFilesContent()
        {
            var sortedFiles = _service.GetSortedFiles();

            if (!string.IsNullOrWhiteSpace(_filterQuery))
            {
                sortedFiles = sortedFiles.Where(f =>
                    f.FileName.Contains(_filterQuery, StringComparison.OrdinalIgnoreCase) ||
                    f.Extension.Contains(_filterQuery, StringComparison.OrdinalIgnoreCase));
            }

            var fileList = sortedFiles.ToList();

            if (_service.ActiveShelf?.Files.Count == 0)
            {
                _emptyStatePanel.Visibility = Visibility.Visible;
                _filesScrollViewer.Visibility = Visibility.Collapsed;
                return;
            }

            _emptyStatePanel.Visibility = Visibility.Collapsed;
            _filesScrollViewer.Visibility = Visibility.Visible;

            if (_isListView)
            {
                _filesListStack.Children.Clear();
                _filesScrollViewer.Content = _filesListStack;
                foreach (var file in fileList)
                {
                    _filesListStack.Children.Add(CreateFileListRow(file));
                }
            }
            else
            {
                _filesWrapPanel.Children.Clear();
                _filesScrollViewer.Content = _filesWrapPanel;
                foreach (var file in fileList)
                {
                    _filesWrapPanel.Children.Add(CreateFileGridTile(file));
                }
            }
        }

        private UIElement CreateFileGridTile(ParkedFile file)
        {
            var p = CapsuleTheme.Current;
            bool exists = File.Exists(file.FilePath) || Directory.Exists(file.FilePath);

            var card = new Border
            {
                Width = 114,
                Height = 110,
                CornerRadius = new CornerRadius(7),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = exists ? CapsuleTheme.Brush(p.Border) : new SolidColorBrush(Color.FromArgb(120, 255, 69, 58)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                Opacity = exists ? 1.0 : 0.65
            };

            var grid = new Grid { Margin = new Thickness(6) };

            // Close / Remove button (top right)
            var closeBtn = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = CapsuleTheme.Brush(p.Surface2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand,
                ToolTip = "Remove from shelf"
            };
            var closeText = new TextBlock
            {
                Text = "✕",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Child = closeText;
            closeBtn.MouseEnter += (_, _) => closeText.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
            closeBtn.MouseLeave += (_, _) => closeText.Foreground = CapsuleTheme.Brush(p.Text2);
            closeBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _service.RemoveFile(file.Id);
            };
            grid.Children.Add(closeBtn);

            // Center Content
            var contentStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var thumb = _service.GetThumbnailOrIcon(file.FilePath);
            if (thumb != null)
            {
                var img = new Image
                {
                    Source = thumb,
                    Width = 38,
                    Height = 38,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                contentStack.Children.Add(img);
            }
            else
            {
                var glyph = new TextBlock
                {
                    Text = GetDefaultGlyph(file.Extension),
                    FontSize = 26,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                contentStack.Children.Add(glyph);
            }

            var nameText = new TextBlock
            {
                Text = file.FileName,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = CapsuleTheme.Brush(p.Text),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                MaxHeight = 30,
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = file.FilePath
            };
            contentStack.Children.Add(nameText);

            string badgeStr = exists ? file.DisplaySize : "Missing file";
            var sizeText = new TextBlock
            {
                Text = badgeStr,
                FontSize = 9.5,
                Foreground = exists ? CapsuleTheme.Brush(p.Text2) : new SolidColorBrush(Color.FromRgb(255, 69, 58)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            contentStack.Children.Add(sizeText);

            grid.Children.Add(contentStack);
            card.Child = grid;

            // Hover effects
            card.MouseEnter += (_, _) =>
            {
                card.Background = CapsuleTheme.Brush(p.Surface2);
                closeBtn.Visibility = Visibility.Visible;
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = CapsuleTheme.Brush(p.Surface);
                closeBtn.Visibility = Visibility.Collapsed;
            };

            // Double Click to Launch
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    LaunchFile(file.FilePath);
                }
                else
                {
                    _dragStartPoint = e.GetPosition(this);
                    _isDraggingTile = true;
                }
            };

            // Drag-out to external windows/apps
            card.PreviewMouseMove += (s, e) =>
            {
                if (_isDraggingTile && e.LeftButton == MouseButtonState.Pressed)
                {
                    var currentPoint = e.GetPosition(this);
                    var diff = _dragStartPoint - currentPoint;
                    if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
                    {
                        _isDraggingTile = false;
                        if (File.Exists(file.FilePath) || Directory.Exists(file.FilePath))
                        {
                            var data = new DataObject(DataFormats.FileDrop, new[] { file.FilePath });
                            DragDrop.DoDragDrop(card, data, DragDropEffects.Copy | DragDropEffects.Move);
                        }
                    }
                }
            };
            card.PreviewMouseLeftButtonUp += (_, _) => _isDraggingTile = false;

            card.ContextMenu = BuildFileContextMenu(file);
            return card;
        }

        private UIElement CreateFileListRow(ParkedFile file)
        {
            var p = CapsuleTheme.Current;
            bool exists = File.Exists(file.FilePath) || Directory.Exists(file.FilePath);

            var row = new Border
            {
                Height = 32,
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = exists ? CapsuleTheme.Brush(p.Border) : new SolidColorBrush(Color.FromArgb(120, 255, 69, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 1.5, 0, 1.5),
                Cursor = Cursors.Hand,
                Opacity = exists ? 1.0 : 0.65
            };

            var dock = new DockPanel();

            // Remove button on right
            var closeBtn = new TextBlock
            {
                Text = "✕",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = "Remove from shelf"
            };
            closeBtn.MouseEnter += (_, _) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
            closeBtn.MouseLeave += (_, _) => closeBtn.Foreground = CapsuleTheme.Brush(p.Text2);
            closeBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _service.RemoveFile(file.Id);
            };
            DockPanel.SetDock(closeBtn, Dock.Right);
            dock.Children.Add(closeBtn);

            // Size on right
            var sizeText = new TextBlock
            {
                Text = exists ? file.DisplaySize : "Missing",
                FontSize = 10.5,
                Foreground = exists ? CapsuleTheme.Brush(p.Text2) : new SolidColorBrush(Color.FromRgb(255, 69, 58)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            DockPanel.SetDock(sizeText, Dock.Right);
            dock.Children.Add(sizeText);

            // Icon on left
            var thumb = _service.GetThumbnailOrIcon(file.FilePath);
            UIElement iconElem;
            if (thumb != null)
            {
                iconElem = new Image
                {
                    Source = thumb,
                    Width = 18,
                    Height = 18,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                iconElem = new TextBlock
                {
                    Text = GetDefaultGlyph(file.Extension),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            DockPanel.SetDock(iconElem, Dock.Left);
            dock.Children.Add(iconElem);

            // Center Name
            var nameText = new TextBlock
            {
                Text = file.FileName,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = CapsuleTheme.Brush(p.Text),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = file.FilePath
            };
            dock.Children.Add(nameText);

            row.Child = dock;

            row.MouseEnter += (_, _) => row.Background = CapsuleTheme.Brush(p.Surface2);
            row.MouseLeave += (_, _) => row.Background = CapsuleTheme.Brush(p.Surface);

            row.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    LaunchFile(file.FilePath);
                }
                else
                {
                    _dragStartPoint = e.GetPosition(this);
                    _isDraggingTile = true;
                }
            };
            row.PreviewMouseMove += (s, e) =>
            {
                if (_isDraggingTile && e.LeftButton == MouseButtonState.Pressed)
                {
                    var currentPoint = e.GetPosition(this);
                    var diff = _dragStartPoint - currentPoint;
                    if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
                    {
                        _isDraggingTile = false;
                        if (File.Exists(file.FilePath) || Directory.Exists(file.FilePath))
                        {
                            var data = new DataObject(DataFormats.FileDrop, new[] { file.FilePath });
                            DragDrop.DoDragDrop(row, data, DragDropEffects.Copy | DragDropEffects.Move);
                        }
                    }
                }
            };
            row.PreviewMouseLeftButtonUp += (_, _) => _isDraggingTile = false;

            row.ContextMenu = BuildFileContextMenu(file);
            return row;
        }

        private ContextMenu BuildFileContextMenu(ParkedFile file)
        {
            var cm = new ContextMenu();

            var miOpen = new MenuItem { Header = "Open", Icon = new TextBlock { Text = "▶" } };
            miOpen.Click += (_, _) => LaunchFile(file.FilePath);
            cm.Items.Add(miOpen);

            var miExplore = new MenuItem { Header = "Reveal in File Explorer", Icon = new TextBlock { Text = "📂" } };
            miExplore.Click += (_, _) => RevealInExplorer(file.FilePath);
            cm.Items.Add(miExplore);

            var miCopy = new MenuItem { Header = "Copy File", Icon = new TextBlock { Text = "📋" } };
            miCopy.Click += (_, _) =>
            {
                var col = new System.Collections.Specialized.StringCollection { file.FilePath };
                System.Windows.Clipboard.SetFileDropList(col);
                FlashStatus("File copied!");
            };
            cm.Items.Add(miCopy);

            var miCopyPath = new MenuItem { Header = "Copy Path", Icon = new TextBlock { Text = "📑" } };
            miCopyPath.Click += (_, _) =>
            {
                System.Windows.Clipboard.SetText(file.FilePath);
                FlashStatus("Path copied!");
            };
            cm.Items.Add(miCopyPath);

            // Move to another shelf submenu
            if (_service.Shelves.Count > 1)
            {
                var miMove = new MenuItem { Header = "Move to Shelf", Icon = new TextBlock { Text = "🔀" } };
                foreach (var otherShelf in _service.Shelves)
                {
                    if (otherShelf != _service.ActiveShelf)
                    {
                        string targetId = otherShelf.Id;
                        var shelfItem = new MenuItem { Header = otherShelf.Name };
                        shelfItem.Click += (_, _) =>
                        {
                            _service.MoveFileToShelf(file.Id, targetId);
                            FlashStatus($"Moved to {otherShelf.Name}");
                        };
                        miMove.Items.Add(shelfItem);
                    }
                }
                cm.Items.Add(miMove);
            }

            cm.Items.Add(new Separator());

            var miRemove = new MenuItem { Header = "Remove from Shelf", Icon = new TextBlock { Text = "✕" } };
            miRemove.Click += (_, _) => _service.RemoveFile(file.Id);
            cm.Items.Add(miRemove);

            return cm;
        }

        private static string GetDefaultGlyph(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return "📄";
            ext = ext.ToLowerInvariant();
            if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz") return "🗜";
            if (ext == ".pdf") return "📑";
            if (ext == ".doc" || ext == ".docx" || ext == ".txt" || ext == ".rtf" || ext == ".md") return "📝";
            if (ext == ".xls" || ext == ".xlsx" || ext == ".csv") return "📊";
            if (ext == ".ppt" || ext == ".pptx") return "📈";
            if (ext == ".mp3" || ext == ".wav" || ext == ".flac") return "🎵";
            if (ext == ".mp4" || ext == ".mkv" || ext == ".mov" || ext == ".avi") return "🎬";
            if (ext == "[dir]") return "📁";
            return "📄";
        }

        private void LaunchFile(string path)
        {
            try
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void RevealInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void ShowSortMenu()
        {
            var cm = new ContextMenu();
            var active = _service.ActiveShelf;
            var currentSort = active?.SortMode ?? ShelfSort.Recent;

            AddSortMenuItem(cm, "Recent", ShelfSort.Recent, currentSort);
            AddSortMenuItem(cm, "Name", ShelfSort.Name, currentSort);
            AddSortMenuItem(cm, "Size", ShelfSort.Size, currentSort);
            AddSortMenuItem(cm, "Type", ShelfSort.Type, currentSort);

            cm.PlacementTarget = _sortBtnText;
            cm.IsOpen = true;
        }

        private void AddSortMenuItem(ContextMenu cm, string label, ShelfSort sort, ShelfSort current)
        {
            var mi = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = sort == current
            };
            mi.Click += (_, _) => _service.SetSortMode(sort);
            cm.Items.Add(mi);
        }

        private void FlashStatus(string msg)
        {
            _shelfStatusText.Text = msg;
            _shelfStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 149, 0));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var active = _service.ActiveShelf;
                if (active != null)
                {
                    if (active.Files.Count == 0) _shelfStatusText.Text = LocalizationService.T("shelf.empty", "Empty");
                    else
                    {
                        long totalBytes = active.Files.Sum(f => f.FileSize);
                        _shelfStatusText.Text = string.Format(LocalizationService.T("shelf.items_fmt", "{0} items · {1}"), active.Files.Count, ParkedFile.FormatBytes(totalBytes));
                    }
                    _shelfStatusText.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
                }
            };
            timer.Start();
        }
    }
}
