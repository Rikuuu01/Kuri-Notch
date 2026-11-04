using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class ClipboardViewModel : ObservableObject
    {
        private readonly IClipboardService _clipboardService;

        public event EventHandler<string>? ToastRequested;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        public ObservableCollection<ClipboardItem> FilteredItems { get; } = new();
        public ObservableCollection<ClipboardItem> FilteredImageItems { get; } = new();
        public ObservableCollection<ClipboardItem> FilteredTextItems { get; } = new();

        public bool HasItems => FilteredItems.Count > 0;
        public bool HasNoItems => !HasItems;
        public bool HasImageItems => FilteredImageItems.Count > 0;
        public bool HasNoImageItems => !HasImageItems;
        public bool HasTextItems => FilteredTextItems.Count > 0;
        public bool HasNoTextItems => !HasTextItems;

        public ClipboardViewModel(IClipboardService clipboardService)
        {
            _clipboardService = clipboardService;
            _clipboardService.HistoryChanged += (s, e) => RefreshFilteredList();

            RefreshFilteredList();
        }

        public void Initialize(IntPtr windowHandle)
        {
            _clipboardService.Initialize(windowHandle);
        }

        public void ProcessClipboardUpdate()
        {
            _clipboardService.ProcessClipboardUpdate();
        }

        partial void OnSearchQueryChanged(string value)
        {
            RefreshFilteredList();
        }

        private void RefreshFilteredList()
        {
            var all = _clipboardService.History;
            var query = SearchQuery?.Trim() ?? string.Empty;

            var filtered = string.IsNullOrEmpty(query)
                ? all
                : all.Where(x => x.TextContent.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 x.PreviewText.Contains(query, StringComparison.OrdinalIgnoreCase));

            FilteredItems.Clear();
            FilteredImageItems.Clear();
            FilteredTextItems.Clear();

            foreach (var item in filtered)
            {
                FilteredItems.Add(item);
                if (item.IsImage)
                {
                    FilteredImageItems.Add(item);
                }
                else
                {
                    FilteredTextItems.Add(item);
                }
            }

            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasNoItems));
            OnPropertyChanged(nameof(HasImageItems));
            OnPropertyChanged(nameof(HasNoImageItems));
            OnPropertyChanged(nameof(HasTextItems));
            OnPropertyChanged(nameof(HasNoTextItems));
        }

        [RelayCommand]
        public async Task CopyItemAsync(ClipboardItem? item)
        {
            if (item == null) return;
            await _clipboardService.CopyToClipboardAsync(item);
            ToastRequested?.Invoke(this, LocalizationService.Instance["Notification.ClipboardCopied"]);
        }

        [RelayCommand]
        public void DeleteItem(ClipboardItem? item)
        {
            if (item == null) return;
            _clipboardService.DeleteItem(item.Id);
        }

        [RelayCommand]
        public void TogglePin(ClipboardItem? item)
        {
            if (item == null) return;
            _clipboardService.TogglePin(item.Id);
        }

        [RelayCommand]
        public void ClearAll()
        {
            _clipboardService.ClearAll();
        }

        [RelayCommand]
        public void ClearImages()
        {
            _clipboardService.ClearImages();
        }

        [RelayCommand]
        public void ClearTexts()
        {
            _clipboardService.ClearTexts();
        }
    }
}
