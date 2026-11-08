using System;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IScreenTimeService : IDisposable
    {
        event EventHandler? DataUpdated;

        bool IsEnabled { get; set; }
        int IdleThresholdSeconds { get; set; }

        DayScreenTimeData? GetTodayData();
        DayScreenTimeData? GetDayData(DateTime date);
        long GetTodayTotalSeconds();
        int GetTodaySwitches();

        void ClearAllHistory();
        void ForceSave();
    }
}
