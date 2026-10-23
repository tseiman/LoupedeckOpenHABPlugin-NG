using System;
namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class SwitchItem : ItemCommand
    {
        private readonly string _icon;
        public SwitchItem() : this("Switch (automatic icon)", "") { }
        protected SwitchItem(string title, string icon) : base(title, "Switches") { _icon = icon; }
        protected override System.Collections.Generic.IEnumerable<OpenHABCommandItem> AvailableItems => Service.Switches;
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            var parameter = ItemName(parameters);
            var state = Service.GetStateOfItem(parameter);
            if (state != "ON" && state != "OFF") return false;
            _ = Owner.ExecuteAsync(parameter, state == "ON" ? "OFF" : "ON");
            return true;
        }
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height)
        {
            var parameter = ItemName(parameters);
            var item = Service.GetItem(parameter);
            var category = (item?.Category ?? "").ToLowerInvariant();
            var icon = _icon;
            if (icon.Length == 0)
                icon = category.Contains("light") || category.Contains("lamp") || category.Contains("bulb") ? "lamp" :
                    category.Contains("socket") || category.Contains("outlet") || category.Contains("poweroutlet") ? "socket" : "switch";
            var value = item?.State == "ON" ? "AN" : item?.State == "OFF" ? "AUS" : "—";
            var categoryOverride = _icon == "lamp" ? "light" : _icon == "socket" ? "socket" : null;
            return ItemPresentation.Render(width, height, item?.Label ?? "Select item", value, icon, item?.State == "ON",
                Service.GetCachedIconSvg(parameter, categoryOverride));
        }
    }
    public class LampItem : SwitchItem { public LampItem() : base("Lamp ON/OFF", "lamp") { } }
    public class SocketItem : SwitchItem { public SocketItem() : base("Socket ON/OFF", "socket") { } }
}
