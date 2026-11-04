using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Serilog;

namespace NotchWin.Services
{
    public class VolumeService : IVolumeService, IMMNotificationClient
    {
        #region Win32 PolicyConfig Interop (Audio Endpoint Switching)

        public enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class CPolicyConfigClient { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat();
            [PreserveSig] int GetDeviceFormat();
            [PreserveSig] int ResetDeviceFormat();
            [PreserveSig] int SetDeviceFormat();
            [PreserveSig] int GetProcessingPeriod();
            [PreserveSig] int SetProcessingPeriod();
            [PreserveSig] int GetShareMode();
            [PreserveSig] int SetShareMode();
            [PreserveSig] int GetPropertyValue();
            [PreserveSig] int SetPropertyValue();
            [PreserveSig] int SetDefaultEndpoint([In, MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, [In, MarshalAs(UnmanagedType.U4)] ERole role);
            [PreserveSig] int SetEndpointVisibility();
        }

        #endregion

        #region Win32 Bluetooth Interop

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint dwSize;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnAuthenticated;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnRemembered;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnUnknown;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnConnected;
            [MarshalAs(UnmanagedType.Bool)] public bool fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_INFO
        {
            public uint dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
            [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
            [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string szName;
        }

        [DllImport("bthprops.cpl", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS searchParams, ref BLUETOOTH_DEVICE_INFO deviceInfo);

        [DllImport("bthprops.cpl", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO deviceInfo);

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        #endregion

        private readonly object _deviceLock = new();
        private MMDeviceEnumerator? _enumerator;
        private MMDevice? _defaultDevice;
        private AudioEndpointVolume? _endpointVolume;
        private DispatcherTimer? _btPollTimer;
        private DispatcherTimer? _debounceTimer;

        private float _masterVolume = 1f;
        private bool _isMuted;
        private bool _isInitialized;
        private volatile bool _suppressStartupNotification = true;
        private bool _hasInitialVolumeSnapshot;
        private bool _isBluetoothRadioOn = true;
        private bool _btRadioRefreshInFlight;
        private bool _btRadioStateInitialized;

        public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
        public event EventHandler? DeviceChanged;
        public event EventHandler<string>? BluetoothAudioConnected;
        public event EventHandler<string>? BluetoothAudioDisconnected;
        public event EventHandler<bool>? BluetoothRadioStateChanged;

        public float MasterVolume => _masterVolume;
        public int VolumePercent => (int)Math.Round(Math.Clamp(_masterVolume, 0f, 1f) * 100f);
        public bool IsMuted => _isMuted;

        public string ActiveDeviceName { get; private set; } = "Speaker";
        public bool IsHeadphone { get; private set; }
        public bool IsBluetoothAudio { get; private set; }
        public bool IsBluetoothRadioOn => _isBluetoothRadioOn;
        public string BluetoothDeviceName { get; private set; } = string.Empty;
        public string BluetoothBatteryText { get; private set; } = string.Empty;

        private readonly List<AudioDeviceInfo> _availableDevices = new();
        public IReadOnlyList<AudioDeviceInfo> AvailableDevices
        {
            get
            {
                lock (_deviceLock)
                {
                    return _availableDevices.ToList();
                }
            }
        }

        public Task InitializeAsync()
        {
            if (_isInitialized) return Task.CompletedTask;

            try
            {
                _enumerator = new MMDeviceEnumerator();
                try
                {
                    _enumerator.RegisterEndpointNotificationCallback(this);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "VolumeService: Failed to register endpoint notification callback");
                }

                UpdateDefaultDevice();

                // Periodic lightweight poll to detect bluetooth connect/disconnect if OS notification is delayed
                _btPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8.0) };
                _btPollTimer.Tick += async (s, e) =>
                {
                    RefreshBluetoothRadioState();
                    try
                    {
                        string connectedBt = await Task.Run(() => DetectConnectedBluetoothDevice());
                        bool isBt = !string.IsNullOrEmpty(connectedBt);
                        if (isBt != IsBluetoothAudio || (isBt && connectedBt != BluetoothDeviceName))
                        {
                            TriggerDeviceUpdate();
                        }
                        else if (isBt && (string.IsNullOrEmpty(BluetoothBatteryText) || BluetoothBatteryText == "Terhubung"))
                        {
                            _ = QueryBluetoothBatteryAsync(connectedBt);
                        }
                    }
                    catch { }
                };
                _btPollTimer.Start();

                _isInitialized = true;
                RefreshBluetoothRadioState();
                _ = Task.Delay(1000).ContinueWith(_ => _suppressStartupNotification = false);
                Log.Information("VolumeService initialized via NAudio. Volume={Volume}%, Muted={Muted}, Device={Device}", VolumePercent, _isMuted, ActiveDeviceName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "VolumeService initialization error");
            }

            return Task.CompletedTask;
        }

        public void RefreshBluetoothRadioState()
        {
            if (_btRadioRefreshInFlight) return;
            _btRadioRefreshInFlight = true;
            _ = RefreshBluetoothRadioStateAsync();
        }

        private async Task RefreshBluetoothRadioStateAsync()
        {
            try
            {
                bool isOn;
                bool radioFound = false;
                try
                {
                    var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();
                    isOn = false;
                    foreach (var radio in radios)
                    {
                        if (radio.Kind == Windows.Devices.Radios.RadioKind.Bluetooth)
                        {
                            isOn = radio.State == Windows.Devices.Radios.RadioState.On;
                            radioFound = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "VolumeService: Bluetooth radio state query failed");
                    return;
                }

                // No Bluetooth radio hardware: keep the last known state.
                if (!radioFound) return;

                bool isFirstQuery = !_btRadioStateInitialized;
                _btRadioStateInitialized = true;

                bool radioChanged = isOn != _isBluetoothRadioOn;
                bool changed = radioChanged;
                _isBluetoothRadioOn = isOn;

                if (!isOn && IsBluetoothAudio)
                {
                    IsBluetoothAudio = false;
                    BluetoothDeviceName = string.Empty;
                    BluetoothBatteryText = string.Empty;
                    changed = true;
                }

                if (changed)
                {
                    Log.Debug("VolumeService: Bluetooth radio state changed. On={On}", isOn);
                    SafeInvoke(() =>
                    {
                        DeviceChanged?.Invoke(this, EventArgs.Empty);
                        // Skip the toast for the very first query (startup snapshot).
                        if (radioChanged && !isFirstQuery)
                        {
                            BluetoothRadioStateChanged?.Invoke(this, isOn);
                        }
                    });
                }
            }
            finally
            {
                _btRadioRefreshInFlight = false;
            }
        }

        private void TriggerDeviceUpdate()
        {
            SafeInvoke(() =>
            {
                if (_debounceTimer == null)
                {
                    _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    _debounceTimer.Tick += (s, e) =>
                    {
                        _debounceTimer.Stop();
                        UpdateDefaultDevice();
                    };
                }
                _debounceTimer.Stop();
                _debounceTimer.Start();
            });
        }

        private void UpdateDefaultDevice()
        {
            lock (_deviceLock)
            {
                try
                {
                    float oldVol = _masterVolume;
                    bool oldMute = _isMuted;

                    // 1. Clean up old endpoint volume safely
                    if (_endpointVolume != null)
                    {
                        try { _endpointVolume.OnVolumeNotification -= OnVolumeNotification; } catch { }
                        try { _endpointVolume.Dispose(); } catch { }
                        _endpointVolume = null;
                    }

                    // 2. Clean up old default device safely
                    if (_defaultDevice != null)
                    {
                        try { _defaultDevice.Dispose(); } catch { }
                        _defaultDevice = null;
                    }

                    // 3. Acquire default device with try-catch
                    try
                    {
                        _defaultDevice = _enumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "VolumeService: Default render endpoint not available yet");
                        _defaultDevice = null;
                    }

                    if (_defaultDevice != null)
                    {
                        try
                        {
                            _endpointVolume = _defaultDevice.AudioEndpointVolume;
                            if (_endpointVolume != null)
                            {
                                _masterVolume = _endpointVolume.MasterVolumeLevelScalar;
                                _isMuted = _endpointVolume.Mute;
                                _endpointVolume.OnVolumeNotification += OnVolumeNotification;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "VolumeService: Failed to bind AudioEndpointVolume");
                            _endpointVolume = null;
                        }

                        string rawFriendlyName = "Speaker";
                        try
                        {
                            rawFriendlyName = _defaultDevice.FriendlyName ?? "Speaker";
                        }
                        catch { }

                        ActiveDeviceName = CleanDeviceFriendlyName(rawFriendlyName);

                        bool isHp = rawFriendlyName.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
                                    rawFriendlyName.Contains("Headset", StringComparison.OrdinalIgnoreCase) ||
                                    rawFriendlyName.Contains("Earphone", StringComparison.OrdinalIgnoreCase) ||
                                    rawFriendlyName.Contains("AirPods", StringComparison.OrdinalIgnoreCase) ||
                                    rawFriendlyName.Contains("Buds", StringComparison.OrdinalIgnoreCase) ||
                                    rawFriendlyName.Contains("soundcore", StringComparison.OrdinalIgnoreCase);

                        IsHeadphone = isHp;
                    }
                    else
                    {
                        ActiveDeviceName = "Speaker";
                        IsHeadphone = false;
                    }

                    // 4. Query Bluetooth state safely
                    string lastBtDevice = BluetoothDeviceName;
                    string connectedBtName = DetectConnectedBluetoothDevice();
                    bool wasBluetooth = IsBluetoothAudio;
                    bool isBt = !string.IsNullOrEmpty(connectedBtName);

                    IsBluetoothAudio = isBt;
                    if (isBt)
                    {
                        BluetoothDeviceName = connectedBtName;
                        if (string.IsNullOrEmpty(BluetoothBatteryText))
                        {
                            BluetoothBatteryText = "Terhubung";
                        }
                        _ = QueryBluetoothBatteryAsync(connectedBtName);
                    }
                    else
                    {
                        BluetoothDeviceName = string.Empty;
                        BluetoothBatteryText = string.Empty;
                    }

                    RefreshAvailableDevicesInternal();

                    bool volChanged = Math.Abs(oldVol - _masterVolume) > 0.005f || oldMute != _isMuted;

                    // The first successful endpoint bind is an initial snapshot, not a user change:
                    // listeners must sync state without showing the volume HUD at startup.
                    bool isInitialVolume = _endpointVolume != null && !_hasInitialVolumeSnapshot;
                    if (_endpointVolume != null)
                    {
                        _hasInitialVolumeSnapshot = true;
                    }

                    // 5. Notify changes on UI dispatcher
                    SafeInvoke(() =>
                    {
                        DeviceChanged?.Invoke(this, EventArgs.Empty);
                        if (volChanged)
                        {
                            VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(_masterVolume, _isMuted, isInitialVolume));
                        }
                    });

                    // 6. Connected / Disconnected transient notification triggers
                    if (isBt && !wasBluetooth && !_suppressStartupNotification)
                    {
                        BluetoothAudioConnected?.Invoke(this, BluetoothDeviceName);
                    }
                    else if (!isBt && wasBluetooth && !_suppressStartupNotification)
                    {
                        string devName = !string.IsNullOrEmpty(lastBtDevice) ? lastBtDevice : "Perangkat Audio";
                        BluetoothAudioDisconnected?.Invoke(this, devName);
                    }

                    Log.Debug("VolumeService: Audio update complete. Device={Device}, BT={BtDevice} (Connected={IsBt})", ActiveDeviceName, BluetoothDeviceName, isBt);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "VolumeService: Failed to update audio endpoint");
                }
            }
        }

