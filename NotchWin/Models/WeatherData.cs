using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;

namespace NotchWin.Models
{
    public static class WeatherSvgPaths
    {
        public const string Sunny = "M 8,4 A 4,4 0 1,1 8,12 A 4,4 0 1,1 8,4 Z M 8,0 V 2 M 8,14 V 16 M 0,8 H 2 M 14,8 H 16 M 2.3,2.3 L 3.7,3.7 M 12.3,12.3 L 13.7,13.7 M 2.3,13.7 L 3.7,12.3 M 12.3,3.7 L 13.7,2.3";
        public const string PartlyCloudy = "M 11,3 A 3,3 0 0,0 8.5,4.3 A 3.5,3.5 0 0,0 4,8 A 3.5,3.5 0 0,0 7.5,12 H 14.5 A 3,3 0 0,0 17,9 A 3,3 0 0,0 14.2,6.1 A 3,3 0 0,0 11,3 Z";
        public const string Cloudy = "M 12,5 A 4,4 0 0,0 8.1,8.3 A 4.5,4.5 0 0,0 4,12.5 A 4.5,4.5 0 0,0 8.5,17 H 16.5 A 3.5,3.5 0 0,0 20,13.5 A 3.5,3.5 0 0,0 16.8,10.1 A 4,4 0 0,0 12,5 Z";
        public const string Rainy = "M 8,2 A 3,3 0 0,0 5.1,4.3 A 3.5,3.5 0 0,0 2,7.5 A 3.5,3.5 0 0,0 5.5,11 H 11.5 A 2.5,2.5 0 0,0 14,8.5 A 2.5,2.5 0 0,0 11.8,6.1 A 3,3 0 0,0 8,2 Z M 5,12.5 L 4,14.5 M 8,12.5 L 7,14.5 M 11,12.5 L 10,14.5";
        public const string Thunderstorm = "M 8,2 A 3,3 0 0,0 5.1,4.3 A 3.5,3.5 0 0,0 2,7.5 A 3.5,3.5 0 0,0 5.5,11 H 11.5 A 2.5,2.5 0 0,0 14,8.5 A 2.5,2.5 0 0,0 11.8,6.1 A 3,3 0 0,0 8,2 Z M 8.5,11.5 L 6.5,14 H 9 L 7.5,16.5";
        public const string Snowy = "M 8,2 A 3,3 0 0,0 5.1,4.3 A 3.5,3.5 0 0,0 2,7.5 A 3.5,3.5 0 0,0 5.5,11 H 11.5 A 2.5,2.5 0 0,0 14,8.5 A 2.5,2.5 0 0,0 11.8,6.1 A 3,3 0 0,0 8,2 Z M 5,13 H 5.01 M 8,13 H 8.01 M 11,13 H 11.01";
        public const string Foggy = "M 2,4 H 14 M 4,7 H 12 M 1,10 H 15 M 3,13 H 13";

        // Metrics & Navigation Icons
        public const string Wind = "M 1,4 H 8.5 A 1.8,1.8 0 1,1 7,2 M 1,8 H 12 A 1.8,1.8 0 1,0 10.5,10 M 1,12 H 6 A 1.4,1.4 0 1,1 5,13.5";
        public const string Humidity = "M 8,1 C 8,1 2.5,8 2.5,11.5 A 5.5,5.5 0 0,0 13.5,11.5 C 13.5,8 8,1 8,1 Z";
        public const string Umbrella = "M 8,2 C 4.5,2 2,5.5 2,9 H 14 C 14,5.5 11.5,2 8,2 Z M 8,9 V 13.5 A 1.5,1.5 0 0,1 5,13.5 M 8,2 V 1";
        public const string Sunrise = "M 2,13 H 14 M 8,3 V 1 M 4.5,4.5 L 3.5,3.5 M 11.5,4.5 L 12.5,3.5 M 4,10 A 4,4 0 0,1 12,10 M 8,6 L 8,9 M 7,7 L 8,6 L 9,7";
        public const string Sunset = "M 2,13 H 14 M 8,3 V 1 M 4.5,4.5 L 3.5,3.5 M 11.5,4.5 L 12.5,3.5 M 4,10 A 4,4 0 0,1 12,10 M 8,6 L 8,9 M 7,8 L 8,9 L 9,8";
        public const string LocationPin = "M 8,1 C 4.7,1 2,3.7 2,7 C 2,11.5 8,15 8,15 C 8,15 14,11.5 14,7 C 14,3.7 11.3,1 8,1 Z M 8,9.2 A 2.2,2.2 0 1,1 8,4.8 A 2.2,2.2 0 0,1 8,9.2 Z";
        public const string Refresh = "M 13.6,2.4 C 12.2,0.9 10.2,0 8,0 C 3.6,0 0,3.6 0,8 C 0,12.4 3.6,16 8,16 C 11.7,16 14.8,13.4 15.7,10 H 13.6 C 12.8,12.3 10.6,14 8,14 C 4.7,14 2,11.3 2,8 C 2,4.7 4.7,2 8,2 C 9.7,2 11.1,2.7 12.2,3.8 L 9,7 H 16 V 0 L 13.6,2.4 Z";
    }

