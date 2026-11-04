using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NotchWin.Services
{
    public class AudioDeviceInfo
    {
        public string Id { get; }
        public string Name { get; }
        public bool IsHeadphone { get; }
        public bool IsActive { get; set; }
        public string Icon => IsHeadphone ? "🎧" : "🔊";

        public AudioDeviceInfo(string id, string name, bool isHeadphone, bool isActive = false)
        {
            Id = id;
            Name = name;
            IsHeadphone = isHeadphone;
            IsActive = isActive;
        }
    }

    public class VolumeChangedEventArgs : EventArgs
    {
        public float MasterVolume { get; }
        public bool IsMuted { get; }
        public bool IsInitial { get; }
        public int VolumePercent => (int)Math.Round(Math.Clamp(MasterVolume, 0f, 1f) * 100f);

        public VolumeChangedEventArgs(float masterVolume, bool isMuted, bool isInitial = false)
        {
            MasterVolume = masterVolume;
            IsMuted = isMuted;
            IsInitial = isInitial;
        }
    }

    public interface IVolumeService : IDisposable
    {
        event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
        event EventHandler? DeviceChanged;
        event EventHandler<string>? BluetoothAudioConnected;
        event EventHandler<string>? BluetoothAudioDisconnected;
        event EventHandler<bool>? BluetoothRadioStateChanged;

        float MasterVolume { get; }
        int VolumePercent { get; }
        bool IsMuted { get; }
        string ActiveDeviceName { get; }
        bool IsHeadphone { get; }
        bool IsBluetoothAudio { get; }
        bool IsBluetoothRadioOn { get; }
        string BluetoothDeviceName { get; }
        string BluetoothBatteryText { get; }
        IReadOnlyList<AudioDeviceInfo> AvailableDevices { get; }

        Task InitializeAsync();
        Task SetVolumeAsync(float volumeScalar);
        Task ToggleMuteAsync();
        Task SetDefaultDeviceAsync(string deviceId);
        void RefreshAvailableDevices();
        void RefreshBluetoothRadioState();
    }
}
