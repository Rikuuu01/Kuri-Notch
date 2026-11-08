using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using NotchWin.Interop;
using NotchWin.Models;
using QRCoder;
using Serilog;

namespace NotchWin.Services
{
    public class PhoneShareToastEventArgs : EventArgs
    {
        public string Icon { get; set; } = "📱";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public partial class PhoneShareService : ObservableObject, IDisposable
    {
        private const long MaxFileBytes = 2L * 1024 * 1024 * 1024;
        private const int MaxTextBytes = 64 * 1024;
        private const int MaxHeaderBytes = 16 * 1024;
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);

        private readonly IShelfService _shelfService;
        private readonly ISettingsService _settingsService;

        private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
        private readonly ConcurrentDictionary<string, (DateTime At, string SavedName)> _recentUploads = new();
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private string _token = string.Empty;
        private int _port;
        private long _lastActivityUtcTicks;
        private bool _sawClient;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private ImageSource? _qrcode;

        [ObservableProperty]
        private bool _isPanelOpen;

        public bool HasQr => Qrcode != null;

        public LocalizationService L10n => LocalizationService.Instance;

        public event EventHandler<PhoneShareToastEventArgs>? ToastRequested;

        public PhoneShareService(IShelfService shelfService, ISettingsService settingsService)
        {
            _shelfService = shelfService;
            _settingsService = settingsService;
            StatusText = LocalizationService.Instance.Get("Phone.StatusStopped");
        }

