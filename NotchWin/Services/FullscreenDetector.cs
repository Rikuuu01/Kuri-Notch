using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using NotchWin.Interop;
using Serilog;

namespace NotchWin.Services
{
    public class FullscreenDetector : IFullscreenDetector
    {
        private readonly DispatcherTimer _pollTimer;
        private bool _isFullscreenActive;
        private bool _isDisposed;

        public bool IsFullscreenActive => _isFullscreenActive;
        public event EventHandler<bool>? FullscreenChanged;

        public FullscreenDetector()
        {
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(2500)
            };
            _pollTimer.Tick += OnPollTick;
        }

        public void Start()
        {
            if (!_pollTimer.IsEnabled)
            {
                _pollTimer.Start();
                Log.Debug("FullscreenDetector started.");
            }

            // Evaluasi langsung agar kondisi fullscreen yang sudah aktif terdeteksi
            // tanpa menunggu tick pertama (mencegah kedipan notch saat startup).
            OnPollTick(null, EventArgs.Empty);
        }

        public void Stop()
        {
            if (_pollTimer.IsEnabled)
            {
                _pollTimer.Stop();
                Log.Debug("FullscreenDetector stopped.");
            }
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            bool isCurrentFs = CheckIsFullscreen();
            if (isCurrentFs != _isFullscreenActive)
            {
                _isFullscreenActive = isCurrentFs;
                Log.Information("Fullscreen status transitioned to: {IsFullscreen}", _isFullscreenActive);
                FullscreenChanged?.Invoke(this, _isFullscreenActive);
            }
        }

        private bool CheckIsFullscreen()
        {
            try
            {
                // 1. Direct Shell Query (captures Direct3D/Vulkan exclusive fullscreen games & presentations)
                int hr = NativeMethods.SHQueryUserNotificationState(out var state);
                if (hr == 0) // S_OK
                {
                    if (state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                        state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE)
                    {
                        return true;
                    }
                }

                // 2. Foreground Window Geometry Check (captures borderless fullscreen games & fullscreen video players)
                var fgHwnd = NativeMethods.GetForegroundWindow();
                if (fgHwnd == IntPtr.Zero)
                    return false;

                if (fgHwnd == NativeMethods.GetDesktopWindow() || fgHwnd == NativeMethods.GetShellWindow())
                    return false;

                // Saat pengguna berinteraksi dengan notch (fullscreen peek), foreground
                // adalah jendela NotchWin sendiri. Jangan anggap fullscreen berakhir —
                // pertahankan status terakhir sampai foreground kembali ke aplikasi lain.
                NativeMethods.GetWindowThreadProcessId(fgHwnd, out uint foregroundProcessId);
                if (foregroundProcessId == (uint)Environment.ProcessId)
                    return _isFullscreenActive;

                if (NativeMethods.IsIconic(fgHwnd))
                    return false;

                if (!TryGetWindowBounds(fgHwnd, out var rect))
                    return false;

                var monitor = NativeMethods.MonitorFromWindow(fgHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (monitor == IntPtr.Zero)
                    return false;

                var monitorInfo = new NativeMethods.MONITORINFOEX();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
                if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                    return false;

                // Bandingkan dalam piksel fisik pada monitor milik window foreground.
                // Perbandingan DIP (SystemParameters.PrimaryScreenWidth) membuat window
                // maximized terdeteksi fullscreen pada display scaling > 100%.
                const int tolerance = 2;
                return rect.Left <= monitorInfo.rcMonitor.Left + tolerance
                    && rect.Top <= monitorInfo.rcMonitor.Top + tolerance
                    && rect.Right >= monitorInfo.rcMonitor.Right - tolerance
                    && rect.Bottom >= monitorInfo.rcMonitor.Bottom - tolerance;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while checking fullscreen state");
            }

            return false;
        }

        private static bool TryGetWindowBounds(IntPtr hwnd, out NativeMethods.RECT bounds)
        {
            // Extended frame bounds mengecualikan border resize tak terlihat milik DWM.
            int hr = NativeMethods.DwmGetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out bounds,
                Marshal.SizeOf(typeof(NativeMethods.RECT)));
            if (hr == 0 && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
            {
                return true;
            }

            return NativeMethods.GetWindowRect(hwnd, out bounds);
        }


        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _pollTimer.Stop();
            }
        }
    }
}
