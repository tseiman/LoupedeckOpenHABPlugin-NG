using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using LoupedeckWebConfigLib;
using Newtonsoft.Json.Linq;

namespace Loupedeck.OpenHABPluginNG
{
    /// <summary>A per-plugin, configuration-only local UI. No device command endpoints.</summary>
    public sealed class OpenHABWebConfiguration : IDisposable
    {
        private readonly OpenHABService _service;
        private readonly Func<JObject> _read;
        private readonly Action<JObject> _save;
        private readonly LoupedeckWebConfigService _server;
        private readonly string _session = Guid.NewGuid().ToString("N");
        private readonly object _sync = new object();
        private bool _publishing;
        public OpenHABWebConfiguration(OpenHABService service, Func<JObject> read, Action<JObject> save, bool openBrowser = true)
        {
            _service = service; _read = read; _save = save;
            _server = new LoupedeckWebConfigService(new LoupedeckWebConfigOptions
            {
                OpenBrowser = openBrowser,
                Log = entry => { if (entry.Level >= LoupedeckWebConfigLogLevel.Warning) Console.WriteLine("[OpenHAB NG] Web configuration warning."); }
            });
        }
        public Uri Open()
        {
            lock (_sync)
            {
                _publishing = true;
                try
                {
                    _server.RegisterPlugin(new LoupedeckPluginRegistration("openhab-ng", "OpenHAB NG", "OpenHAB NG configuration",
                        HtmlSnippet: BuildHtml(), ConfigurationKey: "openhab-ng-settings"), Apply);
                    Publish();
                    return _server.ActivateConfig();
                }
                finally { _publishing = false; }
            }
        }
        public void RefreshSnapshot()
        {
            lock (_sync)
            {
                if (!_server.IsActive) return;
                _publishing = true;
                try { Publish(); } finally { _publishing = false; }
            }
        }
        private void Publish()
        {
            var saved = _read();
            var values = _service.FeedbackValues;
            var snapshot = new JsonObject
            {
                ["session"] = _session, ["url"] = (string)saved["url"] ?? "", ["token"] = "", ["clearToken"] = false,
                ["stopped"] = values.Stopped, ["up"] = values.Up, ["down"] = values.Down
            };
            var mappings = _service.GetRollershutterFeedbackAssignments();
            foreach (var item in _service.Rollershutters)
                snapshot["feedback__" + item.Name] = mappings.TryGetValue(item.Name, out var mapped) ? mapped : "__auto__";
            _server.UpdatePluginConfiguration(snapshot);
        }
        private void Apply(JsonNode configuration)
        {
            lock (_sync)
            {
                if (_publishing || configuration == null) return;
                if (configuration["session"]?.GetValue<string>() != _session) throw new ArgumentException("Invalid configuration session.");
                var url = OpenHABService.ValidateBaseUrl(configuration["url"]?.GetValue<string>() ?? "").ToString();
                var values = new RollershutterFeedbackValues(configuration["stopped"]?.GetValue<string>(), configuration["up"]?.GetValue<string>(), configuration["down"]?.GetValue<string>());
                var current = _service.GetRollershutterFeedbackAssignments();
                var assignments = new Dictionary<string, string>(current);
                foreach (var item in _service.Rollershutters)
                {
                    if (configuration["feedback__" + item.Name] == null) continue;
                    var selected = configuration["feedback__" + item.Name].GetValue<string>();
                    if (selected == "__auto__") assignments.Remove(item.Name);
                    else
                    {
                        var feedback = _service.GetItem(selected);
                        if (feedback?.EffectiveType != "Number" && feedback?.EffectiveType != "String")
                        {
                            if (!current.TryGetValue(item.Name, out var old) || old != selected) throw new ArgumentException("Select a Number or String feedback item.");
                        }
                        assignments[item.Name] = selected;
                    }
                }
                var change = new JObject { ["url"] = url, ["rollershutterFeedbackValues"] = values.ToJson(), ["rollershutterFeedbackAssignments"] = JObject.FromObject(assignments) };
                var token = configuration["token"]?.GetValue<string>() ?? "";
                var clearToken = configuration["clearToken"]?.GetValue<bool>() ?? false;
                if (clearToken && !string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Either replace or clear the token.");
                if (clearToken || token.Length > 0) change["token"] = token.Trim();
                _save(change);
                _publishing = true;
                try { Publish(); } finally { _publishing = false; }
            }
        }
        private string BuildHtml()
        {
            string E(string value) => WebUtility.HtmlEncode(value ?? "");
            var saved = _read();
            var html = new StringBuilder();
            html.Append("<input type=\"hidden\" lwcl-config-key=\"session\"><h3>Connection</h3>");
            html.Append("<label>OpenHAB URL<input type=\"url\" lwcl-config-key=\"url\" lwcl-check-regex=\"https?://.+\" placeholder=\"https://openhab.example:8443\"></label>");
            html.Append("<p>API token: ").Append(string.IsNullOrEmpty((string)saved["token"]) ? "not configured" : "configured (never displayed)").Append("</p>");
            html.Append("<label>Replace API token<input type=\"password\" autocomplete=\"new-password\" lwcl-config-key=\"token\" placeholder=\"Leave blank to keep the saved token\"></label>");
            html.Append("<label style=\"display:flex;align-items:center;gap:8px\"><input style=\"width:auto;margin:0\" type=\"checkbox\" lwcl-config-key=\"clearToken\"> Clear the saved API token</label>");
            html.Append("<h3>Movement values — shared by all rollershutters</h3><p>Default: stopped = 0, up = 1, down = 2. Number and String feedback items are supported.</p>");
            html.Append("<div style=\"display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:16px\">");
            foreach (var field in new[] { ("stopped", "Stopped"), ("up", "Moving up"), ("down", "Moving down") })
                html.Append("<label>").Append(field.Item2).Append("<input lwcl-config-key=\"").Append(field.Item1).Append("\" lwcl-check-regex=\".*\\S.*\"></label>");
            html.Append("</div>");
            html.Append("<h3>Rollershutter feedback assignments</h3><p>Automatic uses the OpenHAB MainUI widget assignment. Overrides below are saved only in this plugin. Per-key Loupedeck overrides take precedence.</p>");
            html.Append("<p>Read-only snapshot: ").Append(E(_service.ConnectionStatusText)).Append(". Reopen configuration from Loupedeck to refresh the catalog and displayed states. No device controls are provided here.</p>");
            html.Append("<div style=\"overflow-x:auto\"><table style=\"width:100%;text-align:left\"><thead><tr><th>Rollershutter</th><th>Movement feedback item</th><th>Reported state</th></tr></thead><tbody>");
            var candidates = _service.Items.Where(i => i.EffectiveType == "Number" || i.EffectiveType == "String").OrderBy(i => i.Label).ToArray();
            var mappings = _service.GetRollershutterFeedbackAssignments();
            foreach (var item in _service.Rollershutters.OrderBy(i => i.Label))
            {
                var automatic = _service.GetItem(item.ShutterFeedbackItem ?? "");
                html.Append("<tr><td>").Append(E(item.Label)).Append("<br><small>").Append(E(item.Name)).Append("</small></td><td><select lwcl-config-key=\"feedback__").Append(E(item.Name)).Append("\"><option value=\"__auto__\">Automatic — ").Append(E(automatic?.Label ?? "no MainUI assignment")).Append("</option>");
                foreach (var feedback in candidates)
                    html.Append("<option value=\"").Append(E(feedback.Name)).Append("\">").Append(E(feedback.Label)).Append(" [").Append(E(feedback.Name)).Append("]</option>");
                if (mappings.TryGetValue(item.Name, out var mapped) && !candidates.Any(i => i.Name == mapped))
                    html.Append("<option value=\"").Append(E(mapped)).Append("\">Missing item: ").Append(E(mapped)).Append("</option>");
                var effective = _service.GetItem(_service.GetRollershutterFeedbackItem(item.Name));
                html.Append("</select></td><td>").Append(E(effective?.State ?? "Not assigned")).Append("</td></tr>");
            }
            html.Append("</tbody></table></div><script>window.registerLoupedeckPluginAutoConfig();</script>");
            return html.ToString();
        }
        public void Dispose() => _server.Dispose();
    }
}
