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
        private double _lastReceived;
        private double _lastConnect;
        private double _lastHeartbeat;
        private double _lastFire;
        private int _pendingSequence;
        private int _pendingTurn;
        private int _pendingCell = -1;
        private double _snapshotAt;
        private bool _reconnectAllowed = true;
        public Snapshot State { get; private set; }
        public string Status { get; private set; } = "Connecting";
        public string LastShot { get; private set; } = "";
        public int PendingCell => _pendingCell;
        public int Slot => _slot;
        public bool Connected => Status == "Connected";
        public event Action Changed;

        public ClientRuntime(int slot, SimulatedTransport transport, SessionStore store, double now)
        {
            _slot = slot;
            _transport = transport;
            _store = store;
            _lastReceived = now;
            SessionStore.PendingShot? pending = _store.Pending(slot);
            if (pending.HasValue)
            {
                _pendingSequence = pending.Value.sequence;
                _pendingTurn = pending.Value.turnId;
                _pendingCell = pending.Value.cell;
                _lastFire = now;
                LastShot = "Shot pending recovery";
            }
            _transport.BindClient(slot, Receive);
            Connect(now);
        }
        public void Tick(double now)
        {
            if (now - _lastReceived > 4 && Status != "Disconnected")
            {
                Status = "Disconnected";
                if (_pendingCell >= 0) LastShot = "Shot unconfirmed; resync when connected";
                Changed?.Invoke();
            }
            if (_reconnectAllowed && Status != "Connected" && now - _lastConnect >= 1) Connect(now);
            if (Status == "Connected" && now - _lastHeartbeat >= 1)
            {
                _lastHeartbeat = now;
                Send(new Packet { type = "Heartbeat" });
            }
            if (Status == "Connected" && _pendingCell >= 0 && now - _lastFire >= 0.7)
            {
                _lastFire = now;
                Send(new Packet { type = "Fire", sequence = _pendingSequence, turnId = _pendingTurn, cell = _pendingCell });
            }
        }
        public void Connect(double now)
        {
            _reconnectAllowed = true;
            _transport.SetEnabled(_slot, true);
            _lastConnect = now;
            Status = "Connecting";
            Send(new Packet { type = "Connect" });
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
                _pendingCell >= 0 || cell < 0 || cell >= State.size * State.size || State.enemyShots[cell] != 0)
                return false;
            _pendingCell = cell;
            _pendingTurn = State.turnId;
            _pendingSequence = _store.NextSequence(_slot);
            _store.SavePending(_slot, _pendingSequence, _pendingTurn, cell);
            _lastFire = now;
            LastShot = "Shot pending";
            Send(new Packet { type = "Fire", sequence = _pendingSequence, turnId = _pendingTurn, cell = cell });
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
            if (packet.type == "Welcome")
            {
                _store.SaveToken(_slot, packet.token);
                Status = "Connected";
                ApplySnapshot(packet.snapshot, now);
            }
            else if (packet.type == "Rejected")
            {
                Status = "Session rejected";
                LastShot = packet.reason;
                _reconnectAllowed = false;
            }
            else if (packet.type == "Snapshot")
            {
                if (_store.Token(_slot) == null) return;
                Status = "Connected";
                ApplySnapshot(packet.snapshot, now);
            }
            else if (packet.type == "Result")
            {
                if (_store.Token(_slot) == null) return;
                Status = "Connected";
                if (packet.sequence == _pendingSequence && _pendingCell >= 0)
                {
                    LastShot = packet.accepted ? ((ShotMark)packet.shotResult).ToString() : packet.reason;
                    _pendingCell = -1;
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
            if (_pendingCell >= 0 && (snapshot.turnId != _pendingTurn || snapshot.winner >= 0))
            {
                LastShot = snapshot.enemyShots[_pendingCell] != 0 ? ((ShotMark)snapshot.enemyShots[_pendingCell]).ToString() : "Shot did not execute";
                _pendingCell = -1;
                _store.ClearPending(_slot);
            }
        }
        public double Remaining(double now) => State == null ? 0 : Math.Max(0, State.remaining - (now - _snapshotAt));
        public void Dispose() { _transport.UnbindClient(_slot); Changed = null; }
    }
}
