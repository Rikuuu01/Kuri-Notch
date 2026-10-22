using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class LyricsService : ILyricsService, IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private readonly ConcurrentDictionary<string, LyricsResult> _cache = new();
        private static readonly Regex LrcLineRegex = new Regex(@"^\[(\d{1,2}):(\d{2})(?:\.(\d{1,3}))?\]\s*(.*)$", RegexOptions.Compiled);

        static LyricsService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NotchWin/1.0 (Windows; Dynamic Notch Music Overlay)");
        }

        public async Task<LyricsResult> GetLyricsAsync(string rawTitle, string rawArtist, TimeSpan duration, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return LyricsResult.NotFound;
            }

            var (cleanTitle, cleanArtist) = CleanTitleAndArtist(rawTitle, rawArtist);
            string cacheKey = $"{cleanArtist.ToLowerInvariant()}:::{cleanTitle.ToLowerInvariant()}";

            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                // Attempt 1: Search using clean track name and artist name
                LrcLibDto? bestMatch = null;
                string primaryArtist = cleanArtist.Split(new[] { ',', '&', ';', '/' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

                if (!string.IsNullOrWhiteSpace(primaryArtist))
                {
                    string url1 = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(primaryArtist)}";
                    bestMatch = await QueryLrcLibAsync(url1, duration, ct);
                }

                // Attempt 2: Fallback query search with combined query
                if (bestMatch == null)
                {
                    string query = string.IsNullOrWhiteSpace(primaryArtist) ? cleanTitle : $"{primaryArtist} {cleanTitle}";
                    string url2 = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
                    bestMatch = await QueryLrcLibAsync(url2, duration, ct);
                }

                // Attempt 3: Search with clean song title alone
                if (bestMatch == null && !string.IsNullOrWhiteSpace(cleanTitle))
                {
                    string url3 = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(cleanTitle)}";
                    bestMatch = await QueryLrcLibAsync(url3, duration, ct);
                }

                if (bestMatch == null)
                {
                    Log.Information("Lyrics not found for '{Title}' by '{Artist}'", cleanTitle, cleanArtist);
                    _cache[cacheKey] = LyricsResult.NotFound;
                    return LyricsResult.NotFound;
                }

                var result = ParseLrcLibDto(bestMatch);
                _cache[cacheKey] = result;
                Log.Information("Lyrics loaded for '{Title}' (Synced={IsSynced}, Lines={Count})", cleanTitle, result.IsSynced, result.Lines.Count);
                return result;
            }
            catch (OperationCanceledException)
            {
                return LyricsResult.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch lyrics from LRCLIB for '{Title}'", cleanTitle);
                return LyricsResult.NotFound;
            }
        }

        private async Task<LrcLibDto?> QueryLrcLibAsync(string url, TimeSpan duration, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var items = JsonSerializer.Deserialize<List<LrcLibDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (items == null || items.Count == 0)
                {
                    return null;
                }

                // If duration is known, try finding the match closest to the expected duration
                if (duration > TimeSpan.FromSeconds(10))
                {
                    LrcLibDto? closest = null;
                    double minDiff = double.MaxValue;

                    foreach (var item in items)
                    {
                        if (item.Duration.HasValue)
                        {
                            double diff = Math.Abs(item.Duration.Value - duration.TotalSeconds);
                            if (diff < minDiff)
                            {
                                minDiff = diff;
                                closest = item;
                            }
                        }
                    }

                    if (closest != null && minDiff <= 6.0)
                    {
                        return closest;
                    }
                }

                // Otherwise pick the first item that contains lyrics
                foreach (var item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item.SyncedLyrics) || !string.IsNullOrWhiteSpace(item.PlainLyrics) || item.Instrumental == true)
                    {
                        return item;
                    }
                }

                return items[0];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Debug(ex, "LRCLIB query failed: {Url}", url);
                return null;
            }
        }

        private static LyricsResult ParseLrcLibDto(LrcLibDto dto)
        {
            if (dto.Instrumental == true)
            {
                return LyricsResult.Instrumental;
            }

            if (!string.IsNullOrWhiteSpace(dto.SyncedLyrics))
            {
                var lines = ParseSyncedLrc(dto.SyncedLyrics);
                if (lines.Count > 0)
                {
                    return new LyricsResult
                    {
                        Found = true,
                        IsSynced = true,
                        Lines = lines,
                        PlainLyrics = dto.PlainLyrics
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.PlainLyrics))
            {
                var plainLines = new List<LyricsLine>();
                var rawLines = dto.PlainLyrics.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var l in rawLines)
                {
                    var trimmed = l.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        plainLines.Add(new LyricsLine(TimeSpan.Zero, trimmed));
                    }
                }

                return new LyricsResult
                {
                    Found = true,
                    IsSynced = false,
                    Lines = plainLines,
                    PlainLyrics = dto.PlainLyrics
                };
            }

            return LyricsResult.NotFound;
        }

        private static List<LyricsLine> ParseSyncedLrc(string lrcText)
        {
            var result = new List<LyricsLine>();
            var rawLines = lrcText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var raw in rawLines)
            {
                var trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var match = LrcLineRegex.Match(trimmed);
                if (match.Success)
                {
                    int min = int.Parse(match.Groups[1].Value);
                    int sec = int.Parse(match.Groups[2].Value);
                    int ms = 0;
                    if (match.Groups[3].Success)
                    {
                        string msStr = match.Groups[3].Value;
                        if (msStr.Length == 2) ms = int.Parse(msStr) * 10;
                        else if (msStr.Length == 3) ms = int.Parse(msStr);
                        else if (msStr.Length == 1) ms = int.Parse(msStr) * 100;
                    }

                    var time = TimeSpan.FromMilliseconds(min * 60000 + sec * 1000 + ms);
                    string text = match.Groups[4].Value.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    result.Add(new LyricsLine(time, text));
                }
            }

            result.Sort((a, b) => a.Time.CompareTo(b.Time));
            return result;
        }

        private static (string Title, string Artist) CleanTitleAndArtist(string rawTitle, string rawArtist)
        {
            string title = rawTitle.Trim();
            string artist = rawArtist?.Trim() ?? string.Empty;

            // Remove YouTube brackets/metadata e.g. "(Official Music Video)", "[Official Lyric Video]", "(MV)", etc.
            title = Regex.Replace(title, @"\s*(\(|\[)(?i:official|lyric|video|mv|audio|remix|full|visualizer|hd|4k|clip)[^\)\]]*(\)|\])", "", RegexOptions.Compiled);

            // Strip trailing pipe comments e.g. "Song Title | prod. by ..." or "Song Title | Album"
            int pipeIdx = title.IndexOf('|');
            if (pipeIdx > 0)
            {
                title = title.Substring(0, pipeIdx).Trim();
            }

            // If title has "Artist - Song", separate them
            int dashIdx = title.IndexOf(" - ");
            if (dashIdx > 0 && dashIdx < title.Length - 3)
            {
                string possibleArtist = title.Substring(0, dashIdx).Trim();
                string possibleTitle = title.Substring(dashIdx + 3).Trim();

                if (!string.IsNullOrWhiteSpace(possibleArtist) && !string.IsNullOrWhiteSpace(possibleTitle))
                {
                    // Clean possibleTitle from trailing features
                    possibleTitle = Regex.Replace(possibleTitle, @"\s*(\(|\[)?(?i:ft\.|feat\.|featuring)[^\)\]]*(\)|\])?", "", RegexOptions.Compiled);
                    return (possibleTitle.Trim(), possibleArtist);
                }
            }

            title = Regex.Replace(title, @"\s*(\(|\[)?(?i:ft\.|feat\.|featuring)[^\)\]]*(\)|\])?", "", RegexOptions.Compiled);
            return (title.Trim(), artist);
        }

        public void Dispose()
        {
            _cache.Clear();
        }
    }
}
