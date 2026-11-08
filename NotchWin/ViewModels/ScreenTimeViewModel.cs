using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public class HourlyBarItem
    {
        public int Hour { get; set; }
        public double HeightPercent { get; set; }
        public bool IsCurrentHour { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Tooltip { get; set; } = string.Empty;
    }

    public class AppUsageDisplayItem
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string DurationText { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryColorHex { get; set; } = "#636366";
        public double PercentOfTotal { get; set; }
        public double BarWidth { get; set; }
    }

    public class CategorySegmentItem
    {
        public string Category { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#636366";
        public double Percent { get; set; }
        public string DurationText { get; set; } = string.Empty;
    }

    public partial class ScreenTimeViewModel : ObservableObject
    {
        private readonly IScreenTimeService _screenTimeService;

        [ObservableProperty]
        private string _totalTimeTodayText = "0m";

        [ObservableProperty]
        private string _switchesCountText = "0";

        [ObservableProperty]
        private bool _hasData;

        [ObservableProperty]
        private string _todayDateText = string.Empty;

        public ObservableCollection<HourlyBarItem> HourlyBars { get; } = new();
        public ObservableCollection<AppUsageDisplayItem> TopApps { get; } = new();
        public ObservableCollection<CategorySegmentItem> CategorySegments { get; } = new();

        public ScreenTimeViewModel(IScreenTimeService screenTimeService)
        {
            _screenTimeService = screenTimeService;
            _screenTimeService.DataUpdated += OnScreenTimeDataUpdated;

            Refresh();
        }

        private void OnScreenTimeDataUpdated(object? sender, EventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            var dayData = _screenTimeService.GetTodayData();
            long totalSeconds = _screenTimeService.GetTodayTotalSeconds();
            int switches = _screenTimeService.GetTodaySwitches();

            TodayDateText = DateTime.Now.ToString("dddd, d MMM");
            TotalTimeTodayText = FormatDuration(totalSeconds);
            SwitchesCountText = switches.ToString();
            HasData = totalSeconds > 0;

            // 1. Refresh Hourly Bars (24 hours)
            int currentHour = DateTime.Now.Hour;
            long maxHourlySecs = 1;
            if (dayData?.Hours != null && dayData.Hours.Length == 24)
            {
                maxHourlySecs = Math.Max(1, dayData.Hours.Max());
            }

            HourlyBars.Clear();
            for (int h = 0; h < 24; h++)
            {
                long secs = (dayData?.Hours != null && h < dayData.Hours.Length) ? dayData.Hours[h] : 0;
                double heightPct = (secs <= 0) ? 4.0 : Math.Max(8.0, (double)secs / maxHourlySecs * 100.0);

                string label = (h == 0 || h == 6 || h == 12 || h == 18 || h == 23) ? $"{h:D2}" : string.Empty;
                string tooltip = $"{h:D2}:00 - {FormatDuration(secs)}";

                HourlyBars.Add(new HourlyBarItem
                {
                    Hour = h,
                    HeightPercent = heightPct,
                    IsCurrentHour = (h == currentHour),
                    Label = label,
                    Tooltip = tooltip
                });
            }

            // 2. Refresh Top Apps (top 4 apps)
            TopApps.Clear();
            if (dayData?.Apps != null && dayData.Apps.Count > 0 && totalSeconds > 0)
            {
                var sortedApps = dayData.Apps.Values
                    .Where(a => a.Seconds > 0)
                    .OrderByDescending(a => a.Seconds)
                    .Take(4)
                    .ToList();

                foreach (var app in sortedApps)
                {
                    double pct = (double)app.Seconds / totalSeconds * 100.0;
                    double barW = Math.Max(4, Math.Min(130, pct * 1.3));

                    string cat = app.Category;
                    if (string.IsNullOrEmpty(cat) || cat == ScreenTimeCategories.Other)
                    {
                        cat = ScreenTimeCategories.Categorize(app.Path, app.Name);
                    }

                    TopApps.Add(new AppUsageDisplayItem
                    {
                        Name = !string.IsNullOrWhiteSpace(app.Name) ? app.Name : PathWithoutExt(app.Path),
                        ProcessName = PathWithoutExt(app.Path),
                        DurationText = FormatDuration(app.Seconds),
                        Category = cat,
                        CategoryColorHex = ScreenTimeCategories.GetColorHex(cat),
                        PercentOfTotal = Math.Round(pct, 1),
                        BarWidth = barW
                    });
                }
            }

            // 3. Refresh Category Segments
            CategorySegments.Clear();
            if (dayData?.Categories != null && totalSeconds > 0)
            {
                foreach (var kvp in dayData.Categories.OrderByDescending(c => c.Value))
                {
                    if (kvp.Value <= 0) continue;
                    double pct = (double)kvp.Value / totalSeconds * 100.0;
                    CategorySegments.Add(new CategorySegmentItem
                    {
                        Category = kvp.Key,
                        ColorHex = ScreenTimeCategories.GetColorHex(kvp.Key),
                        Percent = Math.Round(pct, 1),
                        DurationText = FormatDuration(kvp.Value)
                    });
                }
            }
        }

        private static string PathWithoutExt(string path)
        {
            if (string.IsNullOrEmpty(path)) return "App";
            try
            {
                return System.IO.Path.GetFileNameWithoutExtension(path);
            }
            catch
            {
                return path;
            }
        }

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds}s";
            }
            long minutes = seconds / 60;
            if (minutes < 60)
            {
                return $"{minutes}m";
            }
            long hours = minutes / 60;
            long remMinutes = minutes % 60;
            return remMinutes > 0 ? $"{hours}j {remMinutes}m" : $"{hours}j";
        }
    }
}
