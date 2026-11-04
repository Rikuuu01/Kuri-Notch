using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NotchWin.Services;
using NotchWin.Widgets;

namespace NotchWin.ViewModels
{
    public partial class BatteryViewModel : ObservableObject, INotchWidget
    {
        private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));       // #10B981 Emerald
        private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xEF, 0x44, 0x44));         // #EF4444 Red
        private static readonly SolidColorBrush WhiteGrayBrush = new(Color.FromRgb(0xD4, 0xD4, 0xD8));   // #D4D4D8 White-gray
        private static readonly SolidColorBrush WhiteBrush = new(Color.FromRgb(0xFF, 0xFF, 0xFF));       // #FFFFFF Pure white
        private static readonly SolidColorBrush OutlineGrayBrush = new(Color.FromRgb(0x8E, 0x8E, 0x98)); // #8E8E98 Refined metallic gray

        static BatteryViewModel()
        {
            GreenBrush.Freeze();
            RedBrush.Freeze();
            WhiteGrayBrush.Freeze();
            WhiteBrush.Freeze();
            OutlineGrayBrush.Freeze();
        }

        private readonly IBatteryService _batteryService;

        public WidgetType Type => WidgetType.Battery;
        public string Name => "Battery";
        public int Priority => 60;
        public bool IsActive => false; // Transient widget, only active when triggered (charger or low battery)

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusTitle))]
        [NotifyPropertyChangedFor(nameof(FormattedPercent))]
        [NotifyPropertyChangedFor(nameof(BatteryFillWidth))]
        [NotifyPropertyChangedFor(nameof(BatteryFillBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryOutlineBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryTextBrush))]
        [NotifyPropertyChangedFor(nameof(LightningBrush))]
        private bool _hasBattery;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusTitle))]
        [NotifyPropertyChangedFor(nameof(FormattedPercent))]
        [NotifyPropertyChangedFor(nameof(BatteryBarWidth))]
        [NotifyPropertyChangedFor(nameof(BatteryFillWidth))]
        [NotifyPropertyChangedFor(nameof(BatteryFillBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryOutlineBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryTextBrush))]
        [NotifyPropertyChangedFor(nameof(LightningBrush))]
        private int _remainingPercent = 100;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusTitle))]
        [NotifyPropertyChangedFor(nameof(StatusIcon))]
        [NotifyPropertyChangedFor(nameof(IsChargingOrPluggedIn))]
        [NotifyPropertyChangedFor(nameof(BatteryFillBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryTextBrush))]
        [NotifyPropertyChangedFor(nameof(LightningBrush))]
        private bool _isCharging;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsChargingOrPluggedIn))]
        [NotifyPropertyChangedFor(nameof(BatteryFillBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryTextBrush))]
        [NotifyPropertyChangedFor(nameof(LightningBrush))]
        private bool _isPluggedIn;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusTitle))]
        [NotifyPropertyChangedFor(nameof(StatusIcon))]
        [NotifyPropertyChangedFor(nameof(BatteryFillBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryOutlineBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryTextBrush))]
        [NotifyPropertyChangedFor(nameof(LightningBrush))]
        private bool _isLowBattery;

        [ObservableProperty]
        private string _capacityText = string.Empty;

        [ObservableProperty]
        private string _voltageText = string.Empty;

        [ObservableProperty]
        private string _powerFlowText = string.Empty;

        [ObservableProperty]
        private string _statusSubtitle = string.Empty;

        public string FormattedPercent => $"{RemainingPercent}%";
        public double BatteryBarWidth => Math.Clamp(RemainingPercent, 0, 100);

        // Vector Battery fill width inside 19px outer shell (inner track max 14px)
        public double BatteryFillWidth => Math.Clamp(Math.Round((RemainingPercent / 100.0) * 14.0), 1.0, 14.0);

        public bool IsChargingOrPluggedIn => IsPluggedIn || IsCharging;

        public Brush BatteryFillBrush
        {
            get
            {
                if (RemainingPercent <= 20) return RedBrush;
                if (IsChargingOrPluggedIn) return GreenBrush;
                return WhiteGrayBrush;
            }
        }

        public Brush BatteryOutlineBrush
        {
            get
            {
                if (RemainingPercent <= 20) return WhiteBrush;
                return OutlineGrayBrush;
            }
        }

        public Brush BatteryTextBrush
        {
            get
            {
                if (RemainingPercent <= 20) return RedBrush;
                if (IsChargingOrPluggedIn) return GreenBrush;
                return WhiteGrayBrush;
            }
        }

        public Brush LightningBrush
        {
            get
            {
                if (RemainingPercent <= 20) return WhiteBrush;
                return GreenBrush;
            }
        }

        public string StatusTitle
        {
            get
            {
                if (IsChargingOrPluggedIn) return LocalizationService.Instance["Battery.Charging"];
                if (RemainingPercent <= 20) return LocalizationService.Instance["Battery.BatteryLow"];
                return LocalizationService.Instance["Control.BatteryLevel"];
            }
        }

        public string StatusIcon
        {
            get
            {
                if (IsChargingOrPluggedIn) return "⚡";
                if (RemainingPercent <= 20) return "⚠️";
                return "🔋";
            }
        }

        public event EventHandler? BatteryTransientTriggered;

        public BatteryViewModel(IBatteryService batteryService)
        {
            _batteryService = batteryService;
            UpdateState();

            _batteryService.BatteryStatusChanged += (s, e) => UpdateState();
            _batteryService.ChargerPluggedIn += (s, e) =>
            {
                UpdateState();
                BatteryTransientTriggered?.Invoke(this, EventArgs.Empty);
            };
            _batteryService.ChargerUnplugged += (s, e) =>
            {
                UpdateState();
                BatteryTransientTriggered?.Invoke(this, EventArgs.Empty);
            };
        }

        private void UpdateState()
        {
            void Apply()
            {
                HasBattery = _batteryService.HasBattery;
                RemainingPercent = _batteryService.RemainingPercent;
                IsCharging = _batteryService.IsCharging;
                IsPluggedIn = _batteryService.IsPluggedIn;
                IsLowBattery = _batteryService.IsLowBattery;

                CapacityText = _batteryService.BatteryDetailsText;
                VoltageText = _batteryService.VoltageVolts > 0 ? $"{_batteryService.VoltageVolts:0.1}V" : string.Empty;
                PowerFlowText = _batteryService.PowerFlowText;

                if (!string.IsNullOrEmpty(_batteryService.PowerFlowText))
                {
                    StatusSubtitle = $"{_batteryService.VoltageVolts:0.1}V • {_batteryService.PowerFlowText}";
                }
                else if (_batteryService.BatteryCapacityMah > 0)
                {
                    StatusSubtitle = $"{_batteryService.BatteryCapacityMah:N0} mAh • {_batteryService.VoltageVolts:0.1}V";
                }
                else
                {
                    StatusSubtitle = string.Empty;
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(Apply);
            }
            else
            {
                Apply();
            }
        }
    }
}
