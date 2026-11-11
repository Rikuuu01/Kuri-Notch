using System;

namespace NotchWin.Services.Notifications
{
    public static class NotificationAppResolver
    {
        public static (string AppName, string Icon) Resolve(string? rawAppId)
        {
            if (string.IsNullOrWhiteSpace(rawAppId))
            {
                return ("Windows", "🔔");
            }

            string lower = rawAppId.ToLowerInvariant();

            // WhatsApp
            if (lower.Contains("whatsapp"))
                return ("WhatsApp", "💬");

            // Discord
            if (lower.Contains("discord"))
                return ("Discord", "🎮");

            // Telegram
            if (lower.Contains("telegram"))
                return ("Telegram", "✈️");

            // Spotify
            if (lower.Contains("spotify"))
                return ("Spotify", "🎵");

            // Antigravity
            if (lower.Contains("antigravity"))
                return ("Antigravity", "⚡");

            // VS Code / Visual Studio
            if (lower.Contains("visualstudiocode") || lower.Contains("vscode") || lower.Contains("code.exe"))
                return ("VS Code", "💻");
            if (lower.Contains("visualstudio"))
                return ("Visual Studio", "💻");

            // Teams / Slack
            if (lower.Contains("teams"))
                return ("Teams", "👥");
            if (lower.Contains("slack"))
                return ("Slack", "💼");

            // Mail / Outlook / Thunderbird
            if (lower.Contains("outlook") || lower.Contains("windowscommunicationsapps") || lower.Contains("thunderbird") || lower.Contains("mail"))
                return ("Mail", "✉️");

            // Browsers
            if (lower.Contains("chrome"))
                return ("Chrome", "🌐");
            if (lower.Contains("edge") || lower.Contains("msedge"))
                return ("Edge", "🌐");
            if (lower.Contains("firefox"))
                return ("Firefox", "🦊");
            if (lower.Contains("brave"))
                return ("Brave", "🦁");

            // Windows System Toasts
            if (lower.Contains("screensketch") || lower.Contains("snippingtool"))
                return ("Snipping Tool", "✂️");
            if (lower.Contains("usb"))
                return ("USB Device", "💾");
            if (lower.Contains("wifi") || lower.Contains("network"))
                return ("Network", "📶");
            if (lower.Contains("bth") || lower.Contains("bluetooth"))
                return ("Bluetooth", "🎧");
            if (lower.Contains("power") || lower.Contains("battery"))
                return ("Battery", "🔋");
            if (lower.Contains("print"))
                return ("Printer", "🖨️");
            if (lower.Contains("windowsupdate") || lower.Contains("update"))
                return ("Windows Update", "🔄");
            if (lower.Contains("defender") || lower.Contains("securitycenter"))
                return ("Windows Security", "🛡️");
            if (lower.Contains("systemtoast") || lower.Contains("system"))
                return ("Windows", "💻");

            // Steam
            if (lower.Contains("steam"))
                return ("Steam", "🎮");

            // Generic fallback extraction
            string cleaned = CleanAumid(rawAppId);
            return (cleaned, "🔔");
        }

        private static string CleanAumid(string appId)
        {
            // Remove UWP App suffix like !App or !VSCode
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

            // Remove package publisher suffix (e.g., 5319275A.WhatsAppDesktop_cv1g1gvanyjgm)
            int underscoreIdx = appId.IndexOf('_');
            if (underscoreIdx > 0)
            {
                appId = appId.Substring(0, underscoreIdx);
            }

            // Take last segment after dots (e.g., com.squirrel.Discord -> Discord)
            int dotIdx = appId.LastIndexOf('.');
            if (dotIdx >= 0 && dotIdx < appId.Length - 1)
            {
                appId = appId.Substring(dotIdx + 1);
            }

            return string.IsNullOrWhiteSpace(appId) ? "Windows" : appId;
        }
    }
}
