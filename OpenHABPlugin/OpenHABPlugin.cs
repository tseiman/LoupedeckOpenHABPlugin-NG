namespace Loupedeck.OpenHABPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// This class contains the plugin-level logic of the Loupedeck plugin. 
    /// </summary>
    public class OpenHABPlugin : Plugin
    {
        /// <summary>
        /// Gets a value indicating whether this is an Universal plugin or an Application plugin.
        /// </summary>
        public override Boolean UsesApplicationApiOnly => true;

        /// <summary>
        /// Gets a value indicating whether this is an API-only plugin.
        /// </summary>
        public override Boolean HasNoApplication => true;

        /// <summary>
        /// OpenHAB service controller
        /// </summary>
        public OpenHABService OHService { get; } = new OpenHABService();

        /// <summary>
        /// Plugin configuration file name
        /// </summary>
        private const String ConfigFileName = "config.json";

        /// <summary>
        /// Name of URL setting in configuration file
        /// </summary>
        private const String UrlSetting = "url";

        /// <summary>
        /// Name of token setting in configuration file
        /// </summary>
        private const String TokenSetting = "token";

        /// <summary>
        /// Base URL to be read from config.json file
        /// </summary>
        protected string _baseUrl = "";

        /// <summary>
        /// API token
        /// </summary>
        protected string _apiToken = "";

        /// <summary>
        /// This method is called when the plugin is loaded during the Loupedeck service start-up.
        /// Reads the plugin configuration and initializes the openHAB controller
        /// </summary>
        public override void Load()
        {
            Console.WriteLine("OH Plugin loading");
            var pluginDataDirectory = this.GetPluginDataDirectory();
            if (IoHelpers.EnsureDirectoryExists(pluginDataDirectory))
            {
                var filePath = Path.Combine(pluginDataDirectory, ConfigFileName);
                JObject jsonData;

                try
                {
                    jsonData = JObject.Parse(File.ReadAllText(filePath));
                    if (jsonData[UrlSetting] != null)
                    {
                        _baseUrl = jsonData[UrlSetting]!.ToString();
                    }
                    if (jsonData[TokenSetting] != null)
                    {
                        _apiToken = jsonData[TokenSetting].ToString();
                    }
                }
                catch (Exception ex)
                {
                    // Handle exception if there is any error while reading or parsing the JSON file
                    Console.WriteLine("Error reading JSON file: " + ex.Message);
                }
            }

            if (!string.IsNullOrEmpty(_baseUrl))
            {
                Console.WriteLine($"Initializing connection to URL: {_baseUrl}");
                OHService.Initialize(_baseUrl, _apiToken);
            }
        }

        /// <summary>
        /// This method is called when the plugin is unloaded during the Loupedeck service shutdown.
        /// </summary>
        public override void Unload()
        {
        }

        /// <summary>
        /// Configures the base openHAB url, writes it into the configuration file and reinitializes the openHAB service
        /// </summary>
        /// <param name="baseUrl">OpenHAB base URL</param>
        internal void SetBaseUrl(String baseUrl)
        {
            Console.WriteLine($"Setting base url: {baseUrl}");

            using (var client = new HttpClient())
            {
                var response = client.GetAsync(baseUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    // URL is reachable
                    var pluginDataDirectory = this.GetPluginDataDirectory();
                    if (IoHelpers.EnsureDirectoryExists(pluginDataDirectory))
                    {
                        var filePath = Path.Combine(pluginDataDirectory, ConfigFileName);
                        JObject jsonData = new JObject();
                        jsonData["url"] = baseUrl;
                        File.WriteAllText(filePath, jsonData.ToString());
                    }
                }
                else
                {
                    // URL is not reachable
                    Console.WriteLine($"Could not reach given url: {baseUrl}");
                }
            }

            OHService.Initialize(_baseUrl, _apiToken);
        }
    }
}
