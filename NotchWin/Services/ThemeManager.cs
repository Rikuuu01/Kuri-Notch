using System;

using System.Windows;

using System.Windows.Media;

using Serilog;



namespace NotchWin.Services

{

    public class ThemeTransitionEventArgs : EventArgs

    {

        public Brush NewBackground { get; }

        public Brush NewStroke { get; }

        public Color Primary { get; }

        public Color Light { get; }

        public bool Animate { get; }



        public ThemeTransitionEventArgs(Brush bg, Brush stroke, Color primary, Color light, bool animate)

        {

            NewBackground = bg;

            NewStroke = stroke;

            Primary = primary;

            Light = light;

            Animate = animate;

        }

    }



    public static class ThemeManager

    {

        public static (Color Primary, Color Light) GetAccentColors(string colorName)

        {

            if (colorName == "Windows (Auto)" || colorName == "Auto" || colorName == "Windows")

            {

                try

                {

                    var winColor = SystemParameters.WindowGlassColor;

                    byte r = winColor.R, g = winColor.G, b = winColor.B;

                    // Ensure not pitch black

                    if (r < 30 && g < 30 && b < 30)

                    {

                        r = 37; g = 99; b = 235;

                    }

                    Color primary = Color.FromRgb(r, g, b);

                    Color light = Color.FromRgb(

                        (byte)Math.Min(255, r + 45),

                        (byte)Math.Min(255, g + 45),

                        (byte)Math.Min(255, b + 45));

                    return (primary, light);

                }

                catch

                {

                    return (Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x3B, 0x82, 0xF6));

                }

            }



            if (colorName.StartsWith("#"))
            {
                try
                {
                    var converted = ColorConverter.ConvertFromString(colorName);
                    if (converted is Color hexColor)
                    {
                        byte r = hexColor.R, g = hexColor.G, b = hexColor.B;
                        Color primary = Color.FromRgb(r, g, b);
                        Color light = Color.FromRgb(
                            (byte)Math.Min(255, r + 45),
                            (byte)Math.Min(255, g + 45),
                            (byte)Math.Min(255, b + 45));
                        return (primary, light);
                    }
                }
                catch { }
            }

            return colorName switch

