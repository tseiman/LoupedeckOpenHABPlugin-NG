using System;
namespace Loupedeck.OpenHABPluginNG.Actions
{
    public class SetOpenHABUrl : PluginDynamicCommand
    {
        public SetOpenHABUrl() : base("Set OpenHAB URL", "Configure the OpenHAB NG server", "Settings")
        { this.MakeProfileAction("text;OpenHAB server URL:"); }
        protected override async void RunCommand(string parameter)
        {
            try { await ((OpenHABPlugin)this.Plugin).SetBaseUrlAsync(parameter); }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] URL configuration failed: " + ex.GetType().Name); }
        }
    }
    public class SetOpenHABToken : PluginDynamicCommand
    {
        public SetOpenHABToken() : base("Set OpenHAB API token", "Configure authentication for REST and WebSocket", "Settings")
        { this.MakeProfileAction("text;API token (empty to clear):"); }
        protected override async void RunCommand(string parameter)
        {
            try { await ((OpenHABPlugin)this.Plugin).SetTokenAsync(parameter ?? ""); }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Token configuration failed: " + ex.GetType().Name); }
        }
    }
}
