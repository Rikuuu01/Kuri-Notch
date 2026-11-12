using System;
using System.IO;
using System.Windows.Media;
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
        private string _icon = "🔔";

        [ObservableProperty]
        private string _appName = "Notification";

        [ObservableProperty]
        private string _title = "Storage Connected";

        [ObservableProperty]
        private string _message = "USB Drive ready to use";

        [ObservableProperty]
        private string _timestampText = "now";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasImagePath))]
        [NotifyPropertyChangedFor(nameof(IsGenericVector))]
        private string? _imagePath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWhatsApp))]
        [NotifyPropertyChangedFor(nameof(IsTelegram))]
        [NotifyPropertyChangedFor(nameof(IsDiscord))]
        [NotifyPropertyChangedFor(nameof(IsSpotify))]
        [NotifyPropertyChangedFor(nameof(IsGenericVector))]
        private string _appType = "generic";

        [ObservableProperty]
        private Brush _brandBrush = new SolidColorBrush(Color.FromRgb(37, 211, 102));

        [ObservableProperty]
        private bool _isPrayer;

        [ObservableProperty]
        private bool _isWaterAlarm;

        [ObservableProperty]
        private bool _isUpdateNotice;

        [ObservableProperty]
        private Models.UpdateCheckResult? _updateData;

        public bool HasImagePath => !string.IsNullOrWhiteSpace(ImagePath) && File.Exists(ImagePath);
        public bool IsWhatsApp => string.Equals(AppType, "whatsapp", StringComparison.OrdinalIgnoreCase);
        public bool IsTelegram => string.Equals(AppType, "telegram", StringComparison.OrdinalIgnoreCase);
        public bool IsDiscord => string.Equals(AppType, "discord", StringComparison.OrdinalIgnoreCase);
        public bool IsSpotify => string.Equals(AppType, "spotify", StringComparison.OrdinalIgnoreCase);
        public bool IsGenericVector => !IsWhatsApp && !IsTelegram && !IsDiscord && !IsSpotify && !HasImagePath;

        public void SetNotification(string icon, string title, string message, bool isPrayer = false, bool isWaterAlarm = false)
        {
            Icon = icon;
            Title = title;
            Message = message;
            IsPrayer = isPrayer;
            IsWaterAlarm = isWaterAlarm;
        }

        public void SetBanner(
            string appName,
            string title,
            string message,
            string icon,
            string? imagePath,
            string appType,
            string brandColorHex,
            string timestampText,
            bool isPrayer = false,
            bool isWaterAlarm = false,
            bool isUpdateNotice = false,
            Models.UpdateCheckResult? updateData = null)
        {
            AppName = appName;
            Title = title;
            Message = message;
            Icon = icon;
            ImagePath = imagePath;
            AppType = appType;
            TimestampText = timestampText;
            IsPrayer = isPrayer;
            IsWaterAlarm = isWaterAlarm;
            IsUpdateNotice = isUpdateNotice;
            UpdateData = updateData;

            try
            {
                if (!string.IsNullOrWhiteSpace(brandColorHex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(brandColorHex);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    BrandBrush = brush;
                }
                else
                {
                    var defaultBrush = new SolidColorBrush(Color.FromRgb(37, 211, 102));
                    defaultBrush.Freeze();
                    BrandBrush = defaultBrush;
                }
            }
            catch
            {
                var fallbackBrush = new SolidColorBrush(Color.FromRgb(37, 211, 102));
                fallbackBrush.Freeze();
                BrandBrush = fallbackBrush;
            }

            OnPropertyChanged(nameof(HasImagePath));
            OnPropertyChanged(nameof(IsWhatsApp));
            OnPropertyChanged(nameof(IsTelegram));
            OnPropertyChanged(nameof(IsDiscord));
            OnPropertyChanged(nameof(IsSpotify));
            OnPropertyChanged(nameof(IsGenericVector));
        }
    }
}
