using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Loupedeck;
using Loupedeck.OpenHABPluginNG;
using Loupedeck.OpenHABPluginNG.Actions;
using Newtonsoft.Json.Linq;

// Load the installed host's own rendering dependencies, including its macOS bindings.
// Never copy these binaries into the plugin package.
var host = args.FirstOrDefault() ?? (OperatingSystem.IsMacOS()
    ? "/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle"
    : "C:/Program Files/Logi/LogiPluginService");
var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = new[] { Path.Combine(host, name.Name + ".dll"), Path.Combine(host, ".xamarin", arch, name.Name + ".dll") }.FirstOrDefault(File.Exists);
    return path == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
};
AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
{
    var path = new[] { name, name + ".dylib", name + ".dll" }.Select(n => Path.Combine(host, n)).FirstOrDefault(File.Exists);
    return path == null ? IntPtr.Zero : NativeLibrary.Load(Path.GetFullPath(path));
};
await Run();

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}
static object Call(object target, string method, params object[] values) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(target, values)!;
static ActionEditorActionParameters Parameters(string item) => new(new Dictionary<string, string> { ["Item"] = item, ["Group"] = "__all__" });

async Task Run()
{
    var plugin = new OpenHABPlugin();
    using var service = plugin.OHService;
    var handler = new TestApi();
    using var http = new HttpClient(handler);
    typeof(OpenHABService).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, http);
    typeof(OpenHABService).GetField("_baseUri", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, new Uri("http://openhab.test/"));
    await service.RefreshItemsAsync();
    Check(service.Dimmer.Select(i => i.Name).OrderBy(n => n).SequenceEqual(new[] { "Brightness", "Dimmer" }), "numeric brightness controls are included, sensors and read-only values excluded");
    var actions = new ActionEditorAction[] { new SwitchItem(), new LampItem(), new SocketItem(), new DimmerItem(), new DimmerSwitchItem(), new RollershutterUp(), new RollershutterStop(), new RollershutterDown(), new LabelItem() };
    foreach (var action in actions) typeof(ActionEditorAction).GetProperty("Plugin")!.SetValue(action, plugin);
    // Use the names actually exported by the SDK, as the host does. Its constructors
    // normalize the initial letter; hand-written "Group"/"Item" requests hid a regression.
    foreach (var action in actions)
    {
        var controls = ((IEnumerable<ActionEditorControlBase>)Call(action.ActionEditor, "GetControls")).ToArray();
        var groupName = controls.Single(c => c.Name.Equals("group", StringComparison.OrdinalIgnoreCase)).Name;
        var itemName = controls.Single(c => c.Name.Equals("item", StringComparison.OrdinalIgnoreCase)).Name;
        Check(groupName == "group" && itemName == "item", action.Name + " exposes normalized SDK control names");
        var hostState = new ActionEditorState(new[] { new ActionEditorControlState { Name = groupName, Value = "__all__" } });
        var groups = (ActionEditorListboxItemsRequestedEventArgs)Call(action.ActionEditor, "InvokeListboxItemsRequestedEvent", hostState, groupName);
        var items = (ActionEditorListboxItemsRequestedEventArgs)Call(action.ActionEditor, "InvokeListboxItemsRequestedEvent", hostState, itemName);
        Check(groups.Items.Any(i => i.Name == "__all__") && items.Items.Count > 0, action.Name + " populates real host group and item requests");
        if (action is DimmerItem || action is DimmerSwitchItem)
            Check(items.Items.Any(i => i.Name == "Brightness") && items.Items.All(i => i.Name != "Humidity" && i.Name != "ReadOnlyBrightness"), action.Name + " offers the numeric brightness item in its actual selector");
    }
    var shutter = actions.OfType<RollershutterDown>().Single();
    var state = new ActionEditorState(new[] { new ActionEditorControlState { Name = "Group", Value = "__all__" }, new ActionEditorControlState { Name = "Item", Value = "Blind" } });
    var groupOptions = (ActionEditorListboxItemsRequestedEventArgs)Call(shutter.ActionEditor, "InvokeListboxItemsRequestedEvent", state, "Group");
    Check(groupOptions.Items.Any(i => i.Name == "__all__"), "group selector offers all groups");
    var options = (ActionEditorListboxItemsRequestedEventArgs)Call(shutter.ActionEditor, "InvokeListboxItemsRequestedEvent", state, "Item");
    Check(options.Items.Select(i => i.Name).OrderBy(n => n).SequenceEqual(new[] { "AllBlinds", "Blind" }), "real SDK selector exposes shutter and typed group");
    var ungroupedState = new ActionEditorState(new[] { new ActionEditorControlState { Name = "Group", Value = "Others" } });
    var ungrouped = (ActionEditorListboxItemsRequestedEventArgs)Call(shutter.ActionEditor, "InvokeListboxItemsRequestedEvent", ungroupedState, "Item");
    Check(ungrouped.Items.Select(i => i.Name).SequenceEqual(new[] { "Blind" }), "ungrouped shutters remain selectable");
    var dimmer = actions.OfType<DimmerItem>().Single();
    Check((string)Call(dimmer, "GetAdjustmentDisplayName", Parameters("Dimmer")) == "Dimmer, West", "dial caption resolves the configured item, not a profile ID");
    Check((string)Call(dimmer, "GetCommandDisplayName", Parameters("missing")) == "Select dimmer", "unknown item never leaks an internal identifier");
    var lamp = actions.OfType<LampItem>().Single();
    using var lampOn = (BitmapImage)Call(lamp, "GetCommandImage", Parameters("Lamp"), 90, 90);
    handler.LampState = "OFF";
    await service.RefreshItemsAsync();
    using var lampOff = (BitmapImage)Call(lamp, "GetCommandImage", Parameters("Lamp"), 90, 90);
    Check(!lampOn.ToArray().SequenceEqual(lampOff.ToArray()), "configured lamp action changes its image with the confirmed state");
    foreach (var action in actions.OfType<RollershutterItem>()) Call(action, "RunCommand", Parameters("Blind"));
    Check(handler.Commands.Select(c => c.Command).SequenceEqual(new[] { "UP", "STOP", "DOWN" }), "SDK shutter actions send their own commands to the selected item");
    foreach (var source in new[] { "0", "36", "100" })
    {
        handler.DimmerState = source;
        await service.RefreshItemsAsync();
        Call(actions.OfType<DimmerSwitchItem>().Single(), "RunCommand", Parameters("Dimmer"));
        Check(handler.Commands.Last() == ("Dimmer", source == "0" ? "100" : "0"), "SDK dimmer button sends 0/100 for " + source);
    }
    Call(dimmer, "RunCommand", Parameters("Dimmer"));
    Check(handler.Commands.Last() == ("Dimmer", "0"), "dial press uses the same explicit toggle rule");
    Call(actions.OfType<DimmerSwitchItem>().Single(), "RunCommand", Parameters("Brightness"));
    Check(handler.Commands.Last() == ("Brightness", "0"), "numeric brightness item receives explicit dimmer command");
    var render = typeof(OpenHABPlugin).Assembly.GetType("Loupedeck.OpenHABPluginNG.Actions.ItemPresentation")!.GetMethod("Render", new[] { typeof(int), typeof(int), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(string) })!;
    var connectionRender = render.DeclaringType!.GetMethod("RenderConnection", BindingFlags.Static | BindingFlags.NonPublic)!;
    foreach (var test in new[] { (OpenHABConnectionHealth.Healthy, (byte)54, (byte)201, (byte)107), (OpenHABConnectionHealth.Degraded, (byte)255, (byte)171, (byte)50), (OpenHABConnectionHealth.Disconnected, (byte)255, (byte)69, (byte)69) })
    {
        using var image = (BitmapImage)connectionRender.Invoke(null, new object[] { 90, 90, test.Item1.ToString(), test.Item1 })!;
        Check(HasColor(image, test.Item2, test.Item3, test.Item4), "status icon has the correct color for " + test.Item1);
    }
    void Connection(bool ws, string rest) { typeof(OpenHABService).GetField("_connected", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, ws); typeof(OpenHABService).GetField("_restStatus", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, rest); }
    Connection(true, "REST");
    Check(service.ConnectionHealth == OpenHABConnectionHealth.Healthy && service.ConnectionStatusText == "LIVE", "live WebSocket and working item retrieval are healthy");
    Connection(true, "HTTP 401");
    Check(service.ConnectionHealth == OpenHABConnectionHealth.Degraded && service.ConnectionStatusText.Contains("HTTP 401"), "WebSocket alone cannot hide failed item retrieval");
    Connection(false, "REST");
    Check(service.ConnectionHealth == OpenHABConnectionHealth.Degraded, "REST fallback is orange");
    Connection(false, "TLS ERROR");
    Check(service.ConnectionHealth == OpenHABConnectionHealth.Disconnected, "no working connection is red");
    Connection(false, "REST");
    var output = Path.Combine(AppContext.BaseDirectory, "previews");
    Directory.CreateDirectory(output);
    foreach (var size in new[] { (90, 90), (60, 60), (160, 40), (48, 48), (40, 80), (60, 30), (20, 50) })
    {
        using var image = (BitmapImage)Call(dimmer, "GetAdjustmentImage", Parameters("Dimmer"), size.Item1, size.Item2);
        Check(image.Width == size.Item1 && image.Height == size.Item2, $"SDK dimmer image uses {size.Item1}x{size.Item2}");
        image.SaveToFile(Path.Combine(output, $"dimmer-{size.Item1}x{size.Item2}.png"));
    }
    BitmapImage Render(string value, string symbol, bool active, string svg = null) => (BitmapImage)render.Invoke(null, new object[] { 90, 90, "Kitchen light", value, symbol, active, svg })!;
    var abbreviate = render.DeclaringType!.GetMethod("AbbreviateDimmerLabel", BindingFlags.Static | BindingFlags.NonPublic)!;
    Check((string)abbreviate.Invoke(null, new object[] { "Dimmer, West" })! == "…West" &&
        (string)abbreviate.Invoke(null, new object[] { "Ost" })! == "Ost", "dimmer heading retains at most the final four text elements");
    var solidIcon = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'><rect width='64' height='64' fill='#00ff00'/></svg>";
    using var shortCaption = (BitmapImage)render.Invoke(null, new object[] { 90, 90, "Ost", "0 %", "arrowtriangle_up", false, solidIcon })!;
    using var longCaption = (BitmapImage)render.Invoke(null, new object[] { 90, 90, "Rolladen mit einem sehr langen Raumnamen", "0 %", "arrowtriangle_up", false, solidIcon })!;
    Check(Pixels(shortCaption).Select((c, i) => (c, i)).Where(p => p.c == 0xff00ff00u).Select(p => p.i)
        .SequenceEqual(Pixels(longCaption).Select((c, i) => (c, i)).Where(p => p.c == 0xff00ff00u).Select(p => p.i)),
        "caption length does not change icon size or position");
    using var off = Render("0 %", "lamp", false);
    using var on = Render("36 %", "lamp", true);
    using var again = Render("36 %", "lamp", true);
    Check(!off.ToArray().SequenceEqual(on.ToArray()) && on.ToArray().SequenceEqual(again.ToArray()), "state change redraws while identical renders reuse cached pixels");
    // Exercise the actual host compositor, which previously added a second caption
    // and shrank an otherwise correct plugin bitmap.
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
    var metadata = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../bin", configuration, "metadata/DefaultIconTemplate.ict"));
    var shared = Assembly.Load("LoupedeckShared");
    var iconType = shared.GetType("Loupedeck.Service.ActionIcon")!;
    object[] readArguments = { metadata, null! };
    Check((bool)iconType.GetMethod("TryReadFromFile")!.Invoke(null, readArguments)!, "host reads the packaged full-key icon template");
    var iconTemplate = readArguments[1];
    iconType.GetMethod("SetImage", new[] { typeof(BitmapImage) })!.Invoke(iconTemplate, new object[] { on });
    iconType.GetMethod("SetText", new[] { typeof(string) })!.Invoke(iconTemplate, new object[] { "test" });
    object[] textArguments = { null! };
    Check(!(bool)iconType.GetMethod("TryGetTextItem")!.Invoke(iconTemplate, textArguments)!, "host cannot add a second caption to the image-only template");
    var builderType = shared.GetType("Loupedeck.Service.ActionIconBuilder")!;
    var compose = builderType.GetMethods().Single(m => m.Name == "CreateImage" && m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(int));
    var flagsType = compose.GetParameters()[3].ParameterType;
    using var composed = (BitmapImage)compose.Invoke(null, new object[] { 90, 90, iconTemplate, Enum.ToObject(flagsType, 0) })!;
    Check(Pixels(on).SequenceEqual(Pixels(composed)), "real host compositor preserves the full-size image and adds no test caption");
    Check(Pixels(composed).First() == 0xff000000u, "full-key background is opaque black");
    composed.SaveToFile(Path.Combine(output, "host-full-key.png"));
    using var stop = (BitmapImage)Call(actions.OfType<RollershutterStop>().Single(), "GetCommandImage", Parameters("Blind"), 90, 90);
    Check(HasColor(stop, 255, 69, 69), "SDK preserves red F7 STOP icon");
    Check(HasColor(on, 255, 209, 85), "SDK preserves active lamp color");
    using var serverIcon = Render("36 %", "lamp", true, "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'><rect width='32' height='64' fill='#00ff00'/><rect x='32' width='32' height='64' fill='#0066ff'/></svg>");
    Check(HasColor(serverIcon, 0, 255, 0) && HasColor(serverIcon, 0, 102, 255), "SDK preserves original server SVG colors");
    using var malformed = Render("36 %", "lamp", true, "<svg broken");
    Check(malformed.Width == 90, "malformed server SVG uses local fallback");
    on.SaveToFile(Path.Combine(output, "lamp-on.png")); off.SaveToFile(Path.Combine(output, "lamp-off.png")); stop.SaveToFile(Path.Combine(output, "stop.png"));
    Console.WriteLine("All host SDK tests passed. Previews: " + output);
}

