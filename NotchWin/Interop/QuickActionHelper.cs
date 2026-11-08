using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using Serilog;

namespace NotchWin.Interop
{
    public static class QuickActionHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        private const int HWND_BROADCAST = 0xffff;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        public static void SetKeepAwake(bool enable)
        {
            try
            {
                if (enable)
                {
                    SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
                    Log.Information("QuickAction: KeepAwake ENABLED");
                }
                else
                {
                    SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                    Log.Information("QuickAction: KeepAwake DISABLED");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QuickAction: Failed to set execution state for KeepAwake");
            }
        }

        public static bool ToggleMicrophoneMute()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                if (mic != null)
                {
                    mic.AudioEndpointVolume.Mute = !mic.AudioEndpointVolume.Mute;
                    Log.Information("QuickAction: Microphone mute toggled to {Mute}", mic.AudioEndpointVolume.Mute);
                    return mic.AudioEndpointVolume.Mute;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "QuickAction: Failed to toggle microphone mute");
            }
            return false;
        }

        public static bool GetMicrophoneMute()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                if (mic != null)
                {
                    return mic.AudioEndpointVolume.Mute;
                }
            }
            catch { }
            return false;
        }

        public static bool GetIsDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0;
                    }
                }
            }
            catch { }
            return true;
        }

        public static bool ToggleDarkMode()
        {
            try
            {
                bool isDark = GetIsDarkMode();
                int nextVal = isDark ? 1 : 0;
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
                if (key != null)
                {
                    key.SetValue("AppsUseLightTheme", nextVal, RegistryValueKind.DWord);
                    key.SetValue("SystemUsesLightTheme", nextVal, RegistryValueKind.DWord);
                    SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 100, out _);
                    Log.Information("QuickAction: DarkMode toggled to {IsDark}", !isDark);
                    return !isDark;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QuickAction: Failed to toggle dark mode");
            }
            return false;
        }

        public static void OpenAvailableNetworks()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-availablenetworks:") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "QuickAction: Failed to open ms-availablenetworks:");
            }
        }

        public static void OpenBluetoothSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "QuickAction: Failed to open ms-settings:bluetooth");
            }
        }

        public static void OpenAirplaneModeSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:network-airplanemode") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "QuickAction: Failed to open ms-settings:network-airplanemode");
            }
        }

        public static void LaunchSnippingTool()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-screenclip:")
                {
                    UseShellExecute = true
                });
                Log.Information("QuickAction: Snipping Tool triggered via ms-screenclip:");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QuickAction: Failed to launch Snipping Tool");
            }
        }

        public static void LockComputer()
        {
            try
            {
                LockWorkStation();
                Log.Information("QuickAction: LockWorkStation executed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QuickAction: Failed to lock workstation");
            }
        }

        public static void LaunchTaskManager()
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskmgr.exe")
                {
                    UseShellExecute = true
                });
                Log.Information("QuickAction: Task Manager launched");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QuickAction: Failed to launch Task Manager");
            }
        }
    }
}
