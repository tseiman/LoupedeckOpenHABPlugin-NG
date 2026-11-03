using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Loupedeck.OpenHABPluginNG
{
    public sealed record OpenHABScene(string Uid, string Name, string[] Tags);

    public sealed partial class OpenHABService
    {
        private OpenHABScene[] _scenes = Array.Empty<OpenHABScene>();
        private readonly SemaphoreSlim _sceneRefresh = new(1, 1);
        private volatile bool _scenesRequested;
        public string SceneStatus { get; private set; } = "NOT CONFIGURED";
        public event EventHandler? ScenesChanged;
        public IReadOnlyList<OpenHABScene> Scenes { get { lock (_sync) return _scenes.ToArray(); } }
        public IEnumerable<string> SceneTags => Scenes.SelectMany(s => s.Tags).Where(t => t != "Scene")
            .Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase);
        public static OpenHABScene[] ParseScenes(JArray rules) => rules.OfType<JObject>()
            .Where(r => r["tags"] is JArray tags && tags.Values<string>().Contains("Scene", StringComparer.Ordinal))
            .Where(r => (string?)r["status"]?["status"] != "DISABLED")
            .Where(r => !string.IsNullOrWhiteSpace((string?)r["uid"]))
            .Select(r => new OpenHABScene((string)r["uid"]!, (string?)r["name"] ?? (string)r["uid"]!,
                r["tags"]!.Values<string>().OfType<string>().Distinct(StringComparer.Ordinal).ToArray()))
            .GroupBy(s => s.Uid, StringComparer.Ordinal).Select(g => g.First()).ToArray();
        public async Task RefreshScenesAsync(CancellationToken ct = default)
        {
            _scenesRequested = true;
            await _sceneRefresh.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var client = _http;
                var generation = _generation;
                var server = _baseUri;
                if (client == null || server == null) { ResetScenes("NOT CONFIGURED"); return; }
                try
                {
                    // Summary is available to normal users; full rule definitions require admin rights.
                    using var response = await client.GetAsync(new Uri(server, "rest/rules?tags=Scene&summary=true"), ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var scenes = ParseScenes(JArray.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)));
                    lock (_sync)
                    {
                        if (generation != _generation || !ReferenceEquals(client, _http) || ct.IsCancellationRequested) return;
                        _scenes = scenes; SceneStatus = "READY";
                    }
                    NotifyScenes();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    if (generation != _generation || !ReferenceEquals(client, _http)) return;
                    ResetScenes(ex is HttpRequestException request && request.StatusCode.HasValue
                        ? "HTTP " + (int)request.StatusCode.Value : "Scene catalog unavailable");
                }
            }
            finally { _sceneRefresh.Release(); }
        }
        public async Task RunSceneAsync(string uid)
        {
            var client = _http ?? throw new InvalidOperationException("Configure the server first.");
            var server = _baseUri ?? throw new InvalidOperationException("Configure the server first.");
            lock (_sync)
                if (SceneStatus != "READY" || !_scenes.Any(s => s.Uid == uid)) throw new ArgumentException("Unknown or unavailable scene.");
            using var body = new StringContent("{}", Encoding.UTF8, "application/json");
            // A single request, never retried: a timeout may still mean the server executed it.
            using var response = await client.PostAsync(new Uri(server, "rest/rules/" + Uri.EscapeDataString(uid) + "/runnow"), body, _stop?.Token ?? default).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        public string? GetSceneIconSvg() => _icons?.Get("__scene_icon__", "scene", "");
        private void ResetScenes(string status)
        {
            lock (_sync) { _scenes = Array.Empty<OpenHABScene>(); SceneStatus = status; }
            NotifyScenes();
        }
        private void NotifyScenes()
        {
            foreach (var callback in ScenesChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((EventHandler)callback)(this, EventArgs.Empty); } catch { }
        }
    }
}
