using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Interop;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class SystemHealthViewModel : ObservableObject
    {
        private readonly ISystemMonitorService _monitorService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CpuFormatted))]
        private double _cpuPercent;

        [ObservableProperty]
        private double _ramUsedGb;

        [ObservableProperty]
        private double _ramTotalGb;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RamPercentFormatted))]
        private double _ramPercent;

        public string CpuFormatted => $"{CpuPercent:0}%";
        public string RamPercentFormatted => $"{RamPercent:0}%";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GpuFormatted))]
        private double _gpuPercent;

        public string GpuFormatted => $"{GpuPercent:0}%";

        [ObservableProperty]
        private string _ramFormatted = "0.0 / 0.0 GB";

        [ObservableProperty]
        private string _downloadSpeed = "0 B/s";

        [ObservableProperty]
        private string _uploadSpeed = "0 B/s";

        [ObservableProperty]
        private string _networkName = "Not Connected";

        [ObservableProperty]
        private bool _isWirelessNetwork;

        private static readonly Brush GreenBrush;
        private static readonly Brush BlueBrush;
        private static readonly Brush PurpleBrush;
        private static readonly Brush AmberBrush;
        private static readonly Brush RedBrush;

        static SystemHealthViewModel()
        {
            var purple = new SolidColorBrush(Color.FromRgb(168, 85, 247));
            purple.Freeze();
            PurpleBrush = purple;
            var green = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            green.Freeze();
            GreenBrush = green;

            var blue = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            blue.Freeze();
            BlueBrush = blue;

            var amber = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            amber.Freeze();
            AmberBrush = amber;

            var red = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            red.Freeze();
            RedBrush = red;
        }

        [ObservableProperty]
        private Brush _cpuBrush = GreenBrush;

        [ObservableProperty]
        private Brush _ramBrush = BlueBrush;

        [ObservableProperty]
        private Brush _gpuBrush = PurpleBrush;

        public SystemHealthViewModel(ISystemMonitorService monitorService)
        {
            _monitorService = monitorService;
            _monitorService.MetricsUpdated += OnMetricsUpdated;
            UpdateProperties();
        }

        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            UpdateProperties();
        }

        private void UpdateProperties()
        {
            CpuPercent = _monitorService.CpuUsagePercent;
            RamUsedGb = _monitorService.RamUsedGb;
            RamTotalGb = _monitorService.RamTotalGb;
            RamPercent = _monitorService.RamUsagePercent;
            RamFormatted = $"{RamUsedGb:0.0} / {RamTotalGb:0.0} GB";
            DownloadSpeed = _monitorService.FormattedDownloadSpeed;
            UploadSpeed = _monitorService.FormattedUploadSpeed;
            NetworkName = _monitorService.ActiveNetworkName;
            IsWirelessNetwork = _monitorService.IsWirelessNetwork;
            GpuPercent = _monitorService.GpuUsagePercent;

            // Dynamic color thresholds with static frozen brushes
            var targetCpuBrush = CpuPercent >= 85 ? RedBrush : (CpuPercent >= 65 ? AmberBrush : GreenBrush);
            if (!ReferenceEquals(CpuBrush, targetCpuBrush))
                CpuBrush = targetCpuBrush;

            var targetRamBrush = RamPercent >= 85 ? RedBrush : (RamPercent >= 70 ? AmberBrush : BlueBrush);
            if (!ReferenceEquals(RamBrush, targetRamBrush))
                RamBrush = targetRamBrush;

            var targetGpuBrush = GpuPercent >= 85 ? RedBrush : (GpuPercent >= 65 ? AmberBrush : PurpleBrush);
            if (!ReferenceEquals(GpuBrush, targetGpuBrush))
                GpuBrush = targetGpuBrush;
        }

        [RelayCommand]
        private void LaunchTaskManager()
        {
            QuickActionHelper.LaunchTaskManager();
        }

        [RelayCommand]
        private void OpenTaskManager()
        {
            QuickActionHelper.LaunchTaskManager();
        }

        public void StartMonitoring() => _monitorService.Start();
        public void StopMonitoring() => _monitorService.Stop();
    }
}
