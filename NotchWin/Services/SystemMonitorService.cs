using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Serilog;

namespace NotchWin.Services
{
    public class SystemMonitorService : ISystemMonitorService
    {
        #region Win32 Native Interop

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out System.Runtime.InteropServices.ComTypes.FILETIME idleTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME kernelTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME userTime);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        #region PDH Native Interop (GPU Monitoring)

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern int PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern int PdhGetFormattedCounterArray(IntPtr hCounter, uint dwFormat, ref uint lpdwBufferSize, ref uint lpdwItemCount, IntPtr lpItemBuffer);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern int PdhCloseQuery(IntPtr hQuery);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PDH_FMT_COUNTERVALUE_ITEM
        {
            public IntPtr szName;
            public uint CStatus;
            public double doubleValue;
        }

        private const uint PDH_FMT_DOUBLE = 0x00000200;

        #endregion

        private static ulong FileTimeToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME ft)
        {
            return ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        }

        #endregion

        private readonly DispatcherTimer _timer;
        private ulong _prevIdleTime;
        private ulong _prevKernelTime;
        private ulong _prevUserTime;

        private long _prevRxBytes;
        private long _prevTxBytes;
        private DateTime _prevNetworkTime = DateTime.UtcNow;

        private bool _isFirstCpuSample = true;
        private bool _isFirstNetSample = true;
        private readonly MEMORYSTATUSEX _memStatus = new();
        private NetworkInterface[]? _cachedInterfaces;
        private DateTime _lastInterfaceScan = DateTime.MinValue;

        public double CpuUsagePercent { get; private set; }
        public double RamUsedGb { get; private set; }
        public double RamTotalGb { get; private set; }
        public double RamUsagePercent { get; private set; }
        public double GpuUsagePercent { get; private set; }
        public string FormattedGpuUsage => $"{GpuUsagePercent:0}%";

        private IntPtr _hPdhGpuQuery = IntPtr.Zero;
        private IntPtr _hPdhGpuCounter = IntPtr.Zero;
        private bool _isPdhGpuInitialized;

        public double DownloadSpeedBytesPerSec { get; private set; }
        public double UploadSpeedBytesPerSec { get; private set; }

        public string FormattedDownloadSpeed => FormatBytesPerSec(DownloadSpeedBytesPerSec);
        public string FormattedUploadSpeed => FormatBytesPerSec(UploadSpeedBytesPerSec);
        public string ActiveNetworkName { get; private set; } = "Ethernet";
        public bool IsWirelessNetwork { get; private set; } = false;

        public event EventHandler? MetricsUpdated;

        public SystemMonitorService()
        {
            UpdateNetworkProfile();
            try
            {
                Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += (s) => UpdateNetworkProfile();
            }
            catch { }

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _timer.Tick += (s, e) => UpdateMetrics();

            InitializeGpuCounter();

            // Run initial sample
            UpdateMetrics();
        }

        private void InitializeGpuCounter()
        {
            try
            {
                int status = PdhOpenQuery(null, IntPtr.Zero, out _hPdhGpuQuery);
                if (status == 0 && _hPdhGpuQuery != IntPtr.Zero)
                {
                    status = PdhAddEnglishCounter(_hPdhGpuQuery, @"\GPU Engine(*engtype_3D)\Utilization Percentage", IntPtr.Zero, out _hPdhGpuCounter);
                    if (status == 0 && _hPdhGpuCounter != IntPtr.Zero)
                    {
                        PdhCollectQueryData(_hPdhGpuQuery);
                        _isPdhGpuInitialized = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SystemMonitorService: Failed to initialize PDH GPU counter");
            }
        }

        private void UpdateNetworkProfile()
        {
            try
            {
                _cachedInterfaces = null;
                var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
                if (profile != null)
                {
                    ActiveNetworkName = profile.ProfileName ?? (profile.IsWlanConnectionProfile ? "Wi-Fi" : "Ethernet");
                    IsWirelessNetwork = profile.IsWlanConnectionProfile;
                }
                else
                {
                    ActiveNetworkName = "Tidak Terhubung";
                    IsWirelessNetwork = false;
                }
            }
            catch
            {
                ActiveNetworkName = "Ethernet";
                IsWirelessNetwork = false;
            }
        }

        public void Start()
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
                UpdateMetrics();
            }
        }

        public void Stop()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _isFirstCpuSample = true;
                _isFirstNetSample = true;
            }
        }

        private void UpdateMetrics()
        {
            UpdateCpu();
            UpdateRam();
            UpdateGpu();
            UpdateNetwork();

            MetricsUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateCpu()
        {
            try
            {
                if (GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
                {
                    ulong curIdle = FileTimeToUInt64(idleFt);
                    ulong curKernel = FileTimeToUInt64(kernelFt);
                    ulong curUser = FileTimeToUInt64(userFt);

                    if (!_isFirstCpuSample)
                    {
                        ulong idleDelta = curIdle - _prevIdleTime;
                        ulong kernelDelta = curKernel - _prevKernelTime;
                        ulong userDelta = curUser - _prevUserTime;
                        ulong totalDelta = kernelDelta + userDelta;

                        if (totalDelta > 0)
                        {
                            double usage = (double)(totalDelta - idleDelta) * 100.0 / totalDelta;
                            CpuUsagePercent = Math.Clamp(Math.Round(usage, 0), 0, 100);
                        }
                    }
                    else
                    {
                        _isFirstCpuSample = false;
                    }

                    _prevIdleTime = curIdle;
                    _prevKernelTime = curKernel;
                    _prevUserTime = curUser;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SystemMonitorService: Error reading CPU usage");
            }
        }

        private void UpdateRam()
        {
            try
            {
                if (GlobalMemoryStatusEx(_memStatus))
                {
                    double totalGb = (double)_memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double freeGb = (double)_memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    double usedGb = Math.Max(0, totalGb - freeGb);

                    RamTotalGb = Math.Round(totalGb, 1);
                    RamUsedGb = Math.Round(usedGb, 1);
                    RamUsagePercent = _memStatus.dwMemoryLoad;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SystemMonitorService: Error reading RAM metrics");
            }
        }

        private void UpdateNetwork()
        {
            try
            {
                long currentRx = 0;
                long currentTx = 0;
                DateTime now;

                now = DateTime.UtcNow;
                if (_cachedInterfaces == null || (DateTime.UtcNow - _lastInterfaceScan).TotalSeconds > 10.0)
                {
                    try
                    {
                        var list = new System.Collections.Generic.List<NetworkInterface>();
                        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                        {
                            if (n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                            {
                                list.Add(n);
                            }
                        }
                        _cachedInterfaces = list.ToArray();
                        _lastInterfaceScan = DateTime.UtcNow;
                    }
                    catch
                    {
                        _cachedInterfaces = Array.Empty<NetworkInterface>();
                    }
                }

                foreach (var net in _cachedInterfaces)
                {
                    if (net.OperationalStatus == OperationalStatus.Up &&
                        net.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        net.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        try
                        {
                            var stats = net.GetIPv4Statistics();
                            currentRx += stats.BytesReceived;
                            currentTx += stats.BytesSent;
                        }
                        catch { }
                    }
                }

                now = DateTime.UtcNow;
                double seconds = (now - _prevNetworkTime).TotalSeconds;

                if (!_isFirstNetSample && seconds > 0.5)
                {
                    long deltaRx = Math.Max(0, currentRx - _prevRxBytes);
                    long deltaTx = Math.Max(0, currentTx - _prevTxBytes);

                    DownloadSpeedBytesPerSec = deltaRx / seconds;
                    UploadSpeedBytesPerSec = deltaTx / seconds;
                }
                else
                {
                    _isFirstNetSample = false;
                }

                _prevRxBytes = currentRx;
                _prevTxBytes = currentTx;
                _prevNetworkTime = now;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SystemMonitorService: Error reading network speeds");
            }
        }

        private static string FormatBytesPerSec(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024)
                return $"{bytesPerSec / (1024.0 * 1024.0):0.1} MB/s";
            if (bytesPerSec >= 1024)
                return $"{bytesPerSec / 1024.0:0} KB/s";
            return $"{bytesPerSec:0} B/s";
        }

        private void UpdateGpu()
        {
            if (!_isPdhGpuInitialized || _hPdhGpuQuery == IntPtr.Zero || _hPdhGpuCounter == IntPtr.Zero)
                return;

            try
            {
                int status = PdhCollectQueryData(_hPdhGpuQuery);
                if (status != 0) return;

                uint bufferSize = 0;
                uint itemCount = 0;
                PdhGetFormattedCounterArray(_hPdhGpuCounter, PDH_FMT_DOUBLE, ref bufferSize, ref itemCount, IntPtr.Zero);

                if (bufferSize > 0)
                {
                    IntPtr pItems = Marshal.AllocHGlobal((int)bufferSize);
                    try
                    {
                        status = PdhGetFormattedCounterArray(_hPdhGpuCounter, PDH_FMT_DOUBLE, ref bufferSize, ref itemCount, pItems);
                        if (status == 0)
                        {
                            double total = 0;
                            int itemSize = Marshal.SizeOf(typeof(PDH_FMT_COUNTERVALUE_ITEM));
                            for (int i = 0; i < itemCount; i++)
                            {
                                IntPtr itemPtr = IntPtr.Add(pItems, i * itemSize);
                                var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(itemPtr);
                                if (item.CStatus == 0) total += item.doubleValue;
                            }
                            GpuUsagePercent = Math.Clamp(Math.Round(total, 0), 0, 100);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pItems);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SystemMonitorService: Error reading GPU metrics");
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            if (_hPdhGpuQuery != IntPtr.Zero)
            {
                try { PdhCloseQuery(_hPdhGpuQuery); } catch { }
                _hPdhGpuQuery = IntPtr.Zero;
                _hPdhGpuCounter = IntPtr.Zero;
            }
        }
    }
}
