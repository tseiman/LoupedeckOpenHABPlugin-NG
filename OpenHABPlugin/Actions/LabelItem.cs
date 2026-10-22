namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class LabelItem : ItemCommand
    {
        public LabelItem() : base("Live state", "Displays") { }
        protected override PluginProfileActionData GetProfileActionData() => ItemPresentation.Tree("Select item", Service.Items);
        protected override BitmapImage GetCommandImage(string parameter, PluginImageSize size)
        {
            var item = Service.GetItem(parameter);
            var value = item?.Type == "Dimmer" || item?.Type == "Rollershutter" ? OpenHABService.FormatPercent(item.State) : item?.State ?? "—";
            return ItemPresentation.Render(size, Service.GetItemLabel(parameter), value, "");
        }
    }
}
