namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class LabelItem : ItemCommand
    {
        public LabelItem() : base("Live state", "Displays") { }
        protected override System.Collections.Generic.IEnumerable<OpenHABCommandItem> AvailableItems => Service.Items;
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height)
        {
            var parameter = ItemName(parameters);
            var item = Service.GetItem(parameter);
            var value = item?.EffectiveType == "Dimmer" || item?.EffectiveType == "Rollershutter" ? OpenHABService.FormatPercent(item.State) : item?.State ?? "—";
            return ItemPresentation.Render(width, height, item?.Label ?? "Select item", value, "");
        }
    }
}
