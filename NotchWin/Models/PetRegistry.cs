using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NotchWin.Models
{
    /// <summary>
    /// Metadata descriptor for a selectable notch companion character.
    /// </summary>
    public partial class PetCharacterInfo : ObservableObject
    {
        public PetStyle Style { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Mascots";
        public string AssetFile { get; set; } = string.Empty;
        public string PreviewImage { get; set; } = string.Empty;
        public bool IsNativeVector { get; set; } = true;
        public bool IsRecommended { get; set; }

        [ObservableProperty]
        private bool _isSelected;

        private ImageSource? _thumbnailSource;
        public ImageSource? ThumbnailSource
        {
            get
            {
                if (_thumbnailSource == null && !string.IsNullOrEmpty(PreviewImage))
                {
                    try
                    {
                        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", PreviewImage);
                        if (!File.Exists(localPath))
                        {
                            localPath = Path.Combine("Assets", "Characters", PreviewImage);
                        }
                        if (File.Exists(localPath))
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(Path.GetFullPath(localPath));
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.DecodePixelWidth = 96;
                            bmp.EndInit();
                            if (bmp.CanFreeze) bmp.Freeze();
                            _thumbnailSource = bmp;
                        }
                    }
                    catch { }
                }
                return _thumbnailSource;
            }
        }
    }

    /// <summary>
    /// Complete catalog registry of available character companions in DynamicNotchWindows.
    /// Features four pure-vector premium companions: Pastel Mochi, Astro Mascot, Egg Mascot, and Notch Eyes.
    /// </summary>
    public static class PetRegistry
    {
        public static readonly IReadOnlyList<PetCharacterInfo> AllCharacters = new List<PetCharacterInfo>
        {
            // 1. Pastel Mochi
            new PetCharacterInfo
            {
                Style = PetStyle.PastelMochi,
                Id = "PastelMochi",
                Name = "Pastel Mochi",
                Description = "Lavender-periwinkle gradient companion with eye clipping, happy arcs, and music headphones",
                Category = "Mascots",
                AssetFile = "pastel_mochi.png",
                PreviewImage = "pastel_mochi.png",
                IsNativeVector = true,
                IsRecommended = true
            },
            // 2. Astro Mascot
            new PetCharacterInfo
            {
                Style = PetStyle.AstroMascot,
                Id = "AstroMascot",
                Name = "Astro Mascot",
                Description = "White space-suit companion with glowing amber visor eyes, orange chest light, and music headphones",
                Category = "Mascots",
                AssetFile = "astro_mascot.png",
                PreviewImage = "astro_mascot.png",
                IsNativeVector = true,
                IsRecommended = true
            },
            // 3. Egg Mascot
            new PetCharacterInfo
            {
                Style = PetStyle.EggMascot,
                Id = "EggMascot",
                Name = "Egg Mascot",
                Description = "Minimalist white egg companion with a waving arm, rosy cheeks, and all notch reactions",
                Category = "Mascots",
                AssetFile = "egg_mascot.png",
                PreviewImage = "egg_mascot.png",
                IsNativeVector = true,
                IsRecommended = false
            },
            // 4. Notch Eyes
            new PetCharacterInfo
            {
                Style = PetStyle.NotchEyes,
                Id = "NotchEyes",
                Name = "Notch Eyes",
                Description = "Minimalist dual animated eyes with dynamic pill expansion, reactive status icons, and expressive emotions",
                Category = "Mascots",
                AssetFile = "notch_eyes.png",
                PreviewImage = "notch_eyes.png",
                IsNativeVector = true,
                IsRecommended = true
            }
        };

        public static PetCharacterInfo Get(PetStyle style)
        {
            foreach (var c in AllCharacters)
            {
                if (c.Style == style) return c;
            }
            return AllCharacters[0];
        }
    }
}
