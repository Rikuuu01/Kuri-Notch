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

        public static bool IsAccentColorEnabled { get; set; } = true;

        public static (Color Primary, Color Light) GetAccentColors(string colorName)

        {

            if (!IsAccentColorEnabled)
            {
                return (Color.FromRgb(0x52, 0x52, 0x5B), Color.FromRgb(0xD4, 0xD4, 0xD8));
            }

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

            // If accent colors are disabled, apply pitch black / neutral monochrome theme
            if (!IsAccentColorEnabled)
            {
                var (monoPrimary, monoLight) = GetAccentColors(colorName);
                ApplyColorsToApp(monoPrimary, monoLight, animate: _hasInitialized);
                _hasInitialized = true;
                return;
            }

            // If not forced and an album accent is known, ALWAYS prioritize the album cover accent!
            if (!force && CurrentAlbumAccent.HasValue)
            {
                _isUsingDynamicAlbumAccent = true;
                byte rAccentL = (byte)Math.Min(255, CurrentAlbumAccent.Value.R + 45);
                byte gAccentL = (byte)Math.Min(255, CurrentAlbumAccent.Value.G + 45);
                byte bAccentL = (byte)Math.Min(255, CurrentAlbumAccent.Value.B + 45);
                Color lightAccent = Color.FromRgb(rAccentL, gAccentL, bAccentL);
                ApplyColorsToApp(CurrentAlbumAccent.Value, lightAccent, animate: _hasInitialized);
                _hasInitialized = true;
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

            var previousAccent = CurrentAlbumAccent;
            // Always cache the album accent so it is immediately available when accents are turned on
            CurrentAlbumAccent = primary;

            if (!IsAccentColorEnabled)
            {
                return;
            }

            // Avoid restarting animation if the new color is virtually identical to current

            if (_isUsingDynamicAlbumAccent && previousAccent.HasValue)

            {

                var cur = previousAccent.Value;

                int delta = Math.Abs(cur.R - primary.R) + Math.Abs(cur.G - primary.G) + Math.Abs(cur.B - primary.B);

                if (delta < 8)

                {

                    return;

                }

            }



            _isUsingDynamicAlbumAccent = true;



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
            var (primary, light) = (!IsAccentColorEnabled)
                ? GetAccentColors(ActiveThemeSetting)
                : (CurrentAlbumAccent.HasValue
                    ? (CurrentAlbumAccent.Value, Color.FromRgb((byte)Math.Min(255, CurrentAlbumAccent.Value.R + 45), (byte)Math.Min(255, CurrentAlbumAccent.Value.G + 45), (byte)Math.Min(255, CurrentAlbumAccent.Value.B + 45)))
                    : GetAccentColors(ActiveThemeSetting));
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

                    var cardBgColor = CurrentMaterial switch
                    {
                        "Acrylic" => Color.FromArgb(178, 16, 20, 32),
                        "TintedGlass" => Color.FromArgb(195, 14, 16, 24),
                        _ => Color.FromRgb(0x12, 0x12, 0x1C)
                    };
                    var cardBrush = new SolidColorBrush(cardBgColor);
                    cardBrush.Freeze();

                    var cardBorderColor = CurrentMaterial switch
                    {
                        "Acrylic" => Color.FromArgb(58, 255, 255, 255),
                        "TintedGlass" => Color.FromArgb(40, 255, 255, 255),
                        _ => Color.FromRgb(0x22, 0x22, 0x32)
                    };
                    var cardBorderBrush = new SolidColorBrush(cardBorderColor);
                    cardBorderBrush.Freeze();

                    double sheenOpacity = CurrentMaterial switch
                    {
                        "Acrylic" => 1.0,
                        "TintedGlass" => 0.30,
                        _ => 0.0
                    };



                    // Update Application-level resources for dynamic resource lookups

                    app.Resources["AccentPrimaryBrush"] = primaryBrush;

                    app.Resources["AccentLightBrush"] = lightBrush;

                    app.Resources["AccentGlowColor"] = IsAccentColorEnabled ? light : Color.FromArgb(0, 0, 0, 0);

                    app.Resources["NotchStrokeBrush"] = strokeBrush;

                    app.Resources["NotchBackgroundBrush"] = bgBrush;

                    app.Resources["NotchCardBackgroundBrush"] = cardBrush;

                    app.Resources["NotchCardBorderBrush"] = cardBorderBrush;

                    app.Resources["NotchLiquidGlassSheenOpacity"] = sheenOpacity;

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
            LinearGradientBrush strokeBrush;
            LinearGradientBrush bgBrush;

            if (!IsAccentColorEnabled)
            {
                if (CurrentMaterial == "TintedGlass")
                {
                    bgBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 0),
                        EndPoint = new Point(0.5, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(205, 14, 16, 22), 0.0),
                            new GradientStop(Color.FromArgb(215, 10, 12, 18), 0.35),
                            new GradientStop(Color.FromArgb(228, 10, 12, 18), 0.70),
                            new GradientStop(Color.FromArgb(238, 16, 20, 28), 1.0)
                        }
                    };

                    strokeBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 0),
                        EndPoint = new Point(0.5, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(95, 0xFF, 0xFF, 0xFF), 0.0),
                            new GradientStop(Color.FromArgb(45, 0xB8, 0xC4, 0xDC), 0.35),
                            new GradientStop(Color.FromArgb(35, 0x90, 0xA0, 0xBC), 0.70),
                            new GradientStop(Color.FromArgb(75, 0xD0, 0xDC, 0xF0), 1.0)
                        }
                    };
                }
                else if (CurrentMaterial == "Acrylic")
                {
                    // Liquid Glass (Acrylic): rich frosted crystal translucency (~82% opacity) + crisp specular rim
                    bgBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 0),
                        EndPoint = new Point(0.5, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(208, 26, 31, 44), 0.0),
                            new GradientStop(Color.FromArgb(198, 14, 18, 28), 0.30),
                            new GradientStop(Color.FromArgb(210, 12, 16, 26), 0.70),
                            new GradientStop(Color.FromArgb(226, 22, 28, 42), 1.0)
                        }
                    };

                    strokeBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 0),
                        EndPoint = new Point(0.5, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(185, 0xFF, 0xFF, 0xFF), 0.0),
                            new GradientStop(Color.FromArgb(80, 0xD7, 0xE4, 0xFF), 0.30),
                            new GradientStop(Color.FromArgb(55, 0xBE, 0xD2, 0xF0), 0.70),
                            new GradientStop(Color.FromArgb(140, 0xEB, 0xF5, 0xFF), 1.0)
                        }
                    };
                }
                else
                {
                    // True OLED Pitch Black: 100% solid pure black (#000000)
                    bgBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 0),
                        EndPoint = new Point(0.5, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromRgb(0x00, 0x00, 0x00), 0.0),
                            new GradientStop(Color.FromRgb(0x00, 0x00, 0x00), 0.35),
                            new GradientStop(Color.FromRgb(0x00, 0x00, 0x00), 0.70),
                            new GradientStop(Color.FromRgb(0x00, 0x00, 0x00), 1.0)
                        }
                    };

                    // Zero glow on edges: completely transparent stroke
                    strokeBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 0),
                        EndPoint = new Point(0.5, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0),
                            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.35),
                            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.70),
                            new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0)
                        }
                    };
                }

                strokeBrush.Freeze();
                bgBrush.Freeze();
                return (bgBrush, strokeBrush);
            }

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
                // Liquid Glass (Acrylic) with Accent tint: rich frosted glass (~82% opacity) + bright specular rim
                bgBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(208, 24, 29, 42), 0.0),
                        new GradientStop(Color.FromArgb(202, (byte)Math.Clamp(primary.R * 0.14 + 12, 0, 255), (byte)Math.Clamp(primary.G * 0.14 + 14, 0, 255), (byte)Math.Clamp(primary.B * 0.14 + 22, 0, 255)), 0.35),
                        new GradientStop(Color.FromArgb(214, (byte)Math.Clamp(primary.R * 0.30 + 14, 0, 255), (byte)Math.Clamp(primary.G * 0.30 + 16, 0, 255), (byte)Math.Clamp(primary.B * 0.30 + 24, 0, 255)), 0.70),
                        new GradientStop(Color.FromArgb(228, (byte)Math.Clamp(light.R * 0.50 + 20, 0, 255), (byte)Math.Clamp(light.G * 0.50 + 22, 0, 255), (byte)Math.Clamp(light.B * 0.50 + 30, 0, 255)), 1.0)
                    }
                };

                strokeBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(195, 0xFF, 0xFF, 0xFF), 0.0),
                        new GradientStop(Color.FromArgb(90, 0xD7, 0xE4, 0xFF), 0.35),
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

            if (!IsAccentColorEnabled)
            {
                var monoBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0xE4, 0xE4, 0xE7), 0.0),
                        new GradientStop(Color.FromRgb(0x71, 0x71, 0x7A), 1.0)
                    }
                };
                monoBrush.Freeze();
                return monoBrush;
            }

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

