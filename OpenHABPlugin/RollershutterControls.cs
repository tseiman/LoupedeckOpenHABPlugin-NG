using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Loupedeck.OpenHABPluginNG
{
    public enum RollershutterDirection { Stopped, Up, Down }

    public sealed partial class OpenHABService
    {
        // Shared by every visible action for the same item, never stored in a key instance.
        private readonly Dictionary<string, RollershutterDirection> _shutterDirections = new();
        private readonly Dictionary<string, long> _shutterCommandVersions = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _shutterCommandGates = new();

        public RollershutterDirection GetRollershutterDirection(string parameter)
        {
            var item = GetItem(parameter);
            lock (_sync) return item?.Name != null && _shutterDirections.TryGetValue(item.Name, out var direction)
                ? direction : RollershutterDirection.Stopped;
        }

        public async Task ActivateRollershutterAsync(string parameter, RollershutterDirection requested)
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
                var command = GetRollershutterDirection(item.Name) == requested ? "STOP" : requested == RollershutterDirection.Up ? "UP" : "DOWN";
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
