using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Loupedeck.OpenHABPluginNG;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Newtonsoft.Json.Linq;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}
static async Task Until(Func<bool> condition, string message, int seconds = 12)
{
    var end = DateTime.UtcNow.AddSeconds(seconds);
    while (!condition() && DateTime.UtcNow < end) await Task.Delay(25);
    Check(condition(), message);
}
static string Event(string name, string state, string type = "ItemStateEvent") => new JObject
{
    ["type"] = type, ["topic"] = "openhab/items/" + name + (type == "ItemStateEvent" ? "/state" : "/statechanged"),
    ["payload"] = new JObject { ["type"] = "String", ["value"] = state }.ToString()
}.ToString();

Console.WriteLine("Starting local integration server...");
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders();
builder.Services.AddRouting();
builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
await using var server = builder.Build();
server.UseWebSockets();
var states = new ConcurrentDictionary<string, string>(new[] {
    new KeyValuePair<string,string>("Lamp", "OFF"), new("Socket", "ON"), new("Dimmer", "37"), new("Blind", "50") });
var commands = new ConcurrentQueue<(string Name, string Command)>();
var sockets = new ConcurrentDictionary<int, WebSocket>();
var sends = new ConcurrentDictionary<int, SemaphoreSlim>();
var connections = 0;
var restRequests = 0;
var heartbeats = 0;
var authRequests = 0;
var legacyOnly = false;
var rejectSockets = false;
var failCommands = false;
var holdSnapshot = false;
TaskCompletionSource snapshotStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
TaskCompletionSource releaseSnapshot = new(TaskCreationOptions.RunContinuationsAsynchronously);

