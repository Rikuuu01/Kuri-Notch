using System;
using System.Windows.Media;

namespace NotchWin.Models
{
    public class MediaTrackInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public ImageSource? Thumbnail { get; set; }
        public bool IsPlaying { get; set; }
        public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public bool HasMedia { get; set; }
        public string SourceAppId { get; set; } = string.Empty;

        public static MediaTrackInfo Empty => new MediaTrackInfo
        {
            Title = string.Empty,
            Artist = string.Empty,
            Thumbnail = null,
            IsPlaying = false,
            HasMedia = false
        };
    }
}
