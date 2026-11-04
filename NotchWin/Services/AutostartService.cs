using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Serilog;

namespace NotchWin.Services
{
    public class AutostartService : IAutostartService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NotchWin";
        private const string TaskNamePrefix = "Kuri Autostart";
        private const string HelperArgumentPrefix = "--autostart-task=";
        private const int ElevationCancelledErrorCode = 1223;

        private static readonly string TaskName = $"{TaskNamePrefix}_{GetCurrentUserSid()}";

        private static string UserStartupShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            $"{AppName}.lnk"
        );

        private static string CommonStartupShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            $"{AppName}.lnk"
        );

        public AutostartMode GetAutostartMode()
        {
            if (IsLogonTaskRegistered()) return AutostartMode.LogonTask;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (key?.GetValue(AppName) != null) return AutostartMode.RunKey;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read the legacy autostart registry value");
            }

            if (File.Exists(UserStartupShortcutPath) || File.Exists(CommonStartupShortcutPath))
            {
                return AutostartMode.RunKey;
            }

            return AutostartMode.Disabled;
        }

        public AutostartMode SetAutostart(bool enable)
        {
            try
            {
                CleanStartupShortcuts();
                return enable ? EnableAutostart() : DisableAutostart();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set autostart to {Enable}", enable);
                return GetAutostartMode();
            }
        }

        private static AutostartMode EnableAutostart()
        {
            if (IsLogonTaskRegistered())
            {
                RemoveRunEntry();
                return AutostartMode.LogonTask;
            }

            // The helper is verified from this process, so a task registered under a different
            // elevated account (over-the-shoulder UAC) is rejected and we fall back to the Run key.
            if (TryInvokeHelper("enable") && IsLogonTaskRegistered())
            {
                RemoveRunEntry();
                return AutostartMode.LogonTask;
            }

            if (WriteRunEntry())
            {
                Log.Warning("Logon task unavailable; autostart fell back to the HKCU Run key");
                return AutostartMode.RunKey;
            }

            return AutostartMode.Disabled;
        }

        private static AutostartMode DisableAutostart()
        {
            if (IsLogonTaskRegistered())
            {
                bool removed = TryInvokeHelper("disable") && !IsLogonTaskRegistered();
                if (!removed)
                {
                    Log.Warning("Logon task could not be removed; autostart remains enabled");
                    return AutostartMode.LogonTask;
                }
            }

            RemoveRunEntry();
            return AutostartMode.Disabled;
        }

        private static bool TryInvokeHelper(string command)
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Error("Cannot resolve executable path to run the autostart helper");
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = HelperArgumentPrefix + command,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if (IsElevated())
                {
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;
                }
                else
                {
                    startInfo.UseShellExecute = true;
                    startInfo.Verb = "runas";
                }

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                if (!process.WaitForExit(120_000))
                {
                    Log.Warning("Autostart helper did not finish within the timeout");
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    Log.Warning("Autostart helper {Command} exited with code {ExitCode}", command, process.ExitCode);
                    return false;
                }

                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ElevationCancelledErrorCode)
            {
                Log.Information("Elevation prompt for autostart {Command} was cancelled", command);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to run the autostart helper for {Command}", command);
                return false;
            }
        }

        public static bool IsLogonTaskRegistered()
        {
            return RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;
        }

        public static bool RegisterLogonTask()
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Error("Cannot resolve executable path for the logon task");
                return false;
            }

            string workingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string xmlPath = Path.Combine(Path.GetTempPath(), $"kuri-autostart-{Guid.NewGuid():N}.xml");

            try
            {
                File.WriteAllText(xmlPath, BuildTaskXml(exePath, workingDirectory), new UnicodeEncoding(false, true));

                int exitCode = RunSchtasks($"/Create /F /TN \"{TaskName}\" /XML \"{xmlPath}\"");
                if (exitCode != 0)
                {
                    Log.Error("schtasks failed to create the logon task (exit code {ExitCode})", exitCode);
                    return false;
                }

                bool registered = IsLogonTaskRegistered();
                if (registered)
                {
                    Log.Information("Registered logon task {TaskName} -> {ExePath}", TaskName, exePath);
                }

                return registered;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register the logon task {TaskName}", TaskName);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(xmlPath)) File.Delete(xmlPath);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Could not delete the temporary task XML");
                }
            }
        }

        public static bool UnregisterLogonTask()
        {
            try
            {
                int exitCode = RunSchtasks($"/Delete /F /TN \"{TaskName}\"");
                if (exitCode != 0 && IsLogonTaskRegistered())
                {
                    Log.Error("schtasks failed to delete the logon task (exit code {ExitCode})", exitCode);
                    return false;
                }

                Log.Information("Removed logon task {TaskName}", TaskName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove the logon task {TaskName}", TaskName);
                return false;
            }
        }

        private static string BuildTaskXml(string exePath, string workingDirectory)
        {
            using var identity = WindowsIdentity.GetCurrent();
            string userName = identity.Name;
            string sid = identity.User?.Value ?? userName;

            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Starts Kuri automatically when you sign in to Windows.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{EscapeXml(userName)}</UserId>
      <Delay>PT1S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{EscapeXml(sid)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{EscapeXml(exePath)}</Command>
      <WorkingDirectory>{EscapeXml(workingDirectory)}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
        }

        private static int RunSchtasks(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(startInfo);
                if (process == null) return -1;
                if (!process.WaitForExit(30_000)) return -1;

                return process.ExitCode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to run schtasks with arguments {Arguments}", arguments);
                return -1;
            }
        }

        private static bool WriteRunEntry()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return false;

                using var key = Registry.CurrentUser.CreateSubKey(RunRegistryKey, true);
                if (key == null) return false;

                key.SetValue(AppName, $"\"{exePath}\"");
                Log.Information("Autostart enabled through the HKCU Run key: {Path}", exePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to write the legacy autostart registry value");
                return false;
            }
        }

        private static void RemoveRunEntry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key?.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                    Log.Information("Removed the legacy HKCU Run autostart entry");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to remove the legacy autostart registry value");
            }
        }

        private static void CleanStartupShortcuts()
        {
            CleanStartupShortcut(UserStartupShortcutPath);
            CleanStartupShortcut(CommonStartupShortcutPath);
        }

        private static void CleanStartupShortcut(string shortcutPath)
        {
            try
            {
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                    Log.Information("Removed redundant startup shortcut: {Path}", shortcutPath);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not delete startup shortcut: {Path}", shortcutPath);
            }
        }

        private static bool IsElevated()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string GetCurrentUserSid()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity.User?.Value ?? "unknown";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not resolve the current user SID for the logon task name");
                return "unknown";
            }
        }

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
