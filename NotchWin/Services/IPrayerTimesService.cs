using System;
using System.Collections.Generic;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IPrayerTimesService
    {
        PrayerSchedule? CurrentSchedule { get; }
        IReadOnlyList<CityLocation> AvailableCities { get; }
        event EventHandler<PrayerSchedule>? ScheduleUpdated;
        event EventHandler<string>? PrayerTimeReached; // e.g. "Waktunya Sholat Maghrib (18:02)"

        void UpdateLocation(string cityName, double latitude, double longitude);
        PrayerSchedule CalculateToday(double latitude, double longitude, double timeZoneOffset);
        void StartMonitoring();
        void StopMonitoring();
    }
}
