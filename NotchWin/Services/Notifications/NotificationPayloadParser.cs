using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NotchWin.Services.Notifications
{
    public static class NotificationPayloadParser
    {
        public static (string Title, string Message, string? ImagePath) Parse(byte[] payloadBytes)
        {
            if (payloadBytes == null || payloadBytes.Length == 0)
                return (string.Empty, string.Empty, null);

            string xml;
            try
            {
                xml = Encoding.UTF8.GetString(payloadBytes).TrimEnd('\0').Trim();
            }
            catch
            {
                return (string.Empty, string.Empty, null);
            }

            return ParseXml(xml);
        }

        public static (string Title, string Message, string? ImagePath) ParseXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return (string.Empty, string.Empty, null);

            try
            {
                var doc = XDocument.Parse(xml);
                var texts = new List<string>();
                string? imageSrc = null;

                foreach (var textElem in doc.Descendants("text"))
                {
                    string val = CleanText(textElem.Value);
                    if (!string.IsNullOrEmpty(val))
                    {
                        texts.Add(val);
                    }
                }

                foreach (var imgElem in doc.Descendants("image"))
                {
                    string? src = imgElem.Attribute("src")?.Value;
                    if (!string.IsNullOrWhiteSpace(src))
                    {
                        imageSrc = src;
                        break;
                    }
                }

                string title = texts.Count > 0 ? texts[0] : string.Empty;
                string message = texts.Count > 1 ? texts[1] : string.Empty;

                return (title, message, imageSrc);
            }
            catch
            {
                // Fallback using Regex if XML is slightly malformed
                return ParseWithRegex(xml);
            }
        }

        private static (string Title, string Message, string? ImagePath) ParseWithRegex(string xml)
        {
            var textMatches = Regex.Matches(xml, @"<text[^>]*>(.*?)</text>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var texts = new List<string>();
            foreach (Match match in textMatches)
            {
                string val = CleanText(match.Groups[1].Value);
                if (!string.IsNullOrEmpty(val))
                {
                    texts.Add(val);
                }
            }

            var imgMatch = Regex.Match(xml, @"<image[^>]*src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            string? imageSrc = imgMatch.Success ? imgMatch.Groups[1].Value : null;

            string title = texts.Count > 0 ? texts[0] : string.Empty;
            string message = texts.Count > 1 ? texts[1] : string.Empty;

            return (title, message, imageSrc);
        }

        public static string CleanText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove Unicode directional isolates, zero-width spaces, and control characters
            return input
                .Replace("\u2068", "")
                .Replace("\u2069", "")
                .Replace("\u200E", "")
                .Replace("\u200F", "")
                .Replace("\u200B", "")
                .Replace("\uFEFF", "")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&apos;", "'")
                .Trim();
        }
    }
}