        private string DetectConnectedBluetoothDevice()
        {
            // 1. Query Win32 native Bluetooth API
            try
            {
                var searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_SEARCH_PARAMS)),
                    fReturnRemembered = true,
                    fReturnConnected = true,
                    fReturnAuthenticated = true,
                    fReturnUnknown = false,
                    fIssueInquiry = false,
                    cTimeoutMultiplier = 0,
                    hRadio = IntPtr.Zero
                };

                var deviceInfo = new BLUETOOTH_DEVICE_INFO
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO))
                };

                IntPtr hFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
                if (hFind != IntPtr.Zero)
                {
                    try
                    {
                        do
                        {
                            if (deviceInfo.fConnected && !string.IsNullOrWhiteSpace(deviceInfo.szName))
                            {
                                return deviceInfo.szName.Trim();
                            }
                        }
                        while (BluetoothFindNextDevice(hFind, ref deviceInfo));
                    }
                    finally
                    {
                        try { BluetoothFindDeviceClose(hFind); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "VolumeService: Error querying Win32 Bluetooth devices");
            }

            // 2. Check if any active NAudio endpoint is a Bluetooth/Headphone device
            if (_enumerator != null)
            {
                try
                {
                    var endpoints = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    if (endpoints != null)
                    {
                        string foundName = string.Empty;
                        for (int i = 0; i < endpoints.Count; i++)
                        {
                            MMDevice? dev = null;
                            try
                            {
                                dev = endpoints[i];
                                string name = dev?.FriendlyName ?? string.Empty;
                                if (string.IsNullOrEmpty(foundName) &&
                                    (name.Contains("soundcore", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("AirPods", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("Buds", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("WH-", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("WF-", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("FreeBuds", StringComparison.OrdinalIgnoreCase)))
                                {
                                    foundName = CleanDeviceFriendlyName(name);
                                }
                            }
                            catch { }
                            finally
                            {
                                try { dev?.Dispose(); } catch { }
                            }
                        }

                        if (!string.IsNullOrEmpty(foundName))
                        {
                            return foundName;
                        }
                    }
                }
                catch { }
            }

            return string.Empty;
        }

        private static string CleanDeviceFriendlyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Speaker";

            int openParen = name.IndexOf('(');
            int closeParen = name.LastIndexOf(')');
            if (openParen > 0 && closeParen > openParen)
            {
                string inner = name.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                string prefix = name.Substring(0, openParen).Trim();

                if (prefix.Equals("Headphones", StringComparison.OrdinalIgnoreCase) ||
                    prefix.Equals("Headset", StringComparison.OrdinalIgnoreCase) ||
                    prefix.Equals("Speakers", StringComparison.OrdinalIgnoreCase))
                {
                    return inner.Replace("(R)", "").Replace("(TM)", "").Trim();
                }
            }

            return name.Replace("(R)", "").Replace("(TM)", "").Trim();
        }

        private async Task QueryBluetoothBatteryAsync(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName) || !IsBluetoothAudio) return;

            try
            {
                string[] requestedProperties = new string[]
                {
                    "System.Devices.Aep.DeviceAddress",
                    "System.Devices.Aep.IsConnected",
                    "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2"
                };

                var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                    @"System.Devices.Aep.ProtocolId:=""{e0cbf06c-cd35-46ae-bb8a-263b43f0f974}""",
                    requestedProperties,
                    Windows.Devices.Enumeration.DeviceInformationKind.AssociationEndpoint);

                if (!IsBluetoothAudio) return;

                foreach (var d in devices)
                {
                    if (d.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connObj) &&
                        connObj is bool isConn && isConn)
                    {
                        if (deviceName.Contains(d.Name, StringComparison.OrdinalIgnoreCase) ||
                            d.Name.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (d.Properties.TryGetValue("{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2", out var battObj) && battObj != null)
                            {
                                if (byte.TryParse(battObj.ToString(), out byte battVal))
                                {
                                    BluetoothBatteryText = $"{battVal}%";
                                    SafeInvoke(() => DeviceChanged?.Invoke(this, EventArgs.Empty));
                                    return;
                                }
                            }

                            BluetoothBatteryText = "Terhubung";
                            SafeInvoke(() => DeviceChanged?.Invoke(this, EventArgs.Empty));
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "VolumeService: Failed to query Bluetooth battery");
            }

            if (IsBluetoothAudio && string.IsNullOrEmpty(BluetoothBatteryText))
            {
                BluetoothBatteryText = "Terhubung";
                SafeInvoke(() => DeviceChanged?.Invoke(this, EventArgs.Empty));
            }
        }

        private void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            try
            {
                _masterVolume = data.MasterVolume;
                _isMuted = data.Muted;

                if (_suppressStartupNotification)
                {
                    Log.Debug("VolumeService: Suppressed startup notification (Volume={Volume}%)", VolumePercent);
                    return;
                }

                Log.Debug("VolumeService: OnVolumeNotification received. Volume={Volume}%, Muted={Muted}", VolumePercent, _isMuted);
                SafeInvoke(() => VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(_masterVolume, _isMuted)));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "VolumeService: OnVolumeNotification error");
            }
        }

        public Task SetVolumeAsync(float volumeScalar)
        {
            if (_endpointVolume != null)
            {
                try
                {
                    float clamped = Math.Clamp(volumeScalar, 0f, 1f);
                    _endpointVolume.MasterVolumeLevelScalar = clamped;
                    _masterVolume = clamped;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "VolumeService: Failed to set master volume");
                }
            }
            return Task.CompletedTask;
        }

        public Task ToggleMuteAsync()
        {
            if (_endpointVolume != null)
            {
                try
                {
                    bool newMute = !_isMuted;
                    _endpointVolume.Mute = newMute;
                    _isMuted = newMute;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "VolumeService: Failed to toggle mute");
                }
            }
            return Task.CompletedTask;
        }

        public Task SetDefaultDeviceAsync(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return Task.CompletedTask;

            try
            {
                var client = new CPolicyConfigClient() as IPolicyConfig;
                if (client != null)
                {
                    client.SetDefaultEndpoint(deviceId, ERole.eConsole);
                    client.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                    client.SetDefaultEndpoint(deviceId, ERole.eCommunications);
                    Log.Information("VolumeService: Successfully switched default audio device to {DeviceId}", deviceId);

                    TriggerDeviceUpdate();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "VolumeService: Failed to switch default audio device to {DeviceId}", deviceId);
            }

            return Task.CompletedTask;
        }

        public void RefreshAvailableDevices()
        {
            RefreshAvailableDevicesInternal();
            SafeInvoke(() => DeviceChanged?.Invoke(this, EventArgs.Empty));
        }

        private void RefreshAvailableDevicesInternal()
        {
            lock (_deviceLock)
            {
                _availableDevices.Clear();
                if (_enumerator == null) return;

                try
                {
                    var endpoints = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    if (endpoints != null)
                    {
                        string currentDefaultId = string.Empty;
                        try { currentDefaultId = _defaultDevice?.ID ?? string.Empty; } catch { }

                        for (int i = 0; i < endpoints.Count; i++)
                        {
                            MMDevice? dev = null;
                            try
                            {
                                dev = endpoints[i];
                                string id = dev?.ID ?? string.Empty;
                                string rawName = dev?.FriendlyName ?? "Speaker";
                                string cleanName = CleanDeviceFriendlyName(rawName);
                                bool isHp = rawName.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
                                            rawName.Contains("Headset", StringComparison.OrdinalIgnoreCase) ||
                                            rawName.Contains("Earphone", StringComparison.OrdinalIgnoreCase) ||
                                            rawName.Contains("AirPods", StringComparison.OrdinalIgnoreCase) ||
                                            rawName.Contains("Buds", StringComparison.OrdinalIgnoreCase) ||
                                            rawName.Contains("soundcore", StringComparison.OrdinalIgnoreCase);
                                bool isActive = !string.IsNullOrEmpty(currentDefaultId) && id.Equals(currentDefaultId, StringComparison.OrdinalIgnoreCase);

                                if (!string.IsNullOrEmpty(id))
                                {
                                    _availableDevices.Add(new AudioDeviceInfo(id, cleanName, isHp, isActive));
                                }
                            }
                            catch { }
                            finally
                            {
                                try { dev?.Dispose(); } catch { }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "VolumeService: Failed to enumerate available audio devices");
                }
            }
        }

        private void SafeInvoke(Action action)
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

        #region IMMNotificationClient Implementation

        void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            TriggerDeviceUpdate();
        }

        void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId)
        {
            TriggerDeviceUpdate();
        }

        void IMMNotificationClient.OnDeviceRemoved(string deviceId)
        {
            TriggerDeviceUpdate();
        }

        void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && (role == Role.Multimedia || role == Role.Console))
            {
                TriggerDeviceUpdate();
            }
        }

        void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }

        #endregion

        public void Dispose()
        {
            try
            {
                _btPollTimer?.Stop();
                _debounceTimer?.Stop();

                if (_enumerator != null)
                {
                    try
                    {
                        _enumerator.UnregisterEndpointNotificationCallback(this);
                    }
                    catch { }
                }

                if (_endpointVolume != null)
                {
                    try { _endpointVolume.OnVolumeNotification -= OnVolumeNotification; } catch { }
                    try { _endpointVolume.Dispose(); } catch { }
                    _endpointVolume = null;
                }

                if (_defaultDevice != null)
                {
                    try { _defaultDevice.Dispose(); } catch { }
                    _defaultDevice = null;
                }

                if (_enumerator != null)
                {
                    try { _enumerator.Dispose(); } catch { }
                    _enumerator = null;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "VolumeService dispose error");
            }
        }
    }
}
