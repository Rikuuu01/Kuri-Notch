using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NotchWin.Models
{
    public class LyricsLine
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; } = string.Empty;

        public LyricsLine() { }

        public LyricsLine(TimeSpan time, string text)
        {
            Time = time;
            Text = text;
        }
    }

    public class LyricsResult
    {
        public bool Found { get; set; }
        public bool IsInstrumental { get; set; }
        public bool IsSynced { get; set; }
        public int BaseOffsetMs { get; set; } = 0;
        public List<LyricsLine> Lines { get; set; } = new List<LyricsLine>();
        public string? PlainLyrics { get; set; }

        public static LyricsResult Empty => new LyricsResult { Found = false };

        public static LyricsResult NotFound => new LyricsResult { Found = false };

        public static LyricsResult Instrumental => new LyricsResult
        {
            Found = true,
            IsInstrumental = true
        };
    }

    public class LyricsCandidate
    {
        public int Id { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string? AlbumName { get; set; }
        public double? Duration { get; set; }
        public bool IsInstrumental { get; set; }
        public bool IsSynced { get; set; }
        public double Score { get; set; }
        public LyricsResult Result { get; set; } = LyricsResult.Empty;
    }

    public class LyricsFetchResult
    {
        public List<LyricsCandidate> Candidates { get; set; } = new List<LyricsCandidate>();
        public int AutoCandidateId { get; set; }

        public static LyricsFetchResult Empty => new LyricsFetchResult();
    }

    internal class LrcLibDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("albumName")]
        public string? AlbumName { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("instrumental")]
        public bool? Instrumental { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }

        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }
    }
}
