using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;

namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class DimmerItem : ActionEditorAdjustment
    {
        private OpenHABPlugin Owner => (OpenHABPlugin)this.Plugin;
        private OpenHABService Service => Owner.OHService;
        private sealed class Pending
        {
            public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
            public double Target;
            public DateTime LastInput;
        }
        private readonly ConcurrentDictionary<string, Pending> _pending = new ConcurrentDictionary<string, Pending>();
        public DimmerItem() : base(true)
        {
            this.Name = "DimmerItem";
            this.DisplayName = "Dimmer (%)"; this.GroupName = "Lights"; this.IsWidget = true;
            ItemEditor.Configure(this.ActionEditor, () => Service.Dimmer);
        }
        protected override bool OnLoad()
        {
            var result = base.OnLoad(); Service.ItemChanged += Changed; Service.CatalogChanged += CatalogChanged; return result;
        }
        protected override bool OnUnload()
        {
            Service.ItemChanged -= Changed; Service.CatalogChanged -= CatalogChanged; return base.OnUnload();
        }
        private void Changed(object sender, OpenHABEventArgs e)
        {
            this.AdjustmentValueChanged(); this.ActionImageChanged();
        }
        private void CatalogChanged(object sender, EventArgs e)
        { this.ActionEditor.ListboxItemsChanged("Group"); this.ActionEditor.ListboxItemsChanged("Item"); }
        private static string ItemName(ActionEditorActionParameters parameters) => parameters.GetString("Item", "");
        protected override bool ApplyAdjustment(ActionEditorActionParameters parameters, int difference)
        {
            var parameter = ItemName(parameters);
            if (Service.GetItem(parameter) == null) return false;
            _ = AdjustAsync(parameter, difference);
            return true;
        }
        private async System.Threading.Tasks.Task AdjustAsync(string parameter, int difference)
        {
            var pending = _pending.GetOrAdd(parameter, _ => new Pending());
            await pending.Gate.WaitAsync();
            try
            {
                var state = Service.GetStateOfItem(parameter);
                var current = state == "ON" ? 100 : state == "OFF" ? 0 : OpenHABService.ExtractNumericalValue(state);
                if (double.IsNaN(current)) return;
                // Accumulate quick turns even when device feedback is delayed.
                if ((DateTime.UtcNow - pending.LastInput).TotalSeconds > 2) pending.Target = current;
                pending.Target = Math.Clamp(pending.Target + difference, 0, 100);
                pending.LastInput = DateTime.UtcNow;
                await Owner.ExecuteAsync(parameter, pending.Target.ToString("0", CultureInfo.InvariantCulture));
            }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Dimmer failed: " + ex.GetType().Name); }
            finally { pending.Gate.Release(); }
        }
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            var parameter = ItemName(parameters);
            _pending.TryRemove(parameter, out _);
            var command = OpenHABService.DimmerToggleCommand(Service.GetStateOfItem(parameter));
            if (command == null) return false;
            _ = Owner.ExecuteAsync(parameter, command);
            return true;
        }
        protected override string GetAdjustmentDisplayName(ActionEditorActionParameters parameters) => Service.GetItem(ItemName(parameters))?.Label ?? "Select dimmer";
        protected override string GetCommandDisplayName(ActionEditorActionParameters parameters) => GetAdjustmentDisplayName(parameters);
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height) => Image(ItemName(parameters), width, height);
        protected override BitmapImage GetAdjustmentImage(ActionEditorActionParameters parameters, int width, int height) => Image(ItemName(parameters), width, height);
        private BitmapImage Image(string parameter, int width, int height) => ItemPresentation.Render(width, height,
            Service.GetItem(parameter)?.Label ?? "Select dimmer", OpenHABService.FormatPercent(Service.GetStateOfItem(parameter)), "dimmer",
            OpenHABService.ExtractNumericalValue(Service.GetStateOfItem(parameter)) > 0 || Service.GetStateOfItem(parameter) == "ON",
            Service.GetCachedIconSvg(parameter, string.IsNullOrWhiteSpace(Service.GetItem(parameter)?.Category) ? "light" : Service.GetItem(parameter).Category));
    }
}
