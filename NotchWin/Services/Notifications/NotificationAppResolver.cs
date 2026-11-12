using System;

namespace NotchWin.Services.Notifications
{
    public static class NotificationAppResolver
    {
        public static (string AppName, string Icon) Resolve(string? rawAppId)
        {
            var detailed = ResolveDetailed(rawAppId);
            return (detailed.AppName, detailed.Icon);
        }

        public static (string AppName, string Icon, string AppType, string BrandColor) ResolveDetailed(string? rawAppId, string? fallbackAppName = null, string? fallbackIcon = null)
        {
            if (string.IsNullOrWhiteSpace(rawAppId))
            {
                string name = !string.IsNullOrWhiteSpace(fallbackAppName) ? fallbackAppName : "Windows";
                string icon = !string.IsNullOrWhiteSpace(fallbackIcon) ? fallbackIcon : "🔔";
                return (name, icon, "generic", "#3B82F6");
            }

            string lower = rawAppId.ToLowerInvariant();

            // WhatsApp
            if (lower.Contains("whatsapp"))
                return ("WhatsApp", "💬", "whatsapp", "#25D366");

            // Discord
            if (lower.Contains("discord"))
                return ("Discord", "🎮", "discord", "#5865F2");

            // Telegram
            if (lower.Contains("telegram"))
                return ("Telegram", "✈️", "telegram", "#2AABEE");

            // Spotify
            if (lower.Contains("spotify"))
                return ("Spotify", "🎵", "spotify", "#1DB954");

            // Antigravity
            if (lower.Contains("antigravity"))
                return ("Antigravity", "⚡", "generic", "#38BDF8");

            // VS Code / Visual Studio
            if (lower.Contains("visualstudiocode") || lower.Contains("vscode") || lower.Contains("code.exe"))
                return ("VS Code", "💻", "code", "#007ACC");
            if (lower.Contains("visualstudio"))
                return ("Visual Studio", "💻", "code", "#5C2D91");

            // Teams / Slack
            if (lower.Contains("teams"))
                return ("Teams", "👥", "teams", "#464EB8");
            if (lower.Contains("slack"))
                return ("Slack", "💼", "slack", "#4A154B");

            // Mail / Outlook / Thunderbird
            if (lower.Contains("outlook") || lower.Contains("windowscommunicationsapps") || lower.Contains("thunderbird") || lower.Contains("mail"))
                return ("Outlook", "✉️", "mail", "#0078D4");

            // Browsers
            if (lower.Contains("chrome"))
                return ("Chrome", "🌐", "browser", "#EA4335");
            if (lower.Contains("edge") || lower.Contains("msedge"))
                return ("Edge", "🌐", "browser", "#0078D4");
            if (lower.Contains("firefox"))
                return ("Firefox", "🦊", "browser", "#FF7139");
            if (lower.Contains("brave"))
                return ("Brave", "🦁", "browser", "#FB542B");

            // Windows System Toasts
            if (lower.Contains("screensketch") || lower.Contains("snippingtool"))
                return ("Snipping Tool", "✂️", "screenshot", "#0078D4");
            if (lower.Contains("usb"))
                return ("USB Device", "💾", "generic", "#0284C7");
            if (lower.Contains("wifi") || lower.Contains("network"))
                return ("Network", "📶", "generic", "#3B82F6");
            if (lower.Contains("bth") || lower.Contains("bluetooth"))
                return ("Bluetooth", "🎧", "bluetooth", "#0082FC");
            if (lower.Contains("power") || lower.Contains("battery"))
                return ("Battery", "🔋", "generic", "#10B981");
            if (lower.Contains("print"))
                return ("Printer", "🖨️", "generic", "#64748B");
            if (lower.Contains("windowsupdate") || lower.Contains("update"))
                return ("Windows Update", "🔄", "generic", "#0284C7");
            if (lower.Contains("defender") || lower.Contains("securitycenter"))
                return ("Windows Security", "🛡️", "generic", "#0078D4");
            if (lower.Contains("systemtoast") || lower.Contains("system"))
                return ("Windows", "💻", "generic", "#0078D4");

            // Steam
            if (lower.Contains("steam"))
                return ("Steam", "🎮", "steam", "#171A21");

            // Generic fallback extraction
            string cleaned = !string.IsNullOrWhiteSpace(fallbackAppName) ? fallbackAppName : CleanAumid(rawAppId);
            string fallbackIco = !string.IsNullOrWhiteSpace(fallbackIcon) ? fallbackIcon : "🔔";
            return (cleaned, fallbackIco, "generic", "#3B82F6");
        }

        public static (string AppName, string Icon, string AppType, string BrandColor) ResolveForInternal(string icon, string title)
        {
            return icon switch
            {
                "📸" => ("Snipping Tool", "📸", "screenshot", "#0284C7"),
                "📋" => ("Clipboard", "📋", "clipboard", "#8B5CF6"),
                "🎧" => ("Bluetooth", "🎧", "bluetooth", "#0082FC"),
                "🕌" => ("Prayer", "🕌", "prayer", "#10B981"),
                "💧" => ("Hydration", "💧", "water", "#06B6D4"),
                "⏱️" => ("Timer", "⏱️", "timer", "#F59E0B"),
                "⏰" => ("Alarm", "⏰", "alarm", "#EF4444"),
                _ => ("Kuri", string.IsNullOrEmpty(icon) ? "🔔" : icon, "generic", "#6366F1")
            };
        }

        private static string CleanAumid(string appId)
        {
            int bangIdx = appId.IndexOf('!');
            if (bangIdx > 0)
            {
                string afterBang = appId.Substring(bangIdx + 1);
                if (!string.Equals(afterBang, "App", StringComparison.OrdinalIgnoreCase))
                {
                    return afterBang;
                }
                appId = appId.Substring(0, bangIdx);
            }

            int underscoreIdx = appId.IndexOf('_');
            if (underscoreIdx > 0)
            {
                appId = appId.Substring(0, underscoreIdx);
            }

            int dotIdx = appId.LastIndexOf('.');
            if (dotIdx >= 0 && dotIdx < appId.Length - 1)
            {
                appId = appId.Substring(dotIdx + 1);
            }

            return string.IsNullOrWhiteSpace(appId) ? "Windows" : appId;
        }
    }
}
