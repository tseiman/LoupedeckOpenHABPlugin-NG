using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

#nullable enable
namespace Loupedeck.OpenHABPluginNG
{
    /// <summary>Bounded SVG cache: rendering never waits for HTTP. Icons retain the server's colors.</summary>
    public sealed class OpenHABIconCache
    {
        private sealed class Entry
        {
            public string? Svg;
            public DateTime Attempt;
            public DateTime Used;
            public Task? Pending;
            public readonly HashSet<string> Waiting = new HashSet<string>();
        }
        private readonly object _sync = new object();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly SemaphoreSlim _downloads = new SemaphoreSlim(4, 4);
        private readonly HttpClient _http;
        private readonly Uri _server;
        private readonly CancellationToken _stop;
        private readonly Action<string> _changed;
        public OpenHABIconCache(HttpClient http, Uri server, CancellationToken stop, Action<string> changed)
        { _http = http; _server = server; _stop = stop; _changed = changed; }

        public string? Get(string itemName, string category, string state)
        {
            if (_stop.IsCancellationRequested || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(state) || state == "NULL" || state == "UNDEF") return null;
            var key = category + "\n" + state;
            lock (_sync)
            {
                if (!_entries.TryGetValue(key, out var entry))
                {
                    if (_entries.Count >= 128)
                    {
                        var oldest = _entries.Where(p => p.Value.Pending == null).OrderBy(p => p.Value.Used).FirstOrDefault();
                        if (oldest.Key == null) return null;
                        _entries.Remove(oldest.Key);
                    }
                    entry = new Entry(); _entries[key] = entry;
                }
                entry.Used = DateTime.UtcNow;
                var refresh = DateTime.UtcNow - entry.Attempt > TimeSpan.FromSeconds(entry.Svg == null ? 30 : 300);
                if (entry.Pending != null || refresh) entry.Waiting.Add(itemName);
                if (entry.Pending == null && refresh)
                {
                    entry.Attempt = DateTime.UtcNow;
                    entry.Pending = Task.Run(() => DownloadAsync(entry, category, state));
                }
                return entry.Svg;
            }
        }

        private async Task DownloadAsync(Entry entry, string category, string state)
        {
            string? svg = null;
            try
            {
                await _downloads.WaitAsync(_stop).ConfigureAwait(false);
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    var uri = new Uri(_server, "icon/" + Uri.EscapeDataString(category) + "?format=svg&anyFormat=true&iconset=classic&state=" + Uri.EscapeDataString(state));
                    using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    using var limited = new MemoryStream();
                    var buffer = new byte[8192];
                    int count;
                    while ((count = await stream.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false)) > 0)
                    {
                        if (limited.Length + count > 256 * 1024) throw new InvalidDataException("Icon too large.");
                        limited.Write(buffer, 0, count);
                    }
                    limited.Position = 0;
                    using var reader = XmlReader.Create(limited, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                    var document = XDocument.Load(reader);
                    if (document.Root?.Name != XName.Get("svg", "http://www.w3.org/2000/svg")) throw new InvalidDataException("Not SVG.");
                    svg = document.ToString(SaveOptions.DisableFormatting);
                }
                finally { _downloads.Release(); }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Icon unavailable: " + ex.GetType().Name); }
            string[] waiting;
            lock (_sync)
            {
                if (svg != null) entry.Svg = svg;
                entry.Pending = null;
                waiting = entry.Waiting.ToArray(); entry.Waiting.Clear();
            }
            if (svg != null && !_stop.IsCancellationRequested)
                foreach (var name in waiting)
                    try { _changed(name); } catch (Exception ex) { Console.WriteLine("[OpenHAB NG] Icon refresh failed: " + ex.GetType().Name); }
        }

        public async Task StopAsync()
        {
            Task[] pending;
            lock (_sync) pending = _entries.Values.Select(e => e.Pending).OfType<Task>().ToArray();
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }
}
