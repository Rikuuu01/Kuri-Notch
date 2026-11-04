using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class ShelfService : IShelfService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        public ObservableCollection<ShelfItem> Items { get; } = new();

        private readonly object _lock = new();
        private readonly Dictionary<string, DateTime> _recentlyAdded = new(StringComparer.OrdinalIgnoreCase);

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                string trimmed = path.Trim().Trim('"', '\'');
                string full = Path.GetFullPath(trimmed);
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().TrimEnd('\\', '/');
            }
        }

        public static bool IsPathEqual(string? pathA, string? pathB)
        {
            if (string.IsNullOrWhiteSpace(pathA) || string.IsNullOrWhiteSpace(pathB)) return false;
            return string.Equals(NormalizePath(pathA), NormalizePath(pathB), StringComparison.OrdinalIgnoreCase);
        }

        public int AddFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null) return 0;

            // 1. Deduplikasi seluruh input dalam batch saat ini menggunakan path kanonikal
            var validPaths = new List<string>();
            var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawPath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(rawPath)) continue;
                string normalized = NormalizePath(rawPath);
                if (string.IsNullOrEmpty(normalized)) continue;

                if (seenInBatch.Add(normalized))
                {
                    validPaths.Add(normalized);
                }
            }

            if (validPaths.Count == 0) return 0;

            int addedCount = 0;
            var now = DateTime.Now;

            lock (_lock)
            {
                // Bersihkan entri debounce yang lebih lama dari 3 detik
                var expired = _recentlyAdded.Where(kvp => (now - kvp.Value).TotalMilliseconds > 3000).Select(kvp => kvp.Key).ToList();
                foreach (var exp in expired)
                {
                    _recentlyAdded.Remove(exp);
                }

                foreach (var path in validPaths)
                {
                    // Debounce ganda: Jika file yang sama persis telah diproses dalam 600ms terakhir, abaikan duplikatnya
                    if (_recentlyAdded.TryGetValue(path, out var lastAdded) && (now - lastAdded).TotalMilliseconds < 600)
                    {
                        continue;
                    }

                    try
                    {
                        bool isDir = Directory.Exists(path);
                        bool isFile = File.Exists(path);

                        if (!isDir && !isFile) continue;

                        // Hapus SEMUA entri yang sudah ada sebelumnya dengan path yang sama (pindah ke depan dan cegah duplikat)
                        var existingItems = Items.Where(i => IsPathEqual(i.FilePath, path)).ToList();
                        foreach (var existing in existingItems)
                        {
                            Items.Remove(existing);
                        }

                        string fileName = isDir ? new DirectoryInfo(path).Name : Path.GetFileName(path);
                        string ext = isDir ? string.Empty : Path.GetExtension(path).ToLowerInvariant();
                        long sizeBytes = 0;

                        if (isFile)
                        {
                            try
                            {
                                sizeBytes = new FileInfo(path).Length;
                            }
                            catch { }
                        }

                        bool isImage = isFile && (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".webp" || ext == ".gif");
                        ImageSource? thumbnail = GenerateThumbnail(path, isDir, isImage);

                        var item = new ShelfItem
                        {
                            FilePath = path,
                            FileName = fileName,
                            FileExtension = ext,
                            FileSizeBytes = sizeBytes,
                            FileSizeFormatted = isDir ? "Folder" : FormatFileSize(sizeBytes),
                            IsDirectory = isDir,
                            IsImage = isImage,
                            Thumbnail = thumbnail,
                            AddedAt = DateTime.Now
                        };

                        Items.Insert(0, item);
                        _recentlyAdded[path] = now;
                        addedCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to add file to Shelf: {Path}", path);
                    }
                }
            }

            return addedCount;
        }

        public void RemoveItem(ShelfItem item)
        {
            if (item == null) return;
            var toRemove = Items.Where(i => i.Id == item.Id || IsPathEqual(i.FilePath, item.FilePath)).ToList();
            foreach (var rem in toRemove)
            {
                Items.Remove(rem);
            }
        }

        public void ClearAll()
        {
            Items.Clear();
        }

        public void OpenItem(ShelfItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to open shelf item: {Path}", item.FilePath);
            }
        }

        public void ShowInExplorer(ShelfItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath)) return;

            try
            {
                if (File.Exists(item.FilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                }
                else if (Directory.Exists(item.FilePath))
                {
                    Process.Start("explorer.exe", $"\"{item.FilePath}\"");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to show shelf item in Explorer: {Path}", item.FilePath);
            }
        }

        public void CopyItem(ShelfItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath)) return;

            try
            {
                var list = new StringCollection { item.FilePath };
                Clipboard.SetFileDropList(list);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy shelf item to clipboard: {Path}", item.FilePath);
            }
        }

        public void CopyAllToClipboard()
        {
            if (Items.Count == 0) return;

            try
            {
                var list = new StringCollection();
                foreach (var item in Items)
                {
                    if (!string.IsNullOrWhiteSpace(item.FilePath))
                    {
                        list.Add(item.FilePath);
                    }
                }
                if (list.Count > 0)
                {
                    Clipboard.SetFileDropList(list);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy all shelf items to clipboard");
            }
        }

        private ImageSource? GenerateThumbnail(string path, bool isDir, bool isImage)
        {
            if (isImage && File.Exists(path))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path);
                    bmp.DecodePixelWidth = 96;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch
                {
                    // Corrupt or unreadable image: fall back to its shell icon
                }
            }

            return ExtractShellIcon(path, isDir) ?? ExtractAssociatedFileIcon(path);
        }

        private static ImageSource? ExtractShellIcon(string path, bool isDir)
        {
            try
            {
                bool exists = isDir ? Directory.Exists(path) : File.Exists(path);
                uint flags = SHGFI_ICON | SHGFI_LARGEICON;
                if (!exists)
                {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                }

                var info = new SHFILEINFO();
                IntPtr result = SHGetFileInfo(
                    path,
                    isDir ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL,
                    ref info,
                    (uint)Marshal.SizeOf(info),
                    flags);

                if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var bs = Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bs.Freeze();
                    return bs;
                }
                finally
                {
                    DestroyIcon(info.hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource? ExtractAssociatedFileIcon(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon == null)
                {
                    return null;
                }

                var bs = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            catch
            {
                return null;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
