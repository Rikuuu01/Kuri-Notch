using System;
using System.Threading;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IUpdateService : IDisposable
    {
        Version CurrentVersion { get; }
        string CurrentVersionString { get; }
        bool IsInstalledViaSetup { get; }
        bool IsChecking { get; }
        bool IsDownloading { get; }
        double DownloadProgress { get; }

        event EventHandler<UpdateCheckResult>? UpdateAvailable;

        Task<UpdateCheckResult> CheckForUpdateAsync(bool isManual = false);
        Task<bool> DownloadAndApplyUpdateAsync(UpdateCheckResult result, IProgress<double>? progress = null, CancellationToken ct = default);
        void StartAutoCheck();
    }
}