static bool HasColor(BitmapImage image, byte red, byte green, byte blue)
{
    var type = Assembly.Load("SkiaSharp").GetType("SkiaSharp.SKBitmap")!;
    using var bitmap = (IDisposable)type.GetMethod("Decode", new[] { typeof(byte[]) })!.Invoke(null, new object[] { image.ToArray() })!;
    var pixels = (Array)type.GetProperty("Pixels")!.GetValue(bitmap)!;
    return pixels.Cast<object>().Any(p => (byte)p.GetType().GetProperty("Red")!.GetValue(p)! == red && (byte)p.GetType().GetProperty("Green")!.GetValue(p)! == green && (byte)p.GetType().GetProperty("Blue")!.GetValue(p)! == blue);
}

static IEnumerable<uint> Pixels(BitmapImage image)
{
    var type = Assembly.Load("SkiaSharp").GetType("SkiaSharp.SKBitmap")!;
    using var bitmap = (IDisposable)type.GetMethod("Decode", new[] { typeof(byte[]) })!.Invoke(null, new object[] { image.ToArray() })!;
    var pixels = (Array)type.GetProperty("Pixels")!.GetValue(bitmap)!;
    return pixels.Cast<object>().Select(p => (uint)p.GetType().GetMethod("op_Explicit", new[] { p.GetType() })!.Invoke(null, new[] { p })!).ToArray();
}

