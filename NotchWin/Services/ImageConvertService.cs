using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace NotchWin.Services
{
    public class ImageConvertResult
    {
        public int Converted { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public string? LastOutputName { get; set; }
    }

    public class ImageConvertService
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif"
        };

        public ImageConvertResult ConvertFiles(IEnumerable<string>? filePaths)
        {
            var result = new ImageConvertResult();
            if (filePaths == null) return result;

            foreach (string path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
                {
                    result.Skipped++;
                    continue;
                }

                string ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
                {
                    result.Skipped++;
                    continue;
                }

                try
                {
                    bool toPng = ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                              || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
                    string targetExt = toPng ? ".png" : ".jpg";

                    string directory = Path.GetDirectoryName(path) ?? string.Empty;
                    string baseName = Path.GetFileNameWithoutExtension(path);
                    string targetPath = GetUniquePath(directory, baseName + "-converted", targetExt);

                    var decoder = BitmapDecoder.Create(
                        new Uri(path, UriKind.Absolute),
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    BitmapSource frame = decoder.Frames[0];

                    BitmapEncoder encoder = toPng
                        ? new PngBitmapEncoder()
                        : new JpegBitmapEncoder { QualityLevel = 92 };

                    encoder.Frames.Add(BitmapFrame.Create(toPng ? frame : FlattenToWhite(frame)));

                    using (FileStream stream = File.Create(targetPath))
                    {
                        encoder.Save(stream);
                    }

                    result.Converted++;
                    result.LastOutputName = Path.GetFileName(targetPath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to convert image: {Path}", path);
                    result.Failed++;
                }
            }

            return result;
        }

        private static BitmapSource FlattenToWhite(BitmapSource source)
        {
            BitmapSource converted = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var bounds = new Rect(0, 0, converted.PixelWidth, converted.PixelHeight);
                dc.DrawRectangle(Brushes.White, null, bounds);
                dc.DrawImage(converted, bounds);
            }

            var target = new RenderTargetBitmap(
                converted.PixelWidth, converted.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }

        private static string GetUniquePath(string directory, string baseName, string extension)
        {
            string candidate = Path.Combine(directory, baseName + extension);
            int index = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, $"{baseName}-{index}{extension}");
                index++;
            }

            return candidate;
        }
    }
}
