using System;
using System.Threading.Tasks;

namespace NotchWin.Services
{
    public class BatteryStatusChangedEventArgs : EventArgs
    {
        public bool HasBattery { get; }
        public int RemainingPercent { get; }
        public bool IsCharging { get; }
        public bool IsPluggedIn { get; }
        public bool IsLowBattery => HasBattery && !IsCharging && RemainingPercent <= 20;

        public BatteryStatusChangedEventArgs(bool hasBattery, int remainingPercent, bool isCharging, bool isPluggedIn)
        {
            HasBattery = hasBattery;
            RemainingPercent = remainingPercent;
            IsCharging = isCharging;
            IsPluggedIn = isPluggedIn;
        }
    }

    public interface IBatteryService : IDisposable
    {
        event EventHandler<BatteryStatusChangedEventArgs>? BatteryStatusChanged;
        event EventHandler? ChargerPluggedIn;
        event EventHandler? ChargerUnplugged;

        bool HasBattery { get; }
        int RemainingPercent { get; }
        bool IsCharging { get; }
        bool IsPluggedIn { get; }
        bool IsLowBattery { get; }

        double VoltageVolts { get; }
        int BatteryCapacityMah { get; }
        double BatteryCapacityWh { get; }
        double PowerRateWatts { get; }
        int CurrentFlowMa { get; }
        string BatteryDetailsText { get; }
        string PowerFlowText { get; }

        Task InitializeAsync();
        void RefreshPowerState();
        void SetDetailedTelemetryActive(bool active);
    }
}
