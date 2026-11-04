using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NotchWin.Models
{
    public partial class ReminderItem : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [ObservableProperty] private string _text = string.Empty;
        [ObservableProperty] private DateTime _dueTime = DateTime.Now.AddMinutes(30);
        [ObservableProperty] private bool _isCompleted;
        [ObservableProperty] private bool _isNotified;
        [ObservableProperty] private bool _isAlarmEnabled = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string TimeString => DueTime.ToString("HH:mm");

        public string FormattedDueTime
        {
            get
            {
                var diff = DueTime - DateTime.Now;
                if (diff.TotalSeconds <= 0 && !IsCompleted) return "Waktunya tiba!";
                if (DueTime.Date == DateTime.Today) return DueTime.ToString("HH:mm");
                if (DueTime.Date == DateTime.Today.AddDays(1)) return $"Besok {DueTime:HH:mm}";
                return DueTime.ToString("dd MMM HH:mm");
            }
        }
    }
}
