using System.Collections.ObjectModel;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IShortcutService
    {
        ObservableCollection<ShortcutItem> Shortcuts { get; }
        bool HasShortcuts { get; }
        void Launch(ShortcutItem item);
        void AddShortcut(string targetPath, string? customName = null);
        void RemoveShortcut(ShortcutItem item);
        void ResetToDefaults();
        void ClearAll();
        void LoadShortcuts();
        void SaveShortcuts();
    }
}
