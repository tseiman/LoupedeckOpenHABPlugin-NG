using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Loupedeck.OpenHABPluginNG
{
    public enum RollershutterDirection { Stopped, Up, Down, Unknown }

    /// <summary>One global mapping for all physical movement feedback items.</summary>
    public sealed class RollershutterFeedbackValues
    {
        public string Stopped { get; }
        public string Up { get; }
        public string Down { get; }
        public RollershutterFeedbackValues(string stopped = "0", string up = "1", string down = "2")
        {
            Stopped = (stopped ?? "").Trim(); Up = (up ?? "").Trim(); Down = (down ?? "").Trim();
            var values = new[] { Stopped, Up, Down };
            if (values.Any(v => v.Length == 0 || v.Equals("NULL", StringComparison.OrdinalIgnoreCase) || v.Equals("UNDEF", StringComparison.OrdinalIgnoreCase)) ||
                values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
                throw new ArgumentException("Use three distinct non-empty feedback values; NULL and UNDEF are reserved.");
        }
        public RollershutterDirection Parse(string? state)
        {
            var value = state?.Trim();
            if (string.Equals(value, Stopped, StringComparison.OrdinalIgnoreCase)) return RollershutterDirection.Stopped;
            if (string.Equals(value, Up, StringComparison.OrdinalIgnoreCase)) return RollershutterDirection.Up;
            if (string.Equals(value, Down, StringComparison.OrdinalIgnoreCase)) return RollershutterDirection.Down;
            return RollershutterDirection.Unknown;
        }
        public JObject ToJson() => new JObject { ["stopped"] = Stopped, ["up"] = Up, ["down"] = Down };
        public static RollershutterFeedbackValues FromJson(JObject json) => new RollershutterFeedbackValues(
            (string?)json["stopped"] ?? "0", (string?)json["up"] ?? "1", (string?)json["down"] ?? "2");
    }

    public sealed partial class OpenHABService
    {
        private Dictionary<string, string> _feedbackAssignments = new();
        public IReadOnlyDictionary<string, string> GetRollershutterFeedbackAssignments()
        { lock (_sync) return new Dictionary<string, string>(_feedbackAssignments); }
        public void SetRollershutterFeedbackAssignments(IReadOnlyDictionary<string, string> assignments)
        {
            lock (_sync) _feedbackAssignments = new Dictionary<string, string>(assignments);
            foreach (var item in Rollershutters) Notify(item);
        }
        private volatile RollershutterFeedbackValues _feedbackValues = new RollershutterFeedbackValues();
        public RollershutterFeedbackValues FeedbackValues => _feedbackValues;
        public void SetRollershutterFeedbackValues(RollershutterFeedbackValues values)
        {
            _feedbackValues = values ?? throw new ArgumentNullException(nameof(values));
            foreach (var item in Rollershutters) Notify(item);
        }
        // Shared by every visible action for the same item, never stored in a key instance.
        private readonly Dictionary<string, RollershutterDirection> _shutterDirections = new();
        private readonly Dictionary<string, long> _shutterCommandVersions = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _shutterCommandGates = new();

        public string GetRollershutterFeedbackItem(string parameter, string feedbackItem = "")
        {
            if (!string.IsNullOrWhiteSpace(feedbackItem) && feedbackItem != "__auto__") return feedbackItem;
            lock (_sync)
            {
                var item = GetItem(parameter);
                return item?.Name != null && _feedbackAssignments.TryGetValue(item.Name, out var mapped)
                    ? mapped : item?.ShutterFeedbackItem ?? "";
            }
        }

        public RollershutterDirection GetRollershutterDirection(string parameter, string feedbackItem = "")
        {
            lock (_sync)
            {
                var item = GetItem(parameter);
                var feedbackName = GetRollershutterFeedbackItem(parameter, feedbackItem);
                if (feedbackName.Length > 0)
                {
                    var feedback = GetItem(feedbackName);
                    if ((feedback?.EffectiveType != "Number" && feedback?.EffectiveType != "String") || (!IsRestConnected && !IsWebSocketConnected)) return RollershutterDirection.Unknown;
                    return _feedbackValues.Parse(feedback.State);
                }
                return item?.Name != null && _shutterDirections.TryGetValue(item.Name, out var direction)
                    ? direction : RollershutterDirection.Stopped;
            }
        }

        public async Task ActivateRollershutterAsync(string parameter, RollershutterDirection requested, string feedbackItem = "")
        {
            if (requested != RollershutterDirection.Up && requested != RollershutterDirection.Down)
                throw new ArgumentException("Select UP or DOWN.", nameof(requested));
            var item = GetItem(parameter);
            if (item?.Name == null || item.EffectiveType != "Rollershutter") throw new ArgumentException("Select a rollershutter item.");
            var generation = _generation;
            var ct = _stop?.Token ?? default;
            var gate = _shutterCommandGates.GetOrAdd(item.Name, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (generation != _generation) throw new OperationCanceledException("Server configuration changed.");
                var direction = GetRollershutterDirection(item.Name, feedbackItem);
                if (direction == RollershutterDirection.Unknown) throw new InvalidOperationException("Movement feedback is unavailable.");
                var command = direction == requested ? "STOP" : requested == RollershutterDirection.Up ? "UP" : "DOWN";
                await PostItemCommandAsync(item, command, ct).ConfigureAwait(false);
            }
            finally { gate.Release(); }
            // A slow reconciliation must not block a subsequent STOP or reversal POST.
            await RefreshItemsAsync(ct).ConfigureAwait(false);
        }

        private bool RecordRollershutterCommand(string name, string command)
        {
            OpenHABCommandItem? item;
            bool changed;
            lock (_sync)
            {
                if (!_items.TryGetValue(name, out item) || item.EffectiveType != "Rollershutter") return false;
                RollershutterDirection next;
                switch (command.ToUpperInvariant())
                {
                    case "UP": next = RollershutterDirection.Up; break;
                    case "DOWN": next = RollershutterDirection.Down; break;
                    case "STOP": next = RollershutterDirection.Stopped; break;
                    default: return false;
                }
                changed = !_shutterDirections.TryGetValue(name, out var previous) ? next != RollershutterDirection.Stopped : previous != next;
                _shutterDirections[name] = next;
                _shutterCommandVersions[name] = ++_revision;
            }
            if (changed) Notify(item);
            return true;
        }

        // Called with _sync held. A snapshot started before a command cannot end its new direction.
        private bool ApplyRollershutterStateLocked(OpenHABCommandItem item, string state, long snapshotRevision = long.MaxValue)
        {
            if (item.Name == null || item.EffectiveType != "Rollershutter" ||
                (_shutterCommandVersions.TryGetValue(item.Name, out var commandVersion) && commandVersion > snapshotRevision)) return false;
            _shutterDirections.TryGetValue(item.Name, out var direction);
            var next = direction;
            switch (state.ToUpperInvariant())
            {
                case "UP": case "OPENING": next = RollershutterDirection.Up; break;
                case "DOWN": case "CLOSING": next = RollershutterDirection.Down; break;
                case "STOP": case "STOPPED": case "IDLE": case "NULL": case "UNDEF": next = RollershutterDirection.Stopped; break;
                default:
                    var position = ExtractNumericalValue(state);
                    if ((direction == RollershutterDirection.Up && position == 0) ||
                        (direction == RollershutterDirection.Down && position == 100)) next = RollershutterDirection.Stopped;
                    break;
            }
            if (next == direction) return false;
            _shutterDirections[item.Name] = next;
            return true;
        }

        private void ResetRollershutterDirections()
        {
            OpenHABCommandItem[] changed;
            lock (_sync)
            {
                changed = _shutterDirections.Where(p => p.Value != RollershutterDirection.Stopped && _items.ContainsKey(p.Key))
                    .Select(p => _items[p.Key]).ToArray();
                _shutterDirections.Clear();
                _shutterCommandVersions.Clear();
            }
            foreach (var item in changed) Notify(item);
        }
    }
}
