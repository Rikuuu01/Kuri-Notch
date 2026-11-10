using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using System.Threading;
using NotchWin.Interop;
using NotchWin.Models;

namespace NotchWin.Services
{
    public class ScreenTimeService : IScreenTimeService
    {
        private readonly string _storageFilePath;
        private readonly System.Threading.Timer _trackerTimer;
        private readonly object _lock = new();

        private ScreenTimeStore _store = new();
        private string? _lastActiveExe;
        private int _saveCounter;

        private readonly ConcurrentDictionary<uint, (string ExeName, string FriendlyName, string ExePath, string Category)> _processCache = new();

        public event EventHandler? DataUpdated;

        public bool IsEnabled { get; set; } = true;
        public int IdleThresholdSeconds { get; set; } = 120;

        public ScreenTimeService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appData, "NotchWin");
            Directory.CreateDirectory(appDir);
            _storageFilePath = Path.Combine(appDir, "screentime.json");

            LoadStore();

            _trackerTimer = new System.Threading.Timer(OnTrackerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private void LoadStore()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_storageFilePath))
                    {
                        string json = File.ReadAllText(_storageFilePath);
                        _store = JsonSerializer.Deserialize<ScreenTimeStore>(json) ?? new ScreenTimeStore();
                    }
                    else
                    {
                        // Check if Poni's screentime.json exists to seamlessly import initial history
                        string poniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Poni", "screentime.json");
                        if (File.Exists(poniPath))
                        {
                            string poniJson = File.ReadAllText(poniPath);
                            _store = JsonSerializer.Deserialize<ScreenTimeStore>(poniJson) ?? new ScreenTimeStore();
                            // Save copy to NotchWin immediately
                            File.WriteAllText(_storageFilePath, poniJson);
                        }
                        else
                        {
                            _store = new ScreenTimeStore();
                        }
                    }

                    // Prune old data (> 35 days)
                    PruneOldData();
                }
                catch
                {
                    _store = new ScreenTimeStore();
                }
            }
        }

        private void PruneOldData()
        {
            DateTime cutoff = DateTime.Today.AddDays(-35);
            var toRemove = new List<string>();

            foreach (var key in _store.Days.Keys)
            {
                if (DateTime.TryParse(key, out var dt) && dt < cutoff)
                {
                    toRemove.Add(key);
                }
            }

            foreach (var key in toRemove)
            {
                _store.Days.Remove(key);
            }
        }

        public void ForceSave()
        {
            lock (_lock)
            {
                try
                {
                    PruneOldData();
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_store, options);
                    File.WriteAllText(_storageFilePath, json);
                }
                catch
                {
                    // Ignore transient file I/O errors
                }
            }
        }

        private void OnTrackerTick(object? state)
        {
            if (!IsEnabled)
            {
                return;
            }

            // 1. Idle Detection via GetLastInputInfo
            var lii = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
            if (NativeMethods.GetLastInputInfo(ref lii))
            {
                uint win32Ticks = NativeMethods.GetTickCount();
                uint idleMs = win32Ticks >= lii.dwTime ? (win32Ticks - lii.dwTime) : (uint.MaxValue - lii.dwTime + win32Ticks);
                if (idleMs >= (uint)(IdleThresholdSeconds * 1000))
                {
                    // User is idle, exclude from screen time
                    return;
                }
            }

            // 2. Foreground Window Detection
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            uint pid = 0;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            }

            // 3. Resolve Process Info (with PID caching)
            (string ExeName, string FriendlyName, string ExePath, string Category) procInfo = default;
            if (pid != 0)
            {
                if (!_processCache.TryGetValue(pid, out procInfo))
                {
                    procInfo = ResolveProcessInfo(hwnd, pid);
                    if (procInfo.ExeName.Length > 0)
                    {
                        _processCache[pid] = procInfo;
                    }
                }
            }

            // Fallback: If hwnd is 0 or resolution returned empty but we have a last active exe, continue tracking it
            if (string.IsNullOrEmpty(procInfo.ExeName) && !string.IsNullOrEmpty(_lastActiveExe))
            {
                foreach (var cached in _processCache.Values)
                {
                    if (string.Equals(cached.ExeName, _lastActiveExe, StringComparison.OrdinalIgnoreCase))
                    {
                        procInfo = cached;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(procInfo.ExeName))
            {
                return;
            }

            string todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            int currentHour = DateTime.Now.Hour;

            bool isAppSwitch = false;
            if (_lastActiveExe != null && !string.Equals(_lastActiveExe, procInfo.ExeName, StringComparison.OrdinalIgnoreCase))
            {
                isAppSwitch = true;
            }
            _lastActiveExe = procInfo.ExeName;

            lock (_lock)
            {
                if (!_store.Days.TryGetValue(todayKey, out var dayData))
                {
                    dayData = new DayScreenTimeData();
                    _store.Days[todayKey] = dayData;
                }

                // Make sure hours array has 24 elements
                if (dayData.Hours == null || dayData.Hours.Length != 24)
                {
                    var hours = new long[24];
                    if (dayData.Hours != null)
                    {
                        Array.Copy(dayData.Hours, hours, Math.Min(dayData.Hours.Length, 24));
                    }
                    dayData.Hours = hours;
                }

                // Increment app seconds
                if (!dayData.Apps.TryGetValue(procInfo.ExeName, out var appItem))
                {
                    appItem = new AppUsageItem
                    {
                        Name = procInfo.FriendlyName,
                        Path = procInfo.ExePath,
                        Category = procInfo.Category,
                        Seconds = 0
                    };
                    dayData.Apps[procInfo.ExeName] = appItem;
                }
                appItem.Seconds++;

                // Increment category
                string cat = procInfo.Category;
                if (!dayData.Categories.ContainsKey(cat))
                {
                    dayData.Categories[cat] = 0;
                }
                dayData.Categories[cat]++;

                // Increment hour
                if (currentHour >= 0 && currentHour < 24)
                {
                    dayData.Hours[currentHour]++;
                }

                // Increment switches
                if (isAppSwitch)
                {
                    dayData.Switches++;
                }
            }

            // Fire event for UI update on Dispatcher
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    DataUpdated?.Invoke(this, EventArgs.Empty);
                }));
            }
            else
            {
                DataUpdated?.Invoke(this, EventArgs.Empty);
            }

            // Periodic auto-save every 30 seconds
            _saveCounter++;
            if (_saveCounter >= 30)
            {
                _saveCounter = 0;
                ForceSave();

                // Periodic cache cleanup
                if (_processCache.Count > 100)
                {
                    _processCache.Clear();
                }
            }
        }

        private (string ExeName, string FriendlyName, string ExePath, string Category) ResolveProcessInfo(IntPtr hwnd, uint pid)
        {
            string exePath = string.Empty;
            string exeName = string.Empty;
            string friendlyName = string.Empty;

            try
            {
                IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024);
                        int size = sb.Capacity;
                        if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                        {
                            exePath = sb.ToString();
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hProcess);
                    }
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    var proc = Process.GetProcessById((int)pid);
                    exeName = proc.ProcessName.ToLowerInvariant();
                    if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        exeName += ".exe";
                    }
                    friendlyName = proc.ProcessName;
                }
                else
                {
                    exeName = Path.GetFileName(exePath).ToLowerInvariant();
                    try
                    {
                        var vi = FileVersionInfo.GetVersionInfo(exePath);
                        friendlyName = !string.IsNullOrWhiteSpace(vi.FileDescription) ? vi.FileDescription : Path.GetFileNameWithoutExtension(exePath);
                    }
                    catch
                    {
                        friendlyName = Path.GetFileNameWithoutExtension(exePath);
                    }
                }
            }
            catch
            {
                return (string.Empty, string.Empty, string.Empty, ScreenTimeCategories.Other);
            }

            // Window title check for edge/chrome/browsers
            string windowTitle = string.Empty;
            try
            {
                int len = NativeMethods.GetWindowTextLength(hwnd);
                if (len > 0)
                {
                    var sb = new StringBuilder(len + 1);
                    NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                    windowTitle = sb.ToString();
                }
            }
            catch {}

            string category = CategorizeApp(exeName, windowTitle);
            return (exeName, friendlyName, exePath, category);
        }

        private static string CategorizeApp(string exeName, string windowTitle)
        {
            string lower = exeName.ToLowerInvariant();

            // 1. Browsers
            if (lower is "msedge.exe" or "chrome.exe" or "firefox.exe" or "brave.exe" or "opera.exe" or "vivaldi.exe" or "arc.exe" or "zen.exe" or "waterfox.exe" or "tor.exe" or "iexplore.exe")
            {
                // Can inspect windowTitle to see if it's YouTube, GitHub, etc.
                if (!string.IsNullOrEmpty(windowTitle))
                {
                    string wtLower = windowTitle.ToLowerInvariant();
                    if (wtLower.Contains("youtube") || wtLower.Contains("netflix") || wtLower.Contains("twitch") || wtLower.Contains("bilibili") || wtLower.Contains("disney"))
                    {
                        return ScreenTimeCategories.Entertainment;
                    }
                    if (wtLower.Contains("github") || wtLower.Contains("gitlab") || wtLower.Contains("stack overflow") || wtLower.Contains("localhost") || wtLower.Contains("chatgpt") || wtLower.Contains("gemini") || wtLower.Contains("claude"))
                    {
                        return ScreenTimeCategories.Development;
                    }
                }
                return ScreenTimeCategories.Browsing;
            }

            // 2. Development
            if (lower is "devenv.exe" or "code.exe" or "antigravity.exe" or "cursor.exe" or "idea64.exe" or "pycharm64.exe" or "rider64.exe" or "webstorm64.exe" or "clion64.exe" or "sublime_text.exe" or "notepad++.exe" or "windowsterminal.exe" or "cmd.exe" or "powershell.exe" or "pwsh.exe" or "git-bash.exe" or "wt.exe" or "bash.exe" or "mintty.exe")
            {
                return ScreenTimeCategories.Development;
            }

            // 3. Entertainment / Games / Media
            if (lower is "spotify.exe" or "vlc.exe" or "wmplayer.exe" or "netflix.exe" or "steam.exe" or "epicgameslauncher.exe" or "mpv.exe" or "kmplayer.exe" or "potplayer64.exe" or "foobar2000.exe" or "musicbee.exe" or "obs64.exe" or "valorant.exe" or "genshinimpact.exe")
            {
                return ScreenTimeCategories.Entertainment;
            }

            // 4. Productivity / Office
            if (lower is "winword.exe" or "excel.exe" or "powerpnt.exe" or "notion.exe" or "obsidian.exe" or "onenote.exe" or "acrobat.exe" or "acrodr32.exe" or "pdf24.exe" or "slack.exe" or "teams.exe" or "telegram.exe" or "whatsapp.exe" or "discord.exe")
            {
                return ScreenTimeCategories.Productivity;
            }

            // 5. System
            if (lower is "explorer.exe" or "taskmgr.exe" or "systemsettings.exe" or "searchhost.exe" or "shellexperiencehost.exe" or "applicationframehost.exe" or "pickerhost.exe" or "snippingtool.exe" or "notchwin.exe" or "poni.exe" or "lockapp.exe")
            {
                return ScreenTimeCategories.System;
            }

            return ScreenTimeCategories.Other;
        }

        public DayScreenTimeData? GetTodayData()
        {
            lock (_lock)
            {
                string key = DateTime.Now.ToString("yyyy-MM-dd");
                if (_store.Days.TryGetValue(key, out var dayData))
                {
                    return dayData;
                }
                return null;
            }
        }

        public DayScreenTimeData? GetDayData(DateTime date)
        {
            lock (_lock)
            {
                string key = date.ToString("yyyy-MM-dd");
                if (_store.Days.TryGetValue(key, out var dayData))
                {
                    return dayData;
                }
                return null;
            }
        }

        public long GetTodayTotalSeconds()
        {
            lock (_lock)
            {
                var today = GetTodayData();
                if (today == null) return 0;

                long total = 0;
                foreach (var app in today.Apps.Values)
                {
                    total += app.Seconds;
                }
                return total;
            }
        }

        public int GetTodaySwitches()
        {
            lock (_lock)
            {
                var today = GetTodayData();
                return today?.Switches ?? 0;
            }
        }

        public void ClearAllHistory()
        {
            lock (_lock)
            {
                _store.Days.Clear();
                ForceSave();
            }
            DataUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _trackerTimer?.Dispose();
            ForceSave();
            GC.SuppressFinalize(this);
        }
    }
}
