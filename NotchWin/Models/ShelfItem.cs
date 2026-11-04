using System;
using System.IO;
using System.Windows.Media;

namespace NotchWin.Models
{
    public class ShelfItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public bool IsImage { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public ImageSource? Thumbnail { get; set; }

        public bool HasThumbnail => Thumbnail != null;
    }
}
