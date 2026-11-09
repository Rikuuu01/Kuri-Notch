using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class TtmlLyricsService : ITtmlLyricsService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        static TtmlLyricsService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NotchWin/1.0 (Windows; Dynamic Notch Music Overlay)");
        }

        public async Task<LyricsCandidate?> GetSyllableLyricsAsync(string title, string artist, TimeSpan duration, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            try
            {
                string cleanArtist = artist?.Trim() ?? string.Empty;
                string cleanTitle = title.Trim();

                // 1. Prioritas Utama: Cek official Spicy Lyrics Developer API bila API key disetel
                var apiKey = AppSettings.Current?.SpicyLyricsApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    apiKey = SettingsService.Instance?.CurrentSettings?.SpicyLyricsApiKey;
                }
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    var officialCandidate = await QueryOfficialSpicyLyricsAsync(cleanTitle, cleanArtist, duration, apiKey.Trim(), ct);
                    if (officialCandidate != null)
                    {
                        Log.Information("Loaded official Spicy Lyrics (developers.spicylyrics.org) syllable-synced lyrics for '{Title}' by '{Artist}' (Lines: {Count})",
                            cleanTitle, cleanArtist, officialCandidate.Result.Lines.Count);
                        return officialCandidate;
                    }
                }

                // 2. Prioritas Publik: Query Spicy Lyrics / TTML public mirror dataset
                string url = $"https://api.betterlyrics.org/getLyrics?a={Uri.EscapeDataString(cleanArtist)}&s={Uri.EscapeDataString(cleanTitle)}";
                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Debug("TTML API returned status {StatusCode} for '{Title}' by '{Artist}'", response.StatusCode, cleanTitle, cleanArtist);
                    return null;
                }

                var jsonStr = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(jsonStr))
                {
                    return null;
                }

                using var jsonDoc = JsonDocument.Parse(jsonStr);
                if (!jsonDoc.RootElement.TryGetProperty("ttml", out var ttmlElem))
                {
                    return null;
                }

                string? ttmlContent = ttmlElem.GetString();
                if (string.IsNullOrWhiteSpace(ttmlContent) || !ttmlContent.Contains("<tt"))
                {
                    return null;
                }

                var candidate = ParseTtmlToCandidate(ttmlContent, cleanTitle, cleanArtist, duration);
                if (candidate != null)
                {
                    Log.Information("Loaded Spicy Lyrics syllable-synced lyrics for '{Title}' by '{Artist}' (Lines: {Count})",
                        cleanTitle, cleanArtist, candidate.Result.Lines.Count);
                }
                return candidate;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query Spicy Lyrics syllable lyrics for '{Title}' by '{Artist}'", title, artist);
                return null;
            }
        }

        public static LyricsCandidate? ParseTtmlToCandidate(string ttmlXml, string title, string artist, TimeSpan expectedDuration)
        {
            try
            {
                var doc = XDocument.Parse(ttmlXml, LoadOptions.PreserveWhitespace);
                var pElements = doc.Descendants().Where(e => e.Name.LocalName == "p").ToList();
                if (pElements.Count == 0)
                {
                    return null;
                }

                var lines = new List<LyricsLine>();

                foreach (var p in pElements)
                {
                    var sbText = new StringBuilder();
                    var syllables = new List<SyllableItem>();
                    TimeSpan? lineStart = ParseTimeSpan(p.Attribute("begin")?.Value);
                    TimeSpan? lineEnd = ParseTimeSpan(p.Attribute("end")?.Value);

                    void ProcessNodes(IEnumerable<XNode> nodes)
                    {
                        foreach (var node in nodes)
                        {
                            if (node is XText textNode)
                            {
                                if (string.IsNullOrWhiteSpace(textNode.Value))
                                {
                                    // Spasi antar elemen: gabungkan whitespace menjadi satu spasi jika karakter sebelumnya bukan spasi
                                    if (sbText.Length > 0 && !char.IsWhiteSpace(sbText[^1]))
                                    {
                                        sbText.Append(' ');
                                    }
                                }
                                else
                                {
                                    sbText.Append(textNode.Value);
                                }
                            }
                            else if (node is XElement elem)
                            {
                                if (elem.Name.LocalName == "span")
                                {
                                    var childSpans = elem.Elements().Where(x => x.Name.LocalName == "span").ToList();
                                    if (childSpans.Count > 0)
                                    {
                                        ProcessNodes(elem.Nodes());
                                    }
                                    else
                                    {
                                        var sBegin = ParseTimeSpan(elem.Attribute("begin")?.Value);
                                        var sEnd = ParseTimeSpan(elem.Attribute("end")?.Value);
                                        string spanText = elem.Value;

                                        // Hindari spasi ganda bila spanText diawali spasi dan sbText sudah berakhiran spasi
                                        if (sbText.Length > 0 && char.IsWhiteSpace(sbText[^1]) && spanText.StartsWith(' '))
                                        {
                                            spanText = spanText.TrimStart(' ');
                                        }

                                        int startIdx = sbText.Length;
                                        int len = spanText.Length;
                                        sbText.Append(spanText);

                                        if (sBegin.HasValue && sEnd.HasValue)
                                        {
                                            syllables.Add(new SyllableItem
                                            {
                                                StartTime = sBegin.Value,
                                                EndTime = sEnd.Value,
                                                Text = spanText.Trim(),
                                                CharStartIndex = startIdx,
                                                CharLength = len
                                            });
                                        }
                                    }
                                }
                                else
                                {
                                    ProcessNodes(elem.Nodes());
                                }
                            }
                        }
                    }

                    ProcessNodes(p.Nodes());

                    string fullLine = sbText.ToString();
                    if (string.IsNullOrWhiteSpace(fullLine)) continue;

                    // Compute leading spaces so CharStartIndex can be normalized after Trim()
                    int leadingSpaces = 0;
                    while (leadingSpaces < fullLine.Length && char.IsWhiteSpace(fullLine[leadingSpaces]))
                    {
                        leadingSpaces++;
                    }

                    if (leadingSpaces > 0)
                    {
                        foreach (var syl in syllables)
                        {
                            syl.CharStartIndex = Math.Max(0, syl.CharStartIndex - leadingSpaces);
                        }
                    }

                    string trimmedLine = fullLine.Trim();

                    if (!lineStart.HasValue && syllables.Count > 0)
                    {
                        lineStart = syllables[0].StartTime;
                    }
                    if (!lineEnd.HasValue && syllables.Count > 0)
                    {
                        lineEnd = syllables[^1].EndTime;
                    }

                    var line = new LyricsLine(lineStart ?? TimeSpan.Zero, trimmedLine)
                    {
                        EndTime = lineEnd,
                        Syllables = syllables
                    };

                    lines.Add(line);
                }

                if (lines.Count == 0)
                {
                    return null;
                }

                lines.Sort((a, b) => a.Time.CompareTo(b.Time));
                bool hasSyllables = lines.Any(l => l.HasSyllables);

                var lyricsResult = new LyricsResult
                {
                    Found = true,
                    IsSynced = true,
                    IsSyllableSynced = hasSyllables,
                    Provider = "SpicyLyrics",
                    Lines = lines,
                    PlainLyrics = string.Join("\n", lines.Select(l => l.Text))
                };

                return new LyricsCandidate
                {
                    Id = 99990001,
                    TrackName = title,
                    ArtistName = artist,
                    AlbumName = "Spicy Lyrics · Syllable Sync",
                    Duration = expectedDuration.TotalSeconds > 0 ? expectedDuration.TotalSeconds : null,
                    Provider = "SpicyLyrics",
                    IsInstrumental = false,
                    IsSynced = true,
                    IsSyllableSynced = hasSyllables,
                    Score = 2000.0, // Top priority above line-synced LRCLIB (+1000)
                    Result = lyricsResult
                };
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to parse TTML XML to LyricsCandidate");
                return null;
            }
        }

        public static TimeSpan? ParseTimeSpan(string? str)
        {
            if (string.IsNullOrWhiteSpace(str)) return null;
            str = str.Trim();

            if (str.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(str[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms))
                    return TimeSpan.FromMilliseconds(ms);
                return null;
            }
            if (str.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(str[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double sec))
                    return TimeSpan.FromSeconds(sec);
                return null;
            }

            if (str.Contains(':'))
            {
                var parts = str.Split(':');
                if (parts.Length == 2)
                {
                    if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double m) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                    {
                        return TimeSpan.FromSeconds(m * 60 + s);
                    }
                }
                else if (parts.Length == 3)
                {
                    if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double h) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double m) &&
                        double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                    {
                        return TimeSpan.FromSeconds(h * 3600 + m * 60 + s);
                    }
                }
            }

            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double rawSec))
            {
                return TimeSpan.FromSeconds(rawSec);
            }

            return null;
        }

        private static async Task<LyricsCandidate?> QueryOfficialSpicyLyricsAsync(string title, string artist, TimeSpan duration, string apiKey, CancellationToken ct)
        {
            try
            {
                string searchUrl = $"https://api.spicylyrics.org/v1/tracks/search?title={Uri.EscapeDataString(title)}&artist={Uri.EscapeDataString(artist)}";
                if (duration.TotalSeconds > 10)
                {
                    searchUrl += $"&duration={Math.Round(duration.TotalSeconds)}";
                }

                using var searchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                searchReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                using var searchRes = await _httpClient.SendAsync(searchReq, ct);
                if (!searchRes.IsSuccessStatusCode)
                {
                    Log.Debug("Official Spicy Lyrics search returned HTTP {StatusCode} for '{Title}'", searchRes.StatusCode, title);
                    return null;
                }

                var searchJson = await searchRes.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(searchJson);
                if (!doc.RootElement.TryGetProperty("Body", out var body))
                {
                    return null;
                }

                JsonElement trackArray = default;
                if (body.ValueKind == JsonValueKind.Array)
                {
                    trackArray = body;
                }
                else if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("results", out var resElem) && resElem.ValueKind == JsonValueKind.Array)
                {
                    trackArray = resElem;
                }
                else
                {
                    return null;
                }

                if (trackArray.GetArrayLength() == 0) return null;

                // Urutkan tracks berdasarkan kecocokan durasi & judul agar lirik yang diambil sinkron dengan lagu yang sedang diputar
                var rankedTracks = new List<(JsonElement Track, double Score)>();
                foreach (var track in trackArray.EnumerateArray())
                {
                    double trackScore = 1000.0;
                    double trackDuration = track.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out double d) ? d : 0;
                    if (duration.TotalSeconds > 10 && trackDuration > 0)
                    {
                        double diff = Math.Abs(trackDuration - duration.TotalSeconds);
                        if (diff <= 1.5) trackScore += 400;
                        else if (diff <= 3.0) trackScore += 250;
                        else if (diff <= 5.0) trackScore += 50;
                        else if (diff <= 10.0) trackScore -= (diff * 30);
                        else trackScore -= (diff * 60 + 500);
                    }
                    string tTitle = track.TryGetProperty("title", out var tt) ? tt.GetString() ?? "" : "";
                    if (string.Equals(tTitle.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        trackScore += 150;
                    }
                    rankedTracks.Add((track, trackScore));
                }
                rankedTracks.Sort((a, b) => b.Score.CompareTo(a.Score));

                int maxChecks = Math.Min(3, rankedTracks.Count);
                for (int i = 0; i < maxChecks; i++)
                {
                    var track = rankedTracks[i].Track;
                    if (!track.TryGetProperty("id", out var idProp)) continue;
                    string? trackId = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(trackId)) continue;

                    string trackTitle = track.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? title : title;
                    string trackArtist = artist;
                    if (track.TryGetProperty("artists", out var artElem) && artElem.ValueKind == JsonValueKind.Array && artElem.GetArrayLength() > 0)
                    {
                        var artistList = new List<string>();
                        foreach (var a in artElem.EnumerateArray())
                        {
                            if (a.GetString() is { } str && !string.IsNullOrWhiteSpace(str))
                                artistList.Add(str);
                        }
                        if (artistList.Count > 0)
                        {
                            trackArtist = string.Join(", ", artistList);
                        }
                    }
                    string? trackAlbum = track.TryGetProperty("album", out var alProp) ? alProp.GetString() : null;
                    double trackDuration = track.TryGetProperty("duration", out var durProp) ? durProp.GetDouble() : (duration.TotalSeconds > 0 ? duration.TotalSeconds : 0);

                    string lyricsUrl = $"https://api.spicylyrics.org/v1/lyrics/{Uri.EscapeDataString(trackId)}";
                    using var lyricsReq = new HttpRequestMessage(HttpMethod.Get, lyricsUrl);
                    lyricsReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    using var lyricsRes = await _httpClient.SendAsync(lyricsReq, ct);
                    if (!lyricsRes.IsSuccessStatusCode) continue;

                    var lyricsJson = await lyricsRes.Content.ReadAsStringAsync(ct);
                    using var lDoc = JsonDocument.Parse(lyricsJson);
                    if (!lDoc.RootElement.TryGetProperty("Body", out var lBody) || lBody.ValueKind != JsonValueKind.Object) continue;

                    string? lyricsType = lBody.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() : null;
                    if (!lBody.TryGetProperty("Content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array) continue;

                    var lines = new List<LyricsLine>();
                    bool isSyllable = string.Equals(lyricsType, "Syllable", StringComparison.OrdinalIgnoreCase);

                    foreach (var lineElem in contentArray.EnumerateArray())
                    {
                        JsonElement vocalTarget = default;
                        if (lineElem.TryGetProperty("Lead", out var lead))
                        {
                            vocalTarget = lead;
                        }
                        else if (lineElem.TryGetProperty("Background", out var bg))
                        {
                            vocalTarget = bg;
                        }
                        else
                        {
                            vocalTarget = lineElem;
                        }

                        if (isSyllable && vocalTarget.TryGetProperty("Syllables", out var syllablesArray) && syllablesArray.ValueKind == JsonValueKind.Array)
                        {
                            var syllables = new List<SyllableItem>();
                            var sb = new StringBuilder();

                            foreach (var sylElem in syllablesArray.EnumerateArray())
                            {
                                string text = sylElem.TryGetProperty("Text", out var t) ? t.GetString() ?? "" : "";
                                double startSec = sylElem.TryGetProperty("StartTime", out var s) ? s.GetDouble() : 0;
                                double endSec = sylElem.TryGetProperty("EndTime", out var e) ? e.GetDouble() : startSec;
                                bool isPartOfWord = (sylElem.TryGetProperty("IsPartOfWord", out var ip) && ip.GetBoolean())
                                    || (sylElem.TryGetProperty("PartOfWord", out var p) && p.GetBoolean());

                                int startIdx = sb.Length;
                                int len = text.Length;
                                sb.Append(text);

                                syllables.Add(new SyllableItem
                                {
                                    Text = text,
                                    StartTime = TimeSpan.FromSeconds(startSec),
                                    EndTime = TimeSpan.FromSeconds(endSec),
                                    CharStartIndex = startIdx,
                                    CharLength = len
                                });

                                if (!isPartOfWord)
                                {
                                    sb.Append(' ');
                                }
                            }

                            string fullLine = sb.ToString();
                            int leadingSpaces = 0;
                            while (leadingSpaces < fullLine.Length && char.IsWhiteSpace(fullLine[leadingSpaces]))
                            {
                                leadingSpaces++;
                            }
                            if (leadingSpaces > 0)
                            {
                                foreach (var syl in syllables)
                                {
                                    syl.CharStartIndex = Math.Max(0, syl.CharStartIndex - leadingSpaces);
                                }
                            }

                            string trimmedLine = fullLine.Trim();
                            if (string.IsNullOrWhiteSpace(trimmedLine) || syllables.Count == 0) continue;

                            TimeSpan lineStart = vocalTarget.TryGetProperty("StartTime", out var vs) 
                                ? TimeSpan.FromSeconds(vs.GetDouble()) 
                                : syllables[0].StartTime;
                            TimeSpan lineEnd = vocalTarget.TryGetProperty("EndTime", out var ve) 
                                ? TimeSpan.FromSeconds(ve.GetDouble()) 
                                : syllables[^1].EndTime;

                            lines.Add(new LyricsLine(lineStart, trimmedLine)
                            {
                                EndTime = lineEnd,
                                Syllables = syllables
                            });
                        }
                        else
                        {
                            string text = lineElem.TryGetProperty("Text", out var lt) ? lt.GetString() ?? "" : "";
                            double startSec = lineElem.TryGetProperty("StartTime", out var lst) ? lst.GetDouble() : 0;
                            double endSec = lineElem.TryGetProperty("EndTime", out var let) ? let.GetDouble() : startSec;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                lines.Add(new LyricsLine(TimeSpan.FromSeconds(startSec), text.Trim())
                                {
                                    EndTime = TimeSpan.FromSeconds(endSec)
                                });
                            }
                        }
                    }

                    if (lines.Count == 0) continue;

                    lines.Sort((a, b) => a.Time.CompareTo(b.Time));
                    bool hasSyllables = lines.Any(l => l.HasSyllables);

                    var lyricsResult = new LyricsResult
                    {
                        Found = true,
                        IsSynced = true,
                        IsSyllableSynced = hasSyllables,
                        Provider = "SpicyLyrics",
                        Lines = lines,
                        PlainLyrics = string.Join("\n", lines.Select(l => l.Text))
                    };

                    int numericId = Math.Abs(trackId.GetHashCode());
                    if (numericId == 0) numericId = 99990001;

                    return new LyricsCandidate
                    {
                        Id = numericId,
                        TrackName = trackTitle,
                        ArtistName = trackArtist,
                        AlbumName = !string.IsNullOrWhiteSpace(trackAlbum) ? trackAlbum : "Spicy Lyrics",
                        Duration = trackDuration > 0 ? trackDuration : (duration.TotalSeconds > 0 ? duration.TotalSeconds : null),
                        Provider = "SpicyLyrics",
                        IsInstrumental = false,
                        IsSynced = true,
                        IsSyllableSynced = hasSyllables,
                        Score = (hasSyllables ? 2100.0 : 1100.0) + (duration.TotalSeconds > 10 && trackDuration > 0 ? (Math.Abs(trackDuration - duration.TotalSeconds) <= 1.5 ? 400 : (Math.Abs(trackDuration - duration.TotalSeconds) <= 3.0 ? 250 : (Math.Abs(trackDuration - duration.TotalSeconds) <= 5.0 ? 50 : -(Math.Abs(trackDuration - duration.TotalSeconds) * 40)))) : 0),
                        Result = lyricsResult
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query official Spicy Lyrics API for '{Title}'", title);
                return null;
            }
        }
    }
}