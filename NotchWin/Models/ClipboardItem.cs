using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace NotchWin.Models
{
    public class ClipboardItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ContentType { get; set; } = "Text"; // "Text" or "Image"
        public string TextContent { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public DateTime CopiedAt { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
        public int CharacterCount { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public byte[]? ImageData { get; set; }

        [JsonIgnore]
        private BitmapSource? _thumbnail;

        [JsonIgnore]
        public BitmapSource? Thumbnail
        {
            get
            {
                if (_thumbnail == null && ImageData != null && ImageData.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream(ImageData);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = 160;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        _thumbnail = bmp;
                    }
                    catch { }
                }
                return _thumbnail;
            }
        }

        [JsonIgnore]
        public bool IsImage => ContentType == "Image" && ImageData != null && ImageData.Length > 0;

        [JsonIgnore]
        public bool IsText => ContentType == "Text";

        [JsonIgnore]
        public string FormattedTime
        {
            get
            {
                var diff = DateTime.Now - CopiedAt;
                if (diff.TotalSeconds < 60) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                return CopiedAt.ToString("d MMM HH:mm");
            }
        }
    }
}
