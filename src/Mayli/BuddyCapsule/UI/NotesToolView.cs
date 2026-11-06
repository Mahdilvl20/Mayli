using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.Tools.Notes;

namespace WindowsBuddy.BuddyCapsule.UI
{
    public sealed class NotesToolView : UserControl
    {
        public const double GridW = 728.0;
        public const double GridH = 242.0;
        public const double LeftW = 232.0;

        private static readonly (string Name, string Hex)[] NoteColors = new[]
        {
            ("None", ""),
            ("Coral", "#FF5252"),
            ("Amber", "#FF9800"),
            ("Yellow", "#FFD600"),
            ("Mint", "#4CAF50"),
            ("Cyan", "#00BCD4"),
            ("Purple", "#AB47BC")
        };

        private readonly Grid _rootGrid;

        // Left Column Elements
        private readonly TextBox _searchBox;
        private readonly Border _searchBarContainer;
        private readonly TextBlock _searchCountText;
        private readonly StackPanel _notesStackPanel;
        private readonly ScrollViewer _notesScrollViewer;
        private readonly StackPanel _emptyListPanel;

        // Right Column Elements
        private readonly Grid _editorContainer;
        private readonly TextBlock _emptyEditorText;
        private readonly TextBox _titleBox;
        private readonly TextBlock _titlePlaceholder;
        private readonly TextBox _contentBox;
        private readonly TextBlock _contentPlaceholder;
        private readonly TextBlock _editedText;
        private readonly TextBlock _statsText;
        private readonly Border _pinButton;
        private readonly TextBlock _pinIcon;
        private readonly Border _colorTagButton;
        private readonly Ellipse _colorTagDot;
        private readonly Border _copyButton;
        private readonly TextBlock _copyIcon;
        private readonly Border _exportButton;
        private readonly Border _deleteButton;

        private readonly DispatcherTimer _autosaveTimer;
        private readonly DispatcherTimer _copyFeedbackTimer;
        private NoteItem _selectedNote;
        private string _searchQuery = string.Empty;
        private bool _suppressEditorTextChange = false;

