using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotchWin.Views;
using NotchWin.ViewModels;
using NotchWin.Models;

namespace NotchWin.Services
{
    public static class SnapshotExporter
    {
        public static void ExportSnapshots(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var settingsService = new SettingsService();
            ThemeManager.ApplyAccentColor(settingsService.CurrentSettings.AccentColor);
            FontScaleService.Initialize(settingsService.CurrentSettings.FontScale);

            var window = new NotchWindow();
            window.Show();
            window.UpdateLayout();

            // Set expanded visuals
            if (window.ExpandedContentGrid != null)
            {
                window.ExpandedContentGrid.Visibility = Visibility.Visible;
                window.ExpandedContentGrid.Opacity = 1.0;
            }

            if (window.DataContext is NotchViewModel vm)
            {
                vm.CurrentState = NotchState.Expanded;
                window.UpdateLayout();

                // Save Home
                vm.SelectSlide("home");
                window.UpdateLayout();
                SaveCardSnapshot(window.SlideHomeCard, Path.Combine(outputDir, "kuri_slide_home.png"));

                // Save Control Center
                vm.SelectSlide("control");
                vm.IsWifiExpanded = true;
                vm.IsBluetoothExpanded = false;
                window.UpdateLayout();
                SaveCardSnapshot(window.SlideControlCenterCard, Path.Combine(outputDir, "kuri_slide_control.png"));

                // Save Media
                vm.SelectSlide("media");
                window.UpdateLayout();
                SaveCardSnapshot(window.SlideMediaCard, Path.Combine(outputDir, "kuri_slide_media.png"));

                // Save Weather
                vm.SelectSlide("weather");
                window.UpdateLayout();
                SaveCardSnapshot(window.SlideWeatherCard, Path.Combine(outputDir, "kuri_slide_weather.png"));

                // Save Shelf
                vm.SelectSlide("shelf");
                window.UpdateLayout();
                SaveCardSnapshot(window.SlideShelfCard, Path.Combine(outputDir, "kuri_slide_shelf.png"));

                // Save Timer
                vm.SelectSlide("timer");
                window.UpdateLayout();
                SaveCardSnapshot(window.SlideTimerCard, Path.Combine(outputDir, "kuri_slide_timer.png"));

                // Save Prayer
                vm.SelectSlide("prayer");
                window.UpdateLayout();
                SaveCardSnapshot(window.SlidePrayerCard, Path.Combine(outputDir, "kuri_slide_prayer.png"));

                // Save Water
                vm.SelectSlide("water");
                window.UpdateLayout();
                SaveCardSnapshot(window.SlideWaterCard, Path.Combine(outputDir, "kuri_slide_water.png"));

                // Save full expanded notch
                SaveCardSnapshot(window.ExpandedContentGrid, Path.Combine(outputDir, "kuri_expanded_full.png"));
            }

            window.Close();
        }

        private static void SaveCardSnapshot(FrameworkElement? element, string filePath)
        {
            if (element == null) return;
            try
            {
                element.Measure(new Size(584, 800));
                element.Arrange(new Rect(new Size(584, 800)));
                element.UpdateLayout();

                int w = (int)Math.Max(584, element.ActualWidth > 0 ? element.ActualWidth : element.DesiredSize.Width);
                int h = (int)Math.Max(120, element.ActualHeight > 0 ? element.ActualHeight : element.DesiredSize.Height);

                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(element);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = File.Create(filePath);
                encoder.Save(fs);
                Console.WriteLine($"Rendered: {filePath} ({w}x{h})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering {filePath}: {ex.Message}");
            }
        }
    }
}
