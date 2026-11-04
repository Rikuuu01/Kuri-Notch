using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Services;
using NotchWin.Widgets;

namespace NotchWin.ViewModels
{
    public partial class VolumeViewModel : ObservableObject, INotchWidget
    {
        private readonly IVolumeService _volumeService;

        public WidgetType Type => WidgetType.Volume;
        public string Name => "Volume";
        public int Priority => 50;
        public bool IsActive => false; // Transient widget, only active when triggered

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VolumeText))]
        [NotifyPropertyChangedFor(nameof(VolumeBarWidth))]
        private int _volumePercent = 100;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VolumeText))]
        private bool _isMuted;

        [ObservableProperty]
        private string _activeDeviceName = "Speaker";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AudioDeviceIcon))]
        private bool _isHeadphone;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowBluetoothReady))]
        private bool _isBluetoothAudio;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowBluetoothReady))]
        [NotifyPropertyChangedFor(nameof(IsBluetoothRadioOff))]
        private bool _isBluetoothRadioOn = true;

        [ObservableProperty]
        private string _bluetoothDeviceName = string.Empty;

        [ObservableProperty]
        private string _bluetoothBatteryText = string.Empty;

        [ObservableProperty]
        private bool _isDevicePickerOpen;

        [ObservableProperty]
        private ObservableCollection<AudioDeviceInfo> _availableDevices = new();

        public string AudioDeviceIcon => IsHeadphone ? "🎧" : "🔊";

        public string VolumeText => IsMuted ? "Bisu" : $"{VolumePercent}%";
        public double VolumeBarWidth => Math.Clamp(VolumePercent, 0, 100);
        public bool ShowBluetoothReady => !IsBluetoothAudio && IsBluetoothRadioOn;
        public bool IsBluetoothRadioOff => !IsBluetoothRadioOn;

        public event EventHandler? VolumeTransientTriggered;

        public VolumeViewModel(IVolumeService volumeService)
        {
            _volumeService = volumeService;
            _volumePercent = _volumeService.VolumePercent;
            _isMuted = _volumeService.IsMuted;

            _volumeService.VolumeChanged += OnVolumeChanged;
            _volumeService.DeviceChanged += OnDeviceChanged;
            UpdateDeviceProperties();
        }

        private void SafeDispatch(Action action)
        {
            var app = System.Windows.Application.Current;
            if (app == null || app.Dispatcher == null || app.Dispatcher.HasShutdownStarted)
            {
                action();
                return;
            }

            if (app.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                app.Dispatcher.InvokeAsync(action);
            }
        }

        private void OnVolumeChanged(object? sender, VolumeChangedEventArgs e)
        {
            SafeDispatch(() =>
            {
                bool changed = (VolumePercent != e.VolumePercent) || (IsMuted != e.IsMuted);
                VolumePercent = e.VolumePercent;
                IsMuted = e.IsMuted;

                if (changed && !e.IsInitial)
                {
                    VolumeTransientTriggered?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private void OnDeviceChanged(object? sender, EventArgs e)
        {
            SafeDispatch(UpdateDeviceProperties);
        }

        private void UpdateDeviceProperties()
        {
            ActiveDeviceName = _volumeService.ActiveDeviceName;
            IsHeadphone = _volumeService.IsHeadphone;
            IsBluetoothAudio = _volumeService.IsBluetoothAudio;
            IsBluetoothRadioOn = _volumeService.IsBluetoothRadioOn;
            BluetoothDeviceName = _volumeService.BluetoothDeviceName;
            BluetoothBatteryText = _volumeService.BluetoothBatteryText;

            AvailableDevices.Clear();
            foreach (var dev in _volumeService.AvailableDevices)
            {
                AvailableDevices.Add(dev);
            }
        }

        [RelayCommand]
        public void ToggleDevicePicker()
        {
            if (!IsDevicePickerOpen)
            {
                _volumeService.RefreshAvailableDevices();
                AvailableDevices.Clear();
                foreach (var dev in _volumeService.AvailableDevices)
                {
                    AvailableDevices.Add(dev);
                }
            }
            IsDevicePickerOpen = !IsDevicePickerOpen;
        }

        [RelayCommand]
        public async Task SelectAudioDeviceAsync(AudioDeviceInfo? device)
        {
            if (device == null) return;
            IsDevicePickerOpen = false;
            await _volumeService.SetDefaultDeviceAsync(device.Id);
        }

        [RelayCommand]
        public async Task SetVolumeAsync(double value)
        {
            float scalar = (float)(value / 100.0);
            await _volumeService.SetVolumeAsync(scalar);
        }

        [RelayCommand]
        public async Task ToggleMuteAsync()
        {
            await _volumeService.ToggleMuteAsync();
        }
    }
}
