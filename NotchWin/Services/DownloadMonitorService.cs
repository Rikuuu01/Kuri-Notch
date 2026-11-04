using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using NotchWin.Interop;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    /// <summary>
    /// Detects in-progress downloads by watching the partial files browsers and
    /// other apps create next to their final target (.crdownload, .part, ...).
    /// All state mutation happens on the UI dispatcher; the watcher only posts.
    /// </summary>
    public sealed class DownloadMonitorService : IDownloadMonitorService
    {
        private sealed class TrackedDownload
        {
            public required string PartialPath { get; set; }
            public required string FileName { get; set; }
            public required string DirectoryPath { get; init; }
            public required DownloadSource Source { get; set; }
            public long BytesReceived { get; set; }
            public double BytesPerSecond { get; set; }
            public long? TotalBytes { get; set; }
            public DateTime StartedAt { get; init; }
            public DateTime LastGrowthAt { get; set; }
            public DateTime LastSampleAt { get; set; }
            public DownloadState State { get; set; } = DownloadState.Downloading;
            public bool IsStale { get; set; }
        }

        private static readonly (string Extension, DownloadSource Source)[] PartialExtensions =
        {
            (".crdownload", DownloadSource.Chromium),
            (".part", DownloadSource.Firefox),
            (".partial", DownloadSource.Legacy),
            (".opdownload", DownloadSource.Opera),
            (".download", DownloadSource.Generic),
        };

        private static readonly EnumerationOptions ScanOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };

        private const int MaxRoots = 12;
        private static readonly TimeSpan PauseAfter = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

        private readonly Dictionary<string, TrackedDownload> _items = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly List<string> _roots = new();
        private readonly List<string> _historyDatabases = new();

        private Dispatcher? _dispatcher;
        private DispatcherTimer? _timer;
        private IReadOnlyList<DownloadSnapshot> _snapshot = Array.Empty<DownloadSnapshot>();

        private bool _isStarted;
        private bool _isSuspended;
        private bool _disposed;

        public IReadOnlyList<DownloadSnapshot> ActiveDownloads => _snapshot;

        public event EventHandler? DownloadsChanged;
        public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

        public void Start()
        {
            if (_disposed) return;
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            if (dispatcher.CheckAccess()) StartCore(dispatcher);
            else dispatcher.Invoke(() => StartCore(dispatcher));
        }

        private void StartCore(Dispatcher dispatcher)
        {
            if (_isStarted || _disposed) return;

            _dispatcher = dispatcher;
            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            _isStarted = true;
            _isSuspended = false;

            DiscoverRoots();
            CreateWatchers();
            ScanExistingFiles();
            UpdateTimerState();

            Log.Information("DownloadMonitor started. Watching {Count} folder(s): {Roots}",
                _roots.Count, string.Join("; ", _roots));
        }

        public void Stop()
        {
            if (!_isStarted) return;
            _isStarted = false;
            _isSuspended = false;

            DisposeWatchers();
            _timer?.Stop();

            if (_items.Count > 0)
            {
                _items.Clear();
                Publish();
            }

            Log.Information("DownloadMonitor stopped");
        }

        public void Suspend()
        {
            if (!_isStarted || _isSuspended) return;
            _isSuspended = true;
            DisposeWatchers();
            _timer?.Stop();
            Log.Information("DownloadMonitor suspended");
        }

        public void Resume()
        {
            if (!_isStarted || !_isSuspended) return;
            _isSuspended = false;
            CreateWatchers();
            ScanExistingFiles();
            UpdateTimerState();
            Log.Information("DownloadMonitor resumed");
        }

        #region Folder discovery

        private void DiscoverRoots()
        {
            _roots.Clear();

            AddRoot(NativeMethods.GetKnownDownloadsFolder());

            var browserRoots = new List<string>();
            try { CollectChromiumDownloadDirs(browserRoots); }
            catch (Exception ex) { Log.Debug(ex, "DownloadMonitor: Chromium preference discovery failed"); }
            try { CollectFirefoxDownloadDirs(browserRoots); }
            catch (Exception ex) { Log.Debug(ex, "DownloadMonitor: Firefox preference discovery failed"); }

            foreach (string root in browserRoots)
            {
                if (_roots.Count >= MaxRoots) break;
                AddRoot(root);
            }

            CollectChromiumHistoryDatabases();
        }

        /// <summary>
        /// Locates Chromium/Edge/Brave/Vivaldi/Opera History databases. Only the
        /// downloads table is read from them to obtain the total size of active
        /// downloads; the partial file alone never reveals it.
        /// </summary>
        private void CollectChromiumHistoryDatabases()
        {
            _historyDatabases.Clear();

            foreach (string userData in GetChromiumUserDataDirectories())
            {
                try
                {
                    if (!Directory.Exists(userData)) continue;

                    string direct = Path.Combine(userData, "History");
                    if (File.Exists(direct)) _historyDatabases.Add(direct);

                    foreach (string profile in Directory.EnumerateDirectories(userData))
                    {
                        if (Path.GetFileName(profile).Equals("System Profile", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string history = Path.Combine(profile, "History");
                        if (File.Exists(history)) _historyDatabases.Add(history);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DownloadMonitor: failed to inspect history database under {Dir}", userData);
                }
            }
        }

        private void AddRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
                if (string.IsNullOrWhiteSpace(expanded)) return;

                string full = Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(full)) return;
                if (_roots.Any(r => string.Equals(r, full, StringComparison.OrdinalIgnoreCase))) return;

                _roots.Add(full);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DownloadMonitor: skipping invalid folder {Path}", path);
            }
        }

        private static IEnumerable<string> GetChromiumUserDataDirectories()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            yield return Path.Combine(local, "Google", "Chrome", "User Data");
            yield return Path.Combine(local, "Microsoft", "Edge", "User Data");
            yield return Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data");
            yield return Path.Combine(local, "Vivaldi", "User Data");
            yield return Path.Combine(roaming, "Opera Software", "Opera Stable");
            yield return Path.Combine(roaming, "Opera Software", "Opera GX Stable");
        }

        private static void CollectChromiumDownloadDirs(List<string> results)
        {
            foreach (string userData in GetChromiumUserDataDirectories())
            {
                try
                {
                    if (!Directory.Exists(userData)) continue;

                    // Opera stores Preferences directly in its profile folder.
                    string direct = Path.Combine(userData, "Preferences");
                    if (File.Exists(direct)) TryReadChromiumPreference(direct, results);

                    foreach (string profile in Directory.EnumerateDirectories(userData))
                    {
                        if (Path.GetFileName(profile).Equals("System Profile", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string prefPath = Path.Combine(profile, "Preferences");
                        if (File.Exists(prefPath)) TryReadChromiumPreference(prefPath, results);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DownloadMonitor: failed to inspect {Dir}", userData);
                }
            }
        }

        private static void TryReadChromiumPreference(string prefPath, List<string> results)
        {
            try
            {
                using var stream = new FileStream(prefPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var doc = JsonDocument.Parse(stream);

                if (doc.RootElement.TryGetProperty("download", out var download) &&
                    download.TryGetProperty("default_directory", out var dirElement) &&
                    dirElement.ValueKind == JsonValueKind.String)
                {
                    string? dir = dirElement.GetString();
                    if (!string.IsNullOrWhiteSpace(dir)) results.Add(dir);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DownloadMonitor: could not read browser preference {Path}", prefPath);
            }
        }

        private static void CollectFirefoxDownloadDirs(List<string> results)
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string profilesRoot = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
            if (!Directory.Exists(profilesRoot)) return;

            foreach (string profile in Directory.EnumerateDirectories(profilesRoot))
            {
                string prefsPath = Path.Combine(profile, "prefs.js");
                if (!File.Exists(prefsPath)) continue;

                try
                {
                    string content = File.ReadAllText(prefsPath);
                    if (!Regex.IsMatch(content, @"user_pref\(\s*""browser\.download\.folderList""\s*,\s*2\s*\)"))
                        continue;

                    var match = Regex.Match(content, @"user_pref\(\s*""browser\.download\.dir""\s*,\s*""((?:[^""\\]|\\.)*)""\s*\)");
                    if (match.Success)
                    {
                        string dir = Regex.Unescape(match.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(dir)) results.Add(dir);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DownloadMonitor: could not read Firefox preferences {Path}", prefsPath);
                }
            }
        }

        #endregion

        #region Watchers and scanning

        private void CreateWatchers()
        {
            foreach (string root in _roots)
            {
                try
                {
                    var watcher = new FileSystemWatcher(root)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        Filter = "*",
                        InternalBufferSize = 64 * 1024,
                        EnableRaisingEvents = false
                    };

                    watcher.Created += OnWatcherCreated;
                    watcher.Renamed += OnWatcherRenamed;
                    watcher.Deleted += OnWatcherDeleted;
                    watcher.Error += OnWatcherError;
                    watcher.EnableRaisingEvents = true;

                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "DownloadMonitor: could not watch {Root}", root);
                }
            }
        }

        private void DisposeWatchers()
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= OnWatcherCreated;
                    watcher.Renamed -= OnWatcherRenamed;
                    watcher.Deleted -= OnWatcherDeleted;
                    watcher.Error -= OnWatcherError;
                    watcher.Dispose();
                }
                catch
                {
                }
            }

            _watchers.Clear();
        }

        private void ScanExistingFiles()
        {
            foreach (string root in _roots)
            {
                try
                {
                    foreach (string file in Directory.EnumerateFiles(root, "*", ScanOptions))
                    {
                        if (TryMapPartial(file, out _)) Upsert(file, isNew: true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DownloadMonitor: scan failed for {Root}", root);
                }
            }
        }

        private void OnWatcherCreated(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            Post(() => { if (TryMapPartial(path, out _)) Upsert(path, isNew: true); });
        }

        private void OnWatcherRenamed(object sender, RenamedEventArgs e)
        {
            string oldPath = e.OldFullPath;
            string newPath = e.FullPath;
            Post(() => HandleRenamed(oldPath, newPath));
        }

        private void OnWatcherDeleted(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            Post(() => HandleDeleted(path));
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Log.Warning(e.GetException(), "DownloadMonitor: watcher buffer error, rebuilding watchers");
            Post(RebuildWatchers);
        }

        private void RebuildWatchers()
        {
            if (!_isStarted || _isSuspended) return;
            DisposeWatchers();
            CreateWatchers();
            ScanExistingFiles();
            UpdateTimerState();
        }

        #endregion

        #region State machine

        private static bool TryMapPartial(string path, out DownloadSource source)
        {
            source = DownloadSource.Generic;
            string extension = Path.GetExtension(path);
            foreach (var entry in PartialExtensions)
            {
                if (extension.Equals(entry.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    source = entry.Source;
                    return true;
                }
            }

            return false;
        }

        private static string GetTargetName(string partialPath) => Path.GetFileNameWithoutExtension(partialPath);

        private void Upsert(string path, bool isNew)
        {
            if (_items.ContainsKey(path)) return;

            long length = 0;
            DateTime created = DateTime.UtcNow;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return;
                length = info.Length;
                created = info.CreationTimeUtc;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DownloadMonitor: could not read {Path}", path);
                return;
            }

            if (!TryMapPartial(path, out var source)) return;

            var now = DateTime.UtcNow;
            _items[path] = new TrackedDownload
            {
                PartialPath = path,
                FileName = GetTargetName(path),
                DirectoryPath = Path.GetDirectoryName(path) ?? string.Empty,
                Source = source,
                BytesReceived = length,
                StartedAt = created,
                LastGrowthAt = now,
                LastSampleAt = now,
                State = DownloadState.Downloading
            };

            if (isNew) Log.Debug("DownloadMonitor: tracking {File} ({Source})", GetTargetName(path), source);

            UpdateTimerState();
            Publish();
        }

        private void HandleRenamed(string oldPath, string newPath)
        {
            bool oldIsPartial = TryMapPartial(oldPath, out _);
            bool newIsPartial = TryMapPartial(newPath, out var newSource);

            // Chromium creates "<guid>.tmp" and renames it to "<name>.crdownload"
            // right at the start; the tmp file itself is intentionally ignored.
            if (!oldIsPartial)
            {
                if (newIsPartial) Upsert(newPath, isNew: true);
                return;
            }

            if (_items.TryGetValue(oldPath, out var tracked))
            {
                if (newIsPartial)
                {
                    // Chrome renames "Unconfirmed N.crdownload" to the real name
                    // while the download is still in progress.
                    _items.Remove(oldPath);
                    tracked.PartialPath = newPath;
                    tracked.FileName = GetTargetName(newPath);
                    tracked.Source = newSource;
                    _items[newPath] = tracked;
                }
                else
                {
                    _items.Remove(oldPath);
                    // Use the actual final name: "Unconfirmed N.crdownload" becomes
                    // the real file name only at this rename.
                    tracked.FileName = Path.GetFileName(newPath);
                    RaiseCompleted(tracked);
                }

                UpdateTimerState();
                Publish();
                return;
            }

            // Very short download: partial file was gone before we tracked it.
            if (!newIsPartial && File.Exists(newPath))
            {
                RaiseCompleted(Path.GetFileName(newPath), Path.GetDirectoryName(newPath) ?? string.Empty);
            }
        }

        private void HandleDeleted(string path)
        {
            if (_items.TryGetValue(path, out var tracked))
            {
                _items.Remove(path);
                Log.Debug("DownloadMonitor: cancelled {File}", tracked.FileName);
                UpdateTimerState();
                Publish();
                return;
            }

            // Some apps delete the partial and create the final file separately.
            if (TryMapPartial(path, out _))
            {
                string finalPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, GetTargetName(path));
                if (File.Exists(finalPath))
                {
                    RaiseCompleted(Path.GetFileName(finalPath), Path.GetDirectoryName(finalPath) ?? string.Empty);
                }
            }
        }

        private void RaiseCompleted(TrackedDownload tracked) => RaiseCompleted(tracked.FileName, tracked.DirectoryPath);

        private void RaiseCompleted(string fileName, string directoryPath)
        {
            Log.Information("DownloadMonitor: completed {File}", fileName);
            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs(fileName, directoryPath));
        }

        /// <summary>
        /// Fills TotalBytes for Chromium/Opera items from the browser History
        /// database. Runs only while some tracked browser download still lacks
        /// its total size, so steady state does not touch any database.
        /// </summary>
        private void RefreshBrowserProgress()
        {
            if (_historyDatabases.Count == 0) return;

            bool needsTotals = false;
            foreach (var item in _items.Values)
            {
                if ((item.Source == DownloadSource.Chromium || item.Source == DownloadSource.Opera)
                    && item.TotalBytes == null)
                {
                    needsTotals = true;
                    break;
                }
            }

            if (!needsTotals) return;

            var totals = ChromiumDownloadProgressReader.ReadTotals(_historyDatabases);
            if (totals.Count == 0) return;

            foreach (var item in _items.Values)
            {
                if (item.TotalBytes != null) continue;
                if (item.Source != DownloadSource.Chromium && item.Source != DownloadSource.Opera) continue;

                string partialKey = ChromiumDownloadProgressReader.NormalizePath(item.PartialPath);
                string finalKey = ChromiumDownloadProgressReader.NormalizePath(
                    Path.Combine(item.DirectoryPath, item.FileName));

                long total = 0;
                bool found = (partialKey.Length > 0 && totals.TryGetValue(partialKey, out total))
                             || (finalKey.Length > 0 && totals.TryGetValue(finalKey, out total));

                if (found && total > 0)
                {
                    item.TotalBytes = total;
                    Log.Debug("DownloadMonitor: total size for {File} = {Total} bytes", item.FileName, total);
                }
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_items.Count == 0)
            {
                _timer?.Stop();
                return;
            }

            RefreshBrowserProgress();

            var now = DateTime.UtcNow;
            bool removedAny = false;

            foreach (var entry in _items.ToList())
            {
                var tracked = entry.Value;

                FileInfo info;

                try
                {
                    info = new FileInfo(tracked.PartialPath);
                    if (!info.Exists)
                    {
                        _items.Remove(entry.Key);
                        removedAny = true;

                        string finalPath = Path.Combine(tracked.DirectoryPath, tracked.FileName);
                        if (File.Exists(finalPath)) RaiseCompleted(tracked);
                        continue;
                    }

                    long length = info.Length;
                    double seconds = Math.Max(0.2, (now - tracked.LastSampleAt).TotalSeconds);
                    long delta = length - tracked.BytesReceived;

                    if (delta > 0)
                    {
                        double instant = delta / seconds;
                        tracked.BytesPerSecond = tracked.BytesPerSecond <= 0
                            ? instant
                            : (0.35 * instant) + (0.65 * tracked.BytesPerSecond);
                        tracked.State = DownloadState.Downloading;
                        tracked.LastGrowthAt = now;
                        tracked.IsStale = false;
                    }
                    else
                    {
                        if (now - tracked.LastGrowthAt >= PauseAfter)
                        {
                            tracked.State = DownloadState.Paused;
                            tracked.BytesPerSecond *= 0.5;
                            if (tracked.BytesPerSecond < 1024) tracked.BytesPerSecond = 0;
                            tracked.IsStale = now - tracked.LastGrowthAt >= StaleAfter;
                        }
                        else
                        {
                            tracked.BytesPerSecond *= 0.85;
                        }
                    }

                    tracked.BytesReceived = length;
                    tracked.LastSampleAt = now;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DownloadMonitor: tick failed for {Path}", tracked.PartialPath);
                }
            }

            if (removedAny) UpdateTimerState();
            Publish();
        }

        private void UpdateTimerState()
        {
            if (_timer == null) return;

            if (!_isStarted || _isSuspended || _items.Count == 0)
            {
                _timer.Stop();
                return;
            }

            var interval = _items.Values.Any(i => i.State == DownloadState.Downloading)
                ? TimeSpan.FromSeconds(1)
                : TimeSpan.FromSeconds(5);

            if (_timer.Interval != interval) _timer.Interval = interval;
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void Publish()
        {
            _snapshot = _items.Values
                .OrderByDescending(i => i.State == DownloadState.Downloading)
                .ThenByDescending(i => i.BytesPerSecond)
                .Select(i => new DownloadSnapshot(
                    i.PartialPath,
                    i.FileName,
                    i.DirectoryPath,
                    i.Source,
                    i.State,
                    i.BytesReceived,
                    i.BytesPerSecond,
                    i.TotalBytes,
                    i.StartedAt,
                    i.LastGrowthAt,
                    i.IsStale))
                .ToList();

            DownloadsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Post(Action action)
        {
            var dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;

            if (dispatcher.CheckAccess()) action();
            else dispatcher.InvokeAsync(action);
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _isStarted = false;
            DisposeWatchers();

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTimerTick;
                _timer = null;
            }

            _items.Clear();
        }
    }
}
