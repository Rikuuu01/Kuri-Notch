using System;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace NotchWin.Models
{
    public enum ShortcutItemType
    {
        Shortcut,
        Folder,
        File
    }

    public class ShortcutItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string ToolTip { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconPathData { get; set; } = string.Empty;
        public string IconColorHex { get; set; } = "#38BDF8";
        public bool IsCustom { get; set; } = true;
        public ShortcutItemType ItemType { get; set; } = ShortcutItemType.Shortcut;

        [JsonIgnore]
        public ImageSource? IconSource { get; set; }

        [JsonIgnore]
        public bool HasIconSource => IconSource != null;

        [JsonIgnore]
        public bool HasPathData => !string.IsNullOrWhiteSpace(IconPathData) && IconSource == null;

        [JsonIgnore]
        public bool IsShortcut => ItemType == ShortcutItemType.Shortcut;

        [JsonIgnore]
        public bool IsFolder => ItemType == ShortcutItemType.Folder;

        [JsonIgnore]
        public bool IsFile => ItemType == ShortcutItemType.File;

        [JsonIgnore]
        public string LaunchActionHeader => ItemType switch
        {
            ShortcutItemType.Folder => "Buka Folder",
            ShortcutItemType.File => "Buka File",
            _ => "Buka Pintasan"
        };
    }
}
