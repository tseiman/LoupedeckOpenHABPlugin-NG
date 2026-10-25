using System;
namespace Loupedeck.OpenHABPluginNG.Actions
{
    public sealed class OpenWebConfiguration : PluginDynamicCommand
    {
        public OpenWebConfiguration() : base("Open Configuration", "Open the local OpenHAB NG configuration page", "Settings") { }
        protected override async void RunCommand(string parameter)
        {
            try { await ((OpenHABPlugin)this.Plugin).OpenWebConfigurationAsync(); }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Could not open configuration: " + ex.GetType().Name); }
        }
    }
}