            {

                "Purple" => (Color.FromRgb(0x7C, 0x3A, 0xED), Color.FromRgb(0xA8, 0x55, 0xF7)),

                "Emerald" => (Color.FromRgb(0x05, 0x96, 0x69), Color.FromRgb(0x10, 0xB9, 0x81)),

                "Amber" => (Color.FromRgb(0xD9, 0x77, 0x06), Color.FromRgb(0xF5, 0x9E, 0x0B)),

                "Rose" => (Color.FromRgb(0xE1, 0x1D, 0x48), Color.FromRgb(0xF4, 0x3F, 0x5E)),

                "Cyan" => (Color.FromRgb(0x08, 0x91, 0xB2), Color.FromRgb(0x06, 0xB6, 0xD4)),

                _ => (Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x3B, 0x82, 0xF6)) // Default Blue

            };

        }



        public static string ActiveThemeSetting { get; set; } = "Windows (Auto)";

        public static bool IsUsingDynamicAlbumAccent => _isUsingDynamicAlbumAccent;

        public static Color? CurrentAlbumAccent { get; private set; }

        public static bool IsMediaRunning { get; set; }
        public static string CurrentMaterial { get; set; } = "OLED";
        public static string CustomAccentColorHex { get; set; } = "#3B82F6";



        public static event EventHandler<ThemeTransitionEventArgs>? ThemeTransitionRequested;

        public static event EventHandler? DynamicAccentRefreshRequested;



        public static void RequestDynamicAccentRefresh()

        {

            DynamicAccentRefreshRequested?.Invoke(null, EventArgs.Empty);

        }



        private static bool _isUsingDynamicAlbumAccent;

        private static bool _hasInitialized = false;



        public static void ApplyAccentColor(string colorName, bool force = false)

        {

            ActiveThemeSetting = colorName;



            // If an album accent is actively playing / media is running with dynamic accent, don't overwrite it unless explicitly forced

            if ((_isUsingDynamicAlbumAccent || IsMediaRunning) && CurrentAlbumAccent.HasValue && !force)

            {

                return;

            }



            _isUsingDynamicAlbumAccent = false;

            CurrentAlbumAccent = null;



            var (primary, light) = GetAccentColors(colorName);

            ApplyColorsToApp(primary, light, animate: _hasInitialized);

            _hasInitialized = true;

        }



        public static void ApplyDynamicAccent(Color primary, bool animate = true)

        {

            // Avoid restarting animation if the new color is virtually identical to current

            if (_isUsingDynamicAlbumAccent && CurrentAlbumAccent.HasValue)

            {

                var cur = CurrentAlbumAccent.Value;

                int delta = Math.Abs(cur.R - primary.R) + Math.Abs(cur.G - primary.G) + Math.Abs(cur.B - primary.B);

                if (delta < 8)

                {

                    return;

                }

            }



            _isUsingDynamicAlbumAccent = true;

            CurrentAlbumAccent = primary;



            // Generate an elevated, vibrant light tint for controls and strokes

            byte rL = (byte)Math.Min(255, primary.R + 45);

            byte gL = (byte)Math.Min(255, primary.G + 45);

            byte bL = (byte)Math.Min(255, primary.B + 45);

            Color light = Color.FromRgb(rL, gL, bL);



            ApplyColorsToApp(primary, light, animate: animate && _hasInitialized);

            _hasInitialized = true;

        }



        public static void ApplyMaterial(string material)
        {
            CurrentMaterial = material;
            var (primary, light) = CurrentAlbumAccent.HasValue && _isUsingDynamicAlbumAccent
                ? (CurrentAlbumAccent.Value, Color.FromRgb((byte)Math.Min(255, CurrentAlbumAccent.Value.R + 45), (byte)Math.Min(255, CurrentAlbumAccent.Value.G + 45), (byte)Math.Min(255, CurrentAlbumAccent.Value.B + 45)))
                : GetAccentColors(ActiveThemeSetting);
            ApplyColorsToApp(primary, light, animate: _hasInitialized);
        }

        public static void RestoreDefaultAccent(bool animate = true)

        {

            if (_isUsingDynamicAlbumAccent || CurrentAlbumAccent.HasValue)

            {

                _isUsingDynamicAlbumAccent = false;

                CurrentAlbumAccent = null;

                var (primary, light) = GetAccentColors(ActiveThemeSetting);

                ApplyColorsToApp(primary, light, animate: animate && _hasInitialized);

            }

        }



        private static void ApplyColorsToApp(Color primary, Color light, bool animate)

        {

            var app = Application.Current;

            if (app == null) return;



            try

            {

                app.Dispatcher.Invoke(() =>

                {

                    var (bgBrush, strokeBrush) = CreateGradientBrushes(primary, light);

                    var playBrush = CreatePlayPauseBrush(primary, light);



                    var primaryBrush = new SolidColorBrush(primary);

                    primaryBrush.Freeze();

                    var lightBrush = new SolidColorBrush(light);

                    lightBrush.Freeze();

                    byte cardAlpha = CurrentMaterial switch
                    {
                        "Acrylic" => 135,
                        "TintedGlass" => 165,
                        _ => 220
                    };
                    var cardBrush = new SolidColorBrush(Color.FromArgb(cardAlpha, 14, 16, 22));

                    cardBrush.Freeze();



                    // Update Application-level resources for dynamic resource lookups

                    app.Resources["AccentPrimaryBrush"] = primaryBrush;

                    app.Resources["AccentLightBrush"] = lightBrush;

                    app.Resources["AccentGlowColor"] = light;

                    app.Resources["NotchStrokeBrush"] = strokeBrush;

                    app.Resources["NotchBackgroundBrush"] = bgBrush;

                    app.Resources["NotchCardBackgroundBrush"] = cardBrush;

                    app.Resources["PlayPauseGradientBrush"] = playBrush;



                    // Trigger the hardware-accelerated cross-fade transition event on NotchWindow

                    ThemeTransitionRequested?.Invoke(null, new ThemeTransitionEventArgs(bgBrush, strokeBrush, primary, light, animate));

                });

            }

            catch (Exception ex)

            {

                Log.Error(ex, "Failed to apply colors in ThemeManager");

            }

        }



        public static (LinearGradientBrush Background, LinearGradientBrush Stroke) CreateGradientBrushes(Color primary, Color light)
        {
            byte rStroke = (byte)Math.Clamp(light.R * 0.85 + 30, 0, 255);
            byte gStroke = (byte)Math.Clamp(light.G * 0.85 + 35, 0, 255);
            byte bStroke = (byte)Math.Clamp(light.B * 0.85 + 40, 0, 255);
            Color strokeMid = Color.FromArgb(160, (byte)Math.Clamp(primary.R * 0.6 + 20, 0, 255), (byte)Math.Clamp(primary.G * 0.6 + 20, 0, 255), (byte)Math.Clamp(primary.B * 0.6 + 25, 0, 255));
            Color strokeEnd = Color.FromArgb(240, rStroke, gStroke, bStroke);

            byte rMid1 = (byte)Math.Clamp(primary.R * 0.15 + 4, 0, 255);
            byte gMid1 = (byte)Math.Clamp(primary.G * 0.15 + 6, 0, 255);
            byte bMid1 = (byte)Math.Clamp(primary.B * 0.15 + 8, 0, 255);
            Color bgMid1 = Color.FromRgb(rMid1, gMid1, bMid1);

            byte rMid2 = (byte)Math.Clamp(primary.R * 0.38 + 10, 0, 255);
            byte gMid2 = (byte)Math.Clamp(primary.G * 0.38 + 15, 0, 255);
            byte bMid2 = (byte)Math.Clamp(primary.B * 0.38 + 20, 0, 255);
            Color bgMid2 = Color.FromRgb(rMid2, gMid2, bMid2);

            byte rEnd = (byte)Math.Clamp(light.R * 0.60 + 20, 0, 255);
            byte gEnd = (byte)Math.Clamp(light.G * 0.60 + 25, 0, 255);
            byte bEnd = (byte)Math.Clamp(light.B * 0.60 + 30, 0, 255);
            Color bgEnd = Color.FromRgb(rEnd, gEnd, bEnd);

            LinearGradientBrush strokeBrush;
            LinearGradientBrush bgBrush;

            if (CurrentMaterial == "TintedGlass")
            {
                // Tinted Glass: translucent smoky glass (~85% alpha)
                bgBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(215, 12, 14, 20), 0.0),
                        new GradientStop(Color.FromArgb(220, (byte)Math.Clamp(primary.R * 0.08 + 8, 0, 255), (byte)Math.Clamp(primary.G * 0.08 + 8, 0, 255), (byte)Math.Clamp(primary.B * 0.08 + 12, 0, 255)), 0.35),
                        new GradientStop(Color.FromArgb(230, (byte)Math.Clamp(primary.R * 0.22 + 10, 0, 255), (byte)Math.Clamp(primary.G * 0.22 + 10, 0, 255), (byte)Math.Clamp(primary.B * 0.22 + 15, 0, 255)), 0.70),
                        new GradientStop(Color.FromArgb(242, (byte)Math.Clamp(light.R * 0.48 + 15, 0, 255), (byte)Math.Clamp(light.G * 0.48 + 15, 0, 255), (byte)Math.Clamp(light.B * 0.48 + 20, 0, 255)), 1.0)
                    }
                };

                strokeBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(120, 0xFF, 0xFF, 0xFF), 0.0),
                        new GradientStop(Color.FromArgb(70, 0x40, 0x45, 0x50), 0.35),
                        new GradientStop(strokeMid, 0.70),
                        new GradientStop(strokeEnd, 1.0)
                    }
                };
            }
            else if (CurrentMaterial == "Acrylic")
            {
                // Fluent Mica/Acrylic: softer translucent frosted diffusion (~70% alpha)
                bgBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(175, 16, 18, 24), 0.0),
                        new GradientStop(Color.FromArgb(185, (byte)Math.Clamp(primary.R * 0.12 + 10, 0, 255), (byte)Math.Clamp(primary.G * 0.12 + 10, 0, 255), (byte)Math.Clamp(primary.B * 0.12 + 14, 0, 255)), 0.35),
                        new GradientStop(Color.FromArgb(200, (byte)Math.Clamp(primary.R * 0.30 + 12, 0, 255), (byte)Math.Clamp(primary.G * 0.30 + 12, 0, 255), (byte)Math.Clamp(primary.B * 0.30 + 18, 0, 255)), 0.70),
                        new GradientStop(Color.FromArgb(220, (byte)Math.Clamp(light.R * 0.58 + 20, 0, 255), (byte)Math.Clamp(light.G * 0.58 + 20, 0, 255), (byte)Math.Clamp(light.B * 0.58 + 25, 0, 255)), 1.0)
                    }
                };

                strokeBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(90, 0xFF, 0xFF, 0xFF), 0.0),
                        new GradientStop(Color.FromArgb(50, 0x30, 0x35, 0x40), 0.35),
                        new GradientStop(strokeMid, 0.70),
                        new GradientStop(strokeEnd, 1.0)
                    }
                };
            }
            else
            {
                // True OLED Black: Pure black with minimal accent glow at bottom
                bgBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0x00, 0x00, 0x00), 0.0),
                        new GradientStop(Color.FromRgb(0x02, 0x03, 0x05), 0.35),
                        new GradientStop(bgMid2, 0.70),
                        new GradientStop(bgEnd, 1.0)
                    }
                };

                strokeBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(40, 0x1E, 0x20, 0x24), 0.0),
                        new GradientStop(Color.FromArgb(80, 0x2A, 0x2D, 0x32), 0.35),
                        new GradientStop(strokeMid, 0.70),
                        new GradientStop(strokeEnd, 1.0)
                    }
                };
            }

            strokeBrush.Freeze();
            bgBrush.Freeze();
            return (bgBrush, strokeBrush);
        }

        public static LinearGradientBrush CreatePlayPauseBrush(Color primary, Color light)

        {

            var brush = new LinearGradientBrush

            {

                StartPoint = new Point(0, 0),

                EndPoint = new Point(1, 1),

                GradientStops = new GradientStopCollection

                {

                    new GradientStop(light, 0.0),

                    new GradientStop(primary, 1.0)

                }

            };

            brush.Freeze();

            return brush;

        }

    }

}

