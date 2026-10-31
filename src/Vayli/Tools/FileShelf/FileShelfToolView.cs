using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.Tools.FileShelf
{
    public sealed class FileShelfToolView : Grid
    {
        private readonly FileShelfService _service = FileShelfService.Instance;

        private StackPanel _shelvesListPanel;
        private TextBlock _shelfTitleText;
        private TextBlock _shelfStatusText;
        private TextBlock _sortBtnText;
        private Grid _dropZoneGrid;
        private Rectangle _dropZoneBg;
        private StackPanel _emptyStatePanel;
        private ScrollViewer _filesScrollViewer;
        private WrapPanel _filesWrapPanel;
        private Point _dragStartPoint;
        private bool _isDraggingTile;

        public FileShelfToolView()
        {
            Width = 728;
            Height = 242;
            Focusable = true;

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
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. Left Sidebar: SHELVES
            var sidebar = new Grid { Margin = new Thickness(0) };
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Shelves List
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // New Shelf

            var header = new TextBlock
            {
                Text = "SHELVES",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(14, 12, 0, 8)
            };
            Grid.SetRow(header, 0);
            sidebar.Children.Add(header);

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
                Background = Brushes.Transparent,
                Padding = new Thickness(8, 6, 8, 8),
                Margin = new Thickness(6, 4, 8, 10),
                CornerRadius = new CornerRadius(6)
            };
            var newShelfText = new TextBlock
            {
                Text = "+ New shelf",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            newShelfBtn.Child = newShelfText;
            newShelfBtn.MouseEnter += (_, _) =>
            {
                newShelfText.Foreground = Brushes.White;
                newShelfBtn.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            };
            newShelfBtn.MouseLeave += (_, _) =>
            {
                newShelfText.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                newShelfBtn.Background = Brushes.Transparent;
            };
            newShelfBtn.MouseLeftButtonUp += (_, _) => PromptNewShelf();

            Grid.SetRow(newShelfBtn, 2);
            sidebar.Children.Add(newShelfBtn);

            Grid.SetColumn(sidebar, 0);
            Children.Add(sidebar);

            // 2. Vertical Divider
            var divider = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(divider, 1);
            Children.Add(divider);

            // 3. Right Content Area: Header & Drop Zone
            var rightArea = new DockPanel { Margin = new Thickness(0) };

            // Top action bar
            var topBar = new DockPanel { Margin = new Thickness(14, 8, 14, 8), LastChildFill = false };

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _shelfTitleText = new TextBlock
            {
                Text = "Desk",
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            titleStack.Children.Add(_shelfTitleText);

            _shelfStatusText = new TextBlock
            {
                Text = "Empty",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(8, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(_shelfStatusText);
            DockPanel.SetDock(titleStack, Dock.Left);
            topBar.Children.Add(titleStack);

            var actionsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Sort button
            _sortBtnText = CreateActionLink("Recent ⌵", () => ShowSortMenu());
            actionsStack.Children.Add(_sortBtnText);
            actionsStack.Children.Add(CreateSpacer(14));

            // Select
            actionsStack.Children.Add(CreateActionLink("Select", () => ToggleSelectMode()));
            actionsStack.Children.Add(CreateSpacer(14));

            // Copy all
            actionsStack.Children.Add(CreateActionLink("Copy all", () =>
            {
                _service.CopyAllActiveShelfFiles();
                FlashStatus("Copied files to clipboard!");
            }));
            actionsStack.Children.Add(CreateSpacer(14));

            // Clear
            actionsStack.Children.Add(CreateActionLink("Clear", () =>
            {
                if (_service.ActiveShelf?.Files.Count > 0)
                {
                    _service.ClearActiveShelf();
                }
            }));

            DockPanel.SetDock(actionsStack, Dock.Right);
            topBar.Children.Add(actionsStack);

            DockPanel.SetDock(topBar, Dock.Top);
            rightArea.Children.Add(topBar);

            // Large Dashed Drop Zone Container
            _dropZoneGrid = new Grid
            {
                Margin = new Thickness(10, 0, 14, 12),
                AllowDrop = true
            };

            _dropZoneBg = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromRgb(12, 12, 15)),
                Stroke = new SolidColorBrush(Color.FromRgb(44, 44, 52)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                RadiusX = 12,
                RadiusY = 12
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
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var docIcon = new TextBlock
            {
                Text = "📄",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _emptyStatePanel.Children.Add(docIcon);

            var emptyMsg = new TextBlock
            {
                Text = "Nothing on Desk yet · drop files or paste with Ctrl+V",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            _emptyStatePanel.Children.Add(emptyMsg);

            var sep = new TextBlock
            {
                Text = "|",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 58)),
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _emptyStatePanel.Children.Add(sep);

            var chooseFilesBtn = new TextBlock
            {
                Text = "Choose files",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            chooseFilesBtn.MouseEnter += (_, _) => chooseFilesBtn.TextDecorations = TextDecorations.Underline;
            chooseFilesBtn.MouseLeave += (_, _) => chooseFilesBtn.TextDecorations = null;
            chooseFilesBtn.MouseLeftButtonUp += (_, _) => ChooseFilesDialog();
            _emptyStatePanel.Children.Add(chooseFilesBtn);

            _dropZoneGrid.Children.Add(_emptyStatePanel);

            // Populated State (ScrollViewer with WrapPanel)
            _filesWrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8)
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
        }

        private TextBlock CreateActionLink(string text, Action onClick)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.MouseEnter += (_, _) => tb.Foreground = Brushes.White;
            tb.MouseLeave += (_, _) => tb.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
            tb.MouseLeftButtonUp += (_, _) => onClick?.Invoke();
            return tb;
        }

        private FrameworkElement CreateSpacer(double width) => new FrameworkElement { Width = width };

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                PasteFilesFromClipboard();
                e.Handled = true;
            }
        }

        private void PasteFilesFromClipboard()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var fileList = System.Windows.Clipboard.GetFileDropList();
                    if (fileList != null && fileList.Count > 0)
                    {
                        var paths = fileList.Cast<string>().ToList();
                        _service.AddFilesToActiveShelf(paths);
                    }
                }
                else if (System.Windows.Clipboard.ContainsText())
                {
                    string txt = System.Windows.Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrEmpty(txt) && (File.Exists(txt) || Directory.Exists(txt)))
                    {
                        _service.AddFilesToActiveShelf(new[] { txt });
                    }
                }
            }
            catch { }
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
                if (dlg.ShowDialog() == true)
                {
                    _service.AddFilesToActiveShelf(dlg.FileNames);
                }
            }
            catch { }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                _dropZoneBg.Stroke = new SolidColorBrush(Color.FromRgb(255, 85, 0));
                _dropZoneBg.Fill = new SolidColorBrush(Color.FromRgb(22, 22, 28));
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
            _dropZoneBg.Stroke = new SolidColorBrush(Color.FromRgb(44, 44, 52));
            _dropZoneBg.Fill = new SolidColorBrush(Color.FromRgb(12, 12, 15));
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            _dropZoneBg.Stroke = new SolidColorBrush(Color.FromRgb(44, 44, 52));
            _dropZoneBg.Fill = new SolidColorBrush(Color.FromRgb(12, 12, 15));

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    _service.AddFilesToActiveShelf(files);
                }
            }
            e.Handled = true;
        }

        private void OnShelfDataChanged()
        {
            Dispatcher.Invoke(RefreshUI);
        }

        private void RefreshUI()
        {
            // 1. Rebuild shelves sidebar list
            _shelvesListPanel.Children.Clear();
            var active = _service.ActiveShelf;

            foreach (var shelf in _service.Shelves)
            {
                bool isActive = shelf == active;
                var pill = new Border
                {
                    Height = 32,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(isActive ? Color.FromRgb(36, 36, 42) : Colors.Transparent),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 2, 0, 2)
                };

                var dock = new DockPanel { Margin = new Thickness(10, 0, 10, 0) };

                // Right: count badge
                var countBadge = new TextBlock
                {
                    Text = shelf.Files.Count.ToString(),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(countBadge, Dock.Right);
                dock.Children.Add(countBadge);

                // Left: folder glyph
                var icon = new TextBlock
                {
                    Text = "📁",
                    FontSize = 12.5,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(icon, Dock.Left);
                dock.Children.Add(icon);

                // Center: shelf name
                var nameText = new TextBlock
                {
                    Text = shelf.Name,
                    FontSize = 13,
                    FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dock.Children.Add(nameText);

                pill.Child = dock;

                string sid = shelf.Id;
                pill.MouseLeftButtonUp += (_, _) => _service.SetActiveShelf(sid);

                if (!isActive)
                {
                    pill.MouseEnter += (_, _) => pill.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
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
                    _shelfStatusText.Text = "Empty";
                }
                else
                {
                    long totalBytes = active.Files.Sum(f => f.FileSize);
                    _shelfStatusText.Text = $"{active.Files.Count} items · {ParkedFile.FormatBytes(totalBytes)}";
                }

                _sortBtnText.Text = active.SortMode switch
                {
                    ShelfSort.Name => "Name ⌵",
                    ShelfSort.Size => "Size ⌵",
                    ShelfSort.Type => "Type ⌵",
                    _ => "Recent ⌵"
                };
            }

            // 3. Update Drop Zone Content (Empty vs Populated)
            var sortedFiles = _service.GetSortedFiles().ToList();
            if (sortedFiles.Count == 0)
            {
                _emptyStatePanel.Visibility = Visibility.Visible;
                _filesScrollViewer.Visibility = Visibility.Collapsed;
            }
            else
            {
                _emptyStatePanel.Visibility = Visibility.Collapsed;
                _filesScrollViewer.Visibility = Visibility.Visible;

                _filesWrapPanel.Children.Clear();
                foreach (var file in sortedFiles)
                {
                    _filesWrapPanel.Children.Add(CreateFileTile(file));
                }
            }
        }

        private UIElement CreateFileTile(ParkedFile file)
        {
            var card = new Border
            {
                Width = 114,
                Height = 108,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(23, 23, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { Margin = new Thickness(6) };

            // Close / Remove button (top right)
            var closeBtn = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand
            };
            var closeText = new TextBlock
            {
                Text = "✕",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Child = closeText;
            closeBtn.MouseEnter += (_, _) => closeText.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80));
            closeBtn.MouseLeave += (_, _) => closeText.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200));
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
                    Width = 40,
                    Height = 40,
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
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                contentStack.Children.Add(glyph);
            }

            var nameText = new TextBlock
            {
                Text = file.FileName,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                MaxHeight = 28,
                Margin = new Thickness(0, 5, 0, 0),
                ToolTip = file.FilePath
            };
            contentStack.Children.Add(nameText);

            var sizeText = new TextBlock
            {
                Text = file.DisplaySize,
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            contentStack.Children.Add(sizeText);

            grid.Children.Add(contentStack);
            card.Child = grid;

            // Hover effects
            card.MouseEnter += (_, _) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(34, 34, 42));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 76));
                closeBtn.Visibility = Visibility.Visible;
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(23, 23, 28));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48));
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

            // Drag-out to external windows/apps!
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

            // Context Menu
            var cm = new ContextMenu();
            var miOpen = new MenuItem { Header = "Open" };
            miOpen.Click += (_, _) => LaunchFile(file.FilePath);
            cm.Items.Add(miOpen);

            var miExplore = new MenuItem { Header = "Reveal in File Explorer" };
            miExplore.Click += (_, _) => RevealInExplorer(file.FilePath);
            cm.Items.Add(miExplore);

            var miCopy = new MenuItem { Header = "Copy File" };
            miCopy.Click += (_, _) =>
            {
                var col = new System.Collections.Specialized.StringCollection { file.FilePath };
                System.Windows.Clipboard.SetFileDropList(col);
            };
            cm.Items.Add(miCopy);

            cm.Items.Add(new Separator());

            var miRemove = new MenuItem { Header = "Remove from Shelf" };
            miRemove.Click += (_, _) => _service.RemoveFile(file.Id);
            cm.Items.Add(miRemove);

            card.ContextMenu = cm;

            return card;
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

        private void PromptNewShelf()
        {
            int num = _service.Shelves.Count + 1;
            _service.CreateShelf($"Shelf {num}");
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

        private void ToggleSelectMode()
        {
            // Select all files to clipboard or highlight
            _service.CopyAllActiveShelfFiles();
            FlashStatus("Selected all files");
        }

        private void FlashStatus(string msg)
        {
            _shelfStatusText.Text = msg;
            _shelfStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0));
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var active = _service.ActiveShelf;
                if (active != null)
                {
                    if (active.Files.Count == 0) _shelfStatusText.Text = "Empty";
                    else
                    {
                        long totalBytes = active.Files.Sum(f => f.FileSize);
                        _shelfStatusText.Text = $"{active.Files.Count} items · {ParkedFile.FormatBytes(totalBytes)}";
                    }
                    _shelfStatusText.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                }
            };
            timer.Start();
        }
    }
}
