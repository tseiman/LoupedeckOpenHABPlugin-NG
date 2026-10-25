using System;
using System.Collections.Generic;
using System.Linq;
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
        {
            _caption = caption; _direction = direction; _symbol = symbol;
            this.ActionEditor.AddControlEx(new ActionEditorListbox("feedback", "Movement feedback (optional):"));
            this.ActionEditor.ListboxItemsRequested += (_, e) =>
            {
                if (!string.Equals(e.ControlName, "feedback", StringComparison.OrdinalIgnoreCase)) return;
                e.AddItem("__auto__", "Automatic (OpenHAB widget assignment)", null);
                foreach (var item in Service.Items.Where(i => i.EffectiveType == "Number" || i.EffectiveType == "String").OrderBy(i => i.Label))
                    e.AddItem(item.Name, item.Label ?? item.Name, item.Name);
            };
        }
        protected override bool OnLoad()
        {
            var result = base.OnLoad();
            Service.CatalogChanged += FeedbackCatalogChanged;
            return result;
        }
        protected override bool OnUnload()
        {
            Service.CatalogChanged -= FeedbackCatalogChanged;
            return base.OnUnload();
        }
        private void FeedbackCatalogChanged(object sender, EventArgs e) => this.ActionEditor.ListboxItemsChanged("feedback");
        protected override IEnumerable<OpenHABCommandItem> AvailableItems => Service.Rollershutters;
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            var name = ItemName(parameters);
            if (Service.GetItem(name)?.EffectiveType != "Rollershutter") return false;
            _ = ExecuteAsync(name, parameters.GetString("feedback", ""));
            return true;
        }
        private async Task ExecuteAsync(string name, string feedback)
        {
            try { await Service.ActivateRollershutterAsync(name, _direction, feedback).ConfigureAwait(false); }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Compact rollershutter command failed: " + ex.GetType().Name); }
        }
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height)
        {
            var name = ItemName(parameters);
            var direction = Service.GetRollershutterDirection(name, parameters.GetString("feedback", ""));
            var active = direction == _direction;
            return ItemPresentation.Render(width, height, Service.GetItem(name)?.Label ?? "Select item",
                direction == RollershutterDirection.Unknown ? "STATE ?" :
                    Service.GetRollershutterFeedbackItem(name, parameters.GetString("feedback", "")).Length > 0 ? (active ? "STOP" : _caption) :
                    (active ? "STOP" : _caption) + " · " + OpenHABService.FormatPercent(Service.GetStateOfItem(name)),
                active ? "stop" : _symbol);
        }
    }
    public sealed class CompactRollershutterUp : CompactRollershutterItem
    { public CompactRollershutterUp() : base("AUF", RollershutterDirection.Up, "arrowtriangle_up") { } }
    public sealed class CompactRollershutterDown : CompactRollershutterItem
    { public CompactRollershutterDown() : base("AB", RollershutterDirection.Down, "arrowtriangle_down") { } }
}
