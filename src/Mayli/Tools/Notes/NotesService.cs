using System;
using System.Collections.Generic;
using System.Linq;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Notes
{
    public sealed class NotesService
    {
        private const string FileName = "notes.json";
        private static readonly Lazy<NotesService> _lazy = new(() => new NotesService());
        public static NotesService Instance => _lazy.Value;

        private readonly List<NoteItem> _notes = new();
        private readonly object _lock = new();

        public event Action NotesChanged;

        public IReadOnlyList<NoteItem> Notes
        {
            get
            {
                lock (_lock)
                {
                    return _notes.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt).ToList();
                }
            }
        }

        private NotesService()
        {
            LoadNotes();
        }

        private void LoadNotes()
        {
            lock (_lock)
            {
                try
                {
                    var saved = PersistenceHelper.SafeReadJson<List<NoteItem>>(FileName);
                    if (saved != null && saved.Count > 0)
                    {
                        _notes.AddRange(saved);
                    }
                }
                catch { }
            }
        }

        private void SaveNotes()
        {
            lock (_lock)
            {
                try
                {
                    PersistenceHelper.SafeWriteJson(FileName, _notes);
                }
                catch { }
            }
        }

        public NoteItem CreateNote(string title = "", string content = "")
        {
            NoteItem note;
            lock (_lock)
            {
                note = new NoteItem
                {
                    Title = title,
                    Content = content,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _notes.Insert(0, note);
                SaveNotes();
            }
            NotesChanged?.Invoke();
            return note;
        }

        public void UpdateNote(string id, string title, string content)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                var note = _notes.FirstOrDefault(n => n.Id == id);
                if (note != null)
                {
                    note.Title = title;
                    note.Content = content;
                    note.UpdatedAt = DateTime.Now;
                    SaveNotes();
                }
            }
            NotesChanged?.Invoke();
        }

        public void DeleteNote(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                var note = _notes.FirstOrDefault(n => n.Id == id);
                if (note != null)
                {
                    _notes.Remove(note);
                    SaveNotes();
                }
            }
            NotesChanged?.Invoke();
        }

        public void TogglePin(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                var note = _notes.FirstOrDefault(n => n.Id == id);
                if (note != null)
                {
                    note.IsPinned = !note.IsPinned;
                    SaveNotes();
                }
            }
            NotesChanged?.Invoke();
        }

        public NoteItem DuplicateNote(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            NoteItem newNote = null;
            lock (_lock)
            {
                var existing = _notes.FirstOrDefault(n => n.Id == id);
                if (existing != null)
                {
                    newNote = new NoteItem
                    {
                        Title = string.IsNullOrWhiteSpace(existing.Title) ? "Copy" : $"{existing.Title} (Copy)",
                        Content = existing.Content,
                        ColorTag = existing.ColorTag,
                        IsPinned = false,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _notes.Insert(0, newNote);
                    SaveNotes();
                }
            }
            if (newNote != null)
            {
                NotesChanged?.Invoke();
            }
            return newNote;
        }

        public void SetNoteColorTag(string id, string colorTag)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                var note = _notes.FirstOrDefault(n => n.Id == id);
                if (note != null)
                {
                    note.ColorTag = colorTag ?? string.Empty;
                    note.UpdatedAt = DateTime.Now;
                    SaveNotes();
                }
            }
            NotesChanged?.Invoke();
        }

        public IEnumerable<NoteItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Notes;
            string q = query.Trim();
            lock (_lock)
            {
                return _notes.Where(n =>
                    (!string.IsNullOrEmpty(n.Title) && n.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(n.Content) && n.Content.Contains(q, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(n => n.IsPinned)
                    .ThenByDescending(n => n.UpdatedAt)
                    .ToList();
            }
        }
    }
}
