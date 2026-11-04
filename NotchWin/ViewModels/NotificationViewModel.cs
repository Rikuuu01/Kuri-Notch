using System;
using CommunityToolkit.Mvvm.ComponentModel;
using NotchWin.Widgets;

namespace NotchWin.ViewModels
{
    public partial class NotificationViewModel : ObservableObject, INotchWidget
    {
        public WidgetType Type => WidgetType.Notification;
        public string Name => "Notification";
        public bool IsActive => false; // Transient only
        public int Priority => 10;     // Highest priority when active

        [ObservableProperty]
        private string _icon = "💾";

        [ObservableProperty]
        private string _title = "Storage Connected";

        [ObservableProperty]
        private string _message = "USB Drive ready to use";

        [ObservableProperty]
        private bool _isPrayer;

        [ObservableProperty]
        private bool _isWaterAlarm;

        public void SetNotification(string icon, string title, string message, bool isPrayer = false, bool isWaterAlarm = false)
        {
            Icon = icon;
            Title = title;
            Message = message;
            IsPrayer = isPrayer;
            IsWaterAlarm = isWaterAlarm;
        }
    }
}