sealed class TestApi : HttpMessageHandler
{
    public string DimmerState = "36";
    public string LampState = "ON";
    public List<(string Name, string Command)> Commands { get; } = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method == HttpMethod.Post) Commands.Add((request.RequestUri!.Segments.Last(), await request.Content!.ReadAsStringAsync(ct)));
        var items = new JArray(new[] { ("Lamp", "Switch", LampState), ("Socket", "Switch", "OFF"), ("Dimmer", "Dimmer", DimmerState), ("Blind", "Rollershutter", "50"), ("AllBlinds", "Group", "25"), ("Brightness", "Number", "36"), ("Humidity", "Number:Dimensionless", "65"), ("ReadOnlyBrightness", "Number", "20") }.Select(i => new JObject
        {
            ["name"] = i.Item1, ["type"] = i.Item2, ["groupType"] = i.Item1 == "AllBlinds" ? "Rollershutter" : null,
            ["label"] = i.Item1 == "Dimmer" ? "Dimmer, West" : i.Item1, ["state"] = i.Item3, ["groupNames"] = i.Item1 == "Blind" ? new JArray() : new JArray("Ground floor"),
            ["tags"] = i.Item1.Contains("Brightness") ? new JArray("Control", "Brightness") : new JArray("Measurement"),
            ["stateDescription"] = new JObject { ["readOnly"] = i.Item1 == "ReadOnlyBrightness" }
        }));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(items.ToString()) };
    }
}
