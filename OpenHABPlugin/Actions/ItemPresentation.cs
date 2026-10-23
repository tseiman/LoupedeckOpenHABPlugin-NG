using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections.Concurrent;

namespace Loupedeck.OpenHABPluginNG.Actions
{
    internal static class ItemPresentation
    {
        private static readonly ConcurrentDictionary<string, string> FixedIcons = new ConcurrentDictionary<string, string>();
        private static readonly object RenderLock = new object();
        private static readonly Dictionary<(int, int, string, string, string, bool, string), byte[]> Rendered = new();

        internal static BitmapImage RenderConnection(int width, int height, string status, OpenHABConnectionHealth health)
        {
            var color = health == OpenHABConnectionHealth.Healthy ? "#36c96b" : health == OpenHABConnectionHealth.Degraded ? "#ffab32" : "#ff4545";
            var svg = Svg("switch", false).Replace("#a8b2c3", color);
            return Render(width, height, "OpenHAB NG", status, "switch", false, svg);
        }

        public static BitmapImage Render(PluginImageSize size, string label, string value, string symbol, bool active = false, string serverSvg = null)
            => Render(size.GetWidth(), size.GetHeight(), label, value, symbol, active, serverSvg);

        public static BitmapImage Render(int width, int height, string label, string value, string symbol, bool active = false, string serverSvg = null)
        {
            var svgHash = serverSvg == null ? "" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(serverSvg)));
            var key = (width, height, label, value, symbol, active, svgHash);
            lock (RenderLock)
            {
                if (!Rendered.TryGetValue(key, out var bytes))
                {
                    using var image = Draw(width, height, label, value, symbol, active, serverSvg);
                    bytes = image.ToArray();
                    if (Rendered.Count >= 128) Rendered.Clear();
                    Rendered[key] = bytes;
                }
                // The host owns each returned image; only immutable encoded bytes are shared.
                return BitmapImage.FromArray(bytes);
            }
        }

        private static BitmapImage Draw(int width, int height, string label, string value, string symbol, bool active, string serverSvg)
        {
            using var bitmap = new BitmapBuilder(width, height);
            var foreground = active ? new BitmapColor(0xff, 0xd1, 0x55) : new BitmapColor(0xa8, 0xb2, 0xc3);
            bitmap.Clear(BitmapColor.Black);
            // Dial strips are short and wide: give their text room instead of clipping three rows.
            var compact = height < 60 || width > height * 2;
            var font = compact ? Math.Clamp(height / 3, 10, 16) : Math.Clamp(width / 7, 10, 18);
            var captionLines = (label ?? "Select item").Length * font * 0.55 > width - 4 ? 2 : 1;
            var textHeight = compact ? height / 2 : Math.Min(height / 3, captionLines * font + 4);
            var valueFont = compact ? font : Math.Clamp(width / 6, 11, 18);
            var valueHeight = Math.Min(height / 2, valueFont + 5);
            var iconSize = compact ? Math.Max(12, Math.Min(height - 4, width / 3)) : Math.Max(12, Math.Min(width - 6, height - textHeight - valueHeight - 2));
            var textX = compact && !string.IsNullOrEmpty(symbol) ? iconSize + 6 : 2;
            var textWidth = Math.Max(1, width - textX - 2);
            bitmap.DrawText(label ?? "Select item", textX, 1, textWidth, textHeight, BitmapColor.White, font);
            var iconY = compact ? (height - iconSize) / 2 : textHeight + 1;
            if (!string.IsNullOrEmpty(symbol))
            {
                try
                {
                    using var icon = Rasterize(serverSvg ?? Svg(symbol, active), iconSize);
                    bitmap.DrawImage(icon, compact ? 2 : (width - iconSize) / 2, iconY, iconSize, iconSize);
                }
                catch
                {
                    using var icon = Rasterize(Svg(symbol, active), iconSize);
                    bitmap.DrawImage(icon, compact ? 2 : (width - iconSize) / 2, iconY, iconSize, iconSize);
                }
            }
            var valueWidth = compact ? textWidth : Math.Min(width - 4, Math.Max(30, (int)((value ?? "—").Length * valueFont * 0.7) + 10));
            var valueX = compact ? textX : (width - valueWidth) / 2;
            var valueY = compact ? height / 2 : height - valueHeight;
            // Reserve the value row so the full icon remains visible.
            bitmap.FillRectangle(valueX, valueY, valueWidth, valueHeight, BitmapColor.Black);
            bitmap.DrawText(value ?? "—", valueX, valueY, valueWidth, valueHeight, foreground, valueFont);
            return bitmap.ToImage();
        }

        private static BitmapImage Rasterize(string svg, int size)
        {
            // DrawImage's implicit SVG conversion recolors monochrome images.
            // Rasterize explicitly with the SDK to preserve OpenHAB and F7 colors.
            var vector = new VectorImage();
            if (!vector.Load(svg)) throw new InvalidDataException("Invalid icon SVG.");
            return vector.ToBitmap(size, size) ?? throw new InvalidDataException("Cannot rasterize icon SVG.");
        }

        // Code-native SVG paths remain sharp at every supported key size.
        internal static string Svg(string symbol, bool active)
        {
            if (symbol == "arrowtriangle_up" || symbol == "arrowtriangle_down" || symbol == "stop")
                return FixedIcons.GetOrAdd(symbol, name =>
                {
                    using var stream = typeof(ItemPresentation).Assembly.GetManifestResourceStream("Loupedeck.OpenHABPluginNG.Resources.F7." + name + ".svg")
                        ?? throw new InvalidOperationException("Missing embedded F7 icon: " + name);
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd().Replace("<svg ", "<svg fill=\"" + (name == "stop" ? "#ff4545" : "#929292") + "\" ");
                });
            var color = active ? "#ffd155" : "#a8b2c3";
            var path = symbol switch
            {
                "lamp" => "<path d='M23 38C23 29 18 27 18 19a14 14 0 0 1 28 0c0 8-5 10-5 19z'/><path d='M24 45h16M26 52h12M28 58h8'/>" +
                    (active ? "<path d='M3 20h7M54 20h7M8 3l6 6M50 9l6-6M3 38l8-4M53 34l8 4'/>" : ""),
                "socket" => "<rect x='8' y='5' width='48' height='54' rx='8'/><circle cx='32' cy='32' r='17'/><path d='M25 28v8M39 28v8M32 15v5M32 44v5'/>",
                "dimmer" => "<circle cx='32' cy='32' r='16'/><path d='M32 1v8M32 55v8M1 32h8M55 32h8M9 9l6 6M49 49l6 6M9 55l6-6M49 15l6-6'/>",
                _ => "<path d='M32 5v27M19 12a23 23 0 1 0 26 0'/>"
            };
            return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'><g fill='none' stroke='" + color + "' stroke-width='4' stroke-linecap='round' stroke-linejoin='round'>" + path + "</g></svg>";
        }
    }

    public abstract class ItemCommand : ActionEditorCommand
    {
        protected OpenHABPlugin Owner => (OpenHABPlugin)this.Plugin;
        protected OpenHABService Service => Owner.OHService;
        protected ItemCommand(string title, string group)
        {
            this.Name = GetType().Name;
            this.DisplayName = title; this.GroupName = group; this.IsWidget = true;
            ItemEditor.Configure(this.ActionEditor, () => AvailableItems);
        }
        protected abstract IEnumerable<OpenHABCommandItem> AvailableItems { get; }
        protected static string ItemName(ActionEditorActionParameters parameters) => parameters.GetString("item", "");
        protected override string GetCommandDisplayName(ActionEditorActionParameters parameters) => Service.GetItem(ItemName(parameters))?.Label ?? "Select item";
        protected override bool OnLoad()
        {
            var result = base.OnLoad();
            Service.ItemChanged += Changed;
            Service.CatalogChanged += CatalogChanged;
            return result;
        }
        protected override bool OnUnload()
        {
            Service.ItemChanged -= Changed;
            Service.CatalogChanged -= CatalogChanged;
            return base.OnUnload();
        }
        private void Changed(object sender, OpenHABEventArgs e) => this.ActionImageChanged();
        private void CatalogChanged(object sender, EventArgs e)
        {
            this.ActionEditor.ListboxItemsChanged("group");
            this.ActionEditor.ListboxItemsChanged("item");
        }
    }

    internal static class ItemEditor
    {
        internal static void Configure(ActionEditor editor, Func<IEnumerable<OpenHABCommandItem>> items)
        {
            editor.AddControlEx(new ActionEditorListbox("group", "Group (optional):"));
            editor.AddControlEx(new ActionEditorListbox("item", "Item:").SetRequired());
            editor.ListboxItemsRequested += (_, e) =>
            {
                var snapshot = items().ToArray();
                if (string.Equals(e.ControlName, "group", StringComparison.OrdinalIgnoreCase))
                {
                    e.AddItem("__all__", "All groups", null);
                    foreach (var group in snapshot.Select(i => i.Group ?? "Others").Distinct().OrderBy(g => g)) e.AddItem(group, group, null);
                }
                if (string.Equals(e.ControlName, "item", StringComparison.OrdinalIgnoreCase))
                {
                    var group = e.ActionEditorState.GetControlValue("group");
                    foreach (var item in snapshot.Where(i => string.IsNullOrEmpty(group) || group == "__all__" || i.Group == group).OrderBy(i => i.Label))
                        e.AddItem(item.Name, item.Label ?? item.Name, item.Name);
                }
            };
            editor.ControlValueChanged += (_, e) =>
            {
                if (string.Equals(e.ControlName, "group", StringComparison.OrdinalIgnoreCase)) editor.ListboxItemsChanged("item");
            };
        }
    }
}
