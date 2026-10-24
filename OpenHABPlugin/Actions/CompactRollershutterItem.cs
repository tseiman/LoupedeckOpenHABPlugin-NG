using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loupedeck.OpenHABPluginNG.Actions
{
    public abstract class CompactRollershutterItem : ItemCommand
    {
        private readonly RollershutterDirection _direction;
        private readonly string _caption;
        private readonly string _symbol;
        protected CompactRollershutterItem(string caption, RollershutterDirection direction, string symbol)
            : base("Rollladen " + caption + "/STOP", "Rollershutters (compact)")
        { _caption = caption; _direction = direction; _symbol = symbol; }
        protected override IEnumerable<OpenHABCommandItem> AvailableItems => Service.Rollershutters;
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            var name = ItemName(parameters);
            if (Service.GetItem(name)?.EffectiveType != "Rollershutter") return false;
            _ = ExecuteAsync(name);
            return true;
        }
        private async Task ExecuteAsync(string name)
        {
            try { await Service.ActivateRollershutterAsync(name, _direction).ConfigureAwait(false); }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Compact rollershutter command failed: " + ex.GetType().Name); }
        }
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height)
        {
            var name = ItemName(parameters);
            var active = Service.GetRollershutterDirection(name) == _direction;
            return ItemPresentation.Render(width, height, Service.GetItem(name)?.Label ?? "Select item",
                (active ? "STOP" : _caption) + " · " + OpenHABService.FormatPercent(Service.GetStateOfItem(name)),
                active ? "stop" : _symbol);
        }
    }
    public sealed class CompactRollershutterUp : CompactRollershutterItem
    { public CompactRollershutterUp() : base("AUF", RollershutterDirection.Up, "arrowtriangle_up") { } }
    public sealed class CompactRollershutterDown : CompactRollershutterItem
    { public CompactRollershutterDown() : base("AB", RollershutterDirection.Down, "arrowtriangle_down") { } }
}
