using System;
using System.Text;
using UnityEngine;

namespace Voya.Battleship
{
    public class ClientRuntime : IDisposable
    {
        private readonly int _slot;
        private readonly SimulatedTransport _transport;
        private readonly SessionStore _store;
        private readonly ClientTiming _timing;
        private double _lastReceived;
        private double _lastConnect;
        private double _lastHeartbeat;
        private double _lastFire;
        private int _pendingSequence;
        private int _pendingTurn;
        private double _snapshotAt;
        private bool _reconnectAllowed;
        public Snapshot State { get; private set; }
        public ConnectionStatus Status { get; private set; }
        public string LastShot { get; private set; }
        public int PendingCell { get; private set; }

        public bool Connected => Status == ConnectionStatus.Connected;
        public event Action Changed;

        public ClientRuntime(int slot, SimulatedTransport transport, SessionStore store, ClientTiming timing, double now)
        {
            _slot = slot;
            _transport = transport;
            _store = store;
            _timing = timing.Copy();
            _timing.Validate();
            _lastReceived = now;
            _reconnectAllowed = true;
            Status = ConnectionStatus.Connecting;
            LastShot = string.Empty;
            PendingCell = GameRules.NoPendingCell;
            PendingShot? pending = _store.Pending(slot);
            if (pending.HasValue)
            {
                _pendingSequence = pending.Value.Sequence;
                _pendingTurn = pending.Value.TurnId;
                PendingCell = pending.Value.Cell;
                _lastFire = now;
                LastShot = UiText.PendingRecovery;
            }
            _transport.BindClient(slot, Receive);
            Connect(now);
        }
        public void Tick(double now)
        {
            if (now - _lastReceived > _timing.WatchdogSeconds && Status != ConnectionStatus.Disconnected)
            {
                Status = ConnectionStatus.Disconnected;
                if (PendingCell >= 0) LastShot = UiText.Unconfirmed;
                Changed?.Invoke();
            }
            if (_reconnectAllowed && !Connected && now - _lastConnect >= _timing.ConnectRetrySeconds) Connect(now);
            if (Connected && now - _lastHeartbeat >= _timing.HeartbeatSeconds)
            {
                _lastHeartbeat = now;
                Send(new Packet { type = ProtocolTypes.Heartbeat });
            }
            if (Connected && PendingCell >= 0 && now - _lastFire >= _timing.ShotRetrySeconds)
            {
                _lastFire = now;
                Send(new Packet { type = ProtocolTypes.Fire, sequence = _pendingSequence, turnId = _pendingTurn, cell = PendingCell });
            }
        }
        public void Connect(double now)
        {
            _reconnectAllowed = true;
            _transport.SetEnabled(_slot, true);
            _lastConnect = now;
            Status = ConnectionStatus.Connecting;
            Send(new Packet { type = ProtocolTypes.Connect });
            Changed?.Invoke();
        }
        public void Disconnect()
        {
            _reconnectAllowed = false;
            _transport.SetEnabled(_slot, false);
            // No transport event is sent to the client. Tick discovers the break.
        }
        public bool Fire(int cell, double now)
        {
            if (!Connected || State == null || State.winner >= 0 || State.activePlayer != _slot ||
                PendingCell >= 0 || cell < 0 || cell >= State.size * State.size || State.enemyShots[cell] != 0)
                return false;
            PendingCell = cell;
            _pendingTurn = State.turnId;
            _pendingSequence = _store.NextSequence(_slot);
            _store.SavePending(_slot, _pendingSequence, _pendingTurn, cell);
            _lastFire = now;
            LastShot = UiText.ShotPending;
            Send(new Packet { type = ProtocolTypes.Fire, sequence = _pendingSequence, turnId = _pendingTurn, cell = cell });
            Changed?.Invoke();
            return true;
        }
        private void Send(Packet packet)
        {
            packet.slot = _slot;
            packet.token = _store.Token(_slot);
            _transport.SendToServer(_slot, packet);
        }
        private void Receive(byte[] bytes)
        {
            Packet packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
            double now = Time.realtimeSinceStartupAsDouble;
            _lastReceived = now;
            if (packet.type == ProtocolTypes.Welcome)
            {
                _store.SaveToken(_slot, packet.token);
                Status = ConnectionStatus.Connected;
                ApplySnapshot(packet.snapshot, now);
            }
            else if (packet.type == ProtocolTypes.Rejected)
            {
                Status = ConnectionStatus.SessionRejected;
                LastShot = packet.reason;
                _reconnectAllowed = false;
            }
            else if (packet.type == ProtocolTypes.Snapshot)
            {
                if (_store.Token(_slot) == null) return;
                Status = ConnectionStatus.Connected;
                ApplySnapshot(packet.snapshot, now);
            }
            else if (packet.type == ProtocolTypes.Result)
            {
                if (_store.Token(_slot) == null) return;
                Status = ConnectionStatus.Connected;
                if (packet.sequence == _pendingSequence && PendingCell >= 0)
                {
                    LastShot = packet.accepted ? ((ShotMark)packet.shotResult).ToString() : packet.reason;
                    PendingCell = GameRules.NoPendingCell;
                    _store.ClearPending(_slot);
                }
                ApplySnapshot(packet.snapshot, now);
            }
            Changed?.Invoke();
        }
        public void ApplySnapshot(Snapshot snapshot, double now)
        {
            if (snapshot == null || State != null && snapshot.revision < State.revision) return;
            if (State == null || snapshot.revision > State.revision || snapshot.turnId == State.turnId)
            {
                State = snapshot;
                _snapshotAt = now;
            }
            if (PendingCell >= 0 && (snapshot.turnId != _pendingTurn || snapshot.winner >= 0))
            {
                LastShot = snapshot.enemyShots[PendingCell] != 0 ? ((ShotMark)snapshot.enemyShots[PendingCell]).ToString() : UiText.ShotNotExecuted;
                PendingCell = GameRules.NoPendingCell;
                _store.ClearPending(_slot);
            }
        }
        public double Remaining(double now) => State == null ? 0 : Math.Max(0, State.remaining - (now - _snapshotAt));
        public void Dispose() { _transport.UnbindClient(_slot); Changed = null; }
    }
}
