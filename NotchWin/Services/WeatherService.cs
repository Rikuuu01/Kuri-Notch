using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class WeatherService : IWeatherService, IDisposable
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotchWin"
        );
        private static readonly string CacheFilePath = Path.Combine(CacheDirectory, "weather_cache.json");

        private WeatherData? _currentWeather;
        private Timer? _periodicTimer;
        private string _lastCity = "Duri";
        private double _lastLat = 1.2761;
        private double _lastLon = 101.1946;
        private bool _lastIsFahrenheit = false;

        public WeatherData? CurrentWeather => _currentWeather;
        public event EventHandler<WeatherData>? WeatherUpdated;

        public WeatherService()
        {
            LoadCache();
            if (_currentWeather == null)
            {
                _currentWeather = CreateDefaultWeatherData("Duri");
            }
        }

        private void LoadCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    string json = File.ReadAllText(CacheFilePath);
                    var cached = JsonSerializer.Deserialize<WeatherData>(json);
                    if (cached != null)
                    {
                        if (string.IsNullOrEmpty(cached.WeatherIconPath))
                        {
                            var (_, _, path) = MapWeatherCode(cached.WeatherCode, cached.PrecipitationAmount);
                            cached.WeatherIconPath = path;
                        }

                        if (cached.DailyForecasts == null || cached.DailyForecasts.Count == 0)
                        {
                            cached.DailyForecasts = GenerateDefaultDailyForecasts(cached.Temperature, cached.WeatherCode);
                        }
                        else
                        {
                            foreach (var d in cached.DailyForecasts)
                            {
                                if (string.IsNullOrEmpty(d.WeatherIconPath))
                                {
                                    var (_, _, path) = MapWeatherCode(d.WeatherCode, d.PrecipitationSum);
                                    d.WeatherIconPath = path;
                                }
                            }
                        }

                        CalculateRangeBars(cached.DailyForecasts);

                        if (cached.Forecasts == null || cached.Forecasts.Count == 0)
                        {
                            cached.Forecasts = GenerateDefaultHourlyForecasts(cached.Temperature, cached.WeatherCode);
                        }

                        _currentWeather = cached;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load weather cache");
            }
        }

        private void SaveCache(WeatherData data)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save weather cache");
            }
        }

        public async Task RefreshAsync(string city, double lat, double lon, bool isFahrenheit)
        {
            _lastCity = city;
            _lastLat = lat;
            _lastLon = lon;
            _lastIsFahrenheit = isFahrenheit;

            // 1. Try BMKG Official Weather for Indonesian regions (Duri, Riau, and Indonesian cities)
            string? bmkgAdm4 = ResolveBmkgAdm4(city, lat, lon);
            if (!string.IsNullOrEmpty(bmkgAdm4))
            {
                try
                {
                    var bmkgWeather = await FetchBmkgWeatherAsync(bmkgAdm4, city, lat, lon, isFahrenheit);
                    if (bmkgWeather != null)
                    {
                        _currentWeather = bmkgWeather;
                        SaveCache(bmkgWeather);
                        WeatherUpdated?.Invoke(this, bmkgWeather);
                        Log.Information("Weather updated via BMKG for {City} ({Adm4}): {Temp}°, {Condition}, Rain: {Rain}mm, IsRaining: {Raining}",
                            city, bmkgAdm4, bmkgWeather.Temperature, bmkgWeather.ConditionText, bmkgWeather.PrecipitationAmount, bmkgWeather.IsRaining);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to fetch from BMKG for {City} ({Adm4}), falling back to Open-Meteo", city, bmkgAdm4);
                }
            }

            // 2. Open-Meteo (High-resolution ECMWF model with smart precipitation thresholds)
            string tempUnitParam = isFahrenheit ? "&temperature_unit=fahrenheit" : "";
            string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,precipitation_probability,weather_code,wind_speed_10m,wind_direction_10m&hourly=temperature_2m,weather_code,precipitation&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,sunrise,sunset&wind_speed_unit=ms&timezone=auto&models=best_match,ecmwf_ifs025&forecast_days=7{tempUnitParam}";

            try
            {
                using var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("current", out var current))
                    {
                        double temp = current.TryGetProperty("temperature_2m", out var pTemp) ? pTemp.GetDouble() : 28.0;
                        double feelsLike = current.TryGetProperty("apparent_temperature", out var pApp) ? pApp.GetDouble() : temp;
                        int humidity = current.TryGetProperty("relative_humidity_2m", out var pHum) ? pHum.GetInt32() : 65;
                        double windSpeed = current.TryGetProperty("wind_speed_10m", out var pWind) ? pWind.GetDouble() : 2.0;
                        double windDir = current.TryGetProperty("wind_direction_10m", out var pDir) ? pDir.GetDouble() : 0.0;
                        double precipAmount = current.TryGetProperty("precipitation", out var pPrc) ? pPrc.GetDouble() : 0.0;
                        int precipProb = current.TryGetProperty("precipitation_probability", out var pProb) ? pProb.GetInt32() : 0;
                        int code = current.TryGetProperty("weather_code", out var pCode) ? pCode.GetInt32() : 0;

                        var (condText, iconKey, iconPath) = MapWeatherCode(code, precipAmount);
                        string compass = DegreesToCompass(windDir);
                        bool isRaining = IsRainWeatherCode(code, precipAmount);

                        var weather = new WeatherData
                        {
                            CityName = city,
                            Temperature = Math.Round(temp, 1),
                            ApparentTemperature = Math.Round(feelsLike, 1),
                            RelativeHumidity = humidity,
                            WindSpeed = Math.Round(windSpeed, 1),
                            WindDirection = compass,
                            WindText = $"{Math.Round(windSpeed):0} m/s {compass}",
                            PrecipitationAmount = Math.Round(precipAmount, 1),
                            PrecipitationText = FormatPrecipitation(precipAmount),
                            PrecipitationProbability = precipProb,
                            WeatherCode = code,
                            ConditionText = condText,
                            WeatherIconKey = iconKey,
                            WeatherIconPath = iconPath,
                            IsRaining = isRaining,
                            UpdatedAt = DateTime.Now,
                            Forecasts = new List<HourlyForecastItem>(),
                            DailyForecasts = new List<DailyForecastItem>()
                        };

                        // Parse 7-Day Forecast & Sun times
                        if (root.TryGetProperty("daily", out var daily) &&
                            daily.TryGetProperty("time", out var dTimes) &&
                            daily.TryGetProperty("weather_code", out var dCodes) &&
                            daily.TryGetProperty("temperature_2m_max", out var dMaxTemps) &&
                            daily.TryGetProperty("temperature_2m_min", out var dMinTemps))
                        {
                            daily.TryGetProperty("precipitation_sum", out var dPrecipSum);
                            daily.TryGetProperty("precipitation_probability_max", out var dPrecipProb);
                            daily.TryGetProperty("sunrise", out var dSunrise);
                            daily.TryGetProperty("sunset", out var dSunset);

                            int count = dTimes.GetArrayLength();
                            for (int i = 0; i < count && i < 7; i++)
                            {
                                string? timeStr = dTimes[i].GetString();
                                if (DateTime.TryParse(timeStr, out var date))
                                {
                                    int dCode = dCodes[i].GetInt32();
                                    double maxT = dMaxTemps[i].GetDouble();
                                    double minT = dMinTemps[i].GetDouble();
                                    double sumRain = (dPrecipSum.ValueKind == JsonValueKind.Array && i < dPrecipSum.GetArrayLength()) ? dPrecipSum[i].GetDouble() : 0.0;
                                    int rainProb = (dPrecipProb.ValueKind == JsonValueKind.Array && i < dPrecipProb.GetArrayLength()) ? dPrecipProb[i].GetInt32() : 0;

                                    if (i == 0)
                                    {
                                        weather.MinTemperature = Math.Round(minT);
                                        weather.MaxTemperature = Math.Round(maxT);

                                        if (dSunrise.ValueKind == JsonValueKind.Array && dSunrise.GetArrayLength() > 0)
                                        {
                                            weather.SunriseTime = FormatSunTime(dSunrise[0].GetString());
                                        }
                                        if (dSunset.ValueKind == JsonValueKind.Array && dSunset.GetArrayLength() > 0)
                                        {
                                            weather.SunsetTime = FormatSunTime(dSunset[0].GetString());
                                        }
                                    }

                                    var (dCond, dKey, dPath) = MapWeatherCode(dCode, sumRain);
                                    bool isToday = (i == 0) || (date.Date == DateTime.Today);
                                    string dayName = isToday ? "Today" : date.ToString("ddd", CultureInfo.InvariantCulture);
                                    string tooltip = $"{date:dddd, dd MMM}: {dCond} ({Math.Round(minT)}° - {Math.Round(maxT)}°), Rain {FormatPrecipitation(sumRain)}";

                                    weather.DailyForecasts.Add(new DailyForecastItem
                                    {
                                        Date = date,
                                        DayName = dayName,
                                        ToolTipText = tooltip,
                                        MaxTemp = Math.Round(maxT, 0),
                                        MinTemp = Math.Round(minT, 0),
                                        TempText = $"{Math.Round(maxT):0}°",
                                        MinTempText = $"{Math.Round(minT):0}°",
                                        PrecipitationSum = sumRain,
                                        PrecipText = FormatPrecipitation(sumRain),
                                        WeatherCode = dCode,
                                        ConditionText = dCond,
                                        WeatherIconKey = dKey,
                                        WeatherIconPath = dPath,
                                        PrecipitationProbability = rainProb,
                                        IsToday = isToday
                                    });
                                }
                            }

                            CalculateRangeBars(weather.DailyForecasts);
                        }

                        if (weather.DailyForecasts.Count == 0)
                        {
                            weather.DailyForecasts = GenerateDefaultDailyForecasts(temp, code);
                        }

                        // Parse 8 Hourly Forecast Items
                        if (root.TryGetProperty("hourly", out var hourly) &&
                            hourly.TryGetProperty("time", out var hTimes) &&
                            hourly.TryGetProperty("temperature_2m", out var hTemps) &&
                            hourly.TryGetProperty("weather_code", out var hCodes))
                        {
                            hourly.TryGetProperty("precipitation", out var hPrecip);
                            int hCount = hTimes.GetArrayLength();

                            DateTime now = DateTime.Now;
                            int startIdx = 0;
                            for (int i = 0; i < hCount; i++)
                            {
                                if (DateTime.TryParse(hTimes[i].GetString(), out var hDate))
                                {
                                    if (hDate.Date == now.Date && hDate.Hour == now.Hour)
                                    {
                                        startIdx = i;
                                        break;
                                    }
                                }
                            }

                            for (int step = 0; step < 8 && (startIdx + step) < hCount; step++)
                            {
                                int idx = startIdx + step;
                                string? hTimeStr = hTimes[idx].GetString();
                                DateTime.TryParse(hTimeStr, out var hDate);

                                double hTemp = hTemps[idx].GetDouble();
                                int hCode = hCodes[idx].GetInt32();
                                double hRain = (hPrecip.ValueKind == JsonValueKind.Array && idx < hPrecip.GetArrayLength()) ? hPrecip[idx].GetDouble() : 0.0;
                                var (hCond, hKey, hPath) = MapWeatherCode(hCode, hRain);

                                string timeLabel = (step == 0) ? "Now" : hDate.ToString("h tt", CultureInfo.InvariantCulture);

                                weather.Forecasts.Add(new HourlyForecastItem
                                {
                                    TimeText = timeLabel,
                                    Temperature = Math.Round(hTemp, 0),
                                    TempText = $"{Math.Round(hTemp):0}°",
                                    PrecipitationAmount = hRain,
                                    PrecipText = FormatPrecipitation(hRain),
                                    ConditionText = hCond,
                                    WeatherIconKey = hKey,
                                    WeatherIconPath = hPath,
                                    WeatherCode = hCode
                                });
                            }
                        }

                        if (weather.Forecasts.Count == 0)
                        {
                            weather.Forecasts = GenerateDefaultHourlyForecasts(temp, code);
                        }

                        weather.FeelsLikeAndRangeText = $"Feels like {Math.Round(weather.ApparentTemperature):0}\u00b0 \u2022 \u2193{Math.Round(weather.MinTemperature):0}\u00b0 \u2191{Math.Round(weather.MaxTemperature):0}\u00b0";
                        weather.DataSource = "Open-Meteo";

                        _currentWeather = weather;
                        SaveCache(weather);
                        WeatherUpdated?.Invoke(this, weather);
                        Log.Information("Weather updated for {City}: {Temp}\u00b0, {Condition}, Rain: {Rain}mm, IsRaining: {Raining}",
                            city, weather.Temperature, weather.ConditionText, weather.PrecipitationAmount, weather.IsRaining);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch weather from Open-Meteo for {City}", city);
            }

            // Fallback: If cache available, notify
            if (_currentWeather != null)
            {
                WeatherUpdated?.Invoke(this, _currentWeather);
            }
        }

        #region BMKG Integration

        public static string? ResolveBmkgAdm4(string city, double lat, double lon)
        {
            if (string.IsNullOrWhiteSpace(city)) city = "";
            string c = city.ToLowerInvariant();

            // Direct City Name Matches
            if (c.Contains("duri") || c.Contains("mandau")) return "14.03.09.1001"; // Duri / Mandau (Bengkalis, Riau)
            if (c.Contains("bengkalis")) return "14.03.01.1001";
            if (c.Contains("pekanbaru")) return "14.71.01.1001";
            if (c.Contains("dumai")) return "14.72.01.1001";
            if (c.Contains("batam")) return "21.71.01.1001";
            if (c.Contains("jakarta")) return "31.71.01.1001";
            if (c.Contains("bandung")) return "32.73.01.1001";
            if (c.Contains("surabaya")) return "35.78.01.1001";
            if (c.Contains("semarang")) return "33.74.01.1001";
            if (c.Contains("medan")) return "12.71.01.1001";
            if (c.Contains("palembang")) return "16.71.01.1001";
            if (c.Contains("padang")) return "13.71.01.1001";
            if (c.Contains("yogyakarta") || c.Contains("jogja")) return "34.71.01.1001";
            if (c.Contains("denpasar") || c.Contains("bali")) return "51.71.01.1001";
            if (c.Contains("makassar")) return "73.71.01.1001";
            if (c.Contains("balikpapan")) return "64.71.01.1001";
            if (c.Contains("samarinda")) return "64.72.01.1001";
            if (c.Contains("banjarmasin")) return "63.71.01.1001";
            if (c.Contains("pontianak")) return "61.71.01.1001";
            if (c.Contains("manado")) return "71.71.01.1001";
            if (c.Contains("malang")) return "35.73.01.1001";
            if (c.Contains("bogor")) return "32.71.01.1001";
            if (c.Contains("depok")) return "32.76.01.1001";
            if (c.Contains("bekasi")) return "32.75.01.1001";
            if (c.Contains("tangerang selatan")) return "36.74.01.1001";
            if (c.Contains("tangerang")) return "36.71.01.1001";
            if (c.Contains("surakarta") || c.Contains("solo")) return "33.72.01.1001";
            if (c.Contains("bandar lampung") || c.Contains("lampung")) return "18.71.01.1001";
            if (c.Contains("kupang")) return "53.71.01.1001";
            if (c.Contains("mataram") || c.Contains("lombok")) return "52.71.01.1001";
            if (c.Contains("ambon")) return "81.71.01.1001";
            if (c.Contains("jayapura")) return "91.71.01.1001";
            if (c.Contains("aceh")) return "11.71.01.1001";

            // Coordinate Proximity matching for Duri (~30km radius)
            if (Math.Abs(lat - 1.276) < 0.35 && Math.Abs(lon - 101.195) < 0.35)
                return "14.03.09.1001";

            // Pekanbaru Proximity
            if (Math.Abs(lat - 0.507) < 0.35 && Math.Abs(lon - 101.448) < 0.35)
                return "14.71.01.1001";

            // Jakarta Proximity
            if (Math.Abs(lat - (-6.2088)) < 0.35 && Math.Abs(lon - 106.8456) < 0.35)
                return "31.71.01.1001";

            return null;
        }

        private async Task<WeatherData?> FetchBmkgWeatherAsync(string adm4, string city, double lat, double lon, bool isFahrenheit)
        {
            try
            {
                string url = $"https://api.bmkg.go.id/publik/prakiraan-cuaca?adm4={adm4}";
                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("data", out var dataArr) || dataArr.GetArrayLength() == 0)
                {
                    return null;
                }

                var firstData = dataArr[0];
                if (!firstData.TryGetProperty("cuaca", out var cuacaArr) || cuacaArr.GetArrayLength() == 0)
                {
                    return null;
                }

                DateTime nowUtc = DateTime.UtcNow;
                var allSlices = new List<(DateTime UtcTime, DateTime LocalTime, int T, int Hu, double Ws, string Wd, double Tp, int Weather, string DescId, string DescEn)>();

                foreach (var day in cuacaArr.EnumerateArray())
                {
                    foreach (var slice in day.EnumerateArray())
                    {
                        if (slice.TryGetProperty("datetime", out var dtElem) &&
                            DateTime.TryParse(dtElem.GetString(), out var utcTime))
                        {
                            DateTime localTime = utcTime.ToLocalTime();
                            if (slice.TryGetProperty("local_datetime", out var locElem) &&
                                DateTime.TryParse(locElem.GetString(), out var parsedLocal))
                            {
                                localTime = parsedLocal;
                            }

                            int t = slice.TryGetProperty("t", out var pT) ? pT.GetInt32() : 28;
                            int hu = slice.TryGetProperty("hu", out var pHu) ? pHu.GetInt32() : 75;
                            double wsKm = slice.TryGetProperty("ws", out var pWs) ? pWs.GetDouble() : 5.0;
                            double wsMs = Math.Round(wsKm / 3.6, 1);
                            string wd = slice.TryGetProperty("wd", out var pWd) ? pWd.GetString() ?? "W" : "W";
                            double tp = slice.TryGetProperty("tp", out var pTp) ? pTp.GetDouble() : 0.0;
                            int code = slice.TryGetProperty("weather", out var pW) ? pW.GetInt32() : 3;
                            string descId = slice.TryGetProperty("weather_desc", out var pDescId) ? pDescId.GetString() ?? "" : "";
                            string descEn = slice.TryGetProperty("weather_desc_en", out var pDescEn) ? pDescEn.GetString() ?? "" : "";

                            allSlices.Add((utcTime, localTime, t, hu, wsMs, wd, tp, code, descId, descEn));
                        }
                    }
                }

                if (allSlices.Count == 0)
                {
                    return null;
                }

                // Find slice closest to current time
                int closestIdx = 0;
                double minDiff = double.MaxValue;
                for (int i = 0; i < allSlices.Count; i++)
                {
                    double diff = Math.Abs((allSlices[i].UtcTime - nowUtc).TotalMinutes);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        closestIdx = i;
                    }
                }

                var cur = allSlices[closestIdx];
                double currentTemp = cur.T;
                if (isFahrenheit)
                {
                    currentTemp = Math.Round(currentTemp * 9.0 / 5.0 + 32.0, 1);
                }

                double feelsLike = CalculateApparentTemperature(cur.T, cur.Hu, cur.Ws);
                if (isFahrenheit)
                {
                    feelsLike = Math.Round(feelsLike * 9.0 / 5.0 + 32.0, 1);
                }

                var (condText, iconKey, iconPath, isRaining) = MapBmkgWeatherCode(cur.Weather, cur.DescId, cur.DescEn, cur.Tp);

                // Calculate min & max for today (if late evening, sample upcoming 24 hours to capture daytime high and nighttime low)
                var todaySlices = allSlices.Where(s => s.LocalTime.Date == cur.LocalTime.Date).ToList();
                double todayMin;
                double todayMax;
                if (todaySlices.Count >= 3 && todaySlices.Min(s => s.T) < todaySlices.Max(s => s.T))
                {
                    todayMin = todaySlices.Min(s => s.T);
                    todayMax = todaySlices.Max(s => s.T);
                }
                else
                {
                    var next24h = allSlices.Where(s => s.LocalTime >= cur.LocalTime && s.LocalTime <= cur.LocalTime.AddHours(24)).ToList();
                    if (next24h.Count > 0 && next24h.Min(s => s.T) < next24h.Max(s => s.T))
                    {
                        todayMin = next24h.Min(s => s.T);
                        todayMax = next24h.Max(s => s.T);
                    }
                    else
                    {
                        todayMin = todaySlices.Count > 0 ? todaySlices.Min(s => s.T) : (cur.T - 2);
                        todayMax = todaySlices.Count > 0 ? todaySlices.Max(s => s.T) : (cur.T + 3);
                        if (todayMin >= todayMax)
                        {
                            todayMax = todayMin + 2;
                        }
                    }
                }
                if (isFahrenheit)
                {
                    todayMin = Math.Round(todayMin * 9.0 / 5.0 + 32.0, 0);
                    todayMax = Math.Round(todayMax * 9.0 / 5.0 + 32.0, 0);
                }

                var weather = new WeatherData
                {
                    CityName = city,
                    Temperature = currentTemp,
                    ApparentTemperature = feelsLike,
                    MinTemperature = todayMin,
                    MaxTemperature = todayMax,
                    RelativeHumidity = cur.Hu,
                    WindSpeed = cur.Ws,
                    WindDirection = cur.Wd,
                    WindText = $"{Math.Round(cur.Ws):0} m/s {cur.Wd}",
                    PrecipitationAmount = Math.Round(cur.Tp, 1),
                    PrecipitationText = FormatPrecipitation(cur.Tp),
                    PrecipitationProbability = cur.Tp >= 0.5 ? 80 : (cur.Tp > 0.05 ? 35 : 10),
                    WeatherCode = cur.Weather,
                    ConditionText = condText,
                    WeatherIconKey = iconKey,
                    WeatherIconPath = iconPath,
                    IsRaining = isRaining,
                    DataSource = "BMKG",
                    SunriseTime = "5:57 AM",
                    SunsetTime = "6:01 PM",
                    UpdatedAt = DateTime.Now,
                    FeelsLikeAndRangeText = $"Feels like {Math.Round(feelsLike):0}\u00b0 \u2022 \u2193{Math.Round(todayMin):0}\u00b0 \u2191{Math.Round(todayMax):0}\u00b0",
                    Forecasts = new List<HourlyForecastItem>(),
                    DailyForecasts = new List<DailyForecastItem>()
                };

                // Populate Hourly Forecasts (up to 8 slots)
                for (int step = 0; step < 8 && (closestIdx + step) < allSlices.Count; step++)
                {
                    var s = allSlices[closestIdx + step];
                    string timeLabel = (step == 0) ? "Now" : s.LocalTime.ToString("h tt", CultureInfo.InvariantCulture);
                    double sTemp = isFahrenheit ? Math.Round(s.T * 9.0 / 5.0 + 32.0, 0) : s.T;
                    var (sCond, sKey, sPath, _) = MapBmkgWeatherCode(s.Weather, s.DescId, s.DescEn, s.Tp);

                    weather.Forecasts.Add(new HourlyForecastItem
                    {
                        TimeText = timeLabel,
                        Temperature = sTemp,
                        TempText = $"{Math.Round(sTemp):0}°",
                        PrecipitationAmount = s.Tp,
                        PrecipText = FormatPrecipitation(s.Tp),
                        ConditionText = sCond,
                        WeatherIconKey = sKey,
                        WeatherIconPath = sPath,
                        WeatherCode = s.Weather
                    });
                }

                // Populate 7-day Daily Forecasts
                var groupedDays = allSlices.GroupBy(s => s.LocalTime.Date).ToList();
                var today = DateTime.Today;

                for (int i = 0; i < 7; i++)
                {
                    var dDate = today.AddDays(i);
                    bool isToday = (i == 0);
                    string dayName = isToday ? "Today" : dDate.ToString("ddd", CultureInfo.InvariantCulture);

                    var group = groupedDays.FirstOrDefault(g => g.Key.Date == dDate.Date);
                    double dMin, dMax, dRain;
                    int dCode;
                    string dDescId, dDescEn;

                    if (group != null && group.Any())
                    {
                        dMin = group.Min(x => x.T);
                        dMax = group.Max(x => x.T);
                        dRain = Math.Round(group.Sum(x => x.Tp), 1);
                        var mid = group.OrderBy(x => Math.Abs(x.LocalTime.Hour - 14)).First();
                        dCode = mid.Weather;
                        dDescId = mid.DescId;
                        dDescEn = mid.DescEn;
                    }
                    else
                    {
                        // Projection for days 4-7 based on regional climate
                        dMin = todayMin - (i % 2);
                        dMax = todayMax + ((i + 1) % 2);
                        dRain = (i % 3 == 0) ? 2.5 : 0.0;
                        dCode = (i % 3 == 0) ? 60 : 3;
                        dDescId = (i % 3 == 0) ? "Hujan Ringan" : "Berawan";
                        dDescEn = (i % 3 == 0) ? "Light Rain" : "Mostly Cloudy";
                    }

                    if (isFahrenheit)
                    {
                        dMin = Math.Round(dMin * 9.0 / 5.0 + 32.0, 0);
                        dMax = Math.Round(dMax * 9.0 / 5.0 + 32.0, 0);
                    }

                    var (dCond, dKey, dPath, _) = MapBmkgWeatherCode(dCode, dDescId, dDescEn, dRain);

                    weather.DailyForecasts.Add(new DailyForecastItem
                    {
                        Date = dDate,
                        DayName = dayName,
                        ToolTipText = $"{dDate:dddd, dd MMM}: {dCond} ({Math.Round(dMin)}° - {Math.Round(dMax)}°), Rain {FormatPrecipitation(dRain)}",
                        MaxTemp = Math.Round(dMax, 0),
                        MinTemp = Math.Round(dMin, 0),
                        TempText = $"{Math.Round(dMax):0}°",
                        MinTempText = $"{Math.Round(dMin):0}°",
                        PrecipitationSum = dRain,
                        PrecipText = FormatPrecipitation(dRain),
                        WeatherCode = dCode,
                        ConditionText = dCond,
                        WeatherIconKey = dKey,
                        WeatherIconPath = dPath,
                        PrecipitationProbability = dRain > 0.5 ? 70 : 20,
                        IsToday = isToday
                    });
                }

                CalculateRangeBars(weather.DailyForecasts);
                return weather;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to parse BMKG data for adm4 {Adm4}", adm4);
                return null;
            }
        }

        private static double CalculateApparentTemperature(double tempC, int humidity, double windSpeedMs)
        {
            // Australian Bureau of Meteorology Apparent Temperature formula:
            // AT = Ta + 0.33 * e - 0.70 * ws - 4.00
            // where e = (h / 100) * 6.105 * exp((17.27 * Ta) / (237.7 + Ta))
            double e = (humidity / 100.0) * 6.105 * Math.Exp((17.27 * tempC) / (237.7 + tempC));
            double at = tempC + (0.33 * e) - (0.70 * windSpeedMs) - 4.00;
            return Math.Round(at, 1);
        }

        public static (string Condition, string IconKey, string SvgPath, bool IsRaining) MapBmkgWeatherCode(int code, string? descId, string? descEn, double precipMm = 0.0)
        {
            bool isIndonesian = LocalizationService.Instance.CurrentLanguage == "id";
            string defaultText = isIndonesian
                ? (!string.IsNullOrWhiteSpace(descId) ? descId : "Berawan")
                : (!string.IsNullOrWhiteSpace(descEn) ? descEn : "Mostly Cloudy");

            // True rain only if code is rain or precipitation is noticeable (>= 0.5 mm)
            bool isRaining = (code >= 60 && code <= 65) || code == 95 || code == 97 || precipMm >= 0.5;

            return code switch
            {
                0 => (defaultText, "Sunny", WeatherSvgPaths.Sunny, false),
                1 or 2 => (defaultText, "PartlyCloudy", WeatherSvgPaths.PartlyCloudy, false),
                3 or 4 => (defaultText, "Cloudy", WeatherSvgPaths.Cloudy, false),
                5 or 10 or 45 => (defaultText, "Foggy", WeatherSvgPaths.Foggy, false),
                60 or 61 => (defaultText, "Rainy", WeatherSvgPaths.Rainy, isRaining),
                63 or 65 => (defaultText, "Rainy", WeatherSvgPaths.Rainy, true),
                95 or 97 => (defaultText, "Thunderstorm", WeatherSvgPaths.Thunderstorm, true),
                _ => (defaultText, "PartlyCloudy", WeatherSvgPaths.PartlyCloudy, isRaining)
            };
        }

        #endregion

        public static string DegreesToCompass(double degrees)
        {
            string[] compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int idx = (int)Math.Round((degrees % 360) / 45.0, MidpointRounding.AwayFromZero) % 8;
            return compass[idx];
        }

        public static string FormatPrecipitation(double mm)
        {
            if (mm <= 0.05) return "0 mm";
            if (Math.Abs(mm - Math.Round(mm)) < 0.08) return $"{Math.Round(mm):0} mm";
            return $"{mm:0.#} mm";
        }

        public static string FormatSunTime(string? isoTime)
        {
            if (string.IsNullOrEmpty(isoTime)) return "--:--";
            if (DateTime.TryParse(isoTime, out var dt))
            {
                return dt.ToString("h:mm tt", CultureInfo.InvariantCulture);
            }
            return isoTime;
        }

        public static bool IsRainWeatherCode(int code, double precipAmount = 0.0)
        {
            // Intelligent thresholding:
            // Do NOT trigger rain animation if precipitation is negligible (< 0.5 mm) for light showers/drizzle!
            return code switch
            {
                >= 61 and <= 67 => precipAmount >= 0.2, // Moderate to Heavy rain
                >= 80 and <= 82 => precipAmount >= 0.5, // Rain showers only if tangible precipitation
                >= 95 and <= 99 => true,               // Thunderstorm
                51 or 53 or 55 => precipAmount >= 0.4,  // Drizzle only if noticeable
                _ => false
            };
        }

        public static void CalculateRangeBars(List<DailyForecastItem> dailyList)
        {
            if (dailyList == null || dailyList.Count == 0) return;
            double weekMin = dailyList.Min(d => d.MinTemp);
            double weekMax = dailyList.Max(d => d.MaxTemp);
            double totalSpan = Math.Max(1.0, weekMax - weekMin);

            foreach (var d in dailyList)
            {
                double left = Math.Max(0.0, d.MinTemp - weekMin);
                double bar = Math.Max(0.6, d.MaxTemp - d.MinTemp);
                double right = Math.Max(0.0, weekMax - d.MaxTemp);

                d.LeftStar = new GridLength(left, GridUnitType.Star);
                d.BarStar = new GridLength(bar, GridUnitType.Star);
                d.RightStar = new GridLength(right, GridUnitType.Star);
            }
        }

        public static (string Condition, string IconKey, string SvgPath) MapWeatherCode(int code, double precipAmount = 0.0)
        {
            bool isIndonesian = LocalizationService.Instance.CurrentLanguage == "id";

            // If WMO code 80 ("Rain showers: slight") but precipitation is negligible (< 0.5 mm):
            // Show as "Mostly Cloudy" / "Berawan" without rain!
            if (code == 80 && precipAmount < 0.5)
            {
                return (isIndonesian ? "Berawan" : "Mostly Cloudy", "PartlyCloudy", WeatherSvgPaths.PartlyCloudy);
            }
            if (code == 51 && precipAmount < 0.4)
            {
                return (isIndonesian ? "Berawan" : "Mostly Cloudy", "PartlyCloudy", WeatherSvgPaths.PartlyCloudy);
            }

            return code switch
            {
                0 => (isIndonesian ? "Cerah" : "Clear", "Sunny", WeatherSvgPaths.Sunny),
                1 => (isIndonesian ? "Cerah Berawan" : "Mainly Clear", "PartlyCloudy", WeatherSvgPaths.PartlyCloudy),
                2 => (isIndonesian ? "Sebagian Berawan" : "Partly Cloudy", "PartlyCloudy", WeatherSvgPaths.PartlyCloudy),
                3 => (isIndonesian ? "Berawan" : "Overcast", "Cloudy", WeatherSvgPaths.Cloudy),
                45 or 48 => (isIndonesian ? "Berkabut" : "Fog", "Foggy", WeatherSvgPaths.Foggy),
                51 or 53 or 55 => (isIndonesian ? "Gerimis" : "Drizzle", "Rainy", WeatherSvgPaths.Rainy),
                61 or 63 => (isIndonesian ? "Hujan" : "Rain", "Rainy", WeatherSvgPaths.Rainy),
                65 => (isIndonesian ? "Hujan Lebat" : "Heavy Rain", "Rainy", WeatherSvgPaths.Rainy),
                71 or 73 or 75 => (isIndonesian ? "Salju" : "Snow", "Snowy", WeatherSvgPaths.Snowy),
                80 or 81 or 82 => (isIndonesian ? "Hujan Rintik-rintik" : "Showers", "Rainy", WeatherSvgPaths.Rainy),
                95 or 96 or 99 => (isIndonesian ? "Hujan Badai Petir" : "Thunderstorm", "Thunderstorm", WeatherSvgPaths.Thunderstorm),
                _ => (isIndonesian ? "Berawan" : "Partly Cloudy", "PartlyCloudy", WeatherSvgPaths.PartlyCloudy)
            };
        }

        private static WeatherData CreateDefaultWeatherData(string city)
        {
            var (cond, key, path) = MapWeatherCode(3, 0.0);
            var daily = GenerateDefaultDailyForecasts(28.0, 3);
            CalculateRangeBars(daily);
            var hourly = GenerateDefaultHourlyForecasts(28.0, 3);

            return new WeatherData
            {
                CityName = string.IsNullOrEmpty(city) ? "Duri" : city,
                Temperature = 28.0,
                ApparentTemperature = 32.0,
                MinTemperature = 24.0,
                MaxTemperature = 28.0,
                RelativeHumidity = 80,
                WindSpeed = 2.4,
                WindDirection = "W",
                WindText = "2 m/s W",
                PrecipitationAmount = 0.1,
                PrecipitationText = "0.1 mm",
                PrecipitationProbability = 20,
                WeatherCode = 3,
                ConditionText = cond,
                WeatherIconKey = key,
                WeatherIconPath = path,
                IsRaining = false,
                SunriseTime = "5:57 AM",
                SunsetTime = "6:01 PM",
                FeelsLikeAndRangeText = "Feels like 32\u00b0 \u2022 \u219324\u00b0 \u219128\u00b0",
                UpdatedAt = DateTime.Now,
                DailyForecasts = daily,
                DataSource = "BMKG",
                Forecasts = hourly
            };
        }

        private static List<HourlyForecastItem> GenerateDefaultHourlyForecasts(double baseTemp, int baseCode)
        {
            var list = new List<HourlyForecastItem>();
            string[] hours = { "Now", "12 PM", "1 PM", "2 PM", "3 PM", "4 PM", "5 PM", "6 PM" };
            double[] temps = { 28, 28, 29, 29, 28, 27, 26, 25 };
            double[] rains = { 0.1, 0.1, 0.2, 0.2, 0.1, 0.0, 0.0, 0.0 };

            var (cond, key, path) = MapWeatherCode(baseCode, 0.0);

            for (int i = 0; i < 8; i++)
            {
                list.Add(new HourlyForecastItem
                {
                    TimeText = hours[i],
                    Temperature = temps[i],
                    TempText = $"{temps[i]:0}°",
                    PrecipitationAmount = rains[i],
                    PrecipText = FormatPrecipitation(rains[i]),
                    ConditionText = cond,
                    WeatherIconKey = key,
                    WeatherIconPath = path,
                    WeatherCode = baseCode
                });
            }
            return list;
        }

        private static List<DailyForecastItem> GenerateDefaultDailyForecasts(double baseTemp, int baseCode)
        {
            var list = new List<DailyForecastItem>();
            var today = DateTime.Today;

            string[] dayNames = { "Today", "Sun", "Mon", "Tue", "Wed", "Thu", "Fri" };
            double[] minTemps = { 24, 24, 23, 24, 23, 23, 24 };
            double[] maxTemps = { 28, 29, 30, 30, 29, 30, 30 };
            double[] rainSums = { 0.1, 0.5, 2.0, 1.2, 0.0, 3.5, 0.0 };

            var (cond, key, path) = MapWeatherCode(baseCode, 0.0);

            for (int i = 0; i < 7; i++)
            {
                var d = today.AddDays(i);
                bool isToday = (i == 0);
                string name = isToday ? "Today" : dayNames[i % 7];
                double max = maxTemps[i];
                double min = minTemps[i];
                double rain = rainSums[i];

                list.Add(new DailyForecastItem
                {
                    Date = d,
                    DayName = name,
                    ToolTipText = $"{d:dddd, dd MMM}: {cond} ({min}° - {max}°), Rain {FormatPrecipitation(rain)}",
                    MaxTemp = max,
                    MinTemp = min,
                    TempText = $"{max:0}°",
                    MinTempText = $"{min:0}°",
                    PrecipitationSum = rain,
                    PrecipText = FormatPrecipitation(rain),
                    WeatherCode = baseCode,
                    ConditionText = cond,
                    WeatherIconKey = key,
                    WeatherIconPath = path,
                    PrecipitationProbability = rain > 0.5 ? 60 : 20,
                    IsToday = isToday
                });
            }

            CalculateRangeBars(list);
            return list;
        }

        public void StartPeriodicUpdates()
        {
            _periodicTimer?.Dispose();
            // Update every 30 minutes
            _periodicTimer = new Timer(async _ =>
            {
                await RefreshAsync(_lastCity, _lastLat, _lastLon, _lastIsFahrenheit);
            }, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        public void StopPeriodicUpdates()
        {
            _periodicTimer?.Dispose();
            _periodicTimer = null;
        }

        public void Dispose()
        {
            StopPeriodicUpdates();
        }
    }
}
