using System;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface ISettingsService
    {
        AppSettings CurrentSettings { get; }
        event EventHandler<AppSettings>? SettingsChanged;

        Task LoadAsync();
        Task SaveAsync(AppSettings settings);
    }
}
