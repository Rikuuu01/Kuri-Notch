using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NotchWin.Models
{
    public partial class NoteItem : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [ObservableProperty] private string _title = "Note 1";
        [ObservableProperty] private string _content = string.Empty;
        [ObservableProperty] private string _plainText = string.Empty;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isConfirmingDelete;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
    }
}
