using System;

namespace NotchWin.Models
{
    public class PrayerSchedule
    {
        public DateTime Date { get; set; } = DateTime.Today;
        public TimeSpan Imsak { get; set; }
        public TimeSpan Subuh { get; set; }
        public TimeSpan Terbit { get; set; }
        public TimeSpan Dhuha { get; set; }
        public TimeSpan Dzuhur { get; set; }
        public TimeSpan Ashar { get; set; }
        public TimeSpan Maghrib { get; set; }
        public TimeSpan Isya { get; set; }

        public string NextPrayerName { get; set; } = "Dzuhur";
        public TimeSpan NextPrayerTime { get; set; }
        public TimeSpan RemainingToNext { get; set; }
        public int ActivePrayerIndex { get; set; } = 2; // 0: Subuh, 1: Dhuha, 2: Dzuhur, 3: Ashar, 4: Maghrib, 5: Isya
    }

    public class CityLocation
    {
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double TimeZoneOffset { get; set; } = 7.0; // UTC+7 (WIB), +8 (WITA), +9 (WIT)

        public override string ToString() => Name;
    }
}
