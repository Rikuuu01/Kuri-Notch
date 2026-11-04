using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Interop;
using NotchWin.Models;
using NotchWin.Services;
using Serilog;

namespace NotchWin.ViewModels
{
    public sealed record DownloadCompletionNotice(int Count, string FirstFileName);

    public sealed class DownloadItemViewModel
    {
        public DownloadItemViewModel(DownloadSnapshot snapshot)
        {
            Name = snapshot.FileName;
            SizeText = DownloadViewModel.FormatBytes(snapshot.BytesReceived);
            SpeedText = snapshot.State == DownloadState.Paused
                ? string.Empty
                : DownloadViewModel.FormatSpeed(snapshot.BytesPerSecond);
            IsPaused = snapshot.State == DownloadState.Paused;
            Source = snapshot.Source;

            if (snapshot.TotalBytes is long total && total > 0)
            {
                ProgressPercent = Math.Clamp(snapshot.BytesReceived * 100.0 / total, 0, 100);
                PercentText = DownloadViewModel.FormatPercent(ProgressPercent.Value);
                SizeDetailText = $"{DownloadViewModel.FormatBytes(snapshot.BytesReceived)} / {DownloadViewModel.FormatBytes(total)}";
            }
            else
            {
                SizeDetailText = SizeText;
            }
        }

        public string Name { get; }
        public string SizeText { get; }
        public string SizeDetailText { get; }
        public string SpeedText { get; }
        public double? ProgressPercent { get; }
        public string PercentText { get; } = string.Empty;
        public bool IsPaused { get; }
        public DownloadSource Source { get; }
    }

    public partial class DownloadViewModel : ObservableObject, IDisposable
    {
        private readonly IDownloadMonitorService _service;
        private readonly ObservableCollection<DownloadItemViewModel> _items = new();
        private readonly List<string> _completionBuffer = new();
        private readonly DispatcherTimer _completionTimer;
        private bool _disposed;

        public DownloadViewModel(IDownloadMonitorService service)
        {
            _service = service;
            Items = new ReadOnlyObservableCollection<DownloadItemViewModel>(_items);

            _completionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _completionTimer.Tick += (_, _) => FlushCompletion();

            _service.DownloadsChanged += OnDownloadsChanged;
            _service.DownloadCompleted += OnDownloadCompleted;
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

            Refresh();
        }

        public ReadOnlyObservableCollection<DownloadItemViewModel> Items { get; }

        [ObservableProperty]
        private bool _hasActiveDownloads;

        [ObservableProperty]
        private string _pillFileName = string.Empty;

        [ObservableProperty]
        private string _pillDetail = string.Empty;

        [ObservableProperty]
        private string _pillTooltip = string.Empty;

        [ObservableProperty]
        private string _cardTitle = string.Empty;

        [ObservableProperty]
        private string _cardDetail = string.Empty;

        [ObservableProperty]
        private string _cardTooltip = string.Empty;

        [ObservableProperty]
        private string _primaryDirectory = string.Empty;

        [ObservableProperty]
        private double _pillProgressPercent;

        [ObservableProperty]
        private bool _hasPillProgress;

        /// <summary>Raised once per burst of completed downloads (coalesced).</summary>
        public event EventHandler<DownloadCompletionNotice>? CompletionNotice;

        private void OnDownloadsChanged(object? sender, EventArgs e) => Refresh();

        private void OnLanguageChanged(object? sender, string language) => Refresh();

        private void Refresh()
        {
            var visible = _service.ActiveDownloads
                .Where(d => !d.IsStale)
                .ToList();

            _items.Clear();
            foreach (var download in visible) _items.Add(new DownloadItemViewModel(download));

            HasActiveDownloads = visible.Count > 0;
            BuildTexts(visible);
        }

