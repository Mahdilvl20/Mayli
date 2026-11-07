using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Clipboard
{
    /// <summary>
    /// Persistent SQLite storage for clipboard history using Windows built-in winsqlite3.dll.
    /// Implements exact storage policies based on content type and size:
    /// - Plain text <= 1MB: SQLite; > 1MB: separate file
    /// - Code & Link: SQLite
    /// - HTML / RTF <= 512KB: SQLite; > 512KB: separate file
    /// - PNG / JPG <= 256KB: SQLite BLOB; > 256KB: separate file
    /// - GIF / WebP / BMP: separate file
    /// - SVG <= 256KB: SQLite; > 256KB: separate file
    /// - PDF, Audio, Video, ZIP/RAR/7Z, General files: separate file / file reference
    /// </summary>
    public sealed class ClipboardDatabase
    {
        private static readonly Lazy<ClipboardDatabase> _lazy = new(() => new ClipboardDatabase());
        public static ClipboardDatabase Instance => _lazy.Value;

        private const string Dll = "winsqlite3.dll";
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        #region SQLite Native P/Invoke

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open16([MarshalAs(UnmanagedType.LPWStr)] string filename, out IntPtr db);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare16_v2(IntPtr db, [MarshalAs(UnmanagedType.LPWStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr pStmt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text16(IntPtr pStmt, int idx, [MarshalAs(UnmanagedType.LPWStr)] string val, int len, IntPtr dtor);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_blob(IntPtr pStmt, int idx, byte[] val, int len, IntPtr dtor);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_int64(IntPtr pStmt, int idx, long val);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_int(IntPtr pStmt, int idx, int val);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_null(IntPtr pStmt, int idx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text16(IntPtr pStmt, int iCol);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(IntPtr pStmt, int iCol);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_int(IntPtr pStmt, int iCol);

        #endregion

        private readonly string _dbPath;
        private readonly string _storageDir;
        private readonly object _dbLock = new();
        private IntPtr _db = IntPtr.Zero;

        private ClipboardDatabase()
        {
            _dbPath = Path.Combine(AppPaths.DataDir, "clipboard.db");
            _storageDir = Path.Combine(AppPaths.DataDir, "clipboard_storage");

            try
            {
                if (!Directory.Exists(_storageDir))
                {
                    Directory.CreateDirectory(_storageDir);
                }
            }
            catch { }

            InitializeDb();
        }

        private void InitializeDb()
        {
            lock (_dbLock)
            {
                int rc = sqlite3_open16(_dbPath, out _db);
                if (rc != SQLITE_OK || _db == IntPtr.Zero) return;

                // High-performance SQLite tuning: Write-Ahead Logging & memory caching
                sqlite3_exec(_db, "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA temp_store = MEMORY; PRAGMA cache_size = -2000;", IntPtr.Zero, IntPtr.Zero, out _);

                string schemaSql = @"
CREATE TABLE IF NOT EXISTS clipboard_items (
    id TEXT PRIMARY KEY,
    kind TEXT NOT NULL,
    format_type TEXT,
    storage_method TEXT NOT NULL,
    text_content TEXT,
    blob_data BLOB,
    file_path TEXT,
    file_paths_json TEXT,
    file_size_bytes INTEGER,
    line_count INTEGER,
    word_count INTEGER,
    char_count INTEGER,
    image_width INTEGER,
    image_height INTEGER,
    link_host TEXT,
    link_uri TEXT,
    source_app TEXT,
    source_app_exe TEXT,
    is_pinned INTEGER DEFAULT 0,
    timestamp_local TEXT NOT NULL,
    image_hash TEXT
);
CREATE INDEX IF NOT EXISTS idx_clipboard_timestamp ON clipboard_items(timestamp_local DESC);
CREATE INDEX IF NOT EXISTS idx_clipboard_pinned ON clipboard_items(is_pinned);
";
                sqlite3_exec(_db, schemaSql, IntPtr.Zero, IntPtr.Zero, out _);
                try
                {
                    if (sqlite3_prepare16_v2(_db, "ALTER TABLE clipboard_items ADD COLUMN image_hash TEXT;", -1, out var alterStmt, IntPtr.Zero) == SQLITE_OK)
                    {
                        sqlite3_step(alterStmt);
                        sqlite3_finalize(alterStmt);
                    }
                }
                catch { }
            }
        }

        public void SaveItem(ClipboardItem item)
        {
            if (item == null) return;

            lock (_dbLock)
            {
                if (_db == IntPtr.Zero) return;

                ApplyStoragePolicy(item, out string storageMethod, out string textContent, out byte[] blobData, out string filePath);

                string insertSql = @"
INSERT OR REPLACE INTO clipboard_items (
    id, kind, format_type, storage_method, text_content, blob_data, file_path, file_paths_json,
    file_size_bytes, line_count, word_count, char_count, image_width, image_height,
    link_host, link_uri, source_app, source_app_exe, is_pinned, timestamp_local, image_hash
) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);";

                if (sqlite3_prepare16_v2(_db, insertSql, -1, out var stmt, IntPtr.Zero) != SQLITE_OK)
                    return;

                try
                {
                    // 1: id
                    sqlite3_bind_text16(stmt, 1, item.Id ?? Guid.NewGuid().ToString("N"), -1, SQLITE_TRANSIENT);
                    // 2: kind
                    sqlite3_bind_text16(stmt, 2, item.Kind.ToString(), -1, SQLITE_TRANSIENT);
                    // 3: format_type
                    sqlite3_bind_text16(stmt, 3, item.FormatType ?? string.Empty, -1, SQLITE_TRANSIENT);
                    // 4: storage_method
                    sqlite3_bind_text16(stmt, 4, storageMethod ?? "sqlite_text", -1, SQLITE_TRANSIENT);

                    // 5: text_content
                    if (!string.IsNullOrEmpty(textContent))
                        sqlite3_bind_text16(stmt, 5, textContent, -1, SQLITE_TRANSIENT);
                    else
                        sqlite3_bind_null(stmt, 5);

                    // 6: blob_data
                    if (blobData != null && blobData.Length > 0)
                        sqlite3_bind_blob(stmt, 6, blobData, blobData.Length, SQLITE_TRANSIENT);
                    else
                        sqlite3_bind_null(stmt, 6);

                    // 7: file_path
                    if (!string.IsNullOrEmpty(filePath))
                        sqlite3_bind_text16(stmt, 7, filePath, -1, SQLITE_TRANSIENT);
                    else
                        sqlite3_bind_null(stmt, 7);

                    // 8: file_paths_json
                    string filesJson = (item.FilePaths != null && item.FilePaths.Count > 0)
                        ? JsonSerializer.Serialize(item.FilePaths)
                        : null;
                    if (!string.IsNullOrEmpty(filesJson))
                        sqlite3_bind_text16(stmt, 8, filesJson, -1, SQLITE_TRANSIENT);
                    else
                        sqlite3_bind_null(stmt, 8);

                    // 9: file_size_bytes
                    sqlite3_bind_int64(stmt, 9, item.FileSizeBytes);
                    // 10: line_count
                    sqlite3_bind_int(stmt, 10, item.LineCount);
                    // 11: word_count
                    sqlite3_bind_int(stmt, 11, item.WordCount);
                    // 12: char_count
                    sqlite3_bind_int(stmt, 12, item.CharCount);
                    // 13: image_width
                    sqlite3_bind_int(stmt, 13, item.ImageWidth);
                    // 14: image_height
                    sqlite3_bind_int(stmt, 14, item.ImageHeight);
                    // 15: link_host
                    sqlite3_bind_text16(stmt, 15, item.LinkHost ?? string.Empty, -1, SQLITE_TRANSIENT);
                    // 16: link_uri
                    sqlite3_bind_text16(stmt, 16, item.LinkUri?.ToString() ?? string.Empty, -1, SQLITE_TRANSIENT);
                    // 17: source_app
                    sqlite3_bind_text16(stmt, 17, item.SourceApp ?? "Windows", -1, SQLITE_TRANSIENT);
                    // 18: source_app_exe
                    sqlite3_bind_text16(stmt, 18, item.SourceAppExe ?? string.Empty, -1, SQLITE_TRANSIENT);
                    // 19: is_pinned
                    sqlite3_bind_int(stmt, 19, item.IsPinned ? 1 : 0);
                    // 20: timestamp_local
                    sqlite3_bind_text16(stmt, 20, item.TimestampLocal.ToString("O"), -1, SQLITE_TRANSIENT);
                    // 21: image_hash
                    string hash = item.Kind == ClipKind.Image ? item.GetImageHash() : string.Empty;
                    if (!string.IsNullOrEmpty(hash))
                        sqlite3_bind_text16(stmt, 21, hash, -1, SQLITE_TRANSIENT);
                    else
                        sqlite3_bind_null(stmt, 21);

                    sqlite3_step(stmt);
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }
        }

        private void ApplyStoragePolicy(
            ClipboardItem item,
            out string storageMethod,
            out string textContent,
            out byte[] blobData,
            out string filePath)
        {
            storageMethod = "sqlite_text";
            textContent = null;
            blobData = null;
            filePath = null;

            try
            {
                // POLICY 1: SVG (<= 256KB -> SQLite; > 256KB -> separate file)
                if (item.Kind == ClipKind.Svg)
                {
                    string svg = item.Text ?? string.Empty;
                    long bytes = Encoding.UTF8.GetByteCount(svg);
                    item.FileSizeBytes = bytes;

                    if (bytes <= 256 * 1024)
                    {
                        storageMethod = "sqlite_text";
                        textContent = svg;
                    }
                    else
                    {
                        storageMethod = "file";
                        string targetPath = Path.Combine(_storageDir, $"svg_{item.Id}.svg");
                        File.WriteAllText(targetPath, svg, Encoding.UTF8);
                        filePath = targetPath;
                        item.StoredFilePath = targetPath;
                        textContent = svg.Length > 2000 ? svg.Substring(0, 2000) : svg;
                    }
                    return;
                }

                // POLICY 2: Code and Link (any normal size -> SQLite)
                if (item.Kind == ClipKind.Code || item.Kind == ClipKind.Link)
                {
                    storageMethod = "sqlite_text";
                    textContent = item.Text ?? string.Empty;
                    item.FileSizeBytes = Encoding.UTF8.GetByteCount(textContent);
                    return;
                }

                // POLICY 3: HTML / RTF (<= 512KB -> SQLite; > 512KB -> separate file)
                if (item.Kind == ClipKind.Html || item.Kind == ClipKind.Rtf)
                {
                    string rich = item.Text ?? string.Empty;
                    long bytes = Encoding.UTF8.GetByteCount(rich);
                    item.FileSizeBytes = bytes;
                    string ext = item.Kind == ClipKind.Html ? "html" : "rtf";

                    if (bytes <= 512 * 1024)
                    {
                        storageMethod = "sqlite_text";
                        textContent = rich;
                    }
                    else
                    {
                        storageMethod = "file";
                        string targetPath = Path.Combine(_storageDir, $"doc_{item.Id}.{ext}");
                        File.WriteAllText(targetPath, rich, Encoding.UTF8);
                        filePath = targetPath;
                        item.StoredFilePath = targetPath;
                        textContent = rich.Length > 2000 ? rich.Substring(0, 2000) : rich;
                    }
                    return;
                }

                // POLICY 4 & 5: Images (PNG/JPG <= 256KB -> BLOB; > 256KB -> file; GIF/WebP/BMP -> file)
                if (item.Kind == ClipKind.Image)
                {
                    byte[] imgBytes = item.RawBlobData;
                    string format = (item.FormatType ?? "image/png").ToLowerInvariant();

                    if ((imgBytes == null || imgBytes.Length == 0) && item.Image != null)
                    {
                        imgBytes = EncodeBitmapToPng(item.Image);
                        item.RawBlobData = imgBytes;
                    }

                    if (imgBytes != null && imgBytes.Length > 0)
                    {
                        item.FileSizeBytes = imgBytes.Length;

                        bool isGifWebpBmp = format.Contains("gif") || format.Contains("webp") || format.Contains("bmp");

                        if (!isGifWebpBmp && imgBytes.Length <= 256 * 1024)
                        {
                            storageMethod = "sqlite_blob";
                            blobData = imgBytes;
                        }
                        else
                        {
                            storageMethod = "file";
                            string ext = format.Contains("jpg") || format.Contains("jpeg") ? "jpg"
                                       : format.Contains("gif") ? "gif"
                                       : format.Contains("webp") ? "webp"
                                       : format.Contains("bmp") ? "bmp"
                                       : "png";
                            string targetPath = Path.Combine(_storageDir, $"img_{item.Id}.{ext}");
                            File.WriteAllBytes(targetPath, imgBytes);
                            filePath = targetPath;
                            item.StoredFilePath = targetPath;
                        }
                    }
                    return;
                }

                // POLICY 6-10: Files, PDF, Audio, Video, ZIP/RAR/7Z (separate file)
                if (item.Kind == ClipKind.Files || item.Kind == ClipKind.Pdf ||
                    item.Kind == ClipKind.Audio || item.Kind == ClipKind.Video || item.Kind == ClipKind.Archive)
                {
                    storageMethod = "file";
                    if (item.FilePaths != null && item.FilePaths.Count > 0)
                    {
                        filePath = item.FilePaths[0];
                        item.StoredFilePath = filePath;
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                item.FileSizeBytes = new FileInfo(filePath).Length;
                            }
                        }
                        catch { }
                    }
                    textContent = item.Text;
                    return;
                }

                // POLICY 11: Plain Text (<= 1MB -> SQLite; > 1MB -> separate file)
                string text = item.Text ?? string.Empty;
                long textBytes = Encoding.UTF8.GetByteCount(text);
                item.FileSizeBytes = textBytes;

                if (textBytes <= 1024 * 1024)
                {
                    storageMethod = "sqlite_text";
                    textContent = text;
                }
                else
                {
                    storageMethod = "file";
                    string targetPath = Path.Combine(_storageDir, $"txt_{item.Id}.txt");
                    File.WriteAllText(targetPath, text, Encoding.UTF8);
                    filePath = targetPath;
                    item.StoredFilePath = targetPath;
                    textContent = text.Length > 1000 ? text.Substring(0, 1000) : text;
                }
            }
            catch
            {
                storageMethod = "sqlite_text";
                textContent = item.Text;
            }
        }

        public List<ClipboardItem> LoadAllItems(int limit = 100)
        {
            var list = new List<ClipboardItem>();

            lock (_dbLock)
            {
                if (_db == IntPtr.Zero) return list;

                string query = @"
SELECT id, kind, format_type, storage_method, text_content, blob_data, file_path, file_paths_json,
       file_size_bytes, line_count, word_count, char_count, image_width, image_height,
       link_host, link_uri, source_app, source_app_exe, is_pinned, timestamp_local, image_hash
FROM clipboard_items
ORDER BY is_pinned DESC, timestamp_local DESC
LIMIT ?;";

                if (sqlite3_prepare16_v2(_db, query, -1, out var stmt, IntPtr.Zero) != SQLITE_OK)
                    return list;

                try
                {
                    sqlite3_bind_int(stmt, 1, limit);

                    while (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        var item = new ClipboardItem();

                        item.Id = ReadColumnText(stmt, 0);

                        string kindStr = ReadColumnText(stmt, 1);
                        if (Enum.TryParse<ClipKind>(kindStr, out var kind))
                            item.Kind = kind;

                        item.FormatType = ReadColumnText(stmt, 2);
                        item.StorageMethod = ReadColumnText(stmt, 3);
                        item.Text = ReadColumnText(stmt, 4);

                        // blob data
                        int blobBytes = sqlite3_column_bytes(stmt, 5);
                        if (blobBytes > 0)
                        {
                            IntPtr blobPtr = sqlite3_column_blob(stmt, 5);
                            if (blobPtr != IntPtr.Zero)
                            {
                                item.RawBlobData = new byte[blobBytes];
                                Marshal.Copy(blobPtr, item.RawBlobData, 0, blobBytes);
                            }
                        }

                        item.StoredFilePath = ReadColumnText(stmt, 6);

                        string filesJson = ReadColumnText(stmt, 7);
                        if (!string.IsNullOrEmpty(filesJson))
                        {
                            try
                            {
                                item.FilePaths = JsonSerializer.Deserialize<List<string>>(filesJson) ?? new List<string>();
                            }
                            catch { }
                        }

                        item.FileSizeBytes = sqlite3_column_int64(stmt, 8);
                        item.LineCount = sqlite3_column_int(stmt, 9);
                        item.WordCount = sqlite3_column_int(stmt, 10);
                        item.CharCount = sqlite3_column_int(stmt, 11);
                        item.ImageWidth = sqlite3_column_int(stmt, 12);
                        item.ImageHeight = sqlite3_column_int(stmt, 13);
                        item.LinkHost = ReadColumnText(stmt, 14);

                        string uriStr = ReadColumnText(stmt, 15);
                        if (!string.IsNullOrEmpty(uriStr) && Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
                        {
                            item.LinkUri = uri;
                        }

                        item.SourceApp = ReadColumnText(stmt, 16);
                        item.SourceAppExe = ReadColumnText(stmt, 17);
                        item.IsPinned = sqlite3_column_int(stmt, 18) == 1;

                        string timeStr = ReadColumnText(stmt, 19);
                        if (DateTime.TryParse(timeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                        {
                            item.TimestampLocal = dt.ToLocalTime();
                        }

                        item.ImageHash = ReadColumnText(stmt, 20);

                        // Reconstruct in-memory representations for Images & Thumbnails
                        if (item.Kind == ClipKind.Image)
                        {
                            item.EnsureImage();
                            if (string.IsNullOrEmpty(item.ImageHash))
                            {
                                item.GetImageHash();
                            }
                            if (item.Image != null)
                            {
                                item.Thumbnail = CreateThumbnail(item.Image, 28, 28);
                                if (item.ImageWidth == 0) item.ImageWidth = item.Image.PixelWidth;
                                if (item.ImageHeight == 0) item.ImageHeight = item.Image.PixelHeight;
                            }
                        }

                        list.Add(item);
                    }
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }

            return list;
        }

        public void UpdatePinned(string id, bool isPinned)
        {
            if (string.IsNullOrEmpty(id)) return;

            lock (_dbLock)
            {
                if (_db == IntPtr.Zero) return;

                string sql = "UPDATE clipboard_items SET is_pinned = ? WHERE id = ?;";
                if (sqlite3_prepare16_v2(_db, sql, -1, out var stmt, IntPtr.Zero) == SQLITE_OK)
                {
                    try
                    {
                        sqlite3_bind_int(stmt, 1, isPinned ? 1 : 0);
                        sqlite3_bind_text16(stmt, 2, id, -1, SQLITE_TRANSIENT);
                        sqlite3_step(stmt);
                    }
                    finally
                    {
                        sqlite3_finalize(stmt);
                    }
                }
            }
        }

        public void DeleteItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            lock (_dbLock)
            {
                if (_db == IntPtr.Zero) return;

                // Check if there is an external file stored in clipboard_storage to clean up
                string selectSql = "SELECT storage_method, file_path FROM clipboard_items WHERE id = ?;";
                if (sqlite3_prepare16_v2(_db, selectSql, -1, out var selStmt, IntPtr.Zero) == SQLITE_OK)
                {
                    try
                    {
                        sqlite3_bind_text16(selStmt, 1, id, -1, SQLITE_TRANSIENT);
                        if (sqlite3_step(selStmt) == SQLITE_ROW)
                        {
                            string method = ReadColumnText(selStmt, 0);
                            string path = ReadColumnText(selStmt, 1);
                            if (string.Equals(method, "file", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(path) &&
                                path.StartsWith(_storageDir, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(path); } catch { }
                            }
                        }
                    }
                    finally
                    {
                        sqlite3_finalize(selStmt);
                    }
                }

                string deleteSql = "DELETE FROM clipboard_items WHERE id = ?;";
                if (sqlite3_prepare16_v2(_db, deleteSql, -1, out var stmt, IntPtr.Zero) == SQLITE_OK)
                {
                    try
                    {
                        sqlite3_bind_text16(stmt, 1, id, -1, SQLITE_TRANSIENT);
                        sqlite3_step(stmt);
                    }
                    finally
                    {
                        sqlite3_finalize(stmt);
                    }
                }
            }
        }

        public void ClearAll(bool keepPinned = true)
        {
            lock (_dbLock)
            {
                if (_db == IntPtr.Zero) return;

                // Delete associated files in clipboard_storage for unpinned items
                string query = keepPinned
                    ? "SELECT file_path FROM clipboard_items WHERE is_pinned = 0 AND storage_method = 'file';"
                    : "SELECT file_path FROM clipboard_items WHERE storage_method = 'file';";

                if (sqlite3_prepare16_v2(_db, query, -1, out var selStmt, IntPtr.Zero) == SQLITE_OK)
                {
                    try
                    {
                        while (sqlite3_step(selStmt) == SQLITE_ROW)
                        {
                            string path = ReadColumnText(selStmt, 0);
                            if (!string.IsNullOrEmpty(path) && path.StartsWith(_storageDir, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(path); } catch { }
                            }
                        }
                    }
                    finally
                    {
                        sqlite3_finalize(selStmt);
                    }
                }

                string deleteSql = keepPinned
                    ? "DELETE FROM clipboard_items WHERE is_pinned = 0;"
                    : "DELETE FROM clipboard_items;";

                sqlite3_exec(_db, deleteSql, IntPtr.Zero, IntPtr.Zero, out _);
            }
        }

        private static string ReadColumnText(IntPtr stmt, int col)
        {
            IntPtr ptr = sqlite3_column_text16(stmt, col);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringUni(ptr) : string.Empty;
        }

        public static byte[] EncodeBitmapToPng(BitmapSource bitmap)
        {
            if (bitmap == null) return Array.Empty<byte>();
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private static ImageSource CreateThumbnail(BitmapSource source, int width, int height)
        {
            try
            {
                if (source.PixelWidth == 0 || source.PixelHeight == 0) return source;
                double scaleX = (double)width / source.PixelWidth;
                double scaleY = (double)height / source.PixelHeight;
                double scale = Math.Max(scaleX, scaleY);

                var transformed = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scale, scale));
                var cropped = new CroppedBitmap(transformed, new System.Windows.Int32Rect(
                    Math.Max(0, (transformed.PixelWidth - width) / 2),
                    Math.Max(0, (transformed.PixelHeight - height) / 2),
                    Math.Min(width, transformed.PixelWidth),
                    Math.Min(height, transformed.PixelHeight)));
                cropped.Freeze();
                return cropped;
            }
            catch
            {
                return source;
            }
        }
    }
}
