using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Text;

namespace Loupedeck.OpenHABPluginNG.MultiWheel
{
    internal static class ScenePresentation
    {
        private const int WheelCanvasSize = 512;
        private static readonly string FallbackIcon = ReadIcon();
        private static readonly object RenderLock = new();
        private static string ReadIcon()
        {
            using var stream = typeof(ScenePresentation).Assembly.GetManifestResourceStream("Loupedeck.OpenHABPluginNG.Resources.scene.svg");
            using var reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing scene icon."));
            return reader.ReadToEnd();
        }
        internal static BitmapImage Render(int width, int height, string name, string status, bool active, string svg = null, bool wheel = false)
        {
            lock (RenderLock)
            {
                using var bitmap = new BitmapBuilder(width, height);
                bitmap.Clear(BitmapColor.Black);
                var iconSize = Math.Max(1, Math.Min((int)(width * 0.68), (int)(height * (wheel ? 0.55 : 0.66))));
                using var icon = RenderIcon(svg ?? FallbackIcon, iconSize, active);
                bitmap.DrawImage(icon, (width - iconSize) / 2, (int)(height * 0.06), iconSize, iconSize);
                var font = wheel ? 34 : Math.Max(7, Math.Min(width, height) / 7);
                var textY = (int)(height * (wheel ? 0.64 : 0.70));
                var textHeight = (int)(height * (wheel ? 0.24 : 0.17));
                // Fit long labels into multiple centered lines on the circular display.
                if (wheel) font = Math.Min(font, Math.Max(17, (int)Math.Sqrt(width * textHeight / Math.Max(1.0, name.Length * 0.8))));
                bitmap.DrawText(name, width / 8, textY, width * 3 / 4, textHeight, BitmapColor.White, font);
                bitmap.DrawText(status, width / 8, (int)(height * (wheel ? 0.90 : 0.87)), width * 3 / 4, Math.Max(1, (int)(height * (wheel ? 0.08 : 0.13))),
                    active ? BitmapColor.White : new BitmapColor(0xa8, 0xa8, 0xa8), wheel ? 22 : Math.Max(6, font - 2));
                return bitmap.ToImage();
            }
        }
        private static BitmapImage RenderIcon(string svg, int size, bool color)
        {
            var vector = new VectorImage();
            if (!vector.Load(svg)) vector.Load(FallbackIcon);
            if (!color)
            {
                var source = vector.ToString();
                using var reader = XmlReader.Create(new StringReader(source), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var document = XDocument.Load(reader);
                foreach (var element in document.Descendants())
                {
                    foreach (var attribute in element.Attributes().ToArray())
                    {
                        if (attribute.Name.LocalName == "style") attribute.Value = GrayStyles(attribute.Value);
                        else if (new[] { "fill", "stroke", "color", "stop-color", "flood-color", "lighting-color" }.Contains(attribute.Name.LocalName))
                            attribute.Value = GrayPaint(attribute.Value);
                    }
                    if (element.Name.LocalName == "style") element.Value = GrayStyles(element.Value);
                }
                if (!vector.Load(document.ToString(SaveOptions.DisableFormatting))) throw new InvalidDataException("Cannot convert scene icon to grayscale.");
            }
            return vector.ToBitmap(size, size) ?? throw new InvalidDataException("Cannot render scene icon.");
        }
        private static string GrayStyles(string styles) => Regex.Replace(styles,
            @"(?<property>(?<![\w-])(?:fill|stroke|color|stop-color|flood-color|lighting-color)\s*:)\s*(?<paint>[^;}]+)",
            match => match.Groups["property"].Value + GrayPaint(match.Groups["paint"].Value));
        private static string GrayPaint(string paint)
        {
            if (!TryParsePaint(paint.Trim(), out var original)) return paint;
            var gray = Math.Clamp((int)Math.Round(original.R * 0.2126 + original.G * 0.7152 + original.B * 0.0722), 0, 255);
            return original.A == 255 ? $"#{gray:x2}{gray:x2}{gray:x2}" :
                string.Format(CultureInfo.InvariantCulture, "rgba({0},{0},{0},{1})", gray, original.A / 255.0);
        }
        private static bool TryParsePaint(string paint, out BitmapColor color)
        {
            color = BitmapColor.Invalid;
            if (paint.StartsWith("#", StringComparison.Ordinal))
            {
                var hex = paint.Substring(1);
                if (hex.Length == 3 || hex.Length == 4) hex = string.Concat(hex.Select(c => new string(c, 2)));
                if (hex.Length == 6) hex += "ff";
                if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba)) return false;
                color = BitmapColor.FromArgb(((rgba & 255) << 24) | (rgba >> 8));
                return true;
            }
            var rgb = Regex.Match(paint, @"^rgba?\(([^)]+)\)$", RegexOptions.IgnoreCase);
            if (rgb.Success)
            {
                var parts = rgb.Groups[1].Value.Split(',');
                if (parts.Length != 3 && parts.Length != 4) return false;
                var values = new int[4] { 0, 0, 0, 255 };
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();
                    var percent = part.EndsWith("%", StringComparison.Ordinal);
                    if (!double.TryParse(part.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return false;
                    values[i] = (int)Math.Clamp(Math.Round(value * (percent ? 2.55 : i == 3 ? 255 : 1)), 0, 255);
                }
                color = BitmapColor.FromArgb(((uint)values[3] << 24) | ((uint)values[0] << 16) | ((uint)values[1] << 8) | (uint)values[2]);
                return true;
            }
            var named = System.Drawing.Color.FromName(paint);
            if (!named.IsKnownColor) return false;
            color = BitmapColor.FromArgb((uint)named.ToArgb());
            return true;
        }
        internal static BitmapImage RenderAnalogClock(DateTime now)
        {
            // Preserve the ATEM clock geometry; rasterization uses the existing Logi SVG API.
            const float center = WheelCanvasSize / 2f;
            const float radius = center - 16f;
            var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 512 512'><rect width='512' height='512' fill='black'/><circle cx='256' cy='256' r='240' fill='none' stroke='white' stroke-width='3'/>");
            for (var i = 0; i < 12; i++)
            {
                var angle = (i * 30f - 90f) * (float)Math.PI / 180f;
                var inner = i % 3 == 0 ? radius - 32f : radius - 18f;
                var outer = radius - 6f;
                AppendLine(svg, center + (float)Math.Cos(angle) * inner, center + (float)Math.Sin(angle) * inner,
                    center + (float)Math.Cos(angle) * outer, center + (float)Math.Sin(angle) * outer, i % 3 == 0 ? 5f : 2f, "white");
            }
            var hourAngle = ((now.Hour % 12) * 30f + now.Minute * 0.5f - 90f) * (float)Math.PI / 180f;
            var minuteAngle = (now.Minute * 6f + now.Second * 0.1f - 90f) * (float)Math.PI / 180f;
            var secondAngle = (now.Second * 6f - 90f) * (float)Math.PI / 180f;
            AppendHand(svg, hourAngle, radius * 0.5f, 10f, "white");
            AppendHand(svg, minuteAngle, radius * 0.75f, 6f, "white");
            AppendHand(svg, secondAngle, radius * 0.85f, 2f, "red");
            svg.Append("<circle cx='256' cy='256' r='10' fill='white'/></svg>");
            var vector = new VectorImage();
            if (!vector.Load(svg.ToString())) throw new InvalidDataException("Cannot render clock SVG.");
            return vector.ToBitmap(WheelCanvasSize, WheelCanvasSize) ?? throw new InvalidDataException("Cannot rasterize clock.");
        }
        private static void AppendHand(StringBuilder svg, float angle, float length, float width, string color) =>
            AppendLine(svg, 256f, 256f, 256f + (float)Math.Cos(angle) * length, 256f + (float)Math.Sin(angle) * length, width, color);
        private static void AppendLine(StringBuilder svg, float x1, float y1, float x2, float y2, float width, string color) =>
            svg.AppendFormat(CultureInfo.InvariantCulture, "<line x1='{0}' y1='{1}' x2='{2}' y2='{3}' stroke='{4}' stroke-width='{5}'/>", x1, y1, x2, y2, color, width);
    }
}
