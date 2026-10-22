using System;
namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class SwitchItem : ItemCommand
    {
        private readonly string _icon;
        public SwitchItem() : this("Switch (automatic icon)", "") { }
        protected SwitchItem(string title, string icon) : base(title, "Switches") { _icon = icon; }
        protected override PluginProfileActionData GetProfileActionData() => ItemPresentation.Tree("Select switch", Service.Switches);
        protected override async void RunCommand(string parameter)
        {
            var state = Service.GetStateOfItem(parameter);
            if (state == "ON" || state == "OFF") await Owner.ExecuteAsync(parameter, state == "ON" ? "OFF" : "ON");
        }
        protected override BitmapImage GetCommandImage(string parameter, PluginImageSize size)
        {
            var item = Service.GetItem(parameter);
            var category = (item?.Category ?? "").ToLowerInvariant();
            var icon = _icon;
            if (icon.Length == 0)
                icon = category.Contains("light") || category.Contains("lamp") || category.Contains("bulb") ? "lamp" :
                    category.Contains("socket") || category.Contains("outlet") || category.Contains("poweroutlet") ? "socket" : "switch";
            var value = item?.State == "ON" ? "AN" : item?.State == "OFF" ? "AUS" : "—";
            var categoryOverride = _icon == "lamp" ? "light" : _icon == "socket" ? "socket" : null;
            return ItemPresentation.Render(size, Service.GetItemLabel(parameter), value, icon, item?.State == "ON",
                Service.GetCachedIconSvg(parameter, categoryOverride));
        }
    }
    public class LampItem : SwitchItem { public LampItem() : base("Lamp ON/OFF", "lamp") { } }
    public class SocketItem : SwitchItem { public SocketItem() : base("Socket ON/OFF", "socket") { } }
}
