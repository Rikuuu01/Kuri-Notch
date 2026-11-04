using System;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IWeatherService
    {
        WeatherData? CurrentWeather { get; }
        event EventHandler<WeatherData>? WeatherUpdated;
        Task RefreshAsync(string city, double lat, double lon, bool isFahrenheit);
        void StartPeriodicUpdates();
        void StopPeriodicUpdates();
    }
}
