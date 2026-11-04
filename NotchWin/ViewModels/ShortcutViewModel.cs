using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class ShortcutViewModel : ObservableObject
    {
        private readonly IShortcutService _shortcutService;

        public ObservableCollection<ShortcutItem> Shortcuts => _shortcutService.Shortcuts;

        public bool HasShortcuts => Shortcuts.Count > 0;
        public bool IsEmpty => Shortcuts.Count == 0;

        public ShortcutViewModel(IShortcutService shortcutService)
        {
            _shortcutService = shortcutService;
            Shortcuts.CollectionChanged += OnShortcutsCollectionChanged;
        }

        private void OnShortcutsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasShortcuts));
            OnPropertyChanged(nameof(IsEmpty));
        }

        [RelayCommand]
        public void Launch(ShortcutItem? item)
        {
            if (item != null)
            {
                _shortcutService.Launch(item);
            }
        }

        [RelayCommand]
        public void AddShortcut()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select Application or Shortcut for Dock",
                Filter = "Applications & Shortcuts (*.exe;*.lnk)|*.exe;*.lnk|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == DialogResult.OK && dialog.FileNames.Length > 0)
            {
                foreach (var file in dialog.FileNames)
                {
                    _shortcutService.AddShortcut(file);
                }
            }
        }

        [RelayCommand]
        public void RemoveShortcut(ShortcutItem? item)
        {
            if (item != null)
            {
                _shortcutService.RemoveShortcut(item);
            }
        }

        [RelayCommand]
        public void ResetDefaults()
        {
            _shortcutService.ResetToDefaults();
        }

        [RelayCommand]
        public void ClearAll()
        {
            _shortcutService.ClearAll();
        }

        public void AddFromDrop(string[] files)
        {
            if (files == null) return;
            foreach (var file in files)
            {
                _shortcutService.AddShortcut(file);
            }
        }
    }
}
