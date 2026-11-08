using CommunityToolkit.Mvvm.ComponentModel;

namespace NotchWin.ViewModels
{
    public partial class LyricsCandidateViewModel : ObservableObject
    {
        public int Id { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string? AlbumName { get; set; }
        public string DurationText { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string Provider { get; set; } = "LRCLIB";
        public bool IsSpicyLyrics => string.Equals(Provider, "SpicyLyrics", System.StringComparison.OrdinalIgnoreCase);
        public bool IsLrcRed => string.Equals(Provider, "LrcRed", System.StringComparison.OrdinalIgnoreCase);
        public string ProviderLabel => IsSpicyLyrics ? "Spicy Lyrics" : (IsLrcRed ? "lrc.red" : Provider);
        public bool IsSyllableSynced { get; set; }
        public string Subtitle => string.IsNullOrWhiteSpace(AlbumName) ? ArtistName : $"{ArtistName} · {AlbumName}";

        [ObservableProperty]
        private bool _isSelected;
    }
}
