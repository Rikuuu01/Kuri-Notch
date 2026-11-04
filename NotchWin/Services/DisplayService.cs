using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using NotchWin.Interop;
using Serilog;

namespace NotchWin.Services
{
    public class DisplayService : IDisplayService
    {
        public event EventHandler? DisplayConfigurationChanged;

        public DisplayService()
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Log.Information("Display configuration changed event detected from Windows");
            DisplayConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<MonitorDetails> GetMonitors()
        {
            var monitors = new List<MonitorDetails>();

            try
            {
                NativeMethods.MonitorEnumDelegate callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
                {
                    var mi = new NativeMethods.MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));

                    if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                    {
                        bool isPrimary = (mi.dwFlags & 1) != 0;
                        string deviceName = mi.szDevice ?? $"Monitor_{monitors.Count + 1}";

                        double scale = 1.0;
                        try
                        {
                            if (NativeMethods.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out _) == 0 && dpiX > 0)
                            {
                                scale = dpiX / 96.0;
                            }
                        }
                        catch
                        {
                            scale = 1.0;
                        }

                        int w = mi.rcMonitor.Width;
                        int h = mi.rcMonitor.Height;
                        string friendlyName = isPrimary ? "Layar Utama" : $"Layar {monitors.Count + 1}";
                        string displayName = $"{friendlyName} ({w}x{h})";

                        monitors.Add(new MonitorDetails(
                            deviceName,
                            displayName,
                            new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top, w, h),
                            new Rect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height),
                            scale,
                            isPrimary
                        ));
                    }

                    return true;
                };

                NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to enumerate display monitors");
            }

            // Fallback if no monitor enumerated
            if (monitors.Count == 0)
            {
                monitors.Add(new MonitorDetails(
                    "Primary",
                    "Layar Utama",
                    new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                    new Rect(0, 0, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height),
                    1.0,
                    true
                ));
            }

            return monitors;
        }

        public MonitorDetails GetTargetMonitor(string? targetDeviceName)
        {
            var monitors = GetMonitors();

            if (!string.IsNullOrEmpty(targetDeviceName))
            {
                var target = monitors.FirstOrDefault(m => string.Equals(m.DeviceName, targetDeviceName, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    return target;
                }
            }

            return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
    }
}
