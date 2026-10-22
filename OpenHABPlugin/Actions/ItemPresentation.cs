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
        public static PluginProfileActionTree Tree(string title, IEnumerable<OpenHABCommandItem> items)
        {
            var tree = new PluginProfileActionTree(title);
            tree.AddLevel("Group"); tree.AddLevel("Item");
            foreach (var group in items.GroupBy(i => i.Group ?? "Others").OrderBy(g => g.Key))
            {
                var node = tree.Root.AddNode(group.Key);
                foreach (var item in group.OrderBy(i => i.Label)) node.AddItem(item.Name, item.Label ?? item.Name);
            }
            return tree;
        }

        public static BitmapImage Render(PluginImageSize size, string label, string value, string symbol, bool active = false, string serverSvg = null)
        {
            using var bitmap = new BitmapBuilder(size);
            var foreground = active ? new BitmapColor(0xff, 0xd1, 0x55) : new BitmapColor(0xa8, 0xb2, 0xc3);
            bitmap.FillRectangle(0, 0, bitmap.Width, bitmap.Height, new BitmapColor(0x14, 0x1c, 0x2b));
            var font = Math.Max(9, bitmap.Width / 8);
            bitmap.DrawText(label ?? "OpenHAB NG", 2, 2, bitmap.Width - 4, bitmap.Height / 4, BitmapColor.White, font, 0);
            var iconSize = Math.Max(12, Math.Min(bitmap.Width / 2, bitmap.Height / 2));
            if (!string.IsNullOrEmpty(symbol))
            {
                BitmapImage icon;
                try { icon = BitmapImage.FromSvg(serverSvg ?? Svg(symbol, active)); }
                catch { icon = BitmapImage.FromSvg(Svg(symbol, active)); }
                using (icon) bitmap.DrawImage(icon, (bitmap.Width - iconSize) / 2, bitmap.Height / 4, iconSize, iconSize);
            }
            bitmap.DrawText(value ?? "—", 2, bitmap.Height * 3 / 4, bitmap.Width - 4, bitmap.Height / 4, foreground, font, 0);
            return bitmap.ToImage();
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

    public abstract class ItemCommand : PluginDynamicCommand
    {
        protected OpenHABPlugin Owner => (OpenHABPlugin)this.Plugin;
        protected OpenHABService Service => Owner.OHService;
        protected ItemCommand(string title, string group)
        {
            this.DisplayName = title; this.GroupName = group; this.IsWidget = true;
            this.MakeProfileAction("tree;Select OpenHAB item:");
        }
        protected override bool OnLoad()
        {
            var result = base.OnLoad();
            Service.ItemChanged += Changed;
            return result;
        }
        protected override bool OnUnload()
        {
            Service.ItemChanged -= Changed;
            return base.OnUnload();
        }
        private void Changed(object sender, OpenHABEventArgs e) => this.ActionImageChanged(e.Link);
    }
}
