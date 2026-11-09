using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class LrcRedLyricsService : ILrcRedLyricsService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        private static readonly Regex TimestampRegex = new Regex(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
        private static readonly Regex OffsetRegex = new Regex(@"\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static LrcRedLyricsService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("KuriDynamicNotch/1.0 (Windows; Dynamic Notch; contact: admin@notchwin.app)");
        }

        public async Task<List<LyricsCandidate>> GetCandidatesAsync(string cleanTitle, string cleanArtist, TimeSpan duration, CancellationToken ct = default)
        {
            var candidates = new List<LyricsCandidate>();
            if (string.IsNullOrWhiteSpace(cleanTitle))
            {
                return candidates;
            }

            try
            {
                string query = string.IsNullOrWhiteSpace(cleanArtist)
                    ? cleanTitle.Trim()
                    : $"{cleanTitle.Trim()} {cleanArtist.Trim()}";

                var hits = await SearchHitsAsync(query, ct);
                if ((hits == null || hits.Count == 0) && !string.IsNullOrWhiteSpace(cleanArtist))
                {
                    hits = await SearchHitsAsync(cleanTitle.Trim(), ct);
                }

                if (hits == null || hits.Count == 0)
                {
                    return candidates;
                }

                // Score and rank search hits
                var scoredHits = new List<(LrcRedSearchHit Hit, double Score)>();
                foreach (var hit in hits)
                {
                    if (string.IsNullOrWhiteSpace(hit.Isrc)) continue;
                    double score = ScoreHit(hit, cleanTitle, cleanArtist, duration);
                    if (score > 0)
                    {
                        scoredHits.Add((hit, score));
                    }
                }

                var topHits = scoredHits
                    .OrderByDescending(x => x.Score)
                    .Take(3)
                    .Select(x => x.Hit)
                    .ToList();

                foreach (var hit in topHits)
                {
                    ct.ThrowIfCancellationRequested();
                    var candidate = await FetchCandidateForHitAsync(hit, cleanTitle, cleanArtist, duration, ct);
                    if (candidate != null && candidate.Result.Found)
                    {
                        candidates.Add(candidate);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Task was canceled - gracefully return what we have
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to fetch lyrics candidates from lrc.red for '{Title}'", cleanTitle);
            }

            return candidates;
        }

        private static async Task<List<LrcRedSearchHit>> SearchHitsAsync(string query, CancellationToken ct)
        {
            string url = $"https://lrc.red/search.json?q={Uri.EscapeDataString(query)}";
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new List<LrcRedSearchHit>();
            }

            var jsonStr = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(jsonStr))
            {
                return new List<LrcRedSearchHit>();
            }

            using var doc = JsonDocument.Parse(jsonStr);
            if (!doc.RootElement.TryGetProperty("hits", out var hitsElem) || hitsElem.ValueKind != JsonValueKind.Array)
            {
                return new List<LrcRedSearchHit>();
            }

            var list = new List<LrcRedSearchHit>();
            foreach (var item in hitsElem.EnumerateArray())
            {
                string isrc = item.TryGetProperty("isrc", out var pIsrc) ? pIsrc.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(isrc)) continue;

                string title = item.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? string.Empty : string.Empty;
                string artist = item.TryGetProperty("artist", out var pArtist) ? pArtist.GetString() ?? string.Empty : string.Empty;
                string album = item.TryGetProperty("album", out var pAlbum) ? pAlbum.GetString() ?? string.Empty : string.Empty;
                double? hitDuration = item.TryGetProperty("duration", out var pDur) && pDur.TryGetDouble(out double d) ? d : null;

                list.Add(new LrcRedSearchHit
                {
                    Isrc = isrc,
                    Title = title,
                    Artist = artist,
                    Album = album,
                    Duration = hitDuration
                });
            }

            return list;
        }

        private static async Task<LyricsCandidate?> FetchCandidateForHitAsync(LrcRedSearchHit hit, string cleanTitle, string cleanArtist, TimeSpan duration, CancellationToken ct)
        {
            int candidateId = 80000000 + (Math.Abs(hit.Isrc.GetHashCode()) % 10000000);

            // 1. Prioritaskan TTML (tersinkronisasi kata / Apple Music style)
            string ttmlUrl = $"https://lrc.red/s/{hit.Isrc}.ttml";
            try
            {
                using var ttmlResp = await _httpClient.GetAsync(ttmlUrl, ct);
                if (ttmlResp.IsSuccessStatusCode)
                {
                    string ttmlContent = await ttmlResp.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(ttmlContent) && ttmlContent.Contains("<tt"))
                    {
                        var candidate = TtmlLyricsService.ParseTtmlToCandidate(
                            ttmlContent,
                            string.IsNullOrWhiteSpace(hit.Title) ? cleanTitle : hit.Title,
                            string.IsNullOrWhiteSpace(hit.Artist) ? cleanArtist : hit.Artist,
                            duration);

                        if (candidate != null && candidate.Result.Found)
                        {
                            candidate.Id = candidateId;
                            candidate.Provider = "LrcRed";
                            candidate.AlbumName = string.IsNullOrWhiteSpace(hit.Album) ? "lrc.red" : hit.Album;
                            candidate.Result.Provider = "LrcRed";
                            candidate.Duration = hit.Duration ?? duration.TotalSeconds;

                            double baseScore = candidate.IsSyllableSynced ? 1980.0 : 980.0;
                            candidate.Score = baseScore + ComputeMatchBonus(hit, cleanTitle, duration);

                            Log.Information("Loaded lrc.red TTML lyrics for '{Title}' ({Isrc}, Syllables: {HasSyl})",
                                hit.Title, hit.Isrc, candidate.IsSyllableSynced);
                            return candidate;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Debug(ex, "Failed to fetch TTML for {Isrc} from lrc.red", hit.Isrc);
            }

            // 2. Fallback ke file .lrc jika TTML tidak tersedia
            string lrcUrl = $"https://lrc.red/s/{hit.Isrc}.lrc";
            try
            {
                using var lrcResp = await _httpClient.GetAsync(lrcUrl, ct);
                if (lrcResp.IsSuccessStatusCode)
                {
                    string lrcContent = await lrcResp.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(lrcContent))
                    {
                        var lines = ParseSyncedLrc(lrcContent, out int offsetMs);
                        if (lines.Count > 0)
                        {
                            var result = new LyricsResult
                            {
                                Found = true,
                                IsSynced = true,
                                IsSyllableSynced = false,
                                Provider = "LrcRed",
                                BaseOffsetMs = offsetMs,
                                Lines = lines,
                                PlainLyrics = string.Join("\n", lines.Select(l => l.Text))
                            };

                            return new LyricsCandidate
                            {
                                Id = candidateId,
                                TrackName = string.IsNullOrWhiteSpace(hit.Title) ? cleanTitle : hit.Title,
                                ArtistName = string.IsNullOrWhiteSpace(hit.Artist) ? cleanArtist : hit.Artist,
                                AlbumName = string.IsNullOrWhiteSpace(hit.Album) ? "lrc.red" : hit.Album,
                                Duration = hit.Duration ?? duration.TotalSeconds,
                                Provider = "LrcRed",
                                IsInstrumental = false,
                                IsSynced = true,
                                IsSyllableSynced = false,
                                Score = 980.0 + ComputeMatchBonus(hit, cleanTitle, duration),
                                Result = result
                            };
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Debug(ex, "Failed to fetch LRC for {Isrc} from lrc.red", hit.Isrc);
            }

            return null;
        }

        private static double ScoreHit(LrcRedSearchHit hit, string expectedTitle, string expectedArtist, TimeSpan expectedDuration)
        {
            double score = 500.0; // Base score for hit existence

            // Duration closeness
            if (expectedDuration.TotalSeconds > 10 && hit.Duration.HasValue)
            {
                double diff = Math.Abs(hit.Duration.Value - expectedDuration.TotalSeconds);
                if (diff <= 1.5) score += 350;
                else if (diff <= 3.0) score += 200;
                else if (diff <= 6.0) score += 100;
                else if (diff <= 12.0) score += 20;
                else score -= (diff * 20);
            }

            // Title matching and penalties
            string hitTitle = (hit.Title ?? "").ToLowerInvariant();
            string expTitle = expectedTitle.ToLowerInvariant();

            bool expIsLive = expTitle.Contains("live");
            bool expIsRemix = expTitle.Contains("remix");
            bool expIsAcoustic = expTitle.Contains("acoustic");

            if (!expIsLive && hitTitle.Contains("live")) score -= 500;
            if (!expIsRemix && (hitTitle.Contains("remix") || hitTitle.Contains("dubstep") || hitTitle.Contains("club mix"))) score -= 500;
            if (!expIsAcoustic && hitTitle.Contains("acoustic")) score -= 400;
            if (hitTitle.Contains("karaoke") || hitTitle.Contains("tribute") || hitTitle.Contains("instrumental cover")) score -= 600;

            if (string.Equals(hitTitle, expTitle, StringComparison.OrdinalIgnoreCase))
            {
                score += 200;
            }

            return score;
        }

        private static double ComputeMatchBonus(LrcRedSearchHit hit, string expectedTitle, TimeSpan expectedDuration)
        {
            double bonus = 0;
            if (expectedDuration.TotalSeconds > 10 && hit.Duration.HasValue)
            {
                double diff = Math.Abs(hit.Duration.Value - expectedDuration.TotalSeconds);
                if (diff <= 1.5) bonus += 350;
                else if (diff <= 3.0) bonus += 200;
                else if (diff <= 5.0) bonus += 50;
                else if (diff <= 10.0) bonus -= (diff * 30);
                else bonus -= (diff * 60 + 500); // Drastically penalize mismatching edits!
            }

            if (string.Equals((hit.Title ?? "").Trim(), expectedTitle.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                bonus += 150;
            }

            return bonus;
        }

        private static List<LyricsLine> ParseSyncedLrc(string lrcText, out int offsetMs)
        {
            offsetMs = 0;
            var result = new List<LyricsLine>();
            var rawLines = lrcText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var raw in rawLines)
            {
                var trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var offsetMatch = OffsetRegex.Match(trimmed);
                if (offsetMatch.Success && int.TryParse(offsetMatch.Groups[1].Value, out int parsedOffset))
                {
                    offsetMs = parsedOffset;
                    continue;
                }

                var timestampMatches = TimestampRegex.Matches(trimmed);
                if (timestampMatches.Count == 0) continue;

                string text = TimestampRegex.Replace(trimmed, "").Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                foreach (Match match in timestampMatches)
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
                    result.Add(new LyricsLine(time, text));
                }
            }

            result.Sort((a, b) => a.Time.CompareTo(b.Time));
            return result;
        }

        private class LrcRedSearchHit
        {
            public string Isrc { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Artist { get; set; } = string.Empty;
            public string Album { get; set; } = string.Empty;
            public double? Duration { get; set; }
        }
    }
}
