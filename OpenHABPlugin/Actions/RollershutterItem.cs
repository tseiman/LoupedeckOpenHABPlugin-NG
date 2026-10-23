namespace Loupedeck.OpenHABPluginNG.Actions
{
    /// <summary>Arrange AUF / STOP / AB vertically and select the same item on all three keys.</summary>
    public abstract class RollershutterItem : ItemCommand
    {
        private readonly string _command;
        private readonly string _symbol;
        private readonly string _caption;
        protected RollershutterItem(string caption, string command, string symbol) : base("Rollladen " + caption, "Rollershutters")
        { _command = command; _symbol = symbol; _caption = caption; }
        protected override System.Collections.Generic.IEnumerable<OpenHABCommandItem> AvailableItems => Service.Rollershutters;
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            var item = ItemName(parameters);
            if (Service.GetItem(item) == null) return false;
            _ = Owner.ExecuteAsync(item, _command);
            return true;
        }
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height) => ItemPresentation.Render(width, height,
            Service.GetItem(ItemName(parameters))?.Label ?? "Select item", _caption + " · " + OpenHABService.FormatPercent(Service.GetStateOfItem(ItemName(parameters))), _symbol);
    }
    public class RollershutterUp : RollershutterItem { public RollershutterUp() : base("AUF", "UP", "arrowtriangle_up") { } }
    public class RollershutterStop : RollershutterItem { public RollershutterStop() : base("STOP", "STOP", "stop") { } }
    public class RollershutterDown : RollershutterItem { public RollershutterDown() : base("AB", "DOWN", "arrowtriangle_down") { } }
}
