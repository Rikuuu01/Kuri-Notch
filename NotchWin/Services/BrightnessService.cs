using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace NotchWin.Services
{
    public class BrightnessService : IBrightnessService
    {
        private int _currentBrightness = 100;
        private bool _isSupported = false;
        private bool _isInitialized = false;

        private ManagementEventWatcher? _brightnessWatcher;
        private System.Threading.Timer? _pollingTimer;
        private CancellationTokenSource? _debounceCts;
        private readonly object _lock = new();

        public int CurrentBrightness => _currentBrightness;
        public bool IsSupported => _isSupported;

        public event EventHandler<int>? BrightnessChanged;

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                    using var results = searcher.Get();

                    foreach (ManagementObject obj in results)
                    {
                        var val = obj["CurrentBrightness"];
                        if (val != null)
                        {
                            _currentBrightness = Convert.ToInt32(val);
                            _isSupported = true;
                            Log.Information("BrightnessService: Initialized via WMI. CurrentBrightness={Brightness}%", _currentBrightness);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "BrightnessService: WMI monitor brightness query not supported or failed on this device");
                    _isSupported = false;
                }

                if (_isSupported)
                {
                    StartEventWatcher();
                    _pollingTimer = new System.Threading.Timer(CheckBrightnessPoll, null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(800));
                }

                _isInitialized = true;
            });

            BrightnessChanged?.Invoke(this, _currentBrightness);
        }

        private void CheckBrightnessPoll(object? state)
        {
            if (!_isSupported) return;
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    var val = obj["CurrentBrightness"];
                    if (val != null)
                    {
                        int b = Math.Clamp(Convert.ToInt32(val), 0, 100);
                        if (_currentBrightness != b)
                        {
                            _currentBrightness = b;
                            Log.Information("BrightnessService: External brightness detected via poll: {Brightness}%", _currentBrightness);
                            BrightnessChanged?.Invoke(this, _currentBrightness);
                        }
                        break;
                    }
                }
            }
            catch {}
        }

        private void StartEventWatcher()
        {
            try
            {
                var scope = new ManagementScope(@"root\wmi");
                var query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
                _brightnessWatcher = new ManagementEventWatcher(scope, query);
                _brightnessWatcher.EventArrived += (s, e) =>
                {
                    try
                    {
                        var val = e.NewEvent["Brightness"];
                        if (val != null)
                        {
                            int b = Convert.ToInt32(val);
                            int clamped = Math.Clamp(b, 0, 100);
                            if (_currentBrightness != clamped)
                            {
                                _currentBrightness = clamped;
                                Log.Information("BrightnessService: External WMI brightness event: {Brightness}%", _currentBrightness);
                                BrightnessChanged?.Invoke(this, _currentBrightness);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "BrightnessService: Error processing WMI brightness event");
                    }
                };
                _brightnessWatcher.Start();
                Log.Information("BrightnessService: WmiMonitorBrightnessEvent watcher started successfully");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BrightnessService: Could not start WmiMonitorBrightnessEvent watcher");
            }
        }

        public void SetBrightness(int percent)
        {
            int clamped = Math.Clamp(percent, 0, 100);
            if (_currentBrightness == clamped && _isInitialized) return;

            _currentBrightness = clamped;
            BrightnessChanged?.Invoke(this, _currentBrightness);

            if (!_isSupported) return;

            // Debounce hardware write so dragging the slider doesn't flood the bus
            lock (_lock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                _ = Task.Delay(40, token).ContinueWith(t =>
                {
                    if (t.IsCanceled) return;

                    try
                    {
                        using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                        using var results = searcher.Get();

                        foreach (ManagementObject obj in results)
                        {
                            obj.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)clamped });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "BrightnessService: Failed to apply brightness to monitor");
                    }
                }, TaskScheduler.Default);
            }
        }

        public void Dispose()
        {
            try
            {
                _pollingTimer?.Dispose();
                _pollingTimer = null;
                _brightnessWatcher?.Stop();
                _brightnessWatcher?.Dispose();
                _brightnessWatcher = null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BrightnessService: Error disposing brightness watcher");
            }

            lock (_lock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = null;
            }
        }
    }
}
