using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Serilog;

namespace NotchWin.Services
{
    [ComImport, Guid("477B008D-7F0A-4084-BDFD-DF19E2A82817")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorActivitiesReportCallback
    {
        [PreserveSig] int OnActivitiesReport(IMFSensorActivitiesReport sensorActivitiesReport);
    }

    [ComImport, Guid("683F7A5E-4A19-43CD-B1A9-DBF4AB3F7777")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorActivitiesReport
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetActivityReport(uint index, [MarshalAs(UnmanagedType.IUnknown)] out object sensorActivityReport);
    }

    [ComImport, Guid("D0CEF145-B3F4-4340-A2E5-7A5080CA05CB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSensorActivityMonitor
    {
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
    }

    public class PrivacyService : IPrivacyService, IMFSensorActivitiesReportCallback
    {
        [DllImport("mfplat.dll")] private static extern int MFStartup(uint version, uint dwFlags = 0);
        [DllImport("mfplat.dll")] private static extern int MFShutdown();
        [DllImport("mfsensorgroup.dll")] private static extern int MFCreateSensorActivityMonitor(IMFSensorActivitiesReportCallback pCallback, out IMFSensorActivityMonitor ppActivityMonitor);

        private readonly DispatcherTimer _pollTimer;
        private readonly MMDeviceEnumerator _audioEnumerator = new();
        private IMFSensorActivityMonitor? _sensorMonitor;
        private bool _isMfStarted;

        private volatile bool _isCameraActiveFromSensor;
        private bool _isCameraInUse;
        private bool _isMicrophoneInUse;
        private bool _isDisposed;

        public bool IsCameraInUse => _isCameraInUse;
        public bool IsMicrophoneInUse => _isMicrophoneInUse;

        public event EventHandler? PrivacyStateChanged;

        private bool _isChecking;

        public PrivacyService()
        {
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(4000)
            };
            _pollTimer.Tick += OnPollTick;

            InitializeCameraSensorMonitor();
        }

        private void InitializeCameraSensorMonitor()
        {
            try
            {
                int hr = MFStartup(0x00020070); // MF_VERSION
                if (hr == 0)
                {
                    _isMfStarted = true;
                    hr = MFCreateSensorActivityMonitor(this, out _sensorMonitor);
                    if (hr == 0 && _sensorMonitor != null)
                    {
                        hr = _sensorMonitor.Start();
                        if (hr == 0)
                        {
                            Log.Information("MediaFoundation Camera Activity Monitor started successfully");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not initialize MediaFoundation Camera Activity Monitor; registry fallback will be used");
            }
        }

        int IMFSensorActivitiesReportCallback.OnActivitiesReport(IMFSensorActivitiesReport report)
        {
            try
            {
                report.GetCount(out uint count);
                bool active = count > 0;
                if (_isCameraActiveFromSensor != active)
                {
                    _isCameraActiveFromSensor = active;
                    Log.Information("MediaFoundation Camera Sensor Report: ActiveSensors={Count}", count);
                    CheckPrivacyStatus();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error handling sensor activities report");
            }
            return 0;
        }

        public void Start()
        {
            if (!_pollTimer.IsEnabled)
            {
                _pollTimer.Start();
                CheckPrivacyStatus();
            }
        }

        public void Stop()
        {
            if (_pollTimer.IsEnabled)
            {
                _pollTimer.Stop();
            }
        }

        public void Pause()
        {
            Stop();
        }

        public void Resume()
        {
            Start();
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            CheckPrivacyStatus();
        }

        private async void CheckPrivacyStatus()
        {
            if (_isChecking) return;
            _isChecking = true;

            try
            {
                bool camSensor = _isCameraActiveFromSensor;
                var (cam, mic) = await Task.Run(() =>
                {
                    bool c = camSensor || (!(_isMfStarted && _sensorMonitor != null) && CheckCapabilityInUse("webcam"));
                    bool m = CheckMicrophoneCapture() || CheckCapabilityInUse("microphone");
                    return (c, m);
                });

                if (cam != _isCameraInUse || mic != _isMicrophoneInUse)
                {
                    _isCameraInUse = cam;
                    _isMicrophoneInUse = mic;

                    Log.Information("Privacy State Changed: CameraInUse={Cam}, MicInUse={Mic}", cam, mic);
                    PrivacyStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking privacy status");
            }
            finally
            {
                _isChecking = false;
            }
        }

        private bool CheckMicrophoneCapture()
        {
            try
            {
                var captureDevices = _audioEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                bool isCapturing = false;

                foreach (var dev in captureDevices)
                {
                    try
                    {
                        if (!isCapturing)
                        {
                            // 1. Check for any active Audio Session (Discord, Zoom, Google Meet, OBS, Games, etc.)
                            var sessionManager = dev.AudioSessionManager;
                            if (sessionManager != null)
                            {
                                var sessions = sessionManager.Sessions;
                                for (int i = 0; i < sessions.Count; i++)
                                {
                                    var s = sessions[i];
                                    if (s.State == AudioSessionState.AudioSessionStateActive)
                                    {
                                        isCapturing = true;
                                        break;
                                    }
                                }
                            }

                            // 2. Check real-time Audio Meter peak if streaming audio
                            if (!isCapturing)
                            {
                                var meter = dev.AudioMeterInformation;
                                if (meter != null && meter.MasterPeakValue > 0.005f)
                                {
                                    isCapturing = true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore individual endpoint errors
                    }
                    finally
                    {
                        try { dev.Dispose(); } catch { }
                    }
                }

                return isCapturing;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error enumerating audio capture endpoints in PrivacyService");
            }

            return false;
        }

        private static bool CheckCapabilityInUse(string capability)
        {
            string subKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{capability}";

            var hives = new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            var views = new[] { RegistryView.Registry64 };

            foreach (var hive in hives)
            {
                foreach (var view in views)
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var capKey = baseKey.OpenSubKey(subKeyPath);
                        if (capKey != null && ScanKeyForActiveUsage(capKey, capability))
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error reading registry for {Capability} in hive {Hive}", capability, hive);
                    }
                }
            }

            return false;
        }

        private static bool ScanKeyForActiveUsage(RegistryKey key, string capability, int depth = 0)
        {
            if (depth >= 2) return false;
            if (IsKeyActivelyInUse(key, capability))
            {
                return true;
            }

            string[] subKeyNames;
            try
            {
                subKeyNames = key.GetSubKeyNames();
            }
            catch
            {
                return false;
            }

            foreach (var subName in subKeyNames)
            {
                try
                {
                    using var subKey = key.OpenSubKey(subName);
                    if (subKey != null && ScanKeyForActiveUsage(subKey, capability, depth + 1))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore inaccessible subkeys
                }
            }

            return false;
        }

        private static bool IsKeyActivelyInUse(RegistryKey key, string capability)
        {
            try
            {
                object? stopVal = key.GetValue("LastUsedTimeStop");
                object? startVal = key.GetValue("LastUsedTimeStart");

                if (stopVal == null && startVal == null)
                {
                    return false;
                }

                long stopTime = 0;
                long startTime = 0;

                if (stopVal != null)
                {
                    try { stopTime = Convert.ToInt64(stopVal); } catch { }
                }

                if (startVal != null)
                {
                    try { startTime = Convert.ToInt64(startVal); } catch { }
                }

                // If stopVal exists and is explicitly 0, app is actively streaming
                if (stopVal != null && stopTime == 0)
                {
                    Log.Debug("Active {Capability} usage detected in {KeyName} (StopTime=0)", capability, key.Name);
                    return true;
                }

                // If start time is more recent than stop time, device is actively open
                if (startTime > 0 && startTime > stopTime)
                {
                    Log.Debug("Active {Capability} usage detected in {KeyName} (StartTime={StartTime} > StopTime={StopTime})", capability, key.Name, startTime, stopTime);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _pollTimer.Stop();

                try
                {
                    _sensorMonitor?.Stop();
                    if (_isMfStarted)
                    {
                        MFShutdown();
                    }
                }
                catch { }

                try
                {
                    _audioEnumerator.Dispose();
                }
                catch { }
            }
        }
    }
}
