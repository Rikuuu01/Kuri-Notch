using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace NotchWin.Services
{
    public class BluetoothDeviceItem : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public bool IsPaired { get; set; } = true;
        public string DeviceType { get; set; } = "other"; // "audio", "mouse", "phone", "other"
        public string StatusText => IsConnected ? "Terhubung" : "Dipasangkan";
    }

    public class BluetoothManagerService : ObservableObject
    {
        private bool _isScanning;

        public ObservableCollection<BluetoothDeviceItem> Devices { get; } = new();

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    NotifyStateChanged();
                }
            }
        }

        public bool ShowSearching => IsScanning;
        public bool ShowEmpty => !IsScanning && Devices.Count == 0;
        public bool ShowList => !IsScanning && Devices.Count > 0;

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(ShowSearching));
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowList));
        }

        public async Task RefreshDevicesAsync(string? activeAudioDeviceName = null)
        {
            if (IsScanning) return;
            App.Current?.Dispatcher?.Invoke(() =>
            {
                Devices.Clear();
                NotifyStateChanged();
            });
            IsScanning = true;
            NotifyStateChanged();
            try
            {
                await Task.Run(async () =>
                {
                    var list = new List<BluetoothDeviceItem>();
                    try
                    {
                        string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                        var deviceInfos = await DeviceInformation.FindAllAsync(selector);

                        foreach (var info in deviceInfos)
                        {
                            if (string.IsNullOrWhiteSpace(info.Name)) continue;

                            try
                            {
                                using var bt = await BluetoothDevice.FromIdAsync(info.Id);
                                if (bt != null)
                                {
                                    bool isConn = bt.ConnectionStatus == BluetoothConnectionStatus.Connected;
                                    if (!string.IsNullOrEmpty(activeAudioDeviceName) &&
                                        (info.Name.Contains(activeAudioDeviceName, StringComparison.OrdinalIgnoreCase) ||
                                         activeAudioDeviceName.Contains(info.Name, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        isConn = true;
                                    }

                                    string type = bt.ClassOfDevice?.MajorClass switch
                                    {
                                        BluetoothMajorClass.AudioVideo => "audio",
                                        BluetoothMajorClass.Peripheral => "mouse",
                                        BluetoothMajorClass.Phone => "phone",
                                        _ => "other"
                                    };

                                    list.Add(new BluetoothDeviceItem
                                    {
                                        Id = info.Id,
                                        Name = info.Name,
                                        IsConnected = isConn,
                                        IsPaired = true,
                                        DeviceType = type
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "BluetoothManagerService: Error scanning devices");
                    }

                    // Sort: Connected first, then Audio, then by Name
                    list.Sort((a, b) =>
                    {
                        if (a.IsConnected != b.IsConnected) return b.IsConnected ? 1 : -1;
                        if (a.DeviceType == "audio" && b.DeviceType != "audio") return -1;
                        if (a.DeviceType != "audio" && b.DeviceType == "audio") return 1;
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    });

                    App.Current?.Dispatcher?.Invoke(() =>
                    {
                        Devices.Clear();
                        foreach (var dev in list)
                        {
                            Devices.Add(dev);
                        }
                        NotifyStateChanged();
                    });
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "BluetoothManagerService: RefreshDevicesAsync error");
            }
            finally
            {
                IsScanning = false;
                NotifyStateChanged();
            }
        }
    }
}
