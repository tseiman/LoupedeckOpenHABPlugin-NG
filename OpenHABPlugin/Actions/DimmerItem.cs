using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;

namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class DimmerItem : PluginDynamicAdjustment
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
            this.DisplayName = "Dimmer (%)"; this.GroupName = "Lights"; this.IsWidget = true;
            this.MakeProfileAction("tree;Select dimmer:");
        }
        protected override bool OnLoad()
        {
            var result = base.OnLoad(); Service.ItemChanged += Changed; return result;
        }
        protected override bool OnUnload()
        {
            Service.ItemChanged -= Changed; return base.OnUnload();
        }
        private void Changed(object sender, OpenHABEventArgs e)
        {
            this.AdjustmentValueChanged(e.Link); this.ActionImageChanged(e.Link);
        }
        protected override PluginProfileActionData GetProfileActionData() => ItemPresentation.Tree("Select dimmer", Service.Dimmer);
        protected override async void ApplyAdjustment(string parameter, int difference)
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
        protected override async void RunCommand(string parameter)
        {
            _pending.TryRemove(parameter, out _);
            var state = Service.GetStateOfItem(parameter);
            var value = state == "ON" ? 100 : state == "OFF" ? 0 : OpenHABService.ExtractNumericalValue(state);
            if (!double.IsNaN(value)) await Owner.ExecuteAsync(parameter, value > 0 ? "OFF" : "ON");
        }
        protected override string GetAdjustmentValue(string parameter) => OpenHABService.FormatPercent(Service.GetStateOfItem(parameter));
        protected override BitmapImage GetCommandImage(string parameter, PluginImageSize size) => Image(parameter, size);
        protected override BitmapImage GetAdjustmentImage(string parameter, PluginImageSize size) => Image(parameter, size);
        private BitmapImage Image(string parameter, PluginImageSize size) => ItemPresentation.Render(size,
            Service.GetItemLabel(parameter), GetAdjustmentValue(parameter), "dimmer",
            OpenHABService.ExtractNumericalValue(Service.GetStateOfItem(parameter)) > 0 || Service.GetStateOfItem(parameter) == "ON",
            Service.GetCachedIconSvg(parameter, string.IsNullOrWhiteSpace(Service.GetItem(parameter)?.Category) ? "light" : Service.GetItem(parameter).Category));
    }
}
