using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class SettingsService : ISettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotchWin"
        );

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private AppSettings _currentSettings = new();

        public static SettingsService? Instance { get; private set; }

        public AppSettings CurrentSettings => _currentSettings;

        public event EventHandler<AppSettings>? SettingsChanged;

        public SettingsService()
        {
            Instance = this;
            LoadSynchronous();
        }

        private void LoadSynchronous()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        if (!Enum.IsDefined(loaded.PetStyle))
                        {
                            // Nilai enum 0 berasal dari gaya maskot lama yang sudah dihapus.
                            loaded.PetStyle = PetStyle.PastelMochi;
                        }

                        // Pastikan seluruh modul navbar terdaftar lengkap
                        var defaultModules = AppSettings.GetDefaultNavbarModules();
                        if (loaded.NavbarModules == null || loaded.NavbarModules.Count == 0)
                        {
                            loaded.NavbarModules = defaultModules;
                        }
                        else
                        {
                            foreach (var def in defaultModules)
                            {
                                if (!loaded.NavbarModules.Exists(m => string.Equals(m.Id, def.Id, StringComparison.OrdinalIgnoreCase)))
                                {
                                    loaded.NavbarModules.Add(def);
                                }
                            }
                        }

                        _currentSettings = loaded;
                        LocalizationService.Instance.CurrentLanguage = _currentSettings.Language ?? "en";
                        Log.Information("Settings loaded synchronously from {Path} (Language: {Lang})", SettingsFilePath, _currentSettings.Language);
                        return;
                    }
                }
                else
                {
                    // Create default settings.json file on first launch
                    _currentSettings = new AppSettings();
                    Directory.CreateDirectory(SettingsDirectory);
                    string defaultJson = JsonSerializer.Serialize(_currentSettings, JsonOptions);
                    File.WriteAllText(SettingsFilePath, defaultJson);
                    LocalizationService.Instance.CurrentLanguage = _currentSettings.Language ?? "en";
                    Log.Information("Initial default settings.json created at {Path}", SettingsFilePath);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load/initialize settings.json synchronously, falling back to default settings");
            }

            _currentSettings = new AppSettings();
            LocalizationService.Instance.CurrentLanguage = _currentSettings.Language ?? "en";
        }

        public Task LoadAsync()
        {
            LoadSynchronous();
            return Task.CompletedTask;
        }

        public Task SaveAsync(AppSettings settings)
        {
            try
            {
                _currentSettings = settings;
                LocalizationService.Instance.CurrentLanguage = _currentSettings.Language ?? "en";
                Directory.CreateDirectory(SettingsDirectory);

                string json = JsonSerializer.Serialize(_currentSettings, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);

                Log.Information("Settings saved successfully to {Path}", SettingsFilePath);
                SettingsChanged?.Invoke(this, _currentSettings);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save settings.json to {Path}", SettingsFilePath);
            }

            return Task.CompletedTask;
        }
    }
}
