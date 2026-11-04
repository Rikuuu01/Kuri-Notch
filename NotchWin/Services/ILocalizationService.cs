using System;

namespace NotchWin.Services
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; set; }
        string this[string key] { get; }
        string Get(string key, params object[] args);
        event EventHandler<string>? LanguageChanged;
    }
}
