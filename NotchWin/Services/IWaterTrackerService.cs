using System;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IWaterTrackerService
    {
        WaterTrackerState State { get; }
        TimeSpan TimeUntilNextDrink { get; }
        TimeSpan SittingDuration { get; }
        bool IsAlarmActive { get; }
        bool IsUserIdle { get; }
        int IntervalMinutes { get; }

        event EventHandler<WaterTrackerState>? StateChanged;
        event EventHandler<string>? WaterReminderTriggered;
        event EventHandler? CountdownTicked;
        event EventHandler? ReminderCleared;

        void AddWater(int ml);
        void UndoLast();
        void ResetToday();
        void ResetSitting();
        void UpdateGoal(int goalMl);
        void StartReminder(int intervalMinutes);
        void StopReminder();
        void Snooze(int minutes = 2);
        void DismissAlarm();
    }
}
