using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class ShelfViewModel : ObservableObject
    {
        private readonly IShelfService _shelfService;

        public ObservableCollection<ShelfItem> Items => _shelfService.Items;

        public bool HasItems => Items.Count > 0;
        public bool IsEmpty => Items.Count == 0;

        public string ItemsCountText => Items.Count switch
        {
            0 => "Empty Shelf",
            1 => "1 Item",
            _ => $"{Items.Count} Items"
        };

        public string TotalSizeText
        {
            get
            {
                long totalBytes = Items.Sum(i => i.FileSizeBytes);
                if (totalBytes < 1024) return $"{totalBytes} B";
                if (totalBytes < 1024 * 1024) return $"{totalBytes / 1024.0:F1} KB";
                if (totalBytes < 1024 * 1024 * 1024) return $"{totalBytes / (1024.0 * 1024.0):F1} MB";
                return $"{totalBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }

        public ShelfViewModel(IShelfService shelfService)
        {
            _shelfService = shelfService;
            Items.CollectionChanged += OnItemsCollectionChanged;
        }

        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ItemsCountText));
            OnPropertyChanged(nameof(TotalSizeText));
        }

        public int AddFiles(IEnumerable<string> filePaths)
        {
            return _shelfService.AddFiles(filePaths);
        }

        [RelayCommand]
        public void PickFiles()
        {
            using var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select files to temporarily store in Shelf"
            };

            if (dialog.ShowDialog() == DialogResult.OK && dialog.FileNames.Length > 0)
            {
                _shelfService.AddFiles(dialog.FileNames);
            }
        }

        [RelayCommand]
        public void RemoveItem(ShelfItem? item)
        {
            if (item != null)
            {
                _shelfService.RemoveItem(item);
            }
        }

        [RelayCommand]
        public void ClearAll()
        {
            _shelfService.ClearAll();
        }

        [RelayCommand]
        public void OpenItem(ShelfItem? item)
        {
            if (item != null)
            {
                _shelfService.OpenItem(item);
            }
        }

        [RelayCommand]
        public void ShowInExplorer(ShelfItem? item)
        {
            if (item != null)
            {
                _shelfService.ShowInExplorer(item);
            }
        }

        [RelayCommand]
        public void CopyItem(ShelfItem? item)
        {
            if (item != null)
            {
                _shelfService.CopyItem(item);
            }
        }

        [RelayCommand]
        public void CopyAll()
        {
            _shelfService.CopyAllToClipboard();
        }
    }
}