        partial void OnQrcodeChanged(ImageSource? value)
        {
            OnPropertyChanged(nameof(HasQr));
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                var listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();

                _listener = listener;
                _port = ((IPEndPoint)listener.LocalEndpoint).Port;
                _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                _sawClient = false;
                Interlocked.Exchange(ref _lastActivityUtcTicks, DateTime.UtcNow.Ticks);

                var cts = new CancellationTokenSource();
                _cts = cts;

                string url = $"http://{GetLanAddress()}:{_port}/t/{_token}/";

                ImageSource? qr = null;
                try
                {
                    qr = CreateQrImage(url);
                }
                catch (Exception qrEx)
                {
                    Log.Warning(qrEx, "Failed to generate Phone Share QR code");
                }

                IsRunning = true;
                Url = url;
                Qrcode = qr;
                StatusText = LocalizationService.Instance.Get("Phone.StatusRunning");

                _ = Task.Run(() => AcceptLoopAsync(listener, cts.Token));
                _ = Task.Run(() => IdleWatchdogAsync(cts.Token));

                Log.Information("Phone Share started at {Url}", url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start Phone Share server");
                StatusText = LocalizationService.Instance.Get("Phone.Error");
                Stop();
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);

            try { cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;

            foreach (var pair in _clients)
            {
                try { pair.Value.Close(); } catch { }
            }
            _clients.Clear();

            try { cts?.Dispose(); } catch { }

            OnUi(() =>
            {
                IsRunning = false;
                IsPanelOpen = false;
                Url = string.Empty;
                Qrcode = null;
                StatusText = LocalizationService.Instance.Get("Phone.StatusStopped");
            });
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(token);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException)
                {
                    if (token.IsCancellationRequested) break;
                    continue;
                }

                int id = client.GetHashCode();
                _clients[id] = client;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClientAsync(client, token);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Phone Share request failed");
                    }
                    finally
                    {
                        _clients.TryRemove(id, out _);
                        try { client.Close(); } catch { }
                    }
                });
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                client.ReceiveTimeout = 15000;
                client.SendTimeout = 15000;

                NetworkStream stream = client.GetStream();
                var parsed = await ReadHeadersAsync(stream, token);
                if (parsed == null)
                {
                    await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8", "Bad Request");
                    return;
                }

                string method = parsed.Value.Method;
                string target = parsed.Value.Target;
                Dictionary<string, string> headers = parsed.Value.Headers;

                Interlocked.Exchange(ref _lastActivityUtcTicks, DateTime.UtcNow.Ticks);

                string pathOnly = target;
                string query = string.Empty;
                int queryIndex = target.IndexOf('?');
                if (queryIndex >= 0)
                {
                    pathOnly = target[..queryIndex];
                    query = target[(queryIndex + 1)..];
                }

                string prefix = $"/t/{_token}/";
                if (!pathOnly.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteTextAsync(stream, 404, "Not Found", "text/plain; charset=utf-8", "Not Found");
                    return;
                }

                string route = pathOnly[prefix.Length..].Trim('/');

                if (method == "GET" && route.Length == 0)
                {
                    MarkConnected();
                    await WriteTextAsync(stream, 200, "OK", "text/html; charset=utf-8", RenderPage());
                    return;
                }

                if (method == "GET" && route.Equals("list", StringComparison.OrdinalIgnoreCase))
                {
                    await ServeListAsync(stream);
                    return;
                }

                if (method == "GET" && route.StartsWith("file/", StringComparison.OrdinalIgnoreCase))
                {
                    await ServeFileAsync(stream, route[5..]);
                    return;
                }

                if (method == "PUT" && route.Equals("upload", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleUploadAsync(stream, headers, query);
                    return;
                }

                if (method == "POST" && route.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleTextAsync(stream, headers);
                    return;
                }

                await WriteTextAsync(stream, 404, "Not Found", "text/plain; charset=utf-8", "Not Found");
            }
        }

        private void MarkConnected()
        {
            if (_sawClient) return;
            _sawClient = true;
            OnUi(() => StatusText = LocalizationService.Instance.Get("Phone.StatusConnected"));
        }

        private async Task ServeListAsync(NetworkStream stream)
        {
            var files = new List<object>();

            OnUi(() =>
            {
                foreach (ShelfItem item in _shelfService.Items)
                {
                    if (item.IsDirectory || item.IsMissing || string.IsNullOrWhiteSpace(item.FilePath)) continue;
                    if (!File.Exists(item.FilePath)) continue;

                    files.Add(new { id = item.Id, name = item.FileName, size = item.FileSizeBytes });
                }
            });

            string json = JsonSerializer.Serialize(new { files });
            await WriteTextAsync(stream, 200, "OK", "application/json; charset=utf-8", json);
        }

        private async Task ServeFileAsync(NetworkStream stream, string idText)
        {
            if (!Guid.TryParse(idText, out Guid id))
            {
                await WriteTextAsync(stream, 404, "Not Found", "text/plain; charset=utf-8", "Not Found");
                return;
            }

            ShelfItem? item = null;
            OnUi(() => item = _shelfService.Items.FirstOrDefault(i => i.Id == id));

            if (item == null || item.IsMissing || !File.Exists(item.FilePath))
            {
                await WriteTextAsync(stream, 404, "Not Found", "text/plain; charset=utf-8", "Not Found");
                return;
            }

            try
            {
                var info = new FileInfo(item.FilePath);
                string safeName = info.Name.Replace("\"", string.Empty);
                string encodedName = Uri.EscapeDataString(info.Name);

                string header =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/octet-stream\r\n" +
                    $"Content-Length: {info.Length}\r\n" +
                    $"Content-Disposition: attachment; filename=\"{safeName}\"; filename*=UTF-8''{encodedName}\r\n" +
                    "Cache-Control: no-store\r\n" +
                    "Connection: close\r\n\r\n";

                await stream.WriteAsync(Encoding.UTF8.GetBytes(header));

                await using var file = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                await file.CopyToAsync(stream);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to serve shelf file for download: {Path}", item.FilePath);
            }
        }

        private async Task HandleUploadAsync(NetworkStream stream, Dictionary<string, string> headers, string query)
        {
            if (headers.ContainsKey("Transfer-Encoding"))
            {
                await WriteTextAsync(stream, 411, "Length Required", "text/plain; charset=utf-8", "Content-Length required");
                return;
            }

            if (!TryGetContentLength(headers, out long length))
            {
                await WriteTextAsync(stream, 411, "Length Required", "text/plain; charset=utf-8", "Content-Length required");
                return;
            }

            if (length > MaxFileBytes)
            {
                await WriteTextAsync(stream, 413, "Payload Too Large", "text/plain; charset=utf-8", "File is too large");
                return;
            }

            string? rawName = GetQueryValue(query, "name");
            string fileName = SanitizeFileName(rawName);

            // Browser/OS kadang mengirim ulang PUT yang sama (retry). Jangan buat salinan kedua.
            string dedupeKey = $"{fileName}|{length}";
            if (_recentUploads.TryGetValue(dedupeKey, out var recent) &&
                DateTime.UtcNow - recent.At < TimeSpan.FromSeconds(10))
            {
                await WriteContinueIfNeededAsync(stream, headers);
                await DrainAsync(stream, length);
                Log.Information("Phone Share upload deduplicated: {Name} ({Bytes} bytes)", fileName, length);
                await WriteTextAsync(stream, 200, "OK", "application/json; charset=utf-8",
                    JsonSerializer.Serialize(new { ok = true, name = recent.SavedName, duplicate = true }));
                return;
            }

            string folder;
            try
            {
                folder = ResolvePhoneFolder();
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Phone Share upload folder is unavailable");
                await WriteTextAsync(stream, 507, "Insufficient Storage", "text/plain; charset=utf-8", "Upload folder unavailable");
                return;
            }

            await WriteContinueIfNeededAsync(stream, headers);

            string targetPath = GetUniquePath(folder, fileName);
            long total = 0;

            try
            {
                await using (var file = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    while (total < length)
                    {
                        int toRead = (int)Math.Min(buffer.Length, length - total);
                        int read = await stream.ReadAsync(buffer.AsMemory(0, toRead));
                        if (read == 0) break;

                        await file.WriteAsync(buffer.AsMemory(0, read));
                        total += read;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Phone Share failed to write upload: {Path}", targetPath);
                TryDelete(targetPath);
                await WriteTextAsync(stream, 507, "Insufficient Storage", "text/plain; charset=utf-8", "Could not save file");
                return;
            }

            if (total < length)
            {
                TryDelete(targetPath);
                await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8", "Incomplete upload");
                return;
            }

            string savedName = Path.GetFileName(targetPath);
            _recentUploads[dedupeKey] = (DateTime.UtcNow, savedName);
            PruneRecentUploads();
            OnUi(() => _shelfService.AddFiles(new[] { targetPath }));
            RaiseToast("📥", LocalizationService.Instance.Get("Phone.UploadNotificationTitle"), savedName);
            Log.Information("Phone Share upload saved: {Name} ({Bytes} bytes) -> {Path}", savedName, total, targetPath);

            await WriteTextAsync(stream, 200, "OK", "application/json; charset=utf-8",
                JsonSerializer.Serialize(new { ok = true, name = savedName }));
        }

        private async Task HandleTextAsync(NetworkStream stream, Dictionary<string, string> headers)
        {
            if (headers.ContainsKey("Transfer-Encoding"))
            {
                await WriteTextAsync(stream, 411, "Length Required", "text/plain; charset=utf-8", "Content-Length required");
                return;
            }

            if (!TryGetContentLength(headers, out long length))
            {
                await WriteTextAsync(stream, 411, "Length Required", "text/plain; charset=utf-8", "Content-Length required");
                return;
            }

            if (length > MaxTextBytes)
            {
                await WriteTextAsync(stream, 413, "Payload Too Large", "text/plain; charset=utf-8", "Text is too large");
                return;
            }

            await WriteContinueIfNeededAsync(stream, headers);

            var buffer = new byte[length];
            int total = 0;

            while (total < length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(total, (int)length - total));
                if (read == 0) break;
                total += read;
            }

            if (total < length)
            {
                await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8", "Incomplete body");
                return;
            }

            string text = Encoding.UTF8.GetString(buffer);
            if (string.IsNullOrWhiteSpace(text))
            {
                await WriteTextAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8", "Empty text");
                return;
            }

            bool isLink = Regex.IsMatch(text.Trim(), @"^https?://", RegexOptions.IgnoreCase);

            OnUi(() =>
            {
                try { Clipboard.SetText(text); }
                catch (Exception ex) { Log.Warning(ex, "Phone Share failed to set clipboard"); }
            });

            string preview = text.Trim();
            if (preview.Length > 60) preview = preview[..57] + "...";

            RaiseToast(
                isLink ? "🔗" : "📋",
                LocalizationService.Instance.Get(isLink ? "Phone.LinkNotificationTitle" : "Phone.TextNotificationTitle"),
                preview);

            await WriteTextAsync(stream, 200, "OK", "application/json; charset=utf-8",
                JsonSerializer.Serialize(new { ok = true, link = isLink }));
        }

        private static async Task<(string Method, string Target, Dictionary<string, string> Headers)?> ReadHeadersAsync(
            NetworkStream stream, CancellationToken token)
        {
            var buffer = new List<byte>(1024);
            var single = new byte[1];
            int total = 0;

            while (true)
            {
                int read = await stream.ReadAsync(single.AsMemory(0, 1), token);
                if (read == 0) return null;

                buffer.Add(single[0]);
                total++;

                if (total > MaxHeaderBytes) return null;

                int count = buffer.Count;
                if (count >= 4 &&
                    buffer[count - 4] == 13 && buffer[count - 3] == 10 &&
                    buffer[count - 2] == 13 && buffer[count - 1] == 10)
                {
                    break;
                }
            }

            string headerText = Encoding.ASCII.GetString(buffer.ToArray());
            string[] lines = headerText.Split("\r\n");
            if (lines.Length == 0) return null;

            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2) return null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;

                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }

            return (requestLine[0].ToUpperInvariant(), requestLine[1], headers);
        }

        private string ResolvePhoneFolder()
        {
            string configured = _settingsService.CurrentSettings.PhoneFolder;
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            return Path.Combine(NativeMethods.GetKnownDownloadsFolder(), "Kuri Shelf");
        }

        private static string SanitizeFileName(string? rawName)
        {
            string name = string.IsNullOrWhiteSpace(rawName) ? "file" : rawName!;
            name = name.Replace('/', '\\');
            name = Path.GetFileName(name);

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            name = name.Trim().TrimEnd('.', ' ');
            if (string.IsNullOrEmpty(name)) name = "file";

            string bare = Path.GetFileNameWithoutExtension(name);
            string[] reserved =
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            if (reserved.Any(r => bare.Equals(r, StringComparison.OrdinalIgnoreCase)))
            {
                name = "_" + name;
            }

            return name;
        }

        private static string GetUniquePath(string folder, string fileName)
        {
            string candidate = Path.Combine(folder, fileName);
            if (!File.Exists(candidate)) return candidate;

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            for (int index = 1; index < 10000; index++)
            {
                candidate = Path.Combine(folder, $"{baseName} ({index}){extension}");
                if (!File.Exists(candidate)) return candidate;
            }

            return Path.Combine(folder, $"{baseName} ({Guid.NewGuid():N}){extension}");
        }

        private static async Task DrainAsync(NetworkStream stream, long length)
        {
            var buffer = new byte[81920];
            long remaining = length;
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)));
                if (read == 0) break;
                remaining -= read;
            }
        }

        private void PruneRecentUploads()
        {
            DateTime cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(60);
            foreach (var pair in _recentUploads)
            {
                if (pair.Value.At < cutoff)
                {
                    _recentUploads.TryRemove(pair.Key, out _);
                }
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static bool TryGetContentLength(Dictionary<string, string> headers, out long length)
        {
            length = 0;
            return headers.TryGetValue("Content-Length", out string? raw)
                && long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out length)
                && length >= 0;
        }

        private static bool HasExpectContinue(Dictionary<string, string> headers)
        {
            return headers.TryGetValue("Expect", out string? expect)
                && expect.Contains("100-continue", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task WriteContinueIfNeededAsync(NetworkStream stream, Dictionary<string, string> headers)
        {
            if (!HasExpectContinue(headers)) return;
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"));
        }

        private static string? GetQueryValue(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return null;

            foreach (string pair in query.Split('&'))
            {
                int equals = pair.IndexOf('=');
                string rawKey = equals >= 0 ? pair[..equals] : pair;
                if (!Uri.UnescapeDataString(rawKey.Replace("+", " ")).Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return equals >= 0
                    ? Uri.UnescapeDataString(pair[(equals + 1)..].Replace("+", " "))
                    : string.Empty;
            }

            return null;
        }

        private static async Task WriteTextAsync(NetworkStream stream, int code, string reason, string contentType, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            string header =
                $"HTTP/1.1 {code} {reason}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {bytes.Length}\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
            await stream.WriteAsync(bytes);
        }

        private async Task IdleWatchdogAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);

                    long lastTicks = Interlocked.Read(ref _lastActivityUtcTicks);
                    if (DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc) >= IdleTimeout)
                    {
                        Log.Information("Phone Share stopped after idle timeout");
                        Stop();
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private void RaiseToast(string icon, string title, string message)
        {
            OnUi(() => ToastRequested?.Invoke(this, new PhoneShareToastEventArgs
            {
                Icon = icon,
                Title = title,
                Message = message
            }));
        }

        private static void OnUi(Action action)
        {
            Application? app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;

            if (app.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                app.Dispatcher.Invoke(action);
            }
        }

        private static string GetLanAddress()
        {
            try
            {
                var candidates = new List<IPAddress>();

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(unicast.Address)) continue;
                        candidates.Add(unicast.Address);
                    }
                }

                IPAddress? selected = candidates.FirstOrDefault(IsPrivateAddress)
                    ?? candidates.FirstOrDefault()
                    ?? IPAddress.Loopback;

                return selected.ToString();
            }
            catch
            {
                return IPAddress.Loopback.ToString();
            }
        }

        private static bool IsPrivateAddress(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        private static ImageSource CreateQrImage(string url)
        {
            using var generator = new QRCodeGenerator();
            using QRCodeData data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);

            byte[] png = new PngByteQRCode(data).GetGraphic(8);

            var image = new BitmapImage();
            using var memory = new MemoryStream(png);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = memory;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static readonly Lazy<string> PhonePageHtml = new(LoadPhonePage);

        private static string LoadPhonePage()
        {
            try
            {
                Assembly assembly = typeof(PhoneShareService).Assembly;
                string[] names = assembly.GetManifestResourceNames();

                string? resource = names.FirstOrDefault(n => n.EndsWith("Assets.PhoneShare.index.html", StringComparison.OrdinalIgnoreCase))
                    ?? names.FirstOrDefault(n => n.EndsWith("PhoneShare.index.html", StringComparison.OrdinalIgnoreCase))
                    ?? names.FirstOrDefault(n => n.EndsWith("index.html", StringComparison.OrdinalIgnoreCase));

                if (resource == null)
                {
                    Log.Warning("Embedded Phone Share page not found");
                    return "<!DOCTYPE html><html><body style=\"background:#0b0b12;color:#e5e7eb;font-family:sans-serif\"><p>Kuri Phone Share</p></body></html>";
                }

                using Stream? stream = assembly.GetManifestResourceStream(resource);
                if (stream == null) return string.Empty;

                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load embedded Phone Share page");
                return string.Empty;
            }
        }

        private static string RenderPage()
        {
            var strings = new Dictionary<string, string>
            {
                ["title"] = L("PhonePage.Title"),
                ["subtitle"] = L("PhonePage.Subtitle"),
                ["uploadTitle"] = L("PhonePage.UploadTitle"),
                ["chooseFile"] = L("PhonePage.ChooseFile"),
                ["uploading"] = L("PhonePage.Uploading"),
                ["uploaded"] = L("PhonePage.Uploaded"),
                ["uploadFailed"] = L("PhonePage.UploadFailed"),
                ["tooLarge"] = L("PhonePage.TooLarge"),
                ["textTitle"] = L("PhonePage.TextTitle"),
                ["textPlaceholder"] = L("PhonePage.TextPlaceholder"),
                ["sendText"] = L("PhonePage.SendText"),
                ["sent"] = L("PhonePage.Sent"),
                ["filesTitle"] = L("PhonePage.FilesTitle"),
                ["noFiles"] = L("PhonePage.NoFiles"),
                ["download"] = L("PhonePage.Download"),
                ["connected"] = L("PhonePage.Connected"),
                ["offline"] = L("PhonePage.Offline"),
                ["emptyText"] = L("PhonePage.EmptyText"),
                ["textTooLong"] = L("PhonePage.TextTooLong"),
                ["maxFileSize"] = L("PhonePage.MaxFileSize"),
                ["maxTextSize"] = L("PhonePage.MaxTextSize")
            };

            string json = JsonSerializer.Serialize(strings)
                .Replace("<", "\\u003c")
                .Replace(">", "\\u003e");

            return PhonePageHtml.Value
                .Replace("__STRINGS__", json)
                .Replace("__MAX_FILE__", "2 GB")
                .Replace("__MAX_TEXT__", MaxTextBytes.ToString(CultureInfo.InvariantCulture));
        }

        private static string L(string key) => LocalizationService.Instance.Get(key);
    }
}
