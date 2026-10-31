using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WindowsBuddy.Tools.FileShelf
{
    public enum ShelfSort
    {
        Recent,
        Name,
        Size,
        Type
    }

    public sealed class ParkedFile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Extension { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool IsSelected { get; set; }

        [JsonIgnore]
        public string DisplaySize => FormatBytes(FileSize);

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public sealed class ShelfTab
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Desk";
        public List<ParkedFile> Files { get; set; } = new();
        public ShelfSort SortMode { get; set; } = ShelfSort.Recent;
    }

    public sealed class ShelfStorageDto
    {
        public List<ShelfTab> Shelves { get; set; } = new();
        public string ActiveShelfId { get; set; } = string.Empty;
    }
}
