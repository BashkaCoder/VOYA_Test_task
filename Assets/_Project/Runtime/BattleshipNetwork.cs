using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Voya.Battleship
{
    [Serializable]
    public class NetworkProfile
    {
        public int latencyMs = 80;
        public int jitterMs = 30;
        [Range(0, 1)] public float loss = 0.05f;
        [Range(0, 1)] public float duplication = 0.03f;
        public void Validate()
        {
            if (latencyMs < 0 || jitterMs < 0 || loss < 0 || loss > 1 || duplication < 0 || duplication > 1)
                throw new ArgumentException("Invalid network profile");
        }
        public NetworkProfile Copy() => new NetworkProfile { latencyMs = latencyMs, jitterMs = jitterMs, loss = loss, duplication = duplication };
    }

    [CreateAssetMenu(menuName = "VOYA/Battleship Config")]
    public class BattleshipConfig : ScriptableObject
    {
        [SerializeField] private int _boardSize = 6;
        [SerializeField] private int[] _ships = { 3, 2, 2, 1 };
        [SerializeField] private float _turnSeconds = 15;
        [SerializeField] private NetworkProfile _network = new NetworkProfile();
        public int BoardSize => _boardSize;
        public int[] Ships => (int[])_ships.Clone();
        public float TurnSeconds => _turnSeconds;
        public NetworkProfile Network => _network.Copy();
        public void Validate()
        {
            if (_boardSize < 2 || _turnSeconds <= 0 || _ships == null || _ships.Length == 0) throw new ArgumentException("Invalid match config");
            _network.Validate();
            FleetPlacement.Create(_boardSize, _ships, new System.Random(1));
        }
    }

    public class SessionStore
    {
        public struct PendingShot
        {
            public int sequence;
            public int turnId;
            public int cell;
        }
        private readonly string[] _tokens = new string[2];
        private readonly int[] _sequences = { 1, 1 };
        private readonly PendingShot?[] _pending = new PendingShot?[2];
        public string Token(int slot) => _tokens[slot];
        public void SaveToken(int slot, string token) => _tokens[slot] = token;
        public int NextSequence(int slot) => _sequences[slot]++;
        public PendingShot? Pending(int slot) => _pending[slot];
        public void SavePending(int slot, int sequence, int turnId, int cell) => _pending[slot] = new PendingShot { sequence = sequence, turnId = turnId, cell = cell };
        public void ClearPending(int slot) => _pending[slot] = null;
    }

    public class SimulatedTransport : IDisposable
    {
        private readonly NetworkProfile[] _profiles;
        private readonly System.Random[] _random;
        private readonly int[] _generations = new int[2];
        private readonly int[] _lastSnapshotSent = { -1, -1 };
        private readonly int[] _lastSnapshotReceived = { -1, -1 };
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Action<string> _log;
        private Action<int, byte[]> _server;
        private readonly Action<byte[]>[] _clients = new Action<byte[]>[2];
        private readonly bool[] _enabled = { true, true };
        public bool Logging { get; set; } = true;

        public SimulatedTransport(NetworkProfile profile, Action<string> log)
        {
            _profiles = new[] { profile.Copy(), profile.Copy() };
            _random = new[] { new System.Random(117), new System.Random(231) };
            _log = log;
        }
        public NetworkProfile Profile(int slot) => _profiles[slot];
        public bool IsEnabled(int slot) => _enabled[slot];
        public void SetEnabled(int slot, bool enabled) { _enabled[slot] = enabled; Log($"Transport {slot} {(enabled ? "OPEN" : "SILENT BREAK")}"); }
        public void BindServer(Action<int, byte[]> receive) => _server = receive;
        public void BindClient(int slot, Action<byte[]> receive) { _generations[slot]++; _clients[slot] = receive; }
        public void UnbindClient(int slot) { _generations[slot]++; _clients[slot] = null; }

        public void SendToServer(int slot, Packet packet) => Send(slot, packet, true);
        public void SendToClient(int slot, Packet packet) => Send(slot, packet, false);
        private void Send(int slot, Packet packet, bool towardServer)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
            string label = $"{packet.type} #{packet.sequence}";
            bool report = ShouldReport(slot, packet, false);
            if (report) Log($"{(towardServer ? "Client" : "Server")} {slot} SEND {label}");
            if (!_enabled[slot]) return;
            NetworkProfile profile = _profiles[slot];
            System.Random random = _random[slot];
            if (random.NextDouble() < profile.loss) { if (report) Log($"Transport {slot} DROP {label}"); return; }
            Schedule(slot, bytes, towardServer, label);
            if (random.NextDouble() < profile.duplication)
            {
                if (report) Log($"Transport {slot} DUPLICATE {label}");
                Schedule(slot, (byte[])bytes.Clone(), towardServer, label);
            }
        }
        private void Schedule(int slot, byte[] bytes, bool towardServer, string label)
        {
            NetworkProfile profile = _profiles[slot];
            int delay = Math.Max(0, profile.latencyMs + _random[slot].Next(-profile.jitterMs, profile.jitterMs + 1));
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
                if (ShouldReport(slot, decoded, true)) Log($"{(towardServer ? "Server" : "Client")} {slot} RECEIVE {label}");
                if (towardServer) _server?.Invoke(slot, bytes);
                else _clients[slot]?.Invoke(bytes);
            }
            catch (OperationCanceledException) { }
        }
        private bool ShouldReport(int slot, Packet packet, bool receiving)
        {
            if (packet.type == "Heartbeat") return false;
            if (packet.type != "Snapshot") return true;
            int[] revisions = receiving ? _lastSnapshotReceived : _lastSnapshotSent;
            if (packet.snapshot == null || packet.snapshot.revision == revisions[slot]) return false;
            revisions[slot] = packet.snapshot.revision;
            return true;
        }
        private void Log(string message) { if (Logging) _log?.Invoke(message); }
        public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); _server = null; _clients[0] = null; _clients[1] = null; }
    }

    public class BattleshipServer
    {
        private readonly Match _match;
        private readonly SimulatedTransport _transport;
        private readonly string[] _tokens = new string[2];
        private readonly bool[] _confirmed = new bool[2];
        private readonly Dictionary<string, Packet> _processed = new Dictionary<string, Packet>();
        private readonly double _turnSeconds;
        private readonly Func<string> _tokenFactory;
        private double _deadline;
        public Match Match => _match;

        public BattleshipServer(BattleshipConfig config, SimulatedTransport transport, double now, Func<string> tokenFactory = null)
        {
            _transport = transport;
            _tokenFactory = tokenFactory ?? (() => Guid.NewGuid().ToString("N"));
            _turnSeconds = config.TurnSeconds;
            _deadline = now + _turnSeconds;
            _match = new Match(FleetPlacement.Create(config.BoardSize, config.Ships, new System.Random(3181)),
                FleetPlacement.Create(config.BoardSize, config.Ships, new System.Random(7261)));
            _transport.BindServer(Receive);
        }
        public void Tick(double now)
        {
            while (_match.Winner < 0 && now >= _deadline)
            {
                _match.Timeout();
                _deadline += _turnSeconds;
                Broadcast(now);
            }
        }
        private void Receive(int endpointSlot, byte[] bytes)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            Packet packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
            if (packet.slot != endpointSlot) return;
            Handle(packet, now);
        }
        public void Handle(Packet packet, double now)
        {
            Tick(now);
            if (packet.slot < 0 || packet.slot > 1) return;
            if (packet.type == "Connect")
            {
                if (_tokens[packet.slot] == null)
                {
                    if (!string.IsNullOrEmpty(packet.token)) { Reject(packet.slot, "Unknown session token"); return; }
                    _tokens[packet.slot] = _tokenFactory();
                }
                if (string.IsNullOrEmpty(packet.token) && _confirmed[packet.slot]) { Reject(packet.slot, "Session token required"); return; }
                if (!string.IsNullOrEmpty(packet.token) && packet.token != _tokens[packet.slot]) { Reject(packet.slot, "Invalid session token"); return; }
                if (!string.IsNullOrEmpty(packet.token)) _confirmed[packet.slot] = true;
                _transport.SendToClient(packet.slot, new Packet { type = "Welcome", slot = packet.slot, token = _tokens[packet.slot], snapshot = View(packet.slot, now) });
                return;
            }
            if (packet.token != _tokens[packet.slot] || string.IsNullOrEmpty(packet.token)) return;
            _confirmed[packet.slot] = true;
            if (packet.type == "Heartbeat") { SendSnapshot(packet.slot, now); return; }
            if (packet.type != "Fire") return;
            string key = packet.token + ":" + packet.sequence;
            if (_processed.TryGetValue(key, out Packet prior)) { _transport.SendToClient(packet.slot, prior); SendSnapshot(packet.slot, now); return; }
            bool accepted = _match.Fire(packet.slot, packet.turnId, packet.cell, out ShotMark mark, out string reason);
            Packet result = new Packet { type = "Result", slot = packet.slot, sequence = packet.sequence, accepted = accepted,
                reason = reason, shotResult = (int)mark, snapshot = View(packet.slot, now) };
            _processed[key] = result;
            _transport.SendToClient(packet.slot, result);
            if (accepted)
            {
                if (_match.Winner < 0) _deadline = now + _turnSeconds;
                Broadcast(now);
            }
        }
        private void Reject(int slot, string reason) => _transport.SendToClient(slot, new Packet { type = "Rejected", slot = slot, reason = reason });
        private Snapshot View(int player, double now) => _match.View(player, Math.Max(0, _deadline - now));
        private void SendSnapshot(int player, double now)
        {
            if (_tokens[player] != null) _transport.SendToClient(player, new Packet { type = "Snapshot", slot = player, snapshot = View(player, now) });
        }
        private void Broadcast(double now) { SendSnapshot(0, now); SendSnapshot(1, now); }
    }
}
