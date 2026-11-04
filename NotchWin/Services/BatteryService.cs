using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Windows.System.Power;
using Serilog;

namespace NotchWin.Services
{
    public class BatteryService : IBatteryService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus; // 0 = Offline, 1 = Online, 255 = Unknown
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        private bool _hasBattery;
        private int _remainingPercent = 100;
        private bool _isCharging;
        private bool _isPluggedIn;
        private bool _isInitialized;
        private DispatcherTimer? _pollTimer;
        private DispatcherTimer? _detailedTimer;
        private bool _isDetailedTelemetryActive;
        private volatile bool _isQueryingWmi;

        private double _voltageVolts;
        private int _batteryCapacityMah;
        private double _batteryCapacityWh;
        private double _powerRateWatts;
        private int _currentFlowMa;
        private string _batteryDetailsText = string.Empty;
        private string _powerFlowText = string.Empty;

        public event EventHandler<BatteryStatusChangedEventArgs>? BatteryStatusChanged;
        public event EventHandler? ChargerPluggedIn;
        public event EventHandler? ChargerUnplugged;

        public bool HasBattery => _hasBattery;
        public int RemainingPercent => _remainingPercent;
        public bool IsCharging => _isCharging;
        public bool IsPluggedIn => _isPluggedIn;
        public bool IsLowBattery => _hasBattery && _remainingPercent <= 20;

        public double VoltageVolts => _voltageVolts;
        public int BatteryCapacityMah => _batteryCapacityMah;
        public double BatteryCapacityWh => _batteryCapacityWh;
        public double PowerRateWatts => _powerRateWatts;
        public int CurrentFlowMa => _currentFlowMa;
        public string BatteryDetailsText => _batteryDetailsText;
        public string PowerFlowText => _powerFlowText;

        public BatteryService()
        {
            ReadBatteryState();
        }

        public Task InitializeAsync()
        {
            if (_isInitialized) return Task.CompletedTask;

            try
            {
                ReadBatteryState();

                PowerManager.RemainingChargePercentChanged += OnRemainingChargePercentChanged;
                PowerManager.BatteryStatusChanged += OnBatteryStatusChanged;
                PowerManager.PowerSupplyStatusChanged += OnPowerSupplyStatusChanged;

                // Lightweight fallback poll: 30.0s (Windows.System.Power.PowerManager handles real-time percentage/charging events natively)
                _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30.0) };
                _pollTimer.Tick += (s, e) => UpdateAndNotify();
                _pollTimer.Start();

                _isInitialized = true;
                Log.Information("BatteryService initialized. HasBattery={HasBattery}, Percent={Percent}%, Charging={Charging}, PluggedIn={PluggedIn}",
                    _hasBattery, _remainingPercent, _isCharging, _isPluggedIn);

