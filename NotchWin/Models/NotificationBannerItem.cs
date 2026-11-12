namespace NotchWin.Models
{
    public class NotificationBannerItem
    {
        public string AppName { get; set; } = "Notification";
        public string AppId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Icon { get; set; } = "🔔";
        public string? ImagePath { get; set; }
        public string AppType { get; set; } = "generic";
        public string BrandColor { get; set; } = "#25D366";
        public string TimestampText { get; set; } = "now";
        public int DurationMs { get; set; } = 3500;
        public bool IsPrayer { get; set; } = false;
        public bool IsWaterAlarm { get; set; } = false;
        public bool IsUpdateNotice { get; set; } = false;
        public UpdateCheckResult? UpdateData { get; set; }
    }
}
