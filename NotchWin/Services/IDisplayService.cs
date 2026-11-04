using System;
using System.Collections.Generic;
using System.Windows;

namespace NotchWin.Services
{
    public record MonitorDetails(
        string DeviceName,
        string DisplayName,
        Rect Bounds,
        Rect WorkArea,
        double DpiScale,
        bool IsPrimary
    );

    public interface IDisplayService : IDisposable
    {
        event EventHandler? DisplayConfigurationChanged;
        IReadOnlyList<MonitorDetails> GetMonitors();
        MonitorDetails GetTargetMonitor(string? targetDeviceName);
    }
}
