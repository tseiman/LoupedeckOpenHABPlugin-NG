using System.Net;
using System.Reflection;
using Loupedeck;
using Loupedeck.Devices.Loupedeck2Devices;
using Loupedeck.OpenHABPluginNG;
using Loupedeck.OpenHABPluginNG.Actions;
using Loupedeck.OpenHABPluginNG.MultiWheel;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

#nullable enable
static class SceneTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception("FAIL: " + text); Console.WriteLine("PASS: " + text); }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args)!;
    static async Task Until(Func<bool> predicate)
    {
        for (var i = 0; i < 200 && !predicate(); i++) await Task.Delay(10);
        Check(predicate(), "scene asynchronous operation completed");
    }
    public static async Task Run()
    {
        var plugin = new OpenHABPlugin();
        using var service = plugin.OHService;
        using var selection = plugin.SceneSelection;
        var api = new SceneApi();
        using var http = new HttpClient(api);
        typeof(OpenHABService).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, http);
        typeof(OpenHABService).GetField("_baseUri", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, new Uri("http://openhab.test/"));
        await service.RefreshScenesAsync();
        Check(service.Scenes.Count == 3 && service.SceneTags.SequenceEqual(new[] { "ATT_Scene", "GF_Scene" }), "catalog includes native Scene rules only, excludes disabled scenes and discovers tags");
        Check(api.CatalogUris.All(p => p == "/rest/rules?tags=Scene&summary=true"), "scene discovery requests normal-user summaries only");
        var key = new SceneSelect();
        typeof(ActionEditorAction).GetProperty("Plugin")!.SetValue(key, plugin);
        var state = new ActionEditorState(Array.Empty<ActionEditorControlState>());
        var options = (ActionEditorListboxItemsRequestedEventArgs)Call(key.ActionEditor, "InvokeListboxItemsRequestedEvent", state, "tag");
        Check(options.Items.Select(i => i.Name).SequenceEqual(new[] { "ATT_Scene", "GF_Scene" }), "real SDK widget editor offers discovered scene tags");
        var parameters = new ActionEditorActionParameters(new Dictionary<string, string> { ["tag"] = "GF_Scene" });
        using var off = (BitmapImage)Call(key, "GetCommandImage", parameters, 90, 90);
        Check(IsGray(off), "OFF scene key renders the supplied logo in grayscale");
        Check(!(bool)Call(key, "RunCommand", new ActionEditorActionParameters(new Dictionary<string, string>())), "unconfigured key cannot enable arbitrary scenes");
        Call(key, "RunCommand", parameters);
        await Until(() => selection.Selected != null);
        Check(selection.Selected!.Uid == "alpha" && selection.Position == "1 / 2", "tag-filtered scenes are sorted by visible name");
        using var on = (BitmapImage)Call(key, "GetCommandImage", parameters, 90, 90);
        Check(!IsGray(on) && api.Runs.Count == 0, "ON scene key is colored and browsing sends no command");
        selection.Move(-1);
        Check(selection.Selected!.Uid == "zeta", "reverse wheel motion wraps to the last scene");
        selection.Move(3);
        Check(selection.Selected!.Uid == "alpha", "multi-click wheel motion wraps correctly");
        var wheel = new SceneClockWheelTool();
        typeof(WheelTool).GetProperty("Plugin", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(wheel, plugin);
        var output = Path.Combine(AppContext.BaseDirectory, "previews"); Directory.CreateDirectory(output);
        using var wheelScene = (BitmapImage)Call(wheel, "CreateImage");
        File.WriteAllBytes(Path.Combine(output, "scene-wheel.png"), wheelScene.ToArray());
        selection.Move(-1);
        using var longLabel = (BitmapImage)Call(wheel, "CreateImage");
        File.WriteAllBytes(Path.Combine(output, "scene-wheel-long.png"), longLabel.ToArray());
        selection.Move(1);
        File.WriteAllBytes(Path.Combine(output, "scene-key-off.png"), off.ToArray());
        File.WriteAllBytes(Path.Combine(output, "scene-key-on.png"), on.ToArray());
        foreach (var kind in new[] { DeviceTouchEventType.TouchDown, DeviceTouchEventType.Move, DeviceTouchEventType.TouchUp, DeviceTouchEventType.DoubleTap, DeviceTouchEventType.LongPress })
            Call(wheel, "OnTouchEvent", new DeviceTouchEvent(0, default, "test", 0, 0, kind, 0, 250, 250, 0, 0));
        Check(api.Runs.Count == 0 && selection.Active, "down/move/up, double taps and long presses never execute scenes");
        api.HoldRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tap = new DeviceTouchEvent(0, default, "test", 0, 0, DeviceTouchEventType.Tap, 0, 250, 250, 0, 0);
        Call(wheel, "OnTouchEvent", tap);
        Call(wheel, "OnTouchEvent", tap);
        Check(!selection.Active && api.Runs.SequenceEqual(new[] { "/rest/rules/alpha/runnow" }), "a single tap consumes the selection and repeated events cannot duplicate the POST");
        using var clock = (BitmapImage)Call(wheel, "CreateImage");
        File.WriteAllBytes(Path.Combine(output, "scene-clock.png"), clock.ToArray());
        Check(!clock.ToArray().SequenceEqual(wheelScene.ToArray()), "after execution the wheel immediately returns to the analog clock");
        api.HoldRun.SetResult();
        await Until(() => selection.Message == "");
        Check(api.Bodies.Single() == "{}" && api.ContentTypes.Single() == "application/json", "scene execution sends an empty JSON context to the native run endpoint");
        await selection.ToggleAsync("GF_Scene");
        await selection.ToggleAsync("GF_Scene");
        Check(!selection.Active && api.Runs.Count == 1, "pressing the ON key again cancels without executing");
        await selection.ToggleAsync("ATT_Scene");
        Check(selection.Selected!.Uid == "att", "another configured key changes the active tag");
        api.FailRun = true;
        Check(!await selection.ExecuteAsync() && !selection.Active && selection.Message == "HTTP 403", "failed scene POST returns to OFF, reports error and is never retried");
        api.FailRun = false;
        await selection.ToggleAsync("empty");
        Check(selection.Active && selection.Selected == null && !await selection.ExecuteAsync(), "an empty tag cannot execute a scene");
        selection.Cancel();
        api.HoldCatalog = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = selection.ToggleAsync("GF_Scene");
        await selection.ToggleAsync("GF_Scene");
        api.HoldCatalog.SetResult(); await pending;
        Check(!selection.Active, "late catalog responses cannot reactivate a cancelled selection");
        api.FailCatalog = true;
        await selection.ToggleAsync("GF_Scene");
        Check(selection.Selected == null && selection.Message == "HTTP 401", "catalog authentication failure clears stale choices");
        selection.Cancel(); api.FailCatalog = false;
        await selection.ToggleAsync("GF_Scene");
        api.Rules = "[]";
        await service.RefreshScenesAsync();
        Check(selection.Selected == null && !await selection.ExecuteAsync(), "removed scenes disappear while browsing and cannot execute");
        Check(api.Runs.Count == 2, "scene tests sent exactly the two intentional requests to the fake API");
    }
    static bool IsGray(BitmapImage bitmap)
    {
        using var image = Image.Load<Rgba32>(bitmap.ToArray());
        for (var y = 0; y < image.Height; y++) for (var x = 0; x < image.Width; x++)
        { var pixel = image[x,y]; if (pixel.A > 0 && (pixel.R != pixel.G || pixel.G != pixel.B)) return false; }
        return true;
    }
    sealed class SceneApi : HttpMessageHandler
    {
        public string Rules = """
        [{"uid":"zeta","name":"Unten schlafen alle Lichter aus","tags":["Scene","GF_Scene"]},
         {"uid":"alpha","name":"Mittagsschlaf EG","tags":["Scene","GF_Scene"]},
         {"uid":"att","name":"DG Hitzeschutz","tags":["Scene","ATT_Scene"]},
         {"uid":"ordinary","name":"Other rule","tags":["GF_Scene"]},
         {"uid":"disabled","name":"Disabled","tags":["Scene","GF_Scene"],"status":{"status":"DISABLED"}}]
        """;
        public List<string> Runs = new(), Bodies = new(), ContentTypes = new(), CatalogUris = new();
        public bool FailRun, FailCatalog;
        public TaskCompletionSource? HoldRun, HoldCatalog;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get)
            {
                CatalogUris.Add(path);
                if (HoldCatalog != null) await HoldCatalog.Task;
                return new HttpResponseMessage(FailCatalog ? HttpStatusCode.Unauthorized : HttpStatusCode.OK) { Content = new StringContent(Rules) };
            }
            Runs.Add(path);
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            ContentTypes.Add(request.Content.Headers.ContentType!.MediaType!);
            if (HoldRun != null) await HoldRun.Task;
            return new HttpResponseMessage(FailRun ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
        }
    }
}
