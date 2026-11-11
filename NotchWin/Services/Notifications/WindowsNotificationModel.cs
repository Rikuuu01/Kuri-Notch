using System;

namespace NotchWin.Services.Notifications
{
    public class WindowsNotificationModel
    {
        public long Order { get; set; }
        public long Id { get; set; }
        public string AppId { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string Icon { get; set; } = "🔔";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public override string ToString() => $"[{AppName}] {Title}: {Message}";
    }
}
