using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Loupedeck.OpenHABPluginNG
{
    public class OpenHABPlugin : Plugin
    {
        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => true;
        public OpenHABService OHService { get; } = new OpenHABService();
        private string _url = "";
        private string _token = "";
        internal string ConfigurationFile { get; set; }
        private string ConfigPath => ConfigurationFile ?? Path.Combine(this.GetPluginDataDirectory(), "config.json");
        private readonly object _configurationSync = new object();
        private OpenHABWebConfiguration _webConfiguration;

        public override void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var data = JObject.Parse(File.ReadAllText(ConfigPath));
                    _url = (string)data["url"] ?? "";
                    _token = (string)data["token"] ?? "";
                    if (data["rollershutterFeedbackAssignments"] is JObject assignments)
                        OHService.SetRollershutterFeedbackAssignments(assignments.ToObject<Dictionary<string, string>>());
                    if (data["rollershutterFeedbackValues"] is JObject feedback)
                    {
                        try { OHService.SetRollershutterFeedbackValues(RollershutterFeedbackValues.FromJson(feedback)); }
                        catch (ArgumentException) { Console.WriteLine("[OpenHAB NG] Invalid movement mapping; using 0/1/2 defaults."); }
                    }
                }
                if (!string.IsNullOrWhiteSpace(_url)) OHService.ConfigureAsync(_url, _token).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Configuration failed: " + ex.GetType().Name); }
        }
        public override void Unload()
        {
            _webConfiguration?.Dispose();
            OHService.Dispose();
        }
        internal Task SetBaseUrlAsync(string url)
        {
            ApplyWebConfiguration(new JObject { ["url"] = OpenHABService.ValidateBaseUrl(url).ToString() });
            return Task.CompletedTask;
        }
        internal Task SetTokenAsync(string token)
        {
            ApplyWebConfiguration(new JObject { ["token"] = token.Trim() });
            return Task.CompletedTask;
        }
        internal void SetRollershutterFeedbackValues(RollershutterFeedbackValues values) =>
            ApplyWebConfiguration(new JObject { ["rollershutterFeedbackValues"] = values.ToJson() });

        internal async Task OpenWebConfigurationAsync()
        {
            if (OHService.IsRestConnected)
                try { await OHService.RefreshItemsAsync().ConfigureAwait(false); }
                catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Configuration uses cached catalog: " + ex.GetType().Name); }
            lock (_configurationSync)
            {
                _webConfiguration ??= new OpenHABWebConfiguration(OHService, ReadConfigurationSnapshot, ApplyWebConfiguration);
            }
            _webConfiguration.Open();
        }
        private JObject ReadConfigurationSnapshot()
        {
            lock (_configurationSync) return new JObject { ["url"] = _url, ["token"] = _token };
        }
        private void ApplyWebConfiguration(JObject change)
        {
            lock (_configurationSync)
            {
                var url = (string)change["url"] ?? _url;
                if (!string.IsNullOrWhiteSpace(url)) url = OpenHABService.ValidateBaseUrl(url).ToString();
                var token = (string)change["token"] ?? _token;
                var values = change["rollershutterFeedbackValues"] is JObject mapping ? RollershutterFeedbackValues.FromJson(mapping) : OHService.FeedbackValues;
                var assignments = change["rollershutterFeedbackAssignments"] is JObject links
                    ? links.ToObject<Dictionary<string, string>>() : new Dictionary<string, string>(OHService.GetRollershutterFeedbackAssignments());
                var data = File.Exists(ConfigPath) ? JObject.Parse(File.ReadAllText(ConfigPath)) : new JObject();
                data["url"] = url; data["token"] = token;
                data["rollershutterFeedbackValues"] = values.ToJson();
                data["rollershutterFeedbackAssignments"] = JObject.FromObject(assignments);
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                var temporary = ConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, data.ToString());
                    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, File.Exists(ConfigPath)
                        ? File.GetUnixFileMode(ConfigPath) : UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    File.Move(temporary, ConfigPath, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                var reconnect = url != _url || token != _token;
                _url = url; _token = token;
                OHService.SetRollershutterFeedbackAssignments(assignments);
                OHService.SetRollershutterFeedbackValues(values);
                if (reconnect && url.Length > 0) OHService.ConfigureAsync(url, token).GetAwaiter().GetResult();
            }
        }
        internal async Task ExecuteAsync(string item, string command)
        {
            try { await OHService.SendCommandAsync(item, command).ConfigureAwait(false); }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Command failed: " + ex.GetType().Name); }
        }
    }
}
