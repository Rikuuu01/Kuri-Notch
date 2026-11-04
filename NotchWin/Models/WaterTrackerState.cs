using System;

namespace NotchWin.Models
{
    public class WaterTrackerState
    {
        public DateTime Date { get; set; } = DateTime.Today;
        public int CurrentIntakeMl { get; set; } = 0;
        public int DailyGoalMl { get; set; } = 2000;
        public int LastAddedMl { get; set; } = 0;
        public DateTime LastDrinkTime { get; set; } = DateTime.Now;

        // Daily Sitting Statistics
        public int CurrentSittingSeconds { get; set; } = 0;
        public int TotalSittingSecondsToday { get; set; } = 0;
        public int LongestSessionSecondsToday { get; set; } = 0;
        public int BreaksCountToday { get; set; } = 0;
    }
}
