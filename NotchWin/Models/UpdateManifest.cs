using System;
using System.Text.Json.Serialization;

namespace NotchWin.Models
{
    public class UpdateManifest
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("releaseDate")]
        public string ReleaseDate { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; } = string.Empty;

        [JsonPropertyName("installerUrl_x64")]
        public string InstallerUrl_x64 { get; set; } = string.Empty;

        [JsonPropertyName("installerUrl_x86")]
        public string InstallerUrl_x86 { get; set; } = string.Empty;

        [JsonPropertyName("portableZipUrl_x64")]
        public string PortableZipUrl_x64 { get; set; } = string.Empty;

        [JsonPropertyName("portableZipUrl_x86")]
        public string PortableZipUrl_x86 { get; set; } = string.Empty;

        [JsonPropertyName("sha256_x64")]
        public string Sha256_x64 { get; set; } = string.Empty;
    }

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public Version CurrentVersion { get; set; } = new(1, 2, 0);
        public Version LatestVersion { get; set; } = new(1, 2, 0);
        public string Title { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public bool IsInstaller { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
