using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace NotchWin.Models
{
    public static class ScreenTimeCategories
    {
        public const string Browsing = "Browsing";
        public const string Development = "Development";
        public const string Entertainment = "Entertainment";
        public const string Productivity = "Productivity";
        public const string System = "System";
        public const string Other = "Other";

        public static string GetColorHex(string? category) => category?.ToLowerInvariant() switch
        {
            "browsing" => "#64D2FF",
            "development" => "#30D158",
            "entertainment" => "#FF9F0A",
            "productivity" => "#BF5AF2",
            "system" => "#8E8E93",
            _ => "#636366"
        };

        public static string Categorize(string? pathOrExe, string? titleOrName = null)
        {
            if (string.IsNullOrEmpty(pathOrExe)) return Other;
            string exe = Path.GetFileName(pathOrExe).ToLowerInvariant();
            if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) exe += ".exe";

            // 1. Browsing
            if (exe is "msedge.exe" or "chrome.exe" or "firefox.exe" or "brave.exe" or "opera.exe" or "vivaldi.exe" or "arc.exe" or "zen.exe" or "waterfox.exe" or "tor.exe" or "iexplore.exe")
            {
                if (!string.IsNullOrEmpty(titleOrName))
                {
                    string t = titleOrName.ToLowerInvariant();
                    if (t.Contains("youtube") || t.Contains("netflix") || t.Contains("twitch") || t.Contains("bilibili") || t.Contains("disney"))
                        return Entertainment;
                    if (t.Contains("github") || t.Contains("gitlab") || t.Contains("stack overflow") || t.Contains("localhost") || t.Contains("chatgpt") || t.Contains("gemini") || t.Contains("claude"))
                        return Development;
                }
                return Browsing;
            }

            // 2. Development
            if (exe is "devenv.exe" or "code.exe" or "antigravity.exe" or "cursor.exe" or "idea64.exe" or "pycharm64.exe" or "rider64.exe" or "webstorm64.exe" or "clion64.exe" or "sublime_text.exe" or "notepad++.exe" or "windowsterminal.exe" or "cmd.exe" or "powershell.exe" or "pwsh.exe" or "git-bash.exe" or "wt.exe" or "bash.exe" or "mintty.exe")
            {
                return Development;
            }

            // 3. Entertainment
            if (exe is "spotify.exe" or "vlc.exe" or "wmplayer.exe" or "netflix.exe" or "steam.exe" or "epicgameslauncher.exe" or "mpv.exe" or "kmplayer.exe" or "potplayer64.exe" or "foobar2000.exe" or "musicbee.exe" or "obs64.exe" or "valorant.exe" or "genshinimpact.exe")
            {
                return Entertainment;
            }

            // 4. Productivity
            if (exe is "winword.exe" or "excel.exe" or "powerpnt.exe" or "notion.exe" or "obsidian.exe" or "onenote.exe" or "acrobat.exe" or "acrodr32.exe" or "pdf24.exe" or "slack.exe" or "teams.exe" or "telegram.exe" or "whatsapp.exe" or "discord.exe")
            {
                return Productivity;
            }

            // 5. System
            if (exe is "explorer.exe" or "taskmgr.exe" or "systemsettings.exe" or "searchhost.exe" or "shellexperiencehost.exe" or "applicationframehost.exe" or "pickerhost.exe" or "snippingtool.exe" or "notchwin.exe" or "poni.exe" or "lockapp.exe")
            {
                return System;
            }

            return Other;
        }
    }

    public class AppUsageItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("secs")]
        public long Seconds { get; set; }

        [JsonIgnore]
        public string Category { get; set; } = ScreenTimeCategories.Other;
    }

    public class DayScreenTimeData
    {
        [JsonPropertyName("apps")]
        public Dictionary<string, AppUsageItem> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("cats")]
        public Dictionary<string, long> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("hours")]
        public long[] Hours { get; set; } = new long[24];

        [JsonPropertyName("switches")]
        public int Switches { get; set; }
    }

    public class ScreenTimeStore
    {
        [JsonPropertyName("days")]
        public Dictionary<string, DayScreenTimeData> Days { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