server.MapGet("/rest/items", async context =>
{
    if (context.Request.Headers.Authorization != "Bearer test-token") { context.Response.StatusCode = 401; return; }
    Interlocked.Increment(ref authRequests);
    Interlocked.Increment(ref restRequests);
    var data = new JArray(states.OrderBy(p => p.Key).Select(p => new JObject
    {
        ["name"] = p.Key, ["type"] = p.Key == "Dimmer" ? "Dimmer" : p.Key == "Blind" ? "Rollershutter" : "Switch",
        ["label"] = p.Key, ["category"] = p.Key == "Socket" ? "poweroutlet" : "light",
        ["state"] = p.Value, ["groupNames"] = new JArray("Others")
    })).ToString();
    if (holdSnapshot) { snapshotStarted.TrySetResult(); await releaseSnapshot.Task; }
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(data);
});
server.MapPost("/rest/items/{name}", async (HttpContext context, string name) =>
{
    if (context.Request.Headers.Authorization != "Bearer test-token") { context.Response.StatusCode = 401; return; }
    var command = await new StreamReader(context.Request.Body).ReadToEndAsync();
    commands.Enqueue((name, command));
    if (failCommands) { context.Response.StatusCode = 500; return; }
    // Physical state deliberately does not follow commands immediately.
    context.Response.StatusCode = 202;
});
async Task AcceptSocket(HttpContext context)
{
    if (rejectSockets || (legacyOnly && context.Request.Path == "/ws/events")) { context.Response.StatusCode = 404; return; }
    if (!context.WebSockets.WebSocketRequestedProtocols.Contains("org.openhab.ws.accessToken.base64." + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-token")).TrimEnd('=')))
    { context.Response.StatusCode = 401; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync("org.openhab.ws.protocol.default");
    var id = Interlocked.Increment(ref connections);
    sockets[id] = socket;
    var sendGate = new SemaphoreSlim(1, 1);
    sends[id] = sendGate;
    var buffer = new byte[4096];
    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;
            var message = JObject.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if ((string?)message["payload"] == "PING")
            {
                Interlocked.Increment(ref heartbeats);
                await sendGate.WaitAsync();
                try
                {
                    var pong = Encoding.UTF8.GetBytes("{\"type\":\"WebSocketEvent\",\"topic\":\"openhab/websocket/heartbeat\",\"payload\":\"PONG\"}");
                    await socket.SendAsync(pong, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                finally { sendGate.Release(); }
            }
        }
    }
    catch (WebSocketException) { }
    finally { sockets.TryRemove(id, out _); sends.TryRemove(id, out _); }
}
server.Map("/ws/events", AcceptSocket);
server.Map("/ws", AcceptSocket);
Console.WriteLine("Binding loopback listener...");
await server.StartAsync();
var address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

async Task Push(string json, bool fragmented = false)
{
    var connection = sockets.OrderByDescending(p => p.Key).First();
    var socket = connection.Value;
    var gate = sends[connection.Key];
    var bytes = Encoding.UTF8.GetBytes(json);
    await gate.WaitAsync();
    try
    {
    if (fragmented)
    {
        var half = bytes.Length / 2;
        await socket.SendAsync(bytes.AsMemory(0, half), WebSocketMessageType.Text, false, CancellationToken.None);
        await socket.SendAsync(bytes.AsMemory(half), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    else await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    finally { gate.Release(); }
}

Console.WriteLine("Local integration server ready.");
using var service = new OpenHABService();
var notifications = new ConcurrentQueue<OpenHABEventArgs>();
service.ItemChanged += (_, e) => notifications.Enqueue(e);
await service.ConfigureAsync(address, "test-token");
await Until(() => service.Items.Count() == 4 && service.IsWebSocketConnected && heartbeats > 0, "REST snapshot, authenticated WS and heartbeat");
await Until(() => restRequests >= 2, "snapshot reconciled after WebSocket connection");
Check(service.GetStateOfItem("Lamp") == "OFF" && authRequests >= 2, "initial confirmed state");

states["Lamp"] = "ON";
await Push(Event("Lamp", "ON"), true);
await Until(() => service.GetStateOfItem("Lamp") == "ON", "fragmented state event updates cache");
Check(notifications.Any(e => e.Link == "Lamp" && e.State == "ON"), "display notification uses the configured item name");
var prior = notifications.Count;
await Push(Event("Lamp", "ON"));
await Task.Delay(100);
Check(notifications.Count == prior, "duplicate state does not toggle or repaint");
await Push("malformed JSON");
states["Dimmer"] = "62";
await Push(Event("Dimmer", "62", "ItemStateChangedEvent"));
await Until(() => service.GetStateOfItem("Dimmer") == "62", "malformed event ignored; statechanged event handled");
Check(!service.ProcessEvent(Event("Unknown", "ON")), "unrelated item ignored");
Check(!service.ProcessEvent(new JObject { ["type"] = "ItemCommandEvent", ["topic"] = "openhab/items/Blind/command", ["payload"] = "{\"value\":\"UP\"}" }.ToString()), "command events do not masquerade as states");

await service.SendCommandAsync("Lamp", "OFF");
foreach (var command in new[] { "UP", "STOP", "DOWN" }) await service.SendCommandAsync("Blind", command);
Check(commands.Any(c => c == ("Lamp", "OFF")) && commands.Where(c => c.Name == "Blind").Select(c => c.Command).SequenceEqual(new[] { "UP", "STOP", "DOWN" }), "switch and shutter commands reach correct REST endpoint");
Check(service.GetStateOfItem("Lamp") == "ON" && service.GetStateOfItem("Blind") == "50", "commands never overwrite confirmed physical states");
failCommands = true;
try { await service.SendCommandAsync("Lamp", "OFF"); throw new Exception("Expected HTTP failure"); }
catch (HttpRequestException) { Check(service.GetStateOfItem("Lamp") == "ON", "failed command preserves confirmed state"); }
failCommands = false;

holdSnapshot = true;
var refresh = service.RefreshItemsAsync();
await snapshotStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
states["Lamp"] = "OFF";
await Push(Event("Lamp", "OFF"));
await Until(() => service.GetStateOfItem("Lamp") == "OFF", "event received during slow REST request");
holdSnapshot = false; releaseSnapshot.TrySetResult(); await refresh;
Check(service.GetStateOfItem("Lamp") == "OFF", "stale REST response cannot undo newer WS state");

Check(OpenHABService.FormatPercent("37") == "37 %" && OpenHABService.FormatPercent("37.5 %") == "37.5 %" &&
    OpenHABService.FormatPercent("ON") == "100 %" && OpenHABService.FormatPercent("OFF") == "0 %" &&
    OpenHABService.FormatPercent("UNDEF") == "—" && OpenHABService.FormatPercent(null) == "—", "dimmer percentage and undefined states");
var culture = System.Globalization.CultureInfo.CurrentCulture;
System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
Check(OpenHABService.ExtractNumericalValue("12.5 W") == 12.5, "numbers use invariant OpenHAB formatting");
System.Globalization.CultureInfo.CurrentCulture = culture;

var beforeReconnect = connections;
foreach (var socket in sockets.Values) socket.Abort();
await Until(() => connections > beforeReconnect && service.IsWebSocketConnected, "automatic reconnect after connection loss");
legacyOnly = true;
await service.ConfigureAsync(address, "test-token");
await Until(() => service.IsWebSocketConnected, "legacy /ws fallback when /ws/events returns 404");
await Until(() => sockets.Count == 1, "reconfiguration leaves only one active session");

await service.StopAsync();
rejectSockets = true;
states["Socket"] = "ON";
await service.ConfigureAsync(address, "test-token");
await Until(() => service.GetStateOfItem("Socket") == "ON", "REST initial state with WebSocket unavailable");
states["Socket"] = "OFF";
await Until(() => service.GetStateOfItem("Socket") == "OFF", "five-second REST polling fallback");
await service.StopAsync();
Check(!service.IsWebSocketConnected, "stop disconnects the session");
var requestsAfterStop = restRequests;
await Task.Delay(5500);
Check(restRequests == requestsAfterStop, "no polling continues after unload");
await server.StopAsync();
Console.WriteLine("All integration checks passed.");