    public class WeatherData
    {
        public double Temperature { get; set; } = 29.0;
        public double ApparentTemperature { get; set; } = 34.0;
        public double MinTemperature { get; set; } = 25.0;
        public double MaxTemperature { get; set; } = 29.0;
        public int RelativeHumidity { get; set; } = 77;
        public int WeatherCode { get; set; } = 61;
        public string ConditionText { get; set; } = "Rain";
        public string WeatherIconKey { get; set; } = "Rainy";
        public string WeatherIconPath { get; set; } = WeatherSvgPaths.Rainy;
        public double WindSpeed { get; set; } = 2.0;
        public string WindDirection { get; set; } = "NW";
        public string WindText { get; set; } = "2 m/s NW";
        public double PrecipitationAmount { get; set; } = 0.8;
        public string PrecipitationText { get; set; } = "0.8 mm";
        public int PrecipitationProbability { get; set; } = 80;
        public string CityName { get; set; } = "Duri";
        public string SunriseTime { get; set; } = "5:56 AM";
        public string SunsetTime { get; set; } = "6:01 PM";
        public string FeelsLikeAndRangeText { get; set; } = "Feels like 34\u00b0 \u2022 \u219325\u00b0 \u219129\u00b0";
        public string DataSource { get; set; } = "BMKG";
        public bool IsRaining { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<HourlyForecastItem> Forecasts { get; set; } = new();
        public List<DailyForecastItem> DailyForecasts { get; set; } = new();
    }

    public class HourlyForecastItem
    {
        public string TimeText { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public string TempText { get; set; } = string.Empty;
        public double PrecipitationAmount { get; set; }
        public string PrecipText { get; set; } = "0.0 mm";
        public string ConditionText { get; set; } = string.Empty;
        public string WeatherIconKey { get; set; } = "Rainy";
        public string WeatherIconPath { get; set; } = WeatherSvgPaths.Rainy;
        public int WeatherCode { get; set; }

        [JsonIgnore]
        public bool HasPrecipitation => PrecipitationAmount > 0.05;
    }

    public class DailyForecastItem
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string ToolTipText { get; set; } = string.Empty;
        public double MaxTemp { get; set; }
        public double MinTemp { get; set; }
        public string TempText { get; set; } = string.Empty;
        public string MinTempText { get; set; } = string.Empty;
        public double PrecipitationSum { get; set; }
        public string PrecipText { get; set; } = "0 mm";
        public int WeatherCode { get; set; }
        public string ConditionText { get; set; } = string.Empty;
        public string WeatherIconKey { get; set; } = "Rainy";
        public string WeatherIconPath { get; set; } = WeatherSvgPaths.Rainy;
        public int PrecipitationProbability { get; set; }
        public bool IsToday { get; set; }

        [JsonIgnore]
        public bool HasPrecipitation => PrecipitationSum > 0.05;

        // Grid column stars for the horizontal temperature range bar
        [JsonIgnore]
        public GridLength LeftStar { get; set; } = new(0, GridUnitType.Star);

        [JsonIgnore]
        public GridLength BarStar { get; set; } = new(1, GridUnitType.Star);

        [JsonIgnore]
        public GridLength RightStar { get; set; } = new(0, GridUnitType.Star);
    }
}
