using System;
namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class ConnectionStatus : PluginDynamicCommand
    {
        private OpenHABService Service => ((OpenHABPlugin)this.Plugin).OHService;
        public ConnectionStatus() : base("Connection status", "WebSocket live / REST fallback", "Settings") { this.IsWidget = true; }
        protected override bool OnLoad()
        { var result = base.OnLoad(); Service.ConnectionChanged += Changed; return result; }
        protected override bool OnUnload()
        { Service.ConnectionChanged -= Changed; return base.OnUnload(); }
        private void Changed(object sender, EventArgs e) => this.ActionImageChanged();
        protected override BitmapImage GetCommandImage(string parameter, PluginImageSize size) => ItemPresentation.Render(size,
            "OpenHAB NG", Service.ConnectionStatusText, "switch", Service.IsWebSocketConnected || Service.IsRestConnected);
    }
}
