using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class WaterTrackerService : IWaterTrackerService, IDisposable
    {
        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotchWin"
        );
        private static readonly string FilePath = Path.Combine(DataDirectory, "water_state.json");

        private WaterTrackerState _state = new();
        private Timer? _tickerTimer;
        private int _intervalMinutes = 45;
        private bool _isAlarmActive = false;
        private bool _isUserIdle = false;
        private DateTime _snoozedUntil = DateTime.MinValue;
        private int _saveCounter = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private static TimeSpan GetIdleTime()
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref lii))
            {
                uint tickCount = (uint)Environment.TickCount;
                uint idleMs = tickCount - lii.dwTime;
                return TimeSpan.FromMilliseconds(idleMs);
            }
            return TimeSpan.Zero;
        }

        public WaterTrackerState State => _state;
        public int IntervalMinutes => _intervalMinutes;
        public bool IsAlarmActive => _isAlarmActive;
        public bool IsUserIdle => _isUserIdle;

        public TimeSpan SittingDuration => TimeSpan.FromSeconds(_state.CurrentSittingSeconds);

        public TimeSpan TimeUntilNextDrink
        {
            get
            {
                if (_snoozedUntil > DateTime.Now)
                {
                    return _snoozedUntil - DateTime.Now;
                }
                int remainingSeconds = Math.Max(0, (_intervalMinutes * 60) - _state.CurrentSittingSeconds);
                return TimeSpan.FromSeconds(remainingSeconds);
            }
        }

        public event EventHandler<WaterTrackerState>? StateChanged;
        public event EventHandler<string>? WaterReminderTriggered;
        public event EventHandler? CountdownTicked;
        public event EventHandler? ReminderCleared;

        public WaterTrackerService()
        {
            LoadSynchronous();
            CheckDayReset();
            StartTicker();
        }

        private void CheckDayReset()
        {
            if (_state.Date.Date != DateTime.Today)
            {
                _state.Date = DateTime.Today;
                _state.CurrentIntakeMl = 0;
                _state.LastAddedMl = 0;
                _state.LastDrinkTime = DateTime.Now;
                _state.CurrentSittingSeconds = 0;
                _state.TotalSittingSecondsToday = 0;
                _state.LongestSessionSecondsToday = 0;
                _state.BreaksCountToday = 0;
                Save();
            }
        }

        private void LoadSynchronous()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<WaterTrackerState>(json);
                    if (loaded != null)
                    {
                        _state = loaded;
                        if (_state.LastDrinkTime == default || _state.LastDrinkTime > DateTime.Now)
                        {
                            _state.LastDrinkTime = DateTime.Now;
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load water tracker state");
            }

            _state = new WaterTrackerState
            {
                LastDrinkTime = DateTime.Now
            };
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                string json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save water tracker state");
            }
        }

        private void StartTicker()
        {
            _tickerTimer?.Dispose();
            _tickerTimer = new Timer(OnTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void OnTick(object? _)
        {
            CheckDayReset();

            // Detect user activity (mouse / keyboard) via GetLastInputInfo
            var idleTime = GetIdleTime();
            bool idle = idleTime.TotalSeconds >= 60.0; // 60 seconds threshold
            _isUserIdle = idle;

            if (!idle)
            {
                // User is actively working in front of the screen
                _state.CurrentSittingSeconds++;
                _state.TotalSittingSecondsToday++;
                if (_state.CurrentSittingSeconds > _state.LongestSessionSecondsToday)
                {
                    _state.LongestSessionSecondsToday = _state.CurrentSittingSeconds;
                }

                // Check Alarm Trigger
                int targetSeconds = _intervalMinutes * 60;
                bool isSnoozed = _snoozedUntil > DateTime.Now;
                if (!isSnoozed && _state.CurrentSittingSeconds >= targetSeconds)
                {
                    if (!_isAlarmActive)
                    {
                        _isAlarmActive = true;
                        int sittingMinutes = Math.Max(1, _state.CurrentSittingSeconds / 60);
                        WaterReminderTriggered?.Invoke(this, $"Time to hydrate! You've been sitting actively for {sittingMinutes} minutes. Stand up, stretch, and drink a glass of water! 💧");
                    }
                }
            }
            else
            {
                // User is idle / laptop on standby / away from desk
                // If user has been away for more than 5 minutes (300s), they took a break from the screen
                if (idleTime.TotalSeconds >= 300.0 && _state.CurrentSittingSeconds > 0)
                {
                    if (_state.CurrentSittingSeconds >= 120)
                    {
                        _state.BreaksCountToday++;
                    }
                    bool wasAlarmActive = _isAlarmActive;
                    _state.CurrentSittingSeconds = 0;
                    _isAlarmActive = false;
                    _snoozedUntil = DateTime.MinValue;
                    if (wasAlarmActive) ReminderCleared?.Invoke(this, EventArgs.Empty);
                }
            }

            // Periodic auto-save every 60 seconds
            if (++_saveCounter >= 60)
            {
                _saveCounter = 0;
                Save();
            }

            CountdownTicked?.Invoke(this, EventArgs.Empty);
        }

        public void AddWater(int ml)
        {
            CheckDayReset();
            _state.LastAddedMl = ml;
            _state.CurrentIntakeMl += ml;
            ResetSitting();
            Save();
            StateChanged?.Invoke(this, _state);
            CountdownTicked?.Invoke(this, EventArgs.Empty);
        }

        public void UndoLast()
        {
            CheckDayReset();
            if (_state.LastAddedMl > 0)
            {
                _state.CurrentIntakeMl = Math.Max(0, _state.CurrentIntakeMl - _state.LastAddedMl);
                _state.LastAddedMl = 0;
                Save();
                StateChanged?.Invoke(this, _state);
            }
        }

        public void ResetToday()
        {
            _state.CurrentIntakeMl = 0;
            _state.LastAddedMl = 0;
            _state.CurrentSittingSeconds = 0;
            _state.TotalSittingSecondsToday = 0;
            _state.LongestSessionSecondsToday = 0;
            _state.BreaksCountToday = 0;
            _state.LastDrinkTime = DateTime.Now;
            _state.Date = DateTime.Today;
            bool wasAlarmActive = _isAlarmActive;
            _snoozedUntil = DateTime.MinValue;
            _isAlarmActive = false;
            Save();
            StateChanged?.Invoke(this, _state);
            if (wasAlarmActive) ReminderCleared?.Invoke(this, EventArgs.Empty);
            CountdownTicked?.Invoke(this, EventArgs.Empty);
        }

        public void ResetSitting()
        {
            if (_state.CurrentSittingSeconds >= 120)
            {
                _state.BreaksCountToday++;
            }
            if (_state.CurrentSittingSeconds > _state.LongestSessionSecondsToday)
            {
                _state.LongestSessionSecondsToday = _state.CurrentSittingSeconds;
            }
            bool wasAlarmActive = _isAlarmActive;
            _state.CurrentSittingSeconds = 0;
            _state.LastDrinkTime = DateTime.Now;
            _snoozedUntil = DateTime.MinValue;
            _isAlarmActive = false;
            Save();
            CountdownTicked?.Invoke(this, EventArgs.Empty);
            if (wasAlarmActive) ReminderCleared?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateGoal(int goalMl)
        {
            if (goalMl > 0)
            {
                _state.DailyGoalMl = goalMl;
                Save();
                StateChanged?.Invoke(this, _state);
            }
        }

        public void StartReminder(int intervalMinutes)
        {
            int clamped = Math.Clamp(intervalMinutes, 10, 240);
            if (clamped != _intervalMinutes)
            {
                _intervalMinutes = clamped;
                bool wasAlarmActive = _isAlarmActive;
                _snoozedUntil = DateTime.MinValue;
                _isAlarmActive = false;
                if (wasAlarmActive) ReminderCleared?.Invoke(this, EventArgs.Empty);
            }
            StartTicker();
        }

        public void StopReminder()
        {
            _tickerTimer?.Dispose();
            _tickerTimer = null;

            bool wasAlarmActive = _isAlarmActive;
            _isAlarmActive = false;
            _snoozedUntil = DateTime.MinValue;
            if (wasAlarmActive) ReminderCleared?.Invoke(this, EventArgs.Empty);
        }

        public void Snooze(int minutes = 2)
        {
            _snoozedUntil = DateTime.Now.AddMinutes(Math.Max(1, minutes));
            bool wasAlarmActive = _isAlarmActive;
            _isAlarmActive = false;
            CountdownTicked?.Invoke(this, EventArgs.Empty);
            if (wasAlarmActive) ReminderCleared?.Invoke(this, EventArgs.Empty);
        }

        public void DismissAlarm()
        {
            ResetSitting();
        }

        public void Dispose()
        {
            StopReminder();
        }
    }
}