                // Immediately notify listeners of initial state
                BatteryStatusChanged?.Invoke(this, new BatteryStatusChangedEventArgs(_hasBattery, _remainingPercent, _isCharging, _isPluggedIn));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize BatteryService via Windows.System.Power.PowerManager");
            }

            return Task.CompletedTask;
        }

        private void ReadBatteryState()
        {
            try
            {
                bool hasAcLine = false;
                bool isAcOnline = false;
                int win32Percent = -1;

                if (GetSystemPowerStatus(out var sps))
                {
                    hasAcLine = true;
                    isAcOnline = sps.ACLineStatus == 1;
                    if (sps.BatteryLifePercent <= 100)
                    {
                        win32Percent = sps.BatteryLifePercent;
                    }
                    if (sps.BatteryFlag != 128 && sps.BatteryFlag != 255)
                    {
                        _hasBattery = true;
                    }
                }

                var battStatus = PowerManager.BatteryStatus;
                if (battStatus != BatteryStatus.NotPresent)
                {
                    _hasBattery = true;
                }

                if (_hasBattery)
                {
                    int rtPercent = PowerManager.RemainingChargePercent;
                    _remainingPercent = (rtPercent > 0 && rtPercent <= 100) ? rtPercent : (win32Percent >= 0 ? win32Percent : 100);
                    _isCharging = battStatus == BatteryStatus.Charging;

                    var supply = PowerManager.PowerSupplyStatus;
                    bool winRtPlugged = supply == PowerSupplyStatus.Adequate || supply == PowerSupplyStatus.Inadequate || battStatus == BatteryStatus.Charging || battStatus == BatteryStatus.Idle;

                    _isPluggedIn = hasAcLine ? isAcOnline : winRtPlugged;

                    if (_isDetailedTelemetryActive)
                    {
                        QueryWmiBatteryDetails();
                    }
                }
                else
                {
                    _remainingPercent = 100;
                    _isCharging = false;
                    _isPluggedIn = true;
                    _voltageVolts = 0;
                    _batteryCapacityMah = 0;
                    _batteryCapacityWh = 0;
                    _powerRateWatts = 0;
                    _currentFlowMa = 0;
                    _batteryDetailsText = string.Empty;
                    _powerFlowText = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BatteryService: Error reading power state");
            }
        }

        public void SetDetailedTelemetryActive(bool active)
        {
            if (_isDetailedTelemetryActive == active) return;
            _isDetailedTelemetryActive = active;

            if (active && _hasBattery)
            {
                if (_detailedTimer == null)
                {
                    _detailedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
                    _detailedTimer.Tick += (s, e) => QueryWmiBatteryDetails();
                }
                _detailedTimer.Start();
                QueryWmiBatteryDetails();
            }
            else
            {
                _detailedTimer?.Stop();
            }
        }

        private void QueryWmiBatteryDetails()
        {
            if (!_hasBattery || _isQueryingWmi) return;
            _isQueryingWmi = true;

            Task.Run(() =>
            {
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        @"root\wmi",
                        "SELECT Voltage, ChargeRate, DischargeRate, RemainingCapacity, Charging, Discharging FROM BatteryStatus");

                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        uint mv = Convert.ToUInt32(obj["Voltage"]);
                        int chgRate = Convert.ToInt32(obj["ChargeRate"]);
                        int disRate = Convert.ToInt32(obj["DischargeRate"]);
                        uint mwh = Convert.ToUInt32(obj["RemainingCapacity"]);

                        if (mv > 0)
                        {
                            _voltageVolts = Math.Round(mv / 1000.0, 2);
                            _batteryCapacityMah = (int)Math.Round((double)mwh * 1000.0 / mv);
                            _batteryCapacityWh = Math.Round(mwh / 1000.0, 1);

                            if (_isCharging && chgRate > 0)
                            {
                                _powerRateWatts = Math.Round(chgRate / 1000.0, 1);
                                _currentFlowMa = (int)Math.Round((double)chgRate * 1000.0 / mv);
                                _powerFlowText = $"+{_powerRateWatts:0.1}W (+{_currentFlowMa} mA)";
                            }
                            else if (!_isPluggedIn && disRate > 0)
                            {
                                _powerRateWatts = -Math.Round(disRate / 1000.0, 1);
                                _currentFlowMa = -(int)Math.Round((double)disRate * 1000.0 / mv);
                                _powerFlowText = $"{_powerRateWatts:0.1}W ({_currentFlowMa} mA)";
                            }
                            else
                            {
                                _powerRateWatts = 0;
                                _currentFlowMa = 0;
                                _powerFlowText = _isPluggedIn ? "AC Siaga (0W)" : "0W";
                            }

                            _batteryDetailsText = $"{_batteryCapacityMah:N0} mAh ({_batteryCapacityWh:0.1} Wh)";

                            // Safe invoke event to refresh UI with battery telemetry
                            SafeInvoke(() => BatteryStatusChanged?.Invoke(this, new BatteryStatusChangedEventArgs(_hasBattery, _remainingPercent, _isCharging, _isPluggedIn)));
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "BatteryService: Error reading WMI battery status");
                }
                finally
                {
                    _isQueryingWmi = false;
                }
            });
        }

        private static void SafeInvoke(Action action)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null && app.Dispatcher != null && !app.Dispatcher.HasShutdownStarted)
                {
                    app.Dispatcher.InvokeAsync(action);
                }
                else
                {
                    action();
                }
            }
            catch { }
        }

        public void RefreshPowerState()
        {
            UpdateAndNotify();
        }

        private void OnRemainingChargePercentChanged(object? sender, object e)
        {
            UpdateAndNotify();
        }

        private void OnBatteryStatusChanged(object? sender, object e)
        {
            UpdateAndNotify();
        }

        private void OnPowerSupplyStatusChanged(object? sender, object e)
        {
            UpdateAndNotify();
        }

        private void UpdateAndNotify()
        {
            bool oldHas = _hasBattery;
            int oldPct = _remainingPercent;
            bool oldChg = _isCharging;
            bool oldPlug = _isPluggedIn;

            ReadBatteryState();

            if (!oldPlug && _isPluggedIn)
            {
                Log.Information("BatteryService: Charger plugged in (Percent={Percent}%)", _remainingPercent);
                ChargerPluggedIn?.Invoke(this, EventArgs.Empty);
            }
            else if (oldPlug && !_isPluggedIn)
            {
                Log.Information("BatteryService: Charger unplugged (Percent={Percent}%)", _remainingPercent);
                ChargerUnplugged?.Invoke(this, EventArgs.Empty);
            }

            if (oldHas != _hasBattery || oldPct != _remainingPercent || oldChg != _isCharging || oldPlug != _isPluggedIn)
            {
                BatteryStatusChanged?.Invoke(this, new BatteryStatusChangedEventArgs(_hasBattery, _remainingPercent, _isCharging, _isPluggedIn));
            }
        }

        public void Dispose()
        {
            try
            {
                _pollTimer?.Stop();
                _detailedTimer?.Stop();
                PowerManager.RemainingChargePercentChanged -= OnRemainingChargePercentChanged;
                PowerManager.BatteryStatusChanged -= OnBatteryStatusChanged;
                PowerManager.PowerSupplyStatusChanged -= OnPowerSupplyStatusChanged;
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
