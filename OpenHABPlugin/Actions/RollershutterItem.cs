namespace Loupedeck.OpenHABPluginNG.Actions
{
    /// <summary>Place these three widgets next to each other and select the same item.</summary>
    public abstract class RollershutterItem : ItemCommand
    {
        private readonly string _command;
        private readonly string _symbol;
        private readonly string _caption;
        protected RollershutterItem(string caption, string command, string symbol) : base("Rollladen " + caption, "Rollershutters")
        { _command = command; _symbol = symbol; _caption = caption; }
        protected override PluginProfileActionData GetProfileActionData() => ItemPresentation.Tree("Select rollershutter", Service.Rollershutters);
        protected override async void RunCommand(string parameter) => await Owner.ExecuteAsync(parameter, _command);
        protected override BitmapImage GetCommandImage(string parameter, PluginImageSize size) => ItemPresentation.Render(size,
            Service.GetItemLabel(parameter), _caption + " · " + OpenHABService.FormatPercent(Service.GetStateOfItem(parameter)), _symbol);
    }
    public class RollershutterUp : RollershutterItem { public RollershutterUp() : base("AUF", "UP", "up") { } }
    public class RollershutterStop : RollershutterItem { public RollershutterStop() : base("STOP", "STOP", "stop") { } }
    public class RollershutterDown : RollershutterItem { public RollershutterDown() : base("AB", "DOWN", "down") { } }
}
