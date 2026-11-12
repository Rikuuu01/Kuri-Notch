using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NotchWin.Models;
using NotchWin.Widgets;

namespace NotchWin.ViewModels
{
    public partial class HudViewModel : ObservableObject, INotchWidget
    {
        public WidgetType Type => WidgetType.Hud;
        public string Name => "HUD";
        public int Priority => 55;
        public bool IsActive => false;

        private static readonly SolidColorBrush BrushWhite = new(Colors.White);
        private static readonly SolidColorBrush BrushMuted = new(Color.FromRgb(0xA1, 0xA1, 0xAA));
        private static readonly SolidColorBrush BrushBadgeOn = new(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush BrushBadgeOff = new(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));

        static HudViewModel()
        {
            BrushWhite.Freeze();
            BrushMuted.Freeze();
            BrushBadgeOn.Freeze();
            BrushBadgeOff.Freeze();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsVolumeType))]
        [NotifyPropertyChangedFor(nameof(IsBrightnessType))]
        [NotifyPropertyChangedFor(nameof(IsCapsLockType))]
        [NotifyPropertyChangedFor(nameof(IsNumLockType))]
        private HudType _hudType = HudType.Volume;

        [ObservableProperty]
        private string _title = "Volume";

        [ObservableProperty]
        private string _valueText = "0";

        [ObservableProperty]
        private double _progressValue = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ValueForegroundBrush))]
        private bool _hasProgressBar = true;

        [ObservableProperty]
        private bool _isMuted;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ValueForegroundBrush))]
        [NotifyPropertyChangedFor(nameof(StatusBadgeBackgroundBrush))]
        private bool _isStatusOn;

        public bool IsVolumeType => HudType == HudType.Volume;
        public bool IsBrightnessType => HudType == HudType.Brightness;
        public bool IsCapsLockType => HudType == HudType.CapsLock;
        public bool IsNumLockType => HudType == HudType.NumLock;

        public Brush ValueForegroundBrush => (!HasProgressBar && !IsStatusOn) ? BrushMuted : BrushWhite;
        public Brush StatusBadgeBackgroundBrush => IsStatusOn ? BrushBadgeOn : BrushBadgeOff;
    }
}
