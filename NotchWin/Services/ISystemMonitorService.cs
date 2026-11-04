using System;

namespace NotchWin.Services
{
    public interface ISystemMonitorService : IDisposable
    {
        double CpuUsagePercent { get; }
        double RamUsedGb { get; }
        double RamTotalGb { get; }
        double RamUsagePercent { get; }
        double GpuUsagePercent { get; }
        string FormattedGpuUsage { get; }
        double DownloadSpeedBytesPerSec { get; }
        double UploadSpeedBytesPerSec { get; }
        string FormattedDownloadSpeed { get; }
        string FormattedUploadSpeed { get; }
        string ActiveNetworkName { get; }
        bool IsWirelessNetwork { get; }

        event EventHandler? MetricsUpdated;

        void Start();
        void Stop();
    }
}
