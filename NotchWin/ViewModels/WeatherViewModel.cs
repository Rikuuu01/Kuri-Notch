using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class WeatherViewModel : ObservableObject
    {
        private readonly IWeatherService _weatherService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private string _temperatureFormatted = "29°C";

        [ObservableProperty]
        private string _apparentTemperatureFormatted = "Feels like 34°C";

        [ObservableProperty]
        private string _feelsLikeAndRangeText = "Feels like 34\u00b0 \u2022 \u219325\u00b0 \u219129\u00b0";

        [ObservableProperty]
        private string _conditionText = "Rain";

        [ObservableProperty]
        private string _cityName = "Duri";

        [ObservableProperty]
        private string _humidityFormatted = "77%";

        [ObservableProperty]
        private string _windSpeedFormatted = "2 m/s";

        [ObservableProperty]
        private string _windText = "2 m/s NW";

        [ObservableProperty]
        private string _precipitationFormatted = "80%";

        [ObservableProperty]
        private string _precipitationText = "0.8 mm";

        [ObservableProperty]
        private string _sunriseText = "5:56 AM";

        [ObservableProperty]
        private string _sunsetText = "6:01 PM";

        [ObservableProperty]
        private bool _isRaining = true;

        [ObservableProperty]
        private string _weatherIconKey = "Rainy";

        [ObservableProperty]
        private string _weatherIconPath = WeatherSvgPaths.Rainy;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _lastUpdatedText = LocalizationService.Instance["Weather.NotUpdated"];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataSourceToolTip))]
        private string _dataSource = "BMKG";

        public string DataSourceToolTip => DataSource == "BMKG"
            ? LocalizationService.Instance["Weather.DataSourceBmkg"]
            : LocalizationService.Instance["Weather.DataSourceOpenMeteo"];

        public ObservableCollection<HourlyForecastItem> Forecasts { get; } = new();
        public ObservableCollection<DailyForecastItem> DailyForecasts { get; } = new();

        public WeatherViewModel(IWeatherService weatherService, ISettingsService settingsService)
        {
            _weatherService = weatherService;
            _settingsService = settingsService;

            _weatherService.WeatherUpdated += OnWeatherUpdated;
            LocalizationService.Instance.LanguageChanged += (_, _) =>
            {
                if (_weatherService.CurrentWeather != null)
                {
                    App.Current?.Dispatcher?.Invoke(() => ApplyWeatherData(_weatherService.CurrentWeather));
                }
            };
            if (_weatherService.CurrentWeather != null)
            {
                ApplyWeatherData(_weatherService.CurrentWeather);
            }
        }

        private void OnWeatherUpdated(object? sender, WeatherData data)
        {
            App.Current?.Dispatcher?.Invoke(() => ApplyWeatherData(data));
        }

        private void ApplyWeatherData(WeatherData data)
        {
            string unit = _settingsService.CurrentSettings.IsFahrenheit ? "°F" : "°C";
            TemperatureFormatted = $"{data.Temperature:0}{unit}";
            ApparentTemperatureFormatted = $"{LocalizationService.Instance["Weather.FeelsLike"]} {data.ApparentTemperature:0}{unit}";
            
            // Format Feels Like & Min/Max: "Feels like 34\u00b0 \u2022 \u219325\u00b0 \u219129\u00b0"
            string feelsPrefix = LocalizationService.Instance["Weather.FeelsLike"];
            FeelsLikeAndRangeText = $"{feelsPrefix} {data.ApparentTemperature:0}\u00b0 \u2022 \u2193{data.MinTemperature:0}\u00b0 \u2191{data.MaxTemperature:0}\u00b0";

            ConditionText = data.ConditionText;
            CityName = data.CityName;
            HumidityFormatted = $"{data.RelativeHumidity}%";
            WindSpeedFormatted = $"{data.WindSpeed:0} m/s";
            WindText = string.IsNullOrEmpty(data.WindText) ? $"{data.WindSpeed:0} m/s {data.WindDirection}" : data.WindText;
            PrecipitationFormatted = $"{data.PrecipitationProbability}%";
            PrecipitationText = string.IsNullOrEmpty(data.PrecipitationText) ? $"{data.PrecipitationAmount:0.#} mm" : data.PrecipitationText;
            SunriseText = string.IsNullOrEmpty(data.SunriseTime) ? "5:56 AM" : data.SunriseTime;
            SunsetText = string.IsNullOrEmpty(data.SunsetTime) ? "6:01 PM" : data.SunsetTime;
            IsRaining = data.IsRaining;
            WeatherIconKey = data.WeatherIconKey;
            WeatherIconPath = string.IsNullOrEmpty(data.WeatherIconPath) ? WeatherSvgPaths.PartlyCloudy : data.WeatherIconPath;
            LastUpdatedText = LocalizationService.Instance.Get("Weather.Updated", data.UpdatedAt.ToString("HH:mm"));
            DataSource = string.IsNullOrEmpty(data.DataSource) ? "BMKG" : data.DataSource;

            Forecasts.Clear();
            foreach (var f in data.Forecasts)
            {
                Forecasts.Add(f);
            }

            DailyForecasts.Clear();
            foreach (var d in data.DailyForecasts)
            {
                DailyForecasts.Add(d);
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var s = _settingsService.CurrentSettings;
                await _weatherService.RefreshAsync(s.WeatherCity, s.WeatherLatitude, s.WeatherLongitude, s.IsFahrenheit);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
