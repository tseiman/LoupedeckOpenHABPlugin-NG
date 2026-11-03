using System;
using System.Threading;
using Loupedeck.Devices.Loupedeck2Devices;

namespace Loupedeck.OpenHABPluginNG.MultiWheel
{
    /// <summary>One CT wheel page: ATEM-style analog clock or tag-filtered scenes.</summary>
    public sealed class SceneClockWheelTool : WheelTool
    {
        private Timer _timer;
        private readonly object _drawSync = new();
        private OpenHABPlugin OwnerPlugin => this.Plugin as OpenHABPlugin;
        public SceneClockWheelTool() : base("Scenes / Clock", "OpenHAB NG") { }
        protected override void OnStart()
        {
            base.OnStart();
            if (OwnerPlugin != null) OwnerPlugin.SceneSelection.Changed += Changed;
            _timer = new Timer(_ => Redraw(), null, 1000, 1000);
            Redraw();
        }
        protected override void OnStop()
        {
            _timer?.Dispose(); _timer = null;
            if (OwnerPlugin != null)
            {
                OwnerPlugin.SceneSelection.Changed -= Changed;
                OwnerPlugin.SceneSelection.Cancel();
            }
            base.OnStop();
        }
        protected override void OnPause()
        {
            OwnerPlugin?.SceneSelection.Cancel();
            base.OnPause();
        }
        private void Changed(object sender, EventArgs e) => Redraw();
        private void Redraw()
        {
            lock (_drawSync)
            {
                try { this.Draw(); }
                catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Wheel redraw failed: " + ex.GetType().Name); }
            }
        }
        protected override BitmapImage CreateImage()
        {
            var selection = OwnerPlugin?.SceneSelection;
            if (selection?.Active != true) return ScenePresentation.RenderAnalogClock(DateTime.Now);
            return ScenePresentation.Render(512, 512, selection.Selected?.Name ?? selection.Message,
                selection.Position, true, OwnerPlugin.OHService.GetSceneIconSvg(), true);
        }
        protected override void OnEncoderEvent(DeviceEncoderEvent encoderEvent)
        {
            base.OnEncoderEvent(encoderEvent);
            if (encoderEvent != null) OwnerPlugin?.SceneSelection.Move(encoderEvent.Clicks);
        }
        protected override void OnTouchEvent(DeviceTouchEvent touchEvent)
        {
            base.OnTouchEvent(touchEvent);
            // The SDK emits distinct down/move/up events; only a completed single tap commits.
            if (touchEvent?.EventType == DeviceTouchEventType.Tap && OwnerPlugin != null)
                _ = OwnerPlugin.SceneSelection.ExecuteAsync();
        }
    }
}