        private void BuildTexts(IReadOnlyList<DownloadSnapshot> visible)
        {
            if (visible.Count == 0)
            {
                PillFileName = string.Empty;
                PillDetail = string.Empty;
                PillTooltip = string.Empty;
                CardTitle = string.Empty;
                CardDetail = string.Empty;
                CardTooltip = string.Empty;
                PrimaryDirectory = string.Empty;
                PillProgressPercent = 0;
                HasPillProgress = false;
                return;
            }

            var primary = visible[0];
            int extra = visible.Count - 1;
            long totalBytes = visible.Sum(d => d.BytesReceived);
            double totalSpeed = visible.Sum(d => d.BytesPerSecond);
            bool anyDownloading = visible.Any(d => d.State == DownloadState.Downloading);

            string status = anyDownloading
                ? FormatSpeed(totalSpeed)
                : LocalizationService.Instance["Download.Paused"];

            // Percent replaces the byte size in the pill and card; it only
            // exists when every visible download reports its total.
            double? percent = null;
            if (visible.All(d => d.TotalBytes is > 0))
            {
                long totalAll = visible.Sum(d => d.TotalBytes!.Value);
                if (totalAll > 0) percent = Math.Clamp(totalBytes * 100.0 / totalAll, 0, 100);
            }

            string downloadingLabel = LocalizationService.Instance["Download.Downloading"];

            // Browser partial names ("Unconfirmed N") are not meaningful, so
            // they keep the label until the final rename reveals the real name.
            string primaryTitle = downloadingLabel;

            string sizePart = percent.HasValue
                ? FormatPercent(percent.Value)
                : BuildSizeText(visible, totalBytes);

            PrimaryDirectory = primary.DirectoryPath;

            PillFileName = primaryTitle;
            PillDetail = extra > 0
                ? $"+{extra} · {sizePart} | {status}"
                : $"{sizePart} | {status}";
            PillTooltip = BuildTooltip(visible);

            HasPillProgress = percent.HasValue;
            PillProgressPercent = percent ?? 0;

            CardTitle = extra > 0 ? $"{primaryTitle} +{extra}" : primaryTitle;
            CardDetail = $"{sizePart} · {status}";
            CardTooltip = PillTooltip;
        }

        /// <summary>
        /// Received / total size for the pill. For several downloads the total
        /// is only shown when every item reports it.
        /// </summary>
        private static string BuildSizeText(IReadOnlyList<DownloadSnapshot> visible, long receivedBytes)
        {
            long? total = visible.All(d => d.TotalBytes is > 0)
                ? visible.Sum(d => d.TotalBytes!.Value)
                : null;

            return total.HasValue
                ? $"{FormatBytes(receivedBytes)} / {FormatBytes(total.Value)}"
                : FormatBytes(receivedBytes);
        }

        internal static string FormatPercent(double percent) =>
            percent >= 10 ? $"{percent:0}%" : $"{percent:0.0}%";

        private static string BuildTooltip(IReadOnlyList<DownloadSnapshot> visible)
        {
            var lines = new List<string>
            {
                LocalizationService.Instance.Get("Download.ActiveCount", visible.Count)
            };

            foreach (var download in visible)
            {
                string speed = download.State == DownloadState.Paused
                    ? LocalizationService.Instance["Download.Paused"]
                    : FormatSpeed(download.BytesPerSecond);

                string size;
                if (download.TotalBytes is long total && total > 0)
                {
                    double percent = Math.Clamp(download.BytesReceived * 100.0 / total, 0, 100);
                    size = $"{percent:0}% · {FormatBytes(download.BytesReceived)} / {FormatBytes(total)}";
                }
                else
                {
                    size = FormatBytes(download.BytesReceived);
                }

                lines.Add($"{download.FileName} — {size} · {speed}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        [RelayCommand]
        private void OpenPrimaryFolder()
        {
            string directory = PrimaryDirectory;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                directory = NativeMethods.GetKnownDownloadsFolder();
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DownloadMonitor: could not open folder {Dir}", directory);
            }
        }

        private void OnDownloadCompleted(object? sender, DownloadCompletedEventArgs e)
        {
            _completionBuffer.Add(e.FileName);
            _completionTimer.Stop();
            _completionTimer.Start();
        }

        private void FlushCompletion()
        {
            _completionTimer.Stop();
            if (_completionBuffer.Count == 0) return;

            int count = _completionBuffer.Count;
            string firstName = _completionBuffer[0];
            _completionBuffer.Clear();

            CompletionNotice?.Invoke(this, new DownloadCompletionNotice(count, firstName));
        }

        internal static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.0} GB";
            if (bytes >= 1024L * 1024)
                return $"{bytes / (1024.0 * 1024.0):0.0} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:0} KB";
            return $"{bytes} B";
        }

        internal static string FormatSpeed(double bytesPerSecond)
        {
            const double unit = 1024.0;

            if (bytesPerSecond >= unit * unit)
                return $"{bytesPerSecond / (unit * unit):0.0} MB/s";
            if (bytesPerSecond >= unit)
                return $"{bytesPerSecond / unit:0} KB/s";
            return $"{bytesPerSecond:0} B/s";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _completionTimer.Stop();
            _service.DownloadsChanged -= OnDownloadsChanged;
            _service.DownloadCompleted -= OnDownloadCompleted;
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }
}
