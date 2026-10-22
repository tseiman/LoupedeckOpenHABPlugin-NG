using System;
using System.IO;
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
        private string ConfigPath => Path.Combine(this.GetPluginDataDirectory(), "config.json");

        public override void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var data = JObject.Parse(File.ReadAllText(ConfigPath));
                    _url = (string)data["url"] ?? "";
                    _token = (string)data["token"] ?? "";
                }
                if (!string.IsNullOrWhiteSpace(_url)) OHService.ConfigureAsync(_url, _token).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Configuration failed: " + ex.GetType().Name); }
        }
        public override void Unload() => OHService.Dispose();

        internal async Task SetBaseUrlAsync(string url)
        {
            _url = OpenHABService.ValidateBaseUrl(url).ToString();
            Save();
            await OHService.ConfigureAsync(_url, _token).ConfigureAwait(false);
        }
        internal async Task SetTokenAsync(string token)
        {
            _token = token.Trim();
            Save();
            if (_url.Length > 0) await OHService.ConfigureAsync(_url, _token).ConfigureAwait(false);
        }
        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            var data = File.Exists(ConfigPath) ? JObject.Parse(File.ReadAllText(ConfigPath)) : new JObject();
            data["url"] = _url;
            data["token"] = _token;
            File.WriteAllText(ConfigPath, data.ToString());
        }
        internal async Task ExecuteAsync(string item, string command)
        {
            try { await OHService.SendCommandAsync(item, command).ConfigureAwait(false); }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Command failed: " + ex.GetType().Name); }
        }
    }
}
