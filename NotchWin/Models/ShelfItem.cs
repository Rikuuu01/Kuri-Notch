using System;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NotchWin.Models
{
    public partial class ShelfItem : ObservableObject
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

        [JsonIgnore]
        public ImageSource? Thumbnail { get; set; }

        [JsonIgnore]
        public bool HasThumbnail => Thumbnail != null;

        [ObservableProperty]
        private bool _isMissing;

        [ObservableProperty]
        private string _statusText = string.Empty;
    }
}
