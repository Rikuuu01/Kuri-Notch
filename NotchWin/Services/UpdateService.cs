using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class UpdateService : IUpdateService
    {
        private const string ManifestUrl = "https://raw.githubusercontent.com/Rikuuu01/Kuri-Notch/main/update-manifest.json";
        private const string GitHubApiLatestUrl = "https://api.github.com/repos/Rikuuu01/Kuri-Notch/releases/latest";

        private readonly HttpClient _httpClient;
        private CancellationTokenSource? _autoCheckCts;
        private bool _isChecking;
        private bool _isDownloading;
        private double _downloadProgress;

        public Version CurrentVersion { get; }
        public string CurrentVersionString => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

        public bool IsInstalledViaSetup { get; }
        public bool IsChecking => _isChecking;
        public bool IsDownloading => _isDownloading;
        public double DownloadProgress => _downloadProgress;

        public event EventHandler<UpdateCheckResult>? UpdateAvailable;

        public UpdateService()
        {
            var asmVer = typeof(UpdateService).Assembly.GetName().Version;
            CurrentVersion = asmVer != null ? new Version(asmVer.Major, asmVer.Minor, Math.Max(0, asmVer.Build)) : new Version(1, 2, 0);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            IsInstalledViaSetup = File.Exists(Path.Combine(baseDir, "unins000.exe"));

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(25)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Kuri-Notch-Desktop");
        }

        public void StartAutoCheck()
        {
            _autoCheckCts?.Cancel();
            _autoCheckCts = new CancellationTokenSource();
            var token = _autoCheckCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Delay check on startup so the app launches instantly with zero network contention
                    await Task.Delay(15000, token);
                    if (token.IsCancellationRequested) return;

                    var result = await CheckForUpdateAsync(isManual: false);
                    if (result.IsUpdateAvailable)
                    {
                        Log.Information("UpdateService: New version detected: v{Version} (Current: v{Current})", result.LatestVersion, CurrentVersionString);
                        UpdateAvailable?.Invoke(this, result);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log.Debug(ex, "UpdateService: Auto-check failed");
                }
            }, token);
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(bool isManual = false)
        {
            _isChecking = true;
            var result = new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                IsInstaller = IsInstalledViaSetup
            };

            try
            {
                // 1. Try reading lightweight update-manifest.json first (no GitHub API rate limits)
                UpdateManifest? manifest = null;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var response = await _httpClient.GetAsync(ManifestUrl, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cts.Token);
                        manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "UpdateService: Manifest fetch failed, will try GitHub Releases API fallback");
                }

                if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Version))
                {
                    if (TryParseVersion(manifest.Version, out var remoteVer))
                    {
                        result.LatestVersion = remoteVer;
                        result.Title = manifest.Title;
                        result.Changelog = manifest.Changelog;
                        result.ReleaseDate = manifest.ReleaseDate;

                        bool is64 = Environment.Is64BitOperatingSystem;
                        if (IsInstalledViaSetup)
                        {
                            result.DownloadUrl = is64 ? manifest.InstallerUrl_x64 : manifest.InstallerUrl_x86;
                        }
                        else
                        {
                            result.DownloadUrl = is64 ? manifest.PortableZipUrl_x64 : manifest.PortableZipUrl_x86;
                        }

                        result.IsUpdateAvailable = remoteVer > CurrentVersion;
                        return result;
                    }
                }

                // 2. Fallback to GitHub Releases API
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var response = await _httpClient.GetAsync(GitHubApiLatestUrl, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cts.Token);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string tag = root.GetProperty("tag_name").GetString() ?? "";
                        if (TryParseVersion(tag, out var remoteVer))
                        {
                            result.LatestVersion = remoteVer;
                            result.Title = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            result.Changelog = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                            result.ReleaseDate = root.TryGetProperty("published_at", out var p) ? p.GetString() ?? "" : "";

                            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                            {
                                string targetExt = IsInstalledViaSetup ? ".exe" : ".zip";
                                foreach (var asset in assets.EnumerateArray())
                                {
                                    string assetName = asset.GetProperty("name").GetString() ?? "";
                                    if (assetName.EndsWith(targetExt, StringComparison.OrdinalIgnoreCase))
                                    {
                                        result.DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                                        break;
                                    }
                                }
                            }

                            result.IsUpdateAvailable = remoteVer > CurrentVersion;
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "UpdateService: GitHub Releases API query failed");
                    result.ErrorMessage = ex.Message;
                }
            }
            finally
            {
                _isChecking = false;
            }

            return result;
        }

        public async Task<bool> DownloadAndApplyUpdateAsync(UpdateCheckResult result, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(result.DownloadUrl))
            {
                Log.Warning("UpdateService: Download URL is empty");
                return false;
            }

            _isDownloading = true;
            _downloadProgress = 0;

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "KuriUpdates");
                Directory.CreateDirectory(tempDir);

                string fileName = Path.GetFileName(new Uri(result.DownloadUrl).LocalPath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = IsInstalledViaSetup ? "Kuri-Setup.exe" : "Kuri-Update.zip";
                }
                string targetFilePath = Path.Combine(tempDir, fileName);

                Log.Information("UpdateService: Starting download from {Url} to {Target}", result.DownloadUrl, targetFilePath);

                using (var response = await _httpClient.GetAsync(result.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                    await using var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, ct);
                        totalRead += read;

                        if (totalBytes > 0)
                        {
                            double percent = Math.Round(((double)totalRead / totalBytes) * 100.0, 1);
                            _downloadProgress = percent;
                            progress?.Report(percent);
                        }
                    }
                }

                _downloadProgress = 100.0;
                progress?.Report(100.0);
                Log.Information("UpdateService: Download completed successfully. Applying update...");

                // Give file stream a moment to flush
                await Task.Delay(400, ct);

                ApplyDownloadedUpdate(targetFilePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "UpdateService: Failed to download update");
                return false;
            }
            finally
            {
                _isDownloading = false;
            }
        }

        private void ApplyDownloadedUpdate(string downloadedFilePath)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                string exePath = Path.Combine(appDir, "NotchWin.exe");
                int currentPid = Environment.ProcessId;

                if (IsInstalledViaSetup && downloadedFilePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Inno Setup Installer: Silent install + close running app + restart app
                    Log.Information("UpdateService: Executing silent Inno Setup installer: {Path}", downloadedFilePath);
                    var psi = new ProcessStartInfo
                    {
                        FileName = downloadedFilePath,
                        Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                else
                {
                    // Portable Mode: Spawns background PowerShell helper to wait for exit, extract, and relaunch
                    Log.Information("UpdateService: Executing portable zip replacement helper...");
                    string psCommand = $"Wait-Process -Id {currentPid} -Timeout 20 -ErrorAction SilentlyContinue; " +
                                       $"Start-Sleep -Milliseconds 900; " +
                                       $"Expand-Archive -Path '{downloadedFilePath}' -DestinationPath '{appDir}' -Force; " +
                                       $"Start-Process '{exePath}'";

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                }

                // Shutdown current Kuri instance cleanly
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "UpdateService: Could not execute update installer/helper");
            }
        }

        private static bool TryParseVersion(string input, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(input)) return false;

            string cleaned = input.Trim().TrimStart('v', 'V');
            int dashIdx = cleaned.IndexOf('-');
            if (dashIdx > 0)
            {
                cleaned = cleaned.Substring(0, dashIdx);
            }

            return Version.TryParse(cleaned, out version!);
        }

        public void Dispose()
        {
            _autoCheckCts?.Cancel();
            _autoCheckCts?.Dispose();
            _autoCheckCts = null;
            _httpClient.Dispose();
        }
    }
}
