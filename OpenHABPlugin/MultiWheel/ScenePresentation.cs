using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            using var raster = vector.ToBitmap(size, size) ?? throw new InvalidDataException("Cannot render scene icon.");
            if (color) return BitmapImage.FromArray(raster.ToArray());
            using var image = Image.Load<Rgba32>(raster.ToArray());
            image.Mutate(context => context.Grayscale());
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return BitmapImage.FromArray(stream.ToArray());
        }
        internal static BitmapImage RenderAnalogClock(DateTime now)
        {
            using (var image = new Image<Rgba32>(WheelCanvasSize, WheelCanvasSize, Color.Black))
            using (var stream = new MemoryStream())
            {
                var center = new PointF(WheelCanvasSize / 2f, WheelCanvasSize / 2f);
                var radius = (WheelCanvasSize / 2f) - 16f;

                image.Mutate(context =>
                {
                    // Rim
                    var rim = new SixLabors.ImageSharp.Drawing.EllipsePolygon(center, radius);
                    context.Draw(Color.White, 3f, rim);

                    // Hour ticks
                    for (var i = 0; i < 12; i++)
                    {
                        var angle = (i * 30f - 90f) * (Single)Math.PI / 180f;
                        var inner = (i % 3 == 0) ? radius - 32f : radius - 18f;
                        var outer = radius - 6f;
                        var p1 = new PointF(
                            center.X + (Single)Math.Cos(angle) * inner,
                            center.Y + (Single)Math.Sin(angle) * inner);
                        var p2 = new PointF(
                            center.X + (Single)Math.Cos(angle) * outer,
                            center.Y + (Single)Math.Sin(angle) * outer);
                        context.DrawLine(Color.White, (i % 3 == 0) ? 5f : 2f, p1, p2);
                    }

                    // Hands
                    var hourAngle = ((now.Hour % 12) * 30f + now.Minute * 0.5f - 90f) * (Single)Math.PI / 180f;
                    var minuteAngle = (now.Minute * 6f + now.Second * 0.1f - 90f) * (Single)Math.PI / 180f;
                    var secondAngle = (now.Second * 6f - 90f) * (Single)Math.PI / 180f;

                    DrawHand(context, center, hourAngle, radius * 0.5f, 10f, Color.White);
                    DrawHand(context, center, minuteAngle, radius * 0.75f, 6f, Color.White);
                    DrawHand(context, center, secondAngle, radius * 0.85f, 2f, Color.Red);

                    // Center cap
                    var cap = new SixLabors.ImageSharp.Drawing.EllipsePolygon(center, 10f);
                    context.Fill(Color.White, cap);
                });

                image.SaveAsPng(stream);
                return BitmapImage.FromArray(stream.ToArray());
            }
        }

        private static void DrawHand(IImageProcessingContext context, PointF center, Single angle, Single length, Single width, Color color)
        {
            var tip = new PointF(
                center.X + (Single)Math.Cos(angle) * length,
                center.Y + (Single)Math.Sin(angle) * length);
            context.DrawLine(color, width, center, tip);
        }

    }
}
