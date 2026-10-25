using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Loupedeck.OpenHABPluginNG
{
    /// <summary>One connection session; only confirmed server states enter the cache.</summary>
    public sealed partial class OpenHABService : IDisposable
    {
        private readonly object _sync = new object();
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _refresh = new SemaphoreSlim(1, 1);
        private Dictionary<string, OpenHABCommandItem> _items = new Dictionary<string, OpenHABCommandItem>();
        private readonly Dictionary<string, long> _versions = new Dictionary<string, long>();
        private long _revision;
        private int _generation;
        private CancellationTokenSource? _stop;
        private Task? _worker;
        private HttpClient? _http;
        private Uri? _baseUri;
        private string _token = "";
        private OpenHABIconCache? _icons;
        private volatile bool _connected;
        public bool IsWebSocketConnected => _connected;
        public event EventHandler<OpenHABEventArgs>? ItemChanged;
        public event EventHandler? ConnectionChanged;
        public event EventHandler? CatalogChanged;
        public IEnumerable<OpenHABCommandItem> Items { get { lock (_sync) return _items.Values.ToArray(); } }
        public IEnumerable<OpenHABCommandItem> Switches => Items.Where(i => i.EffectiveType == "Switch");
        public IEnumerable<OpenHABCommandItem> Dimmer => Items.Where(i => i.IsDimmer);
        public IEnumerable<OpenHABCommandItem> Rollershutters => Items.Where(i => i.EffectiveType == "Rollershutter");
        public IEnumerable<OpenHABCommandItem> Labels => Items;
        private volatile string _restStatus = "NOT CONFIGURED";
        public string ConnectionStatusText => _connected && IsRestConnected ? "LIVE" : _connected ? "WS / " + _restStatus : _restStatus;
        public bool IsRestConnected => _restStatus == "REST";
        public OpenHABConnectionHealth ConnectionHealth => _connected && IsRestConnected ? OpenHABConnectionHealth.Healthy :
            _connected || IsRestConnected || _restStatus == "CONNECTING" ? OpenHABConnectionHealth.Degraded : OpenHABConnectionHealth.Disconnected;

        public static Uri ValidateBaseUrl(string url)
        {
            if (!Uri.TryCreate(url.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Use an http(s) URL without credentials, query or fragment.");
            return uri;
        }

        public async Task ConfigureAsync(string url, string token)
        {
            var uri = ValidateBaseUrl(url);
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopCoreAsync().ConfigureAwait(false);
                _baseUri = uri; _token = token.Trim();
                SetRestStatus("CONNECTING");
                // The API uses explicit tokens, never browser cookies.
                _http = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(10) };
                if (_token.Length > 0) _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                lock (_sync) { _items.Clear(); _versions.Clear(); _revision = 0; _generation++; }
                _stop = new CancellationTokenSource();
                var ct = _stop.Token;
                var generation = _generation;
                _icons = new OpenHABIconCache(_http, uri, ct, name =>
                {
                    if (generation == _generation && GetItem(name) is OpenHABCommandItem item) Notify(item);
                });
                _worker = Task.Run(() => RunAsync(ct));
            }
            finally { _lifecycle.Release(); }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            await TryRefreshAsync(ct).ConfigureAwait(false);
            await Task.WhenAll(WebSocketLoopAsync(ct), PollLoopAsync(ct)).ConfigureAwait(false);
        }
        private async Task TryRefreshAsync(CancellationToken ct)
        {
            try { await RefreshItemsAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var status = ex is HttpRequestException request && request.StatusCode.HasValue ? "HTTP " + (int)request.StatusCode.Value :
                    ex is OperationCanceledException ? "TIMEOUT" :
                    ex.InnerException is System.Security.Authentication.AuthenticationException || ex.InnerException is System.Security.Cryptography.CryptographicException ? "TLS ERROR" : "REST ERROR";
                SetRestStatus(status);
                Log("Item refresh failed: " + status + " (" + ex.GetType().Name + ")");
            }
        }
        public async Task RefreshItemsAsync(CancellationToken ct = default)
        {
            await _refresh.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                long before;
                int generation;
                lock (_sync) { before = _revision; generation = _generation; }
                var client = _http ?? throw new InvalidOperationException("Configure the server first.");
                using var response = await client.GetAsync(new Uri(_baseUri!, "rest/items?recursive=false&metadata=listWidget"), ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var data = JArray.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                var changed = new List<OpenHABCommandItem>();
                var catalogChanged = false;
                lock (_sync)
                {
                    if (generation != _generation || ct.IsCancellationRequested) return;
                    var next = new Dictionary<string, OpenHABCommandItem>();
                    foreach (var json in data.OfType<JObject>())
                    {
                        var name = (string?)json["name"];
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        _items.TryGetValue(name, out var previous);
                        var state = (string?)json["state"] ?? "NULL";
                        // Do not overwrite an event received after the REST request started.
                        if (_versions.TryGetValue(name, out var version) && version > before && previous != null) state = previous.State ?? "NULL";
                        var item = new OpenHABCommandItem
                        {
                            Name = name, Link = new Uri(_baseUri!, "rest/items/" + Uri.EscapeDataString(name)).ToString(),
                            Label = (string?)json["label"] ?? name, Type = (string?)json["type"], GroupType = (string?)json["groupType"], State = state,
                            Group = (string?)json["groupNames"]?.FirstOrDefault() ?? "Others",
                            Category = (string?)json["category"], Pattern = (string?)json["stateDescription"]?["pattern"],
                            Tags = json["tags"]?.Values<string>().Where(t => t != null).Select(t => t!).ToArray() ?? Array.Empty<string>(),
                            ReadOnly = (bool?)json["stateDescription"]?["readOnly"] ?? false,
                            ShutterFeedbackItem = json["metadata"] is JObject metadata && metadata["listWidget"] is JObject widget &&
                                widget["config"] is JObject widgetConfig && widgetConfig["stateItem"]?.Type == JTokenType.String
                                ? (string?)widgetConfig["stateItem"] : null
                        };
                        next[name] = item;
                        var directionChanged = ApplyRollershutterStateLocked(item, state, before);
                        if (previous == null || previous.Label != item.Label || previous.Group != item.Group || previous.EffectiveType != item.EffectiveType || previous.IsDimmer != item.IsDimmer || previous.ShutterFeedbackItem != item.ShutterFeedbackItem) catalogChanged = true;
                        if (directionChanged || previous == null || previous.State != state || previous.Label != item.Label || previous.Category != item.Category || previous.Group != item.Group || previous.EffectiveType != item.EffectiveType || previous.IsDimmer != item.IsDimmer || previous.ShutterFeedbackItem != item.ShutterFeedbackItem) changed.Add(item);
                    }
                    foreach (var removed in _items.Keys.Except(next.Keys))
                    {
                        catalogChanged = true;
                        _shutterDirections.Remove(removed);
                        _shutterCommandVersions.Remove(removed);
                        changed.Add(new OpenHABCommandItem { Name = removed, State = "NULL" });
                    }
                    _items = next;
                    foreach (var missing in _versions.Keys.Except(next.Keys).ToArray()) _versions.Remove(missing);
                }
                foreach (var item in changed) Notify(item);
                if (catalogChanged)
                    foreach (var handler in CatalogChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                        try { ((EventHandler)handler)(this, EventArgs.Empty); } catch { }
                SetRestStatus("REST");
            }
            finally { _refresh.Release(); }
        }
        private async Task PollLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Fallback without WS; reconciliation repairs missed events as well.
                    await Task.Delay(TimeSpan.FromSeconds(_connected ? 30 : 5), ct).ConfigureAwait(false);
                    await TryRefreshAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }
        private async Task WebSocketLoopAsync(CancellationToken ct)
        {
            var delay = 2;
            while (!ct.IsCancellationRequested)
            {
                foreach (var endpoint in new[] { "ws/events", "ws" })
                {
                    if (ct.IsCancellationRequested) break;
                    using var socket = new ClientWebSocket();
                    using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    try
                    {
                        if (_token.Length > 0)
                        {
                            socket.Options.AddSubProtocol("org.openhab.ws.protocol.default");
                            socket.Options.AddSubProtocol("org.openhab.ws.accessToken.base64." + Convert.ToBase64String(Encoding.UTF8.GetBytes(_token)).TrimEnd('='));
                        }
                        var builder = new UriBuilder(new Uri(_baseUri!, endpoint));
                        builder.Scheme = _baseUri!.Scheme == "https" ? "wss" : "ws";
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(10));
                            await socket.ConnectAsync(builder.Uri, timeout.Token).ConfigureAwait(false);
                        }
                        SetConnected(true); delay = 2;
                        Log("WebSocket connected (" + endpoint + ").");
                        // The server buffers events during initial reconciliation.
                        await TryRefreshAsync(ct).ConfigureAwait(false);
                        var receive = ReceiveAsync(socket, session.Token);
                        var heartbeat = HeartbeatAsync(socket, session.Token);
                        await Task.WhenAny(receive, heartbeat).ConfigureAwait(false);
                        session.Cancel(); socket.Abort();
                        try { await Task.WhenAll(receive, heartbeat).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                    catch (Exception ex) { Log("WebSocket unavailable (" + endpoint + "): " + ex.GetType().Name); }
                    finally { SetConnected(false); }
                }
                try { await Task.Delay(TimeSpan.FromSeconds(delay), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                delay = Math.Min(delay * 2, 30);
            }
        }
        private async Task HeartbeatAsync(ClientWebSocket socket, CancellationToken ct)
        {
            var message = Encoding.UTF8.GetBytes(new JObject
            {
                ["type"] = "WebSocketEvent", ["topic"] = "openhab/websocket/heartbeat",
                ["payload"] = "PING", ["source"] = "LoupedeckOpenHABPlugin-NG"
            }.ToString(Formatting.None));
            while (!ct.IsCancellationRequested)
            {
                await socket.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
        }
        private async Task ReceiveAsync(ClientWebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[4096];
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    if (message.Length + result.Count > 1024 * 1024) throw new InvalidDataException("WebSocket message exceeds limit.");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Text) ProcessEvent(Encoding.UTF8.GetString(message.ToArray()));
            }
        }
        public bool ProcessEvent(string message)
        {
            try
            {
                var envelope = JObject.Parse(message);
                var type = (string?)envelope["type"];
                if (type != "ItemStateEvent" && type != "ItemStateChangedEvent" && type != "ItemCommandEvent") return false;
                var parts = ((string?)envelope["topic"] ?? "").Split('/');
                if (parts.Length != 4 || parts[0] != "openhab" || parts[1] != "items" ||
                    (type == "ItemCommandEvent" ? parts[3] != "command" : parts[3] != "state" && parts[3] != "statechanged")) return false;
                var raw = envelope["payload"];
                var payload = raw is JObject obj ? obj : JObject.Parse(raw?.ToString() ?? "{}");
                var value = (string?)payload["value"];
                return value != null && (type == "ItemCommandEvent" ? RecordRollershutterCommand(parts[2], value) : UpdateState(parts[2], value));
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidCastException)
            { Log("Ignored malformed WebSocket event."); return false; }
        }
        private bool UpdateState(string name, string state)
        {
            OpenHABCommandItem? item;
            lock (_sync)
            {
                if (!_items.TryGetValue(name, out item)) return false;
                _versions[name] = ++_revision;
                var directionChanged = ApplyRollershutterStateLocked(item, state);
                if (item.State == state && !directionChanged) return true;
                item.State = state;
            }
            Notify(item); return true;
        }
        public OpenHABCommandItem? GetItem(string parameter)
        {
            lock (_sync)
            {
                if (_items.TryGetValue(parameter, out var item)) return item;
                return _items.Values.FirstOrDefault(i => i.Link == parameter);
            }
        }
        public string? GetStateOfItem(string parameter) => GetItem(parameter)?.State;
        public string GetItemLabel(string parameter) => GetItem(parameter)?.Label ?? "Select item";
        public string? GetCachedIconSvg(string parameter, string? categoryOverride = null)
        {
            var item = GetItem(parameter);
            if (item?.Name == null || item.State == null) return null;
            var category = categoryOverride ?? item.Category;
            if (string.IsNullOrWhiteSpace(category)) return null;
            var state = item.State;
            if (category == "light" || item.IsDimmer)
            {
                if (state == "ON") state = "100";
                else if (state == "OFF") state = "0";
            }
            return _icons?.Get(item.Name, category, state);
        }
        public async Task SendCommandAsync(string parameter, string command)
        {
            var item = GetItem(parameter) ?? throw new ArgumentException("Unknown item.");
            var ct = _stop?.Token ?? default;
            await PostItemCommandAsync(item, command, ct).ConfigureAwait(false);
            // Commands (UP/STOP/ON) are not confirmed position or switch states.
            await RefreshItemsAsync(ct).ConfigureAwait(false);
        }
        private async Task PostItemCommandAsync(OpenHABCommandItem item, string command, CancellationToken ct)
        {
            var client = _http ?? throw new InvalidOperationException("Configure the server first.");
            using var body = new StringContent(command, Encoding.UTF8, "text/plain");
            using var response = await client.PostAsync(item.Link, body, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (ReferenceEquals(client, _http) && item.Name != null) RecordRollershutterCommand(item.Name, command);
        }
        private void Notify(OpenHABCommandItem item)
        {
            foreach (var handler in ItemChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((EventHandler<OpenHABEventArgs>)handler)(this, new OpenHABEventArgs(item.Name!, item.State ?? "NULL", "")); }
                catch (Exception ex) { Log("Display notification failed: " + ex.GetType().Name); }
        }
        private void SetConnected(bool connected)
        {
            _connected = connected;
            foreach (var handler in ConnectionChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((EventHandler)handler)(this, EventArgs.Empty); } catch { }
        }
        private static void Log(string message) => Console.WriteLine("[OpenHAB NG] " + message);
        public static double ExtractNumericalValue(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return double.NaN;
            var match = Regex.Match(input, @"[-+]?[0-9]*\.?[0-9]+");
            return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
        }
        public static string? DimmerToggleCommand(string? state)
        {
            var value = state == "ON" ? 100 : state == "OFF" ? 0 : ExtractNumericalValue(state);
            return double.IsNaN(value) || value < 0 || value > 100 ? null : value > 0 ? "0" : "100";
        }
        public static string FormatPercent(string? state)
        {
            if (state == "ON") return "100 %";
            if (state == "OFF") return "0 %";
            var value = ExtractNumericalValue(state);
            return double.IsNaN(value) ? "—" : Math.Clamp(value, 0, 100).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        }
        public async Task StopAsync()
        {
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try { await StopCoreAsync().ConfigureAwait(false); }
            finally { _lifecycle.Release(); }
        }
        private void SetRestStatus(string status)
        {
            if (_restStatus == status) return;
            _restStatus = status;
            foreach (var handler in ConnectionChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((EventHandler)handler)(this, EventArgs.Empty); } catch { }
        }
        private async Task StopCoreAsync()
        {
            _stop?.Cancel();
            if (_worker != null) try { await _worker.ConfigureAwait(false); } catch (OperationCanceledException) { }
            if (_icons != null) await _icons.StopAsync().ConfigureAwait(false);
            _icons = null;
            _http?.Dispose(); _http = null;
            _stop?.Dispose(); _stop = null; _worker = null;
            SetConnected(false);
            SetRestStatus("DISCONNECTED");
            ResetRollershutterDirections();
        }
        public void Dispose() => StopAsync().GetAwaiter().GetResult();
    }
    public enum OpenHABConnectionHealth { Healthy, Degraded, Disconnected }
}
