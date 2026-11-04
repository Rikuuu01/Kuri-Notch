using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class ShortcutService : IShortcutService
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
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        private static readonly string ShortcutsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotchWin",
            "shortcuts.json"
        );

        public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

        public bool HasShortcuts => Shortcuts.Count > 0;

        public ShortcutService()
        {
            LoadShortcuts();
        }

        public void LoadShortcuts()
        {
            Shortcuts.Clear();

            try
            {
                if (File.Exists(ShortcutsFilePath))
                {
                    string json = File.ReadAllText(ShortcutsFilePath);
                    var loaded = JsonSerializer.Deserialize<List<ShortcutItem>>(json);
                    if (loaded != null)
                    {
                        foreach (var item in loaded)
                        {
                            if (!string.IsNullOrWhiteSpace(item.TargetPath))
                            {
                                if (Directory.Exists(item.TargetPath))
                                {
                                    item.ItemType = ShortcutItemType.Folder;
                                    if (string.IsNullOrWhiteSpace(item.IconPathData))
                                    {
                                        item.IconPathData = "M 2,4.5 C 2,3.7 2.7,3 3.5,3 H 6.5 C 7.1,3 7.6,3.3 7.9,3.8 L 8.8,5 H 14.5 C 15.3,5 16,5.7 16,6.5 V 13.5 C 16,14.3 15.3,15 14.5,15 H 3.5 C 2.7,15 2,14.3 2,13.5 Z";
                                        item.IconColorHex = "#F59E0B";
                                    }
                                }
                                else if (File.Exists(item.TargetPath))
                                {
                                    string ext = Path.GetExtension(item.TargetPath).ToLowerInvariant();
                                    if (ext == ".lnk" || ext == ".url" || ext == ".exe" || ext == ".bat" || ext == ".cmd")
                                    {
                                        item.ItemType = ShortcutItemType.Shortcut;
                                        if (string.IsNullOrWhiteSpace(item.IconPathData))
                                        {
                                            item.IconPathData = "M 2,10 L 8,4 M 3,4 H 8 V 9";
                                            item.IconColorHex = "#38BDF8";
                                        }
                                    }
                                    else
                                    {
                                        item.ItemType = ShortcutItemType.File;
                                        if (string.IsNullOrWhiteSpace(item.IconPathData))
                                        {
                                            item.IconPathData = "M 3,1.5 H 9.5 L 13.5,5.5 V 14.5 C 13.5,15.1 13.1,15.5 12.5,15.5 H 3 C 2.4,15.5 2,15.1 2,14.5 V 2.5 C 2,1.9 2.4,1.5 3,1.5 Z M 9,1.5 V 6 H 13.5";
                                            item.IconColorHex = "#94A3B8";
                                        }
                                    }
                                }

                                item.IconSource = ExtractIcon(item.TargetPath);
                            }
                            Shortcuts.Add(item);
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load shortcuts from {Path}", ShortcutsFilePath);
            }

            // Defaults: Start clean and empty per user preference (matching Screenshot 1)
            SaveShortcuts();
        }

        public void SaveShortcuts()
        {
            try
            {
                string? dir = Path.GetDirectoryName(ShortcutsFilePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Shortcuts.ToList(), options);
                File.WriteAllText(ShortcutsFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save shortcuts to {Path}", ShortcutsFilePath);
            }
        }

        public void AddShortcut(string targetPath, string? customName = null)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return;

            try
            {
                bool isDir = Directory.Exists(targetPath);
                bool isFile = File.Exists(targetPath);

                if (!isDir && !isFile) return;

                // Avoid duplicate if same target path exists
                var existing = Shortcuts.FirstOrDefault(s => string.Equals(s.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    Shortcuts.Remove(existing);
                }

                string name = customName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (isDir)
                    {
                        name = new DirectoryInfo(targetPath).Name;
                    }
                    else
                    {
                        name = Path.GetFileNameWithoutExtension(targetPath);
                    }
                }

                ShortcutItemType itemType;
                string typeLabel;
                string fallbackPathData;
                string iconColorHex;

                if (isDir)
                {
                    itemType = ShortcutItemType.Folder;
                    typeLabel = "Folder";
                    fallbackPathData = "M 2,4.5 C 2,3.7 2.7,3 3.5,3 H 6.5 C 7.1,3 7.6,3.3 7.9,3.8 L 8.8,5 H 14.5 C 15.3,5 16,5.7 16,6.5 V 13.5 C 16,14.3 15.3,15 14.5,15 H 3.5 C 2.7,15 2,14.3 2,13.5 Z";
                    iconColorHex = "#F59E0B";
                }
                else
                {
                    string ext = Path.GetExtension(targetPath).ToLowerInvariant();
                    if (ext == ".lnk" || ext == ".url" || ext == ".exe" || ext == ".bat" || ext == ".cmd")
                    {
                        itemType = ShortcutItemType.Shortcut;
                        typeLabel = "Pintasan";
                        fallbackPathData = "M 2,10 L 8,4 M 3,4 H 8 V 9";
                        iconColorHex = "#38BDF8";
                    }
                    else
                    {
                        itemType = ShortcutItemType.File;
                        typeLabel = "File";
                        fallbackPathData = "M 3,1.5 H 9.5 L 13.5,5.5 V 14.5 C 13.5,15.1 13.1,15.5 12.5,15.5 H 3 C 2.4,15.5 2,15.1 2,14.5 V 2.5 C 2,1.9 2.4,1.5 3,1.5 Z M 9,1.5 V 6 H 13.5";
                        iconColorHex = "#94A3B8";
                    }
                }

                ImageSource? iconSource = ExtractIcon(targetPath);

                var item = new ShortcutItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    ToolTip = $"{name} ({typeLabel})\n{targetPath}",
                    TargetPath = targetPath,
                    ItemType = itemType,
                    IconSource = iconSource,
                    IconPathData = fallbackPathData,
                    IconColorHex = iconColorHex,
                    IsCustom = true
                };

                Shortcuts.Add(item);
                SaveShortcuts();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add shortcut for {Path}", targetPath);
            }
        }

        public void RemoveShortcut(ShortcutItem item)
        {
            if (item == null) return;

            var existing = Shortcuts.FirstOrDefault(s => s.Id == item.Id || string.Equals(s.TargetPath, item.TargetPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Shortcuts.Remove(existing);
                SaveShortcuts();
            }
        }

        public void ClearAll()
        {
            Shortcuts.Clear();
            SaveShortcuts();
        }

        public void ResetToDefaults()
        {
            Shortcuts.Clear();
            InitializeDefaultShortcuts();
            SaveShortcuts();
        }

        private void InitializeDefaultShortcuts()
        {
            Shortcuts.Add(new ShortcutItem
            {
                Id = "explorer",
                Name = "Explorer",
                ToolTip = "Buka File Explorer (Pintasan)\nexplorer.exe",
                IconColorHex = "#38BDF8",
                IconPathData = "M 1.5,3 C 1.5,2.4 2,2 2.5,2 H 5.5 L 7,3.5 H 12.5 C 13,3.5 13.5,4 13.5,4.5 V 11.5 C 13.5,12 13,12.5 12.5,12.5 H 2.5 C 2,12.5 1.5,12 1.5,11.5 Z",
                TargetPath = "explorer.exe",
                ItemType = ShortcutItemType.Shortcut,
                IsCustom = false
            });

            Shortcuts.Add(new ShortcutItem
            {
                Id = "terminal",
                Name = "Terminal",
                ToolTip = "Buka Terminal / PowerShell (Pintasan)\npowershell.exe",
                IconColorHex = "#A78BFA",
                IconPathData = "M 2,3 L 6,7 L 2,11 M 7,11 H 13",
                TargetPath = "powershell.exe",
                ItemType = ShortcutItemType.Shortcut,
                IsCustom = false
            });

            Shortcuts.Add(new ShortcutItem
            {
                Id = "browser",
                Name = "Browser",
                ToolTip = "Buka Peramban Web (Pintasan)\nhttps://www.google.com",
                IconColorHex = "#60A5FA",
                IconPathData = "M 7,1 A 6,6 0 1,1 7,13 A 6,6 0 0,1 7,1 Z M 1,7 H 13 M 7,1 C 8.5,3 9.5,5 9.5,7 C 9.5,9 8.5,11 7,13 C 5.5,11 4.5,9 4.5,7 C 4.5,5 5.5,3 7,1 Z",
                TargetPath = "https://www.google.com",
                ItemType = ShortcutItemType.Shortcut,
                IsCustom = false
            });

            Shortcuts.Add(new ShortcutItem
            {
                Id = "notepad",
                Name = "Catatan",
                ToolTip = "Buka Notepad (Pintasan)\nnotepad.exe",
                IconColorHex = "#FBBF24",
                IconPathData = "M 2,1.5 H 9.5 L 12.5,4.5 V 12.5 C 12.5,13 12,13.5 11.5,13.5 H 2 C 1.5,13.5 1,13 1,12.5 V 2.5 C 1,2 1.5,1.5 2,1.5 Z M 9,1.5 V 5 H 12.5 M 3,7 H 9 M 3,9.5 H 9",
                TargetPath = "notepad.exe",
                ItemType = ShortcutItemType.Shortcut,
                IsCustom = false
            });
        }

        public void Launch(ShortcutItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.TargetPath)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.TargetPath,
                    Arguments = item.Arguments,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to launch shortcut: {Name} ({Target})", item.Name, item.TargetPath);
            }
        }

        private string? ResolveShortcutTarget(string lnkPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic? shortcut = shell.CreateShortcut(lnkPath);
                        if (shortcut != null)
                        {
                            string iconLoc = shortcut.IconLocation;
                            if (!string.IsNullOrWhiteSpace(iconLoc))
                            {
                                string cleanLoc = iconLoc.Split(',')[0].Trim();
                                if (File.Exists(cleanLoc)) return cleanLoc;
                            }

                            string target = shortcut.TargetPath;
                            if (!string.IsNullOrWhiteSpace(target) && (File.Exists(target) || Directory.Exists(target)))
                            {
                                return target;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to resolve shortcut target for {Path}", lnkPath);
            }
            return null;
        }

        private ImageSource? ExtractIcon(string path)
        {
            try
            {
                string targetToExtract = path;
                if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string? resolved = ResolveShortcutTarget(path);
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        targetToExtract = resolved;
                    }
                }

                if (Directory.Exists(targetToExtract))
                {
                    var shinfo = new SHFILEINFO();
                    IntPtr res = SHGetFileInfo(targetToExtract, FILE_ATTRIBUTE_DIRECTORY, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                    if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                    {
                        var bs = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        DestroyIcon(shinfo.hIcon);
                        return bs;
                    }
                }
                else if (File.Exists(targetToExtract))
                {
                    using var icon = Icon.ExtractAssociatedIcon(targetToExtract);
                    if (icon != null)
                    {
                        var bs = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        return bs;
                    }
                }
            }
            catch {}
            return null;
        }
    }
}
