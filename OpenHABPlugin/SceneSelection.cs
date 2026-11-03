using System;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace Loupedeck.OpenHABPluginNG
{
    /// <summary>Shared wheel/key state. Browsing never executes a scene.</summary>
    public sealed class SceneSelection : IDisposable
    {
        private readonly OpenHABService _service;
        private readonly object _sync = new object();
        private int _revision;
        private bool _busy;
        private string _tag = "";
        private OpenHABScene[] _scenes = Array.Empty<OpenHABScene>();
        private int _index;
        public event EventHandler? Changed;
        public bool Active { get; private set; }
        public string Tag { get { lock (_sync) return _tag; } }
        public string Message { get; private set; } = "";
        public OpenHABScene? Selected { get { lock (_sync) return _scenes.Length == 0 ? null : _scenes[_index]; } }
        public string Position { get { lock (_sync) return _scenes.Length == 0 ? "" : $"{_index + 1} / {_scenes.Length}"; } }
        public SceneSelection(OpenHABService service)
        {
            _service = service;
            service.ScenesChanged += CatalogChanged;
        }
        public async Task ToggleAsync(string tag)
        {
            int revision;
            lock (_sync)
            {
                if (_busy) return;
                if (Active && _tag == tag) { ResetLocked(); revision = -1; }
                else
                {
                    _revision++; revision = _revision; _tag = tag ?? "";
                    Active = true; Message = string.IsNullOrWhiteSpace(_tag) ? "Select a scene tag" : "Loading scenes…";
                    _scenes = Array.Empty<OpenHABScene>(); _index = 0;
                }
            }
            Notify();
            if (revision < 0 || string.IsNullOrWhiteSpace(tag)) return;
            await _service.RefreshScenesAsync().ConfigureAwait(false);
            lock (_sync)
            {
                if (revision != _revision) return;
                UpdateCatalogLocked();
            }
            Notify();
        }
        public void Move(int clicks)
        {
            lock (_sync)
            {
                if (!Active || _busy || _scenes.Length == 0) return;
                _index = (int)(((_index + (long)clicks) % _scenes.Length + _scenes.Length) % _scenes.Length);
            }
            Notify();
        }
        public async Task<bool> ExecuteAsync()
        {
            OpenHABScene scene;
            lock (_sync)
            {
                if (!Active || _busy || _scenes.Length == 0) return false;
                scene = _scenes[_index]; _busy = true;
                // Consume the selection before awaiting HTTP: extra touch events cannot run it again.
                ResetLocked(); Message = "Sending…";
            }
            Notify();
            var success = false;
            try { await _service.RunSceneAsync(scene.Uid).ConfigureAwait(false); success = true; }
            catch (Exception ex)
            {
                lock (_sync) Message = ex is System.Net.Http.HttpRequestException request && request.StatusCode.HasValue
                    ? "HTTP " + (int)request.StatusCode.Value : "Scene request failed";
                Console.WriteLine("[OpenHAB NG] Scene request failed: " + ex.GetType().Name);
            }
            finally
            {
                lock (_sync) { _busy = false; if (success) Message = ""; }
                Notify();
            }
            return success;
        }
        public void Cancel()
        {
            lock (_sync) ResetLocked();
            Notify();
        }
        private void ResetLocked() { _revision++; Active = false; _scenes = Array.Empty<OpenHABScene>(); _index = 0; Message = ""; }
        private void UpdateCatalogLocked()
        {
            var selected = _scenes.Length == 0 ? null : _scenes[_index].Uid;
            _scenes = _service.Scenes.Where(s => s.Tags.Contains(_tag, StringComparer.Ordinal))
                .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Uid, StringComparer.Ordinal).ToArray();
            _index = Math.Max(0, Array.FindIndex(_scenes, s => s.Uid == selected));
            Message = _service.SceneStatus != "READY" ? _service.SceneStatus : _scenes.Length == 0 ? "No scenes for this tag" : "";
        }
        private void CatalogChanged(object? sender, EventArgs e)
        {
            lock (_sync) { if (Active) UpdateCatalogLocked(); }
            Notify();
        }
        private void Notify()
        {
            foreach (var callback in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((EventHandler)callback)(this, EventArgs.Empty); } catch { }
        }
        public void Dispose() { _service.ScenesChanged -= CatalogChanged; Cancel(); }
    }
}
