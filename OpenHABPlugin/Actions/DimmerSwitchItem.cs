using System.Collections.Generic;

namespace Loupedeck.OpenHABPluginNG.Actions
{
    public sealed class DimmerSwitchItem : ItemCommand
    {
        public DimmerSwitchItem() : base("Dimmer ON/OFF (0/100%)", "Lights") { }
        protected override IEnumerable<OpenHABCommandItem> AvailableItems => Service.Dimmer;
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            var item = ItemName(parameters);
            var command = OpenHABService.DimmerToggleCommand(Service.GetStateOfItem(item));
            if (command == null) return false;
            _ = Owner.ExecuteAsync(item, command);
            return true;
        }
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height)
        {
            var name = ItemName(parameters);
            var item = Service.GetItem(name);
            var active = OpenHABService.DimmerToggleCommand(item?.State) == "0";
            return ItemPresentation.RenderDimmer(width, height, item?.Label ?? "", OpenHABService.FormatPercent(item?.State),
                active, Service.GetCachedIconSvg(name, string.IsNullOrWhiteSpace(item?.Category) ? "light" : item.Category));
        }
    }
}