        public NotesToolView()
        {
            var p = CapsuleTheme.Current;
            FlowDirection = LocalizationService.CurrentFlowDirection;
            Background = Brushes.Transparent;

            _rootGrid = new Grid();
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LeftW) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) }); // Hairline Divider
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // LEFT COLUMN (Header + Search Bar + Notes List)
            // -------------------------------------------------------------
            var leftGrid = new Grid { Margin = new Thickness(0, 0, 8, 0) };
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (All notes + count + Search & Plus)
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search Bar
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Notes List

            // 1. Left Header: "All notes" + Notes Count on left, Search 🔍 and Plus ➕ on right
            var leftHeader = new DockPanel { Height = 32, Margin = new Thickness(4, 0, 0, 4) };

            var headerIcons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var searchToggleBtn = CreateHeaderIconButton("🔍", "Search notes", () =>
            {
                bool isVisible = _searchBarContainer.Visibility == Visibility.Visible;
                _searchBarContainer.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
                if (!isVisible)
                {
                    _searchBox.Focus();
                }
                else
                {
                    _searchBox.Text = string.Empty;
                }
            });
            headerIcons.Children.Add(searchToggleBtn);

            var addBtn = CreateHeaderIconButton("+", "Create new note", () => CreateNewNote());
            if (addBtn.Child is TextBlock tbAdd)
            {
                tbAdd.FontSize = 16.0;
                tbAdd.FontWeight = FontWeights.Bold;
            }
            headerIcons.Children.Add(addBtn);

            DockPanel.SetDock(headerIcons, Dock.Right);
            leftHeader.Children.Add(headerIcons);

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var allNotesText = new TextBlock
            {
                Text = LocalizationService.T("notes.all_notes", "All notes"),
                FontSize = 13.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(allNotesText);

            _searchCountText = new TextBlock
            {
                Text = "",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(6, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(_searchCountText);
            leftHeader.Children.Add(titleStack);

            Grid.SetRow(leftHeader, 0);
            leftGrid.Children.Add(leftHeader);

            // 2. Search Bar with Clear Button
            _searchBarContainer = new Border
            {
                Visibility = Visibility.Collapsed,
                Height = 28,
                Margin = new Thickness(4, 0, 4, 6),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 0, 6, 0)
            };
            var searchDock = new DockPanel();

            var clearSearchBtn = new TextBlock
            {
                Text = "✕",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            DockPanel.SetDock(clearSearchBtn, Dock.Right);
            searchDock.Children.Add(clearSearchBtn);

            var searchGrid = new Grid();
            var searchHint = new TextBlock
            {
                Text = "Search notes...",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += (_, _) =>
            {
                _searchQuery = _searchBox.Text.Trim();
                searchHint.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                clearSearchBtn.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Collapsed : Visibility.Visible;
                RefreshList();
            };
            clearSearchBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _searchBox.Text = string.Empty;
            };

            searchGrid.Children.Add(searchHint);
            searchGrid.Children.Add(_searchBox);
            searchDock.Children.Add(searchGrid);
            _searchBarContainer.Child = searchDock;

            Grid.SetRow(_searchBarContainer, 1);
            leftGrid.Children.Add(_searchBarContainer);

            // 3. Notes Scrollable List & Empty state container
            var listContainer = new Grid();

            _notesStackPanel = new StackPanel();
            _notesScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _notesStackPanel
            };
            listContainer.Children.Add(_notesScrollViewer);

            // Empty List Panel
            _emptyListPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _emptyListPanel.Children.Add(new TextBlock
            {
                Text = "📝",
                FontSize = 24.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });
            _emptyListPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("notes.no_notes", "No notes yet"),
                FontSize = 12.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });
            var newNoteBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var newNoteText = new TextBlock
            {
                Text = LocalizationService.T("notes.new_note", "+ New note"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            newNoteBtn.Child = newNoteText;
            newNoteBtn.MouseEnter += (_, _) => newNoteBtn.Background = CapsuleTheme.Brush(p.Surface2);
            newNoteBtn.MouseLeave += (_, _) => newNoteBtn.Background = CapsuleTheme.Brush(p.Surface);
            newNoteBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CreateNewNote();
            };
            _emptyListPanel.Children.Add(newNoteBtn);
            listContainer.Children.Add(_emptyListPanel);

            Grid.SetRow(listContainer, 2);
            leftGrid.Children.Add(listContainer);

            Grid.SetColumn(leftGrid, 0);
            _rootGrid.Children.Add(leftGrid);

            // Vertical hairline separator
            var verticalDivider = new Border
            {
                Width = 1,
                Background = CapsuleTheme.Brush(p.Border),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(verticalDivider, 1);
            _rootGrid.Children.Add(verticalDivider);

            // -------------------------------------------------------------
            // RIGHT COLUMN (Editor Pane or Empty Prompt)
            // -------------------------------------------------------------
            var rightGrid = new Grid { Margin = new Thickness(12, 0, 4, 0) };

            // Empty Editor View
            _emptyEditorText = new TextBlock
            {
                Text = LocalizationService.T("notes.empty_prompt", "Select a note or create one to start writing"),
                FontSize = 13.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };
            rightGrid.Children.Add(_emptyEditorText);

            // Active Note Editor Container
            _editorContainer = new Grid { Visibility = Visibility.Collapsed };
            _editorContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (Title + Actions)
            _editorContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Quick Insert Formatting Bar
            _editorContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Editor Text
            _editorContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer (Stats & Time)

            // 1. Editor Header (Title + Pin + Color Tag + Copy + Export + Delete)
            var editorHeader = new DockPanel { Height = 32, Margin = new Thickness(0, 0, 0, 2) };

            var editorActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Pin Button
            _pinIcon = new TextBlock
            {
                Text = "📌",
                FontSize = 11.5,
                Opacity = 0.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pinButton = CreateActionIconButton(_pinIcon, "Pin note", () =>
            {
                if (_selectedNote != null)
                {
                    NotesService.Instance.TogglePin(_selectedNote.Id);
                    UpdatePinVisual();
                }
            });
            editorActions.Children.Add(_pinButton);

            // Color Tag Button
            _colorTagDot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _colorTagButton = CreateActionIconButton(_colorTagDot, "Color Tag", ShowColorTagMenu);
            editorActions.Children.Add(_colorTagButton);

            // Copy Button
            _copyIcon = new TextBlock
            {
                Text = "📋",
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _copyButton = CreateActionIconButton(_copyIcon, "Copy note text", CopyCurrentNote);
            editorActions.Children.Add(_copyButton);

            // Export Button
            var exportIcon = new TextBlock
            {
                Text = "💾",
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _exportButton = CreateActionIconButton(exportIcon, "Export as .txt or .md", ExportCurrentNote);
            editorActions.Children.Add(_exportButton);

            // Delete Button
            var deleteIcon = new TextBlock
            {
                Text = "🗑️",
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _deleteButton = CreateActionIconButton(deleteIcon, "Delete note", DeleteCurrentNote);
            editorActions.Children.Add(_deleteButton);

            DockPanel.SetDock(editorActions, Dock.Right);
            editorHeader.Children.Add(editorActions);

            var titleGrid = new Grid();
            _titlePlaceholder = new TextBlock
            {
                Text = "Title...",
                FontSize = 15.0,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _titleBox = new TextBox
            {
                FontSize = 15.0,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 2, 8, 2)
            };
            _titleBox.TextChanged += (_, _) =>
            {
                _titlePlaceholder.Visibility = string.IsNullOrEmpty(_titleBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                if (_selectedNote != null && !_suppressEditorTextChange)
                {
                    _selectedNote.Title = _titleBox.Text;
                }
                OnNoteTextChanged();
            };
            _titleBox.LostFocus += (_, _) => SaveCurrentNoteData();

            titleGrid.Children.Add(_titlePlaceholder);
            titleGrid.Children.Add(_titleBox);
            editorHeader.Children.Add(titleGrid);

            Grid.SetRow(editorHeader, 0);
            _editorContainer.Children.Add(editorHeader);

            // 2. Quick Insert Formatting Bar
            var formatBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 4)
            };
            formatBar.Children.Add(CreateQuickInsertChip("☑ Checkbox", () => InsertTextAtCursor("- [ ] ")));
            formatBar.Children.Add(CreateQuickInsertChip("• Bullet", () => InsertTextAtCursor("• ")));
            formatBar.Children.Add(CreateQuickInsertChip("🕒 Time", () => InsertTextAtCursor(DateTime.Now.ToString("HH:mm") + " ")));
            formatBar.Children.Add(CreateQuickInsertChip("📅 Date", () => InsertTextAtCursor(DateTime.Now.ToString("yyyy-MM-dd") + " ")));
            formatBar.Children.Add(CreateQuickInsertChip("— Divider", () => InsertTextAtCursor("\n---\n")));

            Grid.SetRow(formatBar, 1);
            _editorContainer.Children.Add(formatBar);

            // 3. Editor Text Body
            var contentGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            _contentPlaceholder = new TextBlock
            {
                Text = "Start writing, checklist, ideas, paste snippets...",
                FontSize = 12.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 4, 0, 0),
                IsHitTestVisible = false
            };
            _contentBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 2, 4, 2)
            };
            _contentBox.TextChanged += (_, _) =>
            {
                _contentPlaceholder.Visibility = string.IsNullOrEmpty(_contentBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                if (_selectedNote != null && !_suppressEditorTextChange)
                {
                    _selectedNote.Content = _contentBox.Text;
                }
                OnNoteTextChanged();
            };
            _contentBox.LostFocus += (_, _) => SaveCurrentNoteData();

            contentGrid.Children.Add(_contentPlaceholder);
            contentGrid.Children.Add(_contentBox);

            Grid.SetRow(contentGrid, 2);
            _editorContainer.Children.Add(contentGrid);

            // 4. Editor Footer
            var editorFooter = new DockPanel { Height = 22, Margin = new Thickness(0, 2, 0, 0) };

            _statsText = new TextBlock
            {
                Text = "0 words · 0 chars",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_statsText, Dock.Right);
            editorFooter.Children.Add(_statsText);

            _editedText = new TextBlock
            {
                Text = "Edited just now",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            editorFooter.Children.Add(_editedText);

            Grid.SetRow(editorFooter, 3);
            _editorContainer.Children.Add(editorFooter);

            rightGrid.Children.Add(_editorContainer);

            Grid.SetColumn(rightGrid, 2);
            _rootGrid.Children.Add(rightGrid);

            Content = _rootGrid;

            // Autosave debounce timer (400ms)
            _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _autosaveTimer.Tick += (_, _) =>
            {
                _autosaveTimer.Stop();
                SaveCurrentNoteData();
            };

            // Copy feedback timer
            _copyFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _copyFeedbackTimer.Tick += (_, _) =>
            {
                _copyFeedbackTimer.Stop();
                _copyIcon.Text = "📋";
            };

            NotesService.Instance.NotesChanged += () => Dispatcher.InvokeAsync(RefreshList);
            RefreshList();
        }

        private static Border CreateHeaderIconButton(string text, string toolTip, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var btn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = toolTip,
                Margin = new Thickness(0, 0, 4, 0),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12.0,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            btn.MouseEnter += (_, _) => btn.Background = CapsuleTheme.Brush(p.Surface2);
            btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
            btn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return btn;
        }

        private static Border CreateActionIconButton(UIElement content, string toolTip, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var btn = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = toolTip,
                Margin = new Thickness(0, 0, 4, 0),
                Child = content
            };
            btn.MouseEnter += (_, _) => btn.Background = CapsuleTheme.Brush(p.Surface2);
            btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
            btn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return btn;
        }

        private static Border CreateQuickInsertChip(string label, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var chip = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 9.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            chip.MouseEnter += (_, _) =>
            {
                chip.Background = CapsuleTheme.Brush(p.Surface2);
                if (chip.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(p.Text);
            };
            chip.MouseLeave += (_, _) =>
            {
                chip.Background = CapsuleTheme.Brush(p.Surface);
                if (chip.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(p.Text2);
            };
            chip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return chip;
        }

        private void InsertTextAtCursor(string text)
        {
            if (_contentBox == null || string.IsNullOrEmpty(text)) return;
            int idx = _contentBox.SelectionStart;
            _contentBox.SelectedText = text;
            _contentBox.SelectionStart = idx + text.Length;
            _contentBox.SelectionLength = 0;
            _contentBox.Focus();
        }

        public void CreateNewNote()
        {
            SaveCurrentNoteData();
            var note = NotesService.Instance.CreateNote("", "");
            _selectedNote = note;
            RefreshList();

            _titleBox.Focus();
        }

        public void RefreshList()
        {
            var allNotes = string.IsNullOrWhiteSpace(_searchQuery)
                ? NotesService.Instance.Notes
                : NotesService.Instance.Search(_searchQuery).ToList();

            _notesStackPanel.Children.Clear();

            _searchCountText.Text = !string.IsNullOrWhiteSpace(_searchQuery)
                ? $"({allNotes.Count})"
                : $"({NotesService.Instance.Notes.Count})";

            if (allNotes.Count == 0)
            {
                _emptyListPanel.Visibility = Visibility.Visible;
                _selectedNote = null;
                UpdateEditorDisplay();
                return;
            }

            _emptyListPanel.Visibility = Visibility.Collapsed;

            if (_selectedNote == null || !allNotes.Any(n => n.Id == _selectedNote.Id))
            {
                _selectedNote = allNotes[0];
            }

            var p = CapsuleTheme.Current;
            foreach (var note in allNotes)
            {
                bool isSelected = _selectedNote != null && note.Id == _selectedNote.Id;

                var rowBorder = new Border
                {
                    Height = 48,
                    CornerRadius = new CornerRadius(7),
                    Background = isSelected ? CapsuleTheme.Brush(p.Surface) : Brushes.Transparent,
                    BorderBrush = isSelected ? CapsuleTheme.Brush(p.Border) : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(0, 1.5, 0, 1.5),
                    Cursor = Cursors.Hand
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Accent indicator
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Selected Accent Bar
                if (isSelected)
                {
                    var accentBar = new Border
                    {
                        Width = 3,
                        Height = 24,
                        CornerRadius = new CornerRadius(1.5),
                        Background = CapsuleTheme.Brush(p.Accent),
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(accentBar, 0);
                    rowGrid.Children.Add(accentBar);
                }

                var rowStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(rowStack, 1);

                // Top Line (Color Dot + Pin + Title + Time)
                var topLine = new DockPanel();

                var timeBlock = new TextBlock
                {
                    Text = note.GetFormattedTime(),
                    FontSize = 9.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(timeBlock, Dock.Right);
                topLine.Children.Add(timeBlock);

                var titleDock = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // Color Tag Dot
                if (!string.IsNullOrEmpty(note.ColorTag))
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(note.ColorTag);
                        titleDock.Children.Add(new Ellipse
                        {
                            Width = 7,
                            Height = 7,
                            Fill = new SolidColorBrush(c),
                            Margin = new Thickness(0, 0, 4, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                    catch { }
                }

                // Pin Icon
                if (note.IsPinned)
                {
                    titleDock.Children.Add(new TextBlock
                    {
                        Text = "📌",
                        FontSize = 9.5,
                        Margin = new Thickness(0, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                var titleBlock = new TextBlock
                {
                    Text = note.GetDisplayTitle(),
                    FontSize = 12.0,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 135
                };
                titleDock.Children.Add(titleBlock);
                topLine.Children.Add(titleDock);
                rowStack.Children.Add(topLine);

                // Bottom Line (Preview snippet)
                var previewBlock = new TextBlock
                {
                    Text = note.GetDisplayPreview(),
                    FontSize = 10.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                rowStack.Children.Add(previewBlock);

                rowGrid.Children.Add(rowStack);
                rowBorder.Child = rowGrid;

                // Hover styling
                rowBorder.MouseEnter += (_, _) =>
                {
                    if (_selectedNote == null || note.Id != _selectedNote.Id)
                    {
                        rowBorder.Background = CapsuleTheme.Brush(p.Surface2);
                    }
                };
                rowBorder.MouseLeave += (_, _) =>
                {
                    if (_selectedNote == null || note.Id != _selectedNote.Id)
                    {
                        rowBorder.Background = Brushes.Transparent;
                    }
                };
                rowBorder.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    SaveCurrentNoteData();
                    _selectedNote = note;
                    RefreshList();
                };

                // Context Menu for Note in List
                rowBorder.ContextMenu = BuildNoteContextMenu(note);

                _notesStackPanel.Children.Add(rowBorder);
            }

            UpdateEditorDisplay();
        }

        private ContextMenu BuildNoteContextMenu(NoteItem note)
        {
            var cm = new ContextMenu();

            var miPin = new MenuItem { Header = note.IsPinned ? "Unpin Note" : "Pin Note", Icon = new TextBlock { Text = "📌" } };
            miPin.Click += (_, _) =>
            {
                NotesService.Instance.TogglePin(note.Id);
                RefreshList();
            };
            cm.Items.Add(miPin);

            // Color Tags Submenu
            var miColors = new MenuItem { Header = "Color Tag", Icon = new TextBlock { Text = "🎨" } };
            foreach (var (name, hex) in NoteColors)
            {
                string colorHex = hex;
                var colorItem = new MenuItem { Header = name };
                if (!string.IsNullOrEmpty(colorHex))
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(colorHex);
                        colorItem.Icon = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(c) };
                    }
                    catch { }
                }
                colorItem.Click += (_, _) =>
                {
                    NotesService.Instance.SetNoteColorTag(note.Id, colorHex);
                    RefreshList();
                };
                miColors.Items.Add(colorItem);
            }
            cm.Items.Add(miColors);

            var miCopy = new MenuItem { Header = "Copy Text", Icon = new TextBlock { Text = "📋" } };
            miCopy.Click += (_, _) =>
            {
                string full = string.IsNullOrWhiteSpace(note.Title) ? note.Content : $"{note.Title}\n\n{note.Content}";
                if (!string.IsNullOrEmpty(full)) Clipboard.SetText(full);
            };
            cm.Items.Add(miCopy);

            var miDup = new MenuItem { Header = "Duplicate", Icon = new TextBlock { Text = "📑" } };
            miDup.Click += (_, _) =>
            {
                var duplicated = NotesService.Instance.DuplicateNote(note.Id);
                if (duplicated != null)
                {
                    _selectedNote = duplicated;
                    RefreshList();
                }
            };
            cm.Items.Add(miDup);

            cm.Items.Add(new Separator());

            var miDelete = new MenuItem { Header = "Delete Note", Icon = new TextBlock { Text = "🗑️" } };
            miDelete.Click += (_, _) =>
            {
                NotesService.Instance.DeleteNote(note.Id);
                RefreshList();
            };
            cm.Items.Add(miDelete);

            return cm;
        }

        private void UpdateEditorDisplay()
        {
            if (_selectedNote == null)
            {
                _emptyEditorText.Visibility = Visibility.Visible;
                _editorContainer.Visibility = Visibility.Collapsed;
                return;
            }

            _emptyEditorText.Visibility = Visibility.Collapsed;
            _editorContainer.Visibility = Visibility.Visible;

            _suppressEditorTextChange = true;
            _titleBox.Text = _selectedNote.Title ?? "";
            _contentBox.Text = _selectedNote.Content ?? "";
            _titlePlaceholder.Visibility = string.IsNullOrEmpty(_titleBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            _contentPlaceholder.Visibility = string.IsNullOrEmpty(_contentBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            _suppressEditorTextChange = false;

            UpdatePinVisual();
            UpdateColorTagVisual();
            UpdateStatsVisual();
        }

        private void OnNoteTextChanged()
        {
            if (_suppressEditorTextChange || _selectedNote == null) return;
            _autosaveTimer.Stop();
            _autosaveTimer.Start();
            UpdateStatsVisual();
        }

        private void SaveCurrentNoteData()
        {
            if (_selectedNote == null) return;
            string t = _titleBox?.Text ?? "";
            string c = _contentBox?.Text ?? "";
            _selectedNote.Title = t;
            _selectedNote.Content = c;
            NotesService.Instance.UpdateNote(_selectedNote.Id, t, c);
        }

        private void UpdatePinVisual()
        {
            if (_selectedNote != null && _selectedNote.IsPinned)
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

        private void UpdateColorTagVisual()
        {
            var p = CapsuleTheme.Current;
            if (_selectedNote != null && !string.IsNullOrEmpty(_selectedNote.ColorTag))
            {
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(_selectedNote.ColorTag);
                    _colorTagDot.Fill = new SolidColorBrush(c);
                    return;
                }
                catch { }
            }
            _colorTagDot.Fill = CapsuleTheme.Brush(p.Text2);
        }

        private void ShowColorTagMenu()
        {
            if (_selectedNote == null) return;
            var cm = new ContextMenu();
            foreach (var (name, hex) in NoteColors)
            {
                string colorHex = hex;
                var mi = new MenuItem { Header = name };
                if (!string.IsNullOrEmpty(colorHex))
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(colorHex);
                        mi.Icon = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(c) };
                    }
                    catch { }
                }
                mi.Click += (_, _) =>
                {
                    NotesService.Instance.SetNoteColorTag(_selectedNote.Id, colorHex);
                    UpdateColorTagVisual();
                    RefreshList();
                };
                cm.Items.Add(mi);
            }
            cm.PlacementTarget = _colorTagButton;
            cm.IsOpen = true;
        }

        private void UpdateStatsVisual()
        {
            if (_selectedNote == null) return;
            string text = _contentBox.Text ?? "";
            int words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int chars = text.Length;
            _statsText.Text = $"{words} {(words == 1 ? "word" : "words")} · {chars} chars";
            _editedText.Text = $"Edited {_selectedNote.GetFormattedTime()}";
        }

        private void CopyCurrentNote()
        {
            if (_selectedNote == null) return;
            try
            {
                SaveCurrentNoteData();
                string full = string.IsNullOrWhiteSpace(_selectedNote.Title)
                    ? _selectedNote.Content
                    : $"{_selectedNote.Title}\n\n{_selectedNote.Content}";

                if (!string.IsNullOrEmpty(full))
                {
                    Clipboard.SetText(full);
                    _copyIcon.Text = "✓";
                    _copyFeedbackTimer.Stop();
                    _copyFeedbackTimer.Start();
                }
            }
            catch { }
        }

        private void ExportCurrentNote()
        {
            if (_selectedNote == null) return;
            try
            {
                SaveCurrentNoteData();
                string titleSanitized = string.IsNullOrWhiteSpace(_selectedNote.Title)
                    ? "Note"
                    : string.Join("_", _selectedNote.Title.Split(System.IO.Path.GetInvalidFileNameChars()));

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = titleSanitized,
                    DefaultExt = ".txt",
                    Filter = "Text Document (*.txt)|*.txt|Markdown File (*.md)|*.md|All Files (*.*)|*.*",
                    Title = "Export Note"
                };

                if (WindowsBuddy.BuddyCapsule.Core.NotchInteractionGuard.RunModal(() => sfd.ShowDialog()) == true)
                {
                    string content = string.IsNullOrWhiteSpace(_selectedNote.Title)
                        ? _selectedNote.Content
                        : $"# {_selectedNote.Title}\n\n{_selectedNote.Content}";
                    File.WriteAllText(sfd.FileName, content, Encoding.UTF8);
                }
            }
            catch { }
        }

        private void DeleteCurrentNote()
        {
            if (_selectedNote == null) return;
            NotesService.Instance.DeleteNote(_selectedNote.Id);
            RefreshList();
        }
    }
}
