using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.FileShelf
{
    public sealed class FileShelfService
    {
        private static readonly Lazy<FileShelfService> _instance = new(() => new FileShelfService());
        public static FileShelfService Instance => _instance.Value;

        private const string FileName = "shelves.json";
        private readonly object _lock = new();
        private readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        public event Action ShelfChanged;

        public List<ShelfTab> Shelves { get; private set; } = new();
        public ShelfTab ActiveShelf { get; private set; }

        private FileShelfService()
        {
            Load();
        }

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    var dto = PersistenceHelper.SafeReadJson<ShelfStorageDto>(FileName);
                    if (dto != null && dto.Shelves != null && dto.Shelves.Count > 0)
                    {
                        Shelves = dto.Shelves;
                        ActiveShelf = Shelves.FirstOrDefault(s => string.Equals(s.Id, dto.ActiveShelfId, StringComparison.OrdinalIgnoreCase))
                                      ?? Shelves[0];
                    }
                    else
                    {
                        InitDefaults();
                    }
                }
                catch
                {
                    InitDefaults();
                }
            }
        }

        private void InitDefaults()
        {
            Shelves = new List<ShelfTab>
            {
                new ShelfTab
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Desk",
                    Files = new List<ParkedFile>(),
                    SortMode = ShelfSort.Recent
                }
            };
            ActiveShelf = Shelves[0];
            Save();
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var dto = new ShelfStorageDto
                    {
                        Shelves = Shelves,
                        ActiveShelfId = ActiveShelf?.Id ?? (Shelves.Count > 0 ? Shelves[0].Id : string.Empty)
                    };
                    PersistenceHelper.SafeWriteJson(FileName, dto);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public void SetActiveShelf(string shelfId)
        {
            lock (_lock)
            {
                var shelf = Shelves.FirstOrDefault(s => string.Equals(s.Id, shelfId, StringComparison.OrdinalIgnoreCase));
                if (shelf != null && shelf != ActiveShelf)
                {
                    ActiveShelf = shelf;
                    Save();
                    ShelfChanged?.Invoke();
                }
            }
        }

        public void CreateShelf(string name)
        {
            lock (_lock)
            {
                string cleanName = string.IsNullOrWhiteSpace(name) ? $"Shelf {Shelves.Count + 1}" : name.Trim();
                var newShelf = new ShelfTab
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = cleanName,
                    Files = new List<ParkedFile>(),
                    SortMode = ShelfSort.Recent
                };
                Shelves.Add(newShelf);
                ActiveShelf = newShelf;
                Save();
                ShelfChanged?.Invoke();
            }
        }

        public void DeleteShelf(string shelfId)
        {
            lock (_lock)
            {
                if (Shelves.Count <= 1) return; // Keep at least one shelf
                var target = Shelves.FirstOrDefault(s => string.Equals(s.Id, shelfId, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    Shelves.Remove(target);
                    if (ActiveShelf == target)
                    {
                        ActiveShelf = Shelves[0];
                    }
                    Save();
                    ShelfChanged?.Invoke();
                }
            }
        }

        public void RenameShelf(string shelfId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            lock (_lock)
            {
                var target = Shelves.FirstOrDefault(s => string.Equals(s.Id, shelfId, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    target.Name = newName.Trim();
                    Save();
                    ShelfChanged?.Invoke();
                }
            }
        }

        public void MoveFileToShelf(string fileId, string targetShelfId)
        {
            if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(targetShelfId)) return;
            lock (_lock)
            {
                var targetShelf = Shelves.FirstOrDefault(s => string.Equals(s.Id, targetShelfId, StringComparison.OrdinalIgnoreCase));
                if (targetShelf == null) return;

                ParkedFile fileToMove = null;
                foreach (var shelf in Shelves)
                {
                    var match = shelf.Files.FirstOrDefault(f => string.Equals(f.Id, fileId, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        shelf.Files.Remove(match);
                        fileToMove = match;
                        break;
                    }
                }

                if (fileToMove != null)
                {
                    if (!targetShelf.Files.Any(f => string.Equals(f.FilePath, fileToMove.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        targetShelf.Files.Insert(0, fileToMove);
                    }
                    Save();
                    ShelfChanged?.Invoke();
                }
            }
        }

        public int RemoveMissingFiles()
        {
            int removed = 0;
            lock (_lock)
            {
                if (ActiveShelf == null) return 0;
                var missing = ActiveShelf.Files.Where(f => !File.Exists(f.FilePath) && !Directory.Exists(f.FilePath)).ToList();
                foreach (var m in missing)
                {
                    ActiveShelf.Files.Remove(m);
                    removed++;
                }
                if (removed > 0)
                {
                    Save();
                    ShelfChanged?.Invoke();
                }
            }
            return removed;
        }

        public bool PasteClipboardData()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var fileList = System.Windows.Clipboard.GetFileDropList();
                    if (fileList != null && fileList.Count > 0)
                    {
                        AddFilesToActiveShelf(fileList.Cast<string>());
                        return true;
                    }
                }
                else if (System.Windows.Clipboard.ContainsImage())
                {
                    var bs = System.Windows.Clipboard.GetImage();
                    if (bs != null)
                    {
                        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mayli", "ShelfFiles");
                        Directory.CreateDirectory(dir);
                        string fileName = $"Pasted_Image_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string destPath = Path.Combine(dir, fileName);

                        using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bs));
                        encoder.Save(fs);

                        AddFilesToActiveShelf(new[] { destPath });
                        return true;
                    }
                }
                else if (System.Windows.Clipboard.ContainsText())
                {
                    string txt = System.Windows.Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrEmpty(txt))
                    {
                        if (File.Exists(txt) || Directory.Exists(txt))
                        {
                            AddFilesToActiveShelf(new[] { txt });
                            return true;
                        }
                        else if (txt.Length > 0)
                        {
                            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mayli", "ShelfFiles");
                            Directory.CreateDirectory(dir);
                            string fileName = $"Snippet_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                            string destPath = Path.Combine(dir, fileName);
                            File.WriteAllText(destPath, txt);
                            AddFilesToActiveShelf(new[] { destPath });
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public void AddFilesToActiveShelf(IEnumerable<string> paths)
        {
            if (paths == null) return;
            lock (_lock)
            {
                if (ActiveShelf == null)
                {
                    if (Shelves.Count == 0) InitDefaults();
                    else ActiveShelf = Shelves[0];
                }

                bool addedAny = false;
                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    string fullPath = Path.GetFullPath(path);

                    bool isFile = File.Exists(fullPath);
                    bool isDir = Directory.Exists(fullPath);
                    if (!isFile && !isDir) continue;

                    // Avoid duplicate in current shelf
                    if (ActiveShelf.Files.Any(f => string.Equals(f.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    long size = 0;
                    string name = Path.GetFileName(fullPath);
                    string ext = "";

                    if (isFile)
                    {
                        try
                        {
                            var fi = new FileInfo(fullPath);
                            size = fi.Length;
                            ext = fi.Extension;
                            name = fi.Name;
                        }
                        catch { }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(name)) name = fullPath;
                        ext = "[DIR]";
                    }

                    var parked = new ParkedFile
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        FilePath = fullPath,
                        FileName = name,
                        FileSize = size,
                        Extension = ext,
                        AddedAt = DateTime.Now
                    };

                    ActiveShelf.Files.Insert(0, parked);
                    addedAny = true;
                }

                if (addedAny)
                {
                    Save();
                    ShelfChanged?.Invoke();
                }
            }
        }

        public void RemoveFile(string fileId)
        {
            lock (_lock)
            {
                if (ActiveShelf == null) return;
                var item = ActiveShelf.Files.FirstOrDefault(f => string.Equals(f.Id, fileId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    ActiveShelf.Files.Remove(item);
                    Save();
                    ShelfChanged?.Invoke();
                }
            }
        }

        public void ClearActiveShelf()
        {
            lock (_lock)
            {
                if (ActiveShelf == null || ActiveShelf.Files.Count == 0) return;
                ActiveShelf.Files.Clear();
                Save();
                ShelfChanged?.Invoke();
            }
        }

        public void SetSortMode(ShelfSort sort)
        {
            lock (_lock)
            {
                if (ActiveShelf == null) return;
                ActiveShelf.SortMode = sort;
                Save();
                ShelfChanged?.Invoke();
            }
        }

        public IEnumerable<ParkedFile> GetSortedFiles()
        {
            lock (_lock)
            {
                if (ActiveShelf == null || ActiveShelf.Files == null)
                    return Enumerable.Empty<ParkedFile>();

                return ActiveShelf.SortMode switch
                {
                    ShelfSort.Recent => ActiveShelf.Files.OrderByDescending(f => f.AddedAt),
                    ShelfSort.Name => ActiveShelf.Files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase),
                    ShelfSort.Size => ActiveShelf.Files.OrderByDescending(f => f.FileSize),
                    ShelfSort.Type => ActiveShelf.Files.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.FileName),
                    _ => ActiveShelf.Files
                };
            }
        }

        public void CopyAllActiveShelfFiles()
        {
            lock (_lock)
            {
                if (ActiveShelf == null || ActiveShelf.Files.Count == 0) return;
                var validPaths = ActiveShelf.Files
                    .Select(f => f.FilePath)
                    .Where(p => File.Exists(p) || Directory.Exists(p))
                    .ToList();
                if (validPaths.Count == 0) return;

                try
                {
                    var fileCol = new System.Collections.Specialized.StringCollection();
                    fileCol.AddRange(validPaths.ToArray());
                    System.Windows.Clipboard.SetFileDropList(fileCol);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public ImageSource GetThumbnailOrIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            lock (_iconCache)
            {
                if (_iconCache.TryGetValue(path, out var cached))
                    return cached;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
                return null;

            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp")
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(path);
                        bi.DecodePixelWidth = 96;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();

                        lock (_iconCache) _iconCache[path] = bi;
                        return bi;
                    }
                    catch { }
                }

                if (File.Exists(path))
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (icon != null)
                    {
                        IntPtr hBitmap = icon.ToBitmap().GetHbitmap();
                        try
                        {
                            var bs = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            bs.Freeze();

                            lock (_iconCache) _iconCache[path] = bs;
                            return bs;
                        }
                        finally
                        {
                            DeleteObject(hBitmap);
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }
}
