using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

namespace Voya.Battleship
{
    public class SimulatedTransport : IDisposable
    {
        private readonly NetworkProfile[] _profiles;
        private readonly Random[] _random;
        private readonly int[] _generations;
        private readonly int[] _lastSnapshotSent;
        private readonly int[] _lastSnapshotReceived;
        private readonly CancellationTokenSource _lifetime;
        private readonly Action<string> _log;
        private Action<int, byte[]> _server;
        private readonly Action<byte[]>[] _clients;
        private readonly bool[] _enabled;
        public bool Logging { get; set; }

        public SimulatedTransport(NetworkProfile profile, bool loggingEnabled, Action<string> log)
        {
            _generations = new int[GameRules.PlayerCount];
            _lastSnapshotSent = new int[GameRules.PlayerCount];
            _lastSnapshotReceived = new int[GameRules.PlayerCount];
            _lifetime = new CancellationTokenSource();
            _clients = new Action<byte[]>[GameRules.PlayerCount];
            _enabled = new bool[GameRules.PlayerCount];
            _profiles = new[] { profile.Copy(), profile.Copy() };
            _random = new[] { new Random(Guid.NewGuid().GetHashCode()), new Random(Guid.NewGuid().GetHashCode()) };
            _log = log;
            Logging = loggingEnabled;
            for (int slot = 0; slot < GameRules.PlayerCount; slot++)
            {
                _enabled[slot] = true;
                _lastSnapshotSent[slot] = GameRules.NoWinner;
                _lastSnapshotReceived[slot] = GameRules.NoWinner;
            }
        }
        public NetworkProfile Profile(int slot) => _profiles[slot];
        public bool IsEnabled(int slot) => _enabled[slot];
        public void SetEnabled(int slot, bool enabled) { _enabled[slot] = enabled; Log(string.Format(TransportLogText.EndpointFormat, slot, enabled ? TransportLogText.Open : TransportLogText.SilentBreak)); }
        public void BindServer(Action<int, byte[]> receive) => _server = receive;
        public void BindClient(int slot, Action<byte[]> receive) { _generations[slot]++; _clients[slot] = receive; }
        public void UnbindClient(int slot) { _generations[slot]++; _clients[slot] = null; }

        public void SendToServer(int slot, Packet packet) => Send(slot, packet, true);
        public void SendToClient(int slot, Packet packet) => Send(slot, packet, false);
        private void Send(int slot, Packet packet, bool towardServer)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
            string label = string.Format(TransportLogText.MessageLabelFormat, packet.type, packet.sequence);
            bool report = ShouldReport(slot, packet, false);
            if (report) Log(string.Format(TransportLogText.SendFormat, towardServer ? TransportLogText.Client : TransportLogText.Server, slot, label));
            if (!_enabled[slot]) return;
            NetworkProfile profile = _profiles[slot];
            Random random = _random[slot];
            if (random.NextDouble() < profile.Loss) { if (report) Log(string.Format(TransportLogText.DropFormat, slot, label)); return; }
            Schedule(slot, bytes, towardServer, label);
            if (random.NextDouble() < profile.Duplication)
            {
                if (report) Log(string.Format(TransportLogText.DuplicateFormat, slot, label));
                Schedule(slot, (byte[])bytes.Clone(), towardServer, label);
            }
        }
        private void Schedule(int slot, byte[] bytes, bool towardServer, string label)
        {
            NetworkProfile profile = _profiles[slot];
            int delay = Math.Max(0, profile.LatencyMs + _random[slot].Next(-profile.JitterMs, profile.JitterMs + 1));
            int generation = _generations[slot];
            DeliverAsync(slot, bytes, towardServer, label, generation, delay, _lifetime.Token).Forget();
        }
        private async UniTaskVoid DeliverAsync(int slot, byte[] bytes, bool towardServer, string label, int generation, int delay, CancellationToken cancellation)
        {
            try
            {
                await UniTask.Delay(delay, cancellationToken: cancellation);
                if (cancellation.IsCancellationRequested || !_enabled[slot] || generation != _generations[slot]) return;
                Packet decoded = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
                if (ShouldReport(slot, decoded, true)) Log(string.Format(TransportLogText.ReceiveFormat, towardServer ? TransportLogText.Server : TransportLogText.Client, slot, label));
                if (towardServer) _server?.Invoke(slot, bytes);
                else _clients[slot]?.Invoke(bytes);
            }
            catch (OperationCanceledException) { }
        }
        private bool ShouldReport(int slot, Packet packet, bool receiving)
        {
            if (packet.type == ProtocolTypes.Heartbeat) return false;
            if (packet.type != ProtocolTypes.Snapshot) return true;
            int[] revisions = receiving ? _lastSnapshotReceived : _lastSnapshotSent;
            if (packet.snapshot == null || packet.snapshot.revision == revisions[slot]) return false;
            revisions[slot] = packet.snapshot.revision;
            return true;
        }
        private void Log(string message) { if (Logging) _log?.Invoke(message); }
        public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); _server = null; _clients[0] = null; _clients[1] = null; }
    }
}
