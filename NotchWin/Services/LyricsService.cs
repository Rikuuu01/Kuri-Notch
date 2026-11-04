using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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

        private readonly ConcurrentDictionary<string, LyricsFetchResult> _cache = new();
        private static readonly Regex TimestampRegex = new Regex(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
        private static readonly Regex OffsetRegex = new Regex(@"\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotchWin",
            "LyricsCache");

        private static string GetCacheFilePath(string cacheKey)
        {
            using var sha256 = SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(cacheKey);
            var hash = Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
            // v2: cache menyimpan daftar kandidat, bukan satu LyricsResult (cache v1 diabaikan).
            return Path.Combine(_cacheDirectory, $"v2-{hash}.json");
        }

        static LyricsService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NotchWin/1.0 (Windows; Dynamic Notch Music Overlay)");
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to create LyricsCache directory");
            }
        }

        public async Task<LyricsFetchResult> GetLyricsCandidatesAsync(string rawTitle, string rawArtist, TimeSpan duration, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return LyricsFetchResult.Empty;
            }

            var (cleanTitle, cleanArtist) = CleanTitleAndArtist(rawTitle, rawArtist);
            string cacheKey = $"{cleanArtist.ToLowerInvariant()}:::{cleanTitle.ToLowerInvariant()}";
            string diskPath = GetCacheFilePath(cacheKey);

            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            if (File.Exists(diskPath))
            {
                try
                {
                    var cachedJson = await File.ReadAllTextAsync(diskPath, ct);
                    var diskResult = JsonSerializer.Deserialize<LyricsFetchResult>(cachedJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (diskResult != null && diskResult.Candidates.Count > 0)
                    {
                        if (_cache.Count > 20) _cache.Clear();
                        _cache[cacheKey] = diskResult;
                        Log.Information("Loaded {Count} lyrics candidates from persistent disk cache for '{Title}'", diskResult.Candidates.Count, cleanTitle);
                        return diskResult;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.Debug(ex, "Failed to read cached lyrics file from {Path}", diskPath);
                }
            }

            try
            {
                string primaryArtist = cleanArtist.Split(new[] { ',', '&', ';', '/' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                var merged = new Dictionary<string, LyricsCandidate>(StringComparer.OrdinalIgnoreCase);

                void Merge(LrcLibDto dto, double score)
                {
                    if (dto == null) return;

                    string key = GetCandidateKey(dto);
                    if (merged.TryGetValue(key, out var existing) && existing.Score >= score)
                    {
                        return;
                    }

                    var parsed = ParseLrcLibDto(dto);
                    if (!parsed.Found) return;

                    merged[key] = new LyricsCandidate
                    {
                        Id = dto.Id,
                        TrackName = dto.TrackName ?? string.Empty,
                        ArtistName = dto.ArtistName ?? string.Empty,
                        AlbumName = dto.AlbumName,
                        Duration = dto.Duration,
                        IsInstrumental = parsed.IsInstrumental,
                        IsSynced = parsed.IsSynced,
                        Score = score,
                        Result = parsed
                    };
                }

                // Exact /api/get dan search #1 berjalan paralel. Search #1 tetap dijalankan
                // walau exact sudah synced agar pemilih versi punya alternatif.
                Task<LrcLibDto?> exactTask = Task.FromResult<LrcLibDto?>(null);
                Task<List<(LrcLibDto Dto, double Score)>> searchTask = Task.FromResult(new List<(LrcLibDto, double)>());
                if (!string.IsNullOrWhiteSpace(primaryArtist) && duration.TotalSeconds > 10)
                {
                    exactTask = GetExactLrcLibAsync(cleanTitle, primaryArtist, duration, ct);
                    string url1 = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(primaryArtist)}";
                    searchTask = QueryLrcLibCandidatesAsync(url1, cleanTitle, primaryArtist, duration, ct);
                }

                await Task.WhenAll(exactTask, searchTask);

                var exact = exactTask.Result;
                var search1 = searchTask.Result;

                if (exact != null)
                {
                    Merge(exact, ScoreCandidate(exact, cleanTitle, primaryArtist, duration));
                }
                foreach (var item in search1)
                {
                    Merge(item.Dto, item.Score);
                }

                bool exactIsAuto = exact != null &&
                    (!string.IsNullOrWhiteSpace(exact.SyncedLyrics) || exact.Instrumental == true);
                LrcLibDto? search1Best = search1.OrderByDescending(x => x.Score).Select(x => x.Dto).FirstOrDefault();

                // Strategi #2/#3 hanya dijalankan bila exact dan search #1 belum menemukan apa pun,
                // mempertahankan alur lama dan menghindari request tambahan.
                LrcLibDto? search2Best = null;
                LrcLibDto? search3Best = null;
                if (!exactIsAuto && search1Best == null)
                {
                    string query = string.IsNullOrWhiteSpace(primaryArtist) ? cleanTitle : $"{primaryArtist} {cleanTitle}";
                    string url2 = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
                    var search2 = await QueryLrcLibCandidatesAsync(url2, cleanTitle, primaryArtist, duration, ct);
                    foreach (var item in search2)
                    {
                        Merge(item.Dto, item.Score);
                    }
                    search2Best = search2.OrderByDescending(x => x.Score).Select(x => x.Dto).FirstOrDefault();

                    // Bila artist kosong, query #2 dan #3 identik (judul saja).
                    if (search2Best == null && !string.IsNullOrWhiteSpace(primaryArtist) && !string.IsNullOrWhiteSpace(cleanTitle))
                    {
                        string url3 = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(cleanTitle)}";
                        var search3 = await QueryLrcLibCandidatesAsync(url3, cleanTitle, primaryArtist, duration, ct);
                        foreach (var item in search3)
                        {
                            Merge(item.Dto, item.Score);
                        }
                        search3Best = search3.OrderByDescending(x => x.Score).Select(x => x.Dto).FirstOrDefault();
                    }
                }

                // Presedensi Auto sama seperti sebelumnya: exact synced/instrumental menang,
                // lalu hasil search terbaik, dan exact plain hanya sebagai upaya terakhir.
                LrcLibDto? autoDto = exactIsAuto ? exact
                    : search1Best ?? search2Best ?? search3Best ?? exact;

                if (merged.Count == 0 || autoDto == null)
                {
                    Log.Information("Lyrics not found for '{Title}' by '{Artist}'", cleanTitle, cleanArtist);
                    if (_cache.Count > 20) _cache.Clear();
                    _cache[cacheKey] = LyricsFetchResult.Empty;
                    return LyricsFetchResult.Empty;
                }

                var ordered = merged.Values
                    .OrderByDescending(c => c.Score)
                    .ThenByDescending(c => c.IsSynced)
                    .ThenBy(c => c.Id)
                    .ToList();

                string autoKey = GetCandidateKey(autoDto);
                var autoCandidate = merged.TryGetValue(autoKey, out var resolvedAuto) ? resolvedAuto : ordered[0];

                const int maxCandidates = 20;
                var selected = ordered.Take(maxCandidates).ToList();
                if (!selected.Contains(autoCandidate) && selected.Count > 0)
                {
                    selected[selected.Count - 1] = autoCandidate;
                }

                var result = new LyricsFetchResult
                {
                    Candidates = selected,
                    AutoCandidateId = autoCandidate.Id
                };

                if (_cache.Count > 20) _cache.Clear();
                _cache[cacheKey] = result;

                // Persist to offline disk cache asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(result);
                        await File.WriteAllTextAsync(diskPath, json);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to write lyrics to disk cache: {Path}", diskPath);
                    }
                });

                Log.Information("Lyrics candidates loaded for '{Title}' ({Count} versions, AutoId={AutoId}, AutoSynced={IsSynced})",
                    cleanTitle, result.Candidates.Count, result.AutoCandidateId, autoCandidate.IsSynced);
                return result;
            }
            catch (OperationCanceledException)
            {
                return LyricsFetchResult.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch lyrics from LRCLIB for '{Title}'", cleanTitle);
                return LyricsFetchResult.Empty;
            }
        }

        private async Task<LrcLibDto?> GetExactLrcLibAsync(string trackName, string artistName, TimeSpan duration, CancellationToken ct)
        {
            try
            {
                string durParam = duration.TotalSeconds > 10 ? $"&duration={(int)Math.Round(duration.TotalSeconds)}" : "";
                string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artistName)}{durParam}";
                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var item = JsonSerializer.Deserialize<LrcLibDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (item != null && (!string.IsNullOrWhiteSpace(item.SyncedLyrics) || !string.IsNullOrWhiteSpace(item.PlainLyrics) || item.Instrumental == true))
                {
                    return item;
                }
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Debug(ex, "LRCLIB exact get failed for {TrackName} - {ArtistName}", trackName, artistName);
                return null;
            }
        }

        private async Task<List<(LrcLibDto Dto, double Score)>> QueryLrcLibCandidatesAsync(string url, string expectedTitle, string expectedArtist, TimeSpan duration, CancellationToken ct)
        {
            var results = new List<(LrcLibDto Dto, double Score)>();
            try
            {
                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return results;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var items = JsonSerializer.Deserialize<List<LrcLibDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (items == null)
                {
                    return results;
                }

                foreach (var item in items)
                {
                    double score = ScoreCandidate(item, expectedTitle, expectedArtist, duration);
                    if (score > 0)
                    {
                        results.Add((item, score));
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Debug(ex, "LRCLIB query failed: {Url}", url);
            }
            return results;
        }

        private static string GetCandidateKey(LrcLibDto dto)
        {
            return dto.Id > 0 ? $"id:{dto.Id}" : $"{dto.TrackName}|{dto.ArtistName}|{dto.Duration}";
        }

        private static double ScoreCandidate(LrcLibDto item, string expectedTitle, string expectedArtist, TimeSpan expectedDuration)
        {
            double score = 0;

            // 1. Strongly prioritize Synced Lyrics over Plain Lyrics (+1000 vs +50)
            bool hasSynced = !string.IsNullOrWhiteSpace(item.SyncedLyrics);
            bool hasPlain = !string.IsNullOrWhiteSpace(item.PlainLyrics);
            if (hasSynced) score += 1000;
            else if (hasPlain) score += 50;
            else if (item.Instrumental == true) score += 100;
            else return -1000;

            // 2. Duration closeness
            if (expectedDuration.TotalSeconds > 10 && item.Duration.HasValue)
            {
                double diff = Math.Abs(item.Duration.Value - expectedDuration.TotalSeconds);
                if (diff <= 1.5) score += 350;
                else if (diff <= 3.0) score += 200;
                else if (diff <= 6.0) score += 100;
                else if (diff <= 12.0) score += 20;
                else score -= (diff * 20); // Heavily penalize large duration differences
            }

            // 3. Title matching and penalty for unwanted version tags (unless expectedTitle had them)
            string itemTitle = (item.TrackName ?? "").ToLowerInvariant();
            string expTitle = expectedTitle.ToLowerInvariant();

            bool expIsLive = expTitle.Contains("live");
            bool expIsRemix = expTitle.Contains("remix");
            bool expIsAcoustic = expTitle.Contains("acoustic");

            if (!expIsLive && itemTitle.Contains("live")) score -= 500;
            if (!expIsRemix && (itemTitle.Contains("remix") || itemTitle.Contains("dubstep") || itemTitle.Contains("club mix"))) score -= 500;
            if (!expIsAcoustic && itemTitle.Contains("acoustic")) score -= 400;
            if (itemTitle.Contains("karaoke") || itemTitle.Contains("tribute") || itemTitle.Contains("instrumental cover")) score -= 600;

            // 4. Exact title similarity
            if (string.Equals(itemTitle, expTitle, StringComparison.OrdinalIgnoreCase)) score += 150;

            return score;
        }

        private static LyricsResult ParseLrcLibDto(LrcLibDto dto)
        {
            if (dto.Instrumental == true)
            {
                return LyricsResult.Instrumental;
            }

            if (!string.IsNullOrWhiteSpace(dto.SyncedLyrics))
            {
                var lines = ParseSyncedLrc(dto.SyncedLyrics, out int offsetMs);
                if (lines.Count > 0)
                {
                    return new LyricsResult
                    {
                        Found = true,
                        IsSynced = true,
                        BaseOffsetMs = offsetMs,
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

                // Extract clean text by removing all [mm:ss.xx] / [mm:ss:xx] timestamps
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
