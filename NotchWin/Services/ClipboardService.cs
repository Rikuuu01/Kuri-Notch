using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NotchWin.Interop;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class ClipboardService : IClipboardService
    {
        private const int MaxHistoryItems = 50;
        private readonly string _storagePath;
        private readonly List<ClipboardItem> _history = new();
        private IntPtr _hwnd = IntPtr.Zero;
        private string _lastCapturedText = string.Empty;
        private int _lastImageWidth = 0;
        private int _lastImageHeight = 0;
        private int _lastImageByteLength = 0;
        private DateTime _lastImageCaptureTime = DateTime.MinValue;
        private bool _isInternalCopy = false;
        private readonly DispatcherTimer _debounceTimer;

        public event EventHandler<ClipboardItem>? ItemAdded;
        public event EventHandler? HistoryChanged;

        public IReadOnlyList<ClipboardItem> History
        {
            get
            {
                lock (_history)
                {
                    return _history.ToList();
                }
            }
        }

        public ClipboardService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "NotchWin");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "clipboard_history.json");

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ExecuteClipboardRead();
            };

            LoadHistory();
        }

        public void Initialize(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero || _hwnd != IntPtr.Zero) return;
            _hwnd = windowHandle;

            try
            {
                NativeMethods.AddClipboardFormatListener(_hwnd);
                Log.Information("ClipboardService: Format listener registered successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register clipboard format listener");
            }
        }

        public void ProcessClipboardUpdate()
        {
            if (_isInternalCopy)
            {
                _isInternalCopy = false;
                return;
            }

            // Debounce Windows Snipping Tool and rapid multi-format clipboard events
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }));
        }

        private void ExecuteClipboardRead()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (string.IsNullOrWhiteSpace(text) || text == _lastCapturedText) return;
                    _lastCapturedText = text;

                    string preview = text.Trim();
                    if (preview.Length > 120) preview = preview.Substring(0, 117) + "...";

                    var item = new ClipboardItem
                    {
                        ContentType = "Text",
                        TextContent = text,
                        PreviewText = preview,
                        CharacterCount = text.Length,
                        CopiedAt = DateTime.Now
                    };

                    AddItem(item);
                }
                else if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img != null)
                    {
                        int w = (int)img.PixelWidth;
                        int h = (int)img.PixelHeight;

                        // Deduplicate duplicate screenshot triggers from Snipping Tool (e.g. Win+Shift+S)
                        if (w == _lastImageWidth && h == _lastImageHeight && (DateTime.Now - _lastImageCaptureTime).TotalSeconds < 3.0)
                        {
                            return;
                        }

                        byte[]? pngBytes = ConvertBitmapToPngBytes(img, maxDimension: 300);
                        if (pngBytes != null && pngBytes.Length > 0)
                        {
                            if (pngBytes.Length == _lastImageByteLength && (DateTime.Now - _lastImageCaptureTime).TotalSeconds < 3.0)
                            {
                                return;
                            }

                            _lastImageWidth = w;
                            _lastImageHeight = h;
                            _lastImageByteLength = pngBytes.Length;
                            _lastImageCaptureTime = DateTime.Now;

                            var item = new ClipboardItem
                            {
                                ContentType = "Image",
                                TextContent = "[Screenshot / Gambar]",
                                PreviewText = $"{w} \u00d7 {h} px",
                                ImageData = pngBytes,
                                ImageWidth = w,
                                ImageHeight = h,
                                CopiedAt = DateTime.Now
                            };

                            AddItem(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to read clipboard content");
            }
        }

        private void AddItem(ClipboardItem item)
        {
            lock (_history)
            {
                // Remove older identical text entry if exists
                if (item.IsText)
                {
                    _history.RemoveAll(x => x.IsText && x.TextContent == item.TextContent);
                }
                else if (item.IsImage)
                {
                    // Deduplicate recent identical images
                    _history.RemoveAll(x => x.IsImage && x.ImageWidth == item.ImageWidth && x.ImageHeight == item.ImageHeight && Math.Abs((x.CopiedAt - item.CopiedAt).TotalSeconds) < 4.0);
                }

                _history.Insert(0, item);

                // Trim to limit while preserving pinned items
                if (_history.Count > MaxHistoryItems)
                {
                    var unpinned = _history.Where(x => !x.IsPinned).ToList();
                    while (_history.Count > MaxHistoryItems && unpinned.Count > 0)
                    {
                        var toRemove = unpinned.Last();
                        _history.Remove(toRemove);
                        unpinned.Remove(toRemove);
                    }
                }
            }

            SaveHistory();
            ItemAdded?.Invoke(this, item);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task CopyToClipboardAsync(ClipboardItem item)
        {
            _isInternalCopy = true;
            _lastCapturedText = item.TextContent;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (item.IsImage && item.Thumbnail != null)
                    {
                        Clipboard.SetImage(item.Thumbnail);
                    }
                    else if (!string.IsNullOrEmpty(item.TextContent))
                    {
                        Clipboard.SetText(item.TextContent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to copy item to clipboard");
                }
            });
        }

        public void DeleteItem(string id)
        {
            lock (_history)
            {
                _history.RemoveAll(x => x.Id == id);
            }
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void TogglePin(string id)
        {
            lock (_history)
            {
                var item = _history.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    item.IsPinned = !item.IsPinned;
                }
            }
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearAll()
        {
            lock (_history)
            {
                _history.RemoveAll(x => !x.IsPinned);
            }
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearImages()
        {
            lock (_history)
            {
                _history.RemoveAll(x => !x.IsPinned && x.IsImage);
            }
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearTexts()
        {
            lock (_history)
            {
                _history.RemoveAll(x => !x.IsPinned && x.IsText);
            }
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private byte[]? ConvertBitmapToPngBytes(BitmapSource source, int maxDimension)
        {
            try
            {
                BitmapSource processed = source;
                if (source.PixelWidth > maxDimension || source.PixelHeight > maxDimension)
                {
                    double scale = Math.Min((double)maxDimension / source.PixelWidth, (double)maxDimension / source.PixelHeight);
                    processed = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scale, scale));
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(processed));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    var items = JsonSerializer.Deserialize<List<ClipboardItem>>(json);
                    if (items != null)
                    {
                        lock (_history)
                        {
                            _history.Clear();
                            _history.AddRange(items);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load clipboard history");
            }
        }

        private void SaveHistory()
        {
            try
            {
                List<ClipboardItem> copy;
                lock (_history)
                {
                    // To save disk, do not serialize huge unpinned image arrays beyond 15 items
                    copy = _history.Take(MaxHistoryItems).ToList();
                }

                string json = JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save clipboard history");
            }
        }
    }
}
