using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.Tools.Notes;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.UI
{
    public sealed class NotesToolView : UserControl
    {
        public const double GridW = 728.0;
        public const double GridH = 242.0;
        public const double LeftW = 232.0;

        private readonly Grid _rootGrid;

        // Left Column Elements
        private readonly TextBox _searchBox;
        private readonly StackPanel _notesStackPanel;
        private readonly ScrollViewer _notesScrollViewer;
        private readonly StackPanel _emptyListPanel;

        // Right Column Elements
        private readonly Grid _editorContainer;
        private readonly TextBlock _emptyEditorText;
        private readonly TextBox _titleBox;
        private readonly TextBox _contentBox;
        private readonly TextBlock _editedText;
        private readonly TextBlock _wordsText;
        private readonly Border _pinButton;
        private readonly TextBlock _pinIcon;
        private readonly Border _copyButton;
        private readonly TextBlock _copyIcon;
        private readonly Border _deleteButton;

        private readonly DispatcherTimer _autosaveTimer;
        private readonly DispatcherTimer _copyFeedbackTimer;
        private NoteItem _selectedNote;
        private string _searchQuery = string.Empty;
        private bool _suppressEditorTextChange = false;

        public NotesToolView()
        {
            FlowDirection = LocalizationService.CurrentFlowDirection;
            Background = Brushes.Transparent;

            _rootGrid = new Grid();
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LeftW) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) }); // Divider
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // LEFT COLUMN (All notes + Search + New note + List)
            // -------------------------------------------------------------
            var leftGrid = new Grid { Margin = new Thickness(0, 0, 10, 0) };
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (All notes + search + plus)
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search box
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Notes list / Empty state

            // Top Header: "All notes" on left, Search 🔍 and Plus ➕ on right
            var leftHeader = new DockPanel { Height = 32, Margin = new Thickness(4, 0, 0, 4) };

            var headerIcons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var searchBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Search notes",
                Margin = new Thickness(0, 0, 4, 0),
                Child = new TextBlock
                {
                    Text = "🔍",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            searchBtn.MouseLeftButtonUp += (_, e) =>
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
            searchBtn.MouseEnter += (_, _) => searchBtn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            searchBtn.MouseLeave += (_, _) => searchBtn.Background = Brushes.Transparent;
            headerIcons.Children.Add(searchBtn);

            var addBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Create new note",
                Child = new TextBlock
                {
                    Text = "+",
                    FontSize = 15.0,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            addBtn.MouseEnter += (_, _) => addBtn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            addBtn.MouseLeave += (_, _) => addBtn.Background = Brushes.Transparent;
            addBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CreateNewNote();
            };
            headerIcons.Children.Add(addBtn);

            DockPanel.SetDock(headerIcons, Dock.Right);
            leftHeader.Children.Add(headerIcons);

            var allNotesText = new TextBlock
            {
                Text = LocalizationService.T("notes.all_notes", "All notes"),
                FontSize = 13.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftHeader.Children.Add(allNotesText);

            Grid.SetRow(leftHeader, 0);
            leftGrid.Children.Add(leftHeader);

            // Collapsible search box
            _searchBox = new TextBox
            {
                Visibility = Visibility.Collapsed,
                Height = 24,
                Margin = new Thickness(4, 0, 4, 4),
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
            leftGrid.Children.Add(_searchBox);

            // Notes list & Empty state container
            var listContainer = new Grid();

            _notesStackPanel = new StackPanel();
            _notesScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _notesStackPanel
            };
            listContainer.Children.Add(_notesScrollViewer);

            // Empty List Panel (Matching Screenshot: "No notes yet" + "New note")
            _emptyListPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _emptyListPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("notes.no_notes", "No notes yet"),
                FontSize = 12.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });
            var newNoteBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var newNoteText = new TextBlock
            {
                Text = LocalizationService.T("notes.new_note", "New note"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            newNoteBtn.Child = newNoteText;
            newNoteBtn.MouseEnter += (_, _) => newNoteBtn.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            newNoteBtn.MouseLeave += (_, _) => newNoteBtn.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
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
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(verticalDivider, 1);
            _rootGrid.Children.Add(verticalDivider);

            // -------------------------------------------------------------
            // RIGHT COLUMN (Editor Pane or "Create a note to start writing")
            // -------------------------------------------------------------
            var rightGrid = new Grid { Margin = new Thickness(14, 0, 4, 0) };

            // Empty Editor View (Matching Screenshot!)
            _emptyEditorText = new TextBlock
            {
                Text = LocalizationService.T("notes.empty_prompt", "Create a note to start writing"),
                FontSize = 13.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };
            rightGrid.Children.Add(_emptyEditorText);

            // Active Note Editor Container
            _editorContainer = new Grid { Visibility = Visibility.Collapsed };
            _editorContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (Title + Actions)
            _editorContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Editor Text
            _editorContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer (Edited + Words)

            // Editor Header (Title + Pin + Copy + Delete)
            var editorHeader = new DockPanel { Height = 32, Margin = new Thickness(0, 0, 0, 4) };

            var editorActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

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
                ToolTip = "Pin note",
                Margin = new Thickness(0, 0, 4, 0),
                Child = _pinIcon
            };
            _pinButton.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (_selectedNote != null)
                {
                    NotesService.Instance.TogglePin(_selectedNote.Id);
                    UpdatePinVisual();
                }
            };
            editorActions.Children.Add(_pinButton);

            _copyIcon = new TextBlock
            {
                Text = "📋",
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _copyButton = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Copy note text",
                Margin = new Thickness(0, 0, 4, 0),
                Child = _copyIcon
            };
            _copyButton.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CopyCurrentNote();
            };
            editorActions.Children.Add(_copyButton);

            _deleteButton = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Delete note",
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
                DeleteCurrentNote();
            };
            editorActions.Children.Add(_deleteButton);

            DockPanel.SetDock(editorActions, Dock.Right);
            editorHeader.Children.Add(editorActions);

            _titleBox = new TextBox
            {
                FontSize = 15.0,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 2, 8, 2)
            };
            _titleBox.TextChanged += (_, _) => OnNoteTextChanged();
            editorHeader.Children.Add(_titleBox);

            Grid.SetRow(editorHeader, 0);
            _editorContainer.Children.Add(editorHeader);

            // Editor Text Body
            _contentBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 2, 4, 2)
            };
            _contentBox.TextChanged += (_, _) => OnNoteTextChanged();
            Grid.SetRow(_contentBox, 1);
            _editorContainer.Children.Add(_contentBox);

            // Editor Footer
            var editorFooter = new DockPanel { Height = 22, Margin = new Thickness(0, 4, 0, 0) };

            _wordsText = new TextBlock
            {
                Text = "0 words",
                FontSize = 11.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_wordsText, Dock.Right);
            editorFooter.Children.Add(_wordsText);

            _editedText = new TextBlock
            {
                Text = "Edited just now",
                FontSize = 11.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            editorFooter.Children.Add(_editedText);

            Grid.SetRow(editorFooter, 2);
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

        public void CreateNewNote()
        {
            var note = NotesService.Instance.CreateNote("New note", "");
            _selectedNote = note;
            RefreshList();

            _titleBox.Focus();
            _titleBox.SelectAll();
        }

        public void RefreshList()
        {
            var allNotes = string.IsNullOrWhiteSpace(_searchQuery)
                ? NotesService.Instance.Notes
                : NotesService.Instance.Search(_searchQuery).ToList();

            _notesStackPanel.Children.Clear();

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

            foreach (var note in allNotes)
            {
                bool isSelected = _selectedNote != null && note.Id == _selectedNote.Id;

                var rowBorder = new Border
                {
                    Height = 44,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(isSelected ? Color.FromRgb(38, 38, 44) : Colors.Transparent),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 1, 0, 1),
                    Cursor = Cursors.Hand
                };

                var rowStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                var titleBlock = new TextBlock
                {
                    Text = note.GetDisplayTitle(),
                    FontSize = 12.0,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                rowStack.Children.Add(titleBlock);

                var previewBlock = new TextBlock
                {
                    Text = note.GetDisplayPreview(),
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                rowStack.Children.Add(previewBlock);

                rowBorder.Child = rowStack;

                rowBorder.MouseEnter += (_, _) =>
                {
                    if (_selectedNote == null || note.Id != _selectedNote.Id)
                    {
                        rowBorder.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
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
                    _selectedNote = note;
                    RefreshList();
                };

                _notesStackPanel.Children.Add(rowBorder);
            }

            UpdateEditorDisplay();
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
            _titleBox.Text = _selectedNote.Title;
            _contentBox.Text = _selectedNote.Content;
            _suppressEditorTextChange = false;

            UpdatePinVisual();
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
            _selectedNote.Title = _titleBox.Text;
            _selectedNote.Content = _contentBox.Text;
            NotesService.Instance.UpdateNote(_selectedNote.Id, _titleBox.Text, _contentBox.Text);
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

        private void UpdateStatsVisual()
        {
            if (_selectedNote == null) return;
            int words = _contentBox.Text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            _wordsText.Text = $"{words} {(words == 1 ? "word" : "words")}";
            _editedText.Text = $"Edited {_selectedNote.GetFormattedTime()}";
        }

        private void CopyCurrentNote()
        {
            if (_selectedNote == null) return;
            try
            {
                string full = string.IsNullOrWhiteSpace(_selectedNote.Title)
                    ? _selectedNote.Content
                    : $"{_selectedNote.Title}\n\n{_selectedNote.Content}";

                if (!string.IsNullOrEmpty(full))
                {
                    System.Windows.Clipboard.SetText(full);
                    _copyIcon.Text = "✓";
                    _copyFeedbackTimer.Stop();
                    _copyFeedbackTimer.Start();
                }
            }
            catch { }
        }

        private void DeleteCurrentNote()
        {
            if (_selectedNote == null) return;
            NotesService.Instance.DeleteNote(_selectedNote.Id);
        }
    }
}
