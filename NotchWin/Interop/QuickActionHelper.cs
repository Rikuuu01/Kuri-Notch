using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace NotchWin.Interop
{
    public static class QuickActionHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

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
