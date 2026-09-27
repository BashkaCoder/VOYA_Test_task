using System;
using System.Text;
using UnityEngine;
using Voya.Battleship.Configuration;
using Voya.Battleship.Domain;
using Voya.Battleship.Protocol;
using Voya.Battleship.Transport;
using Voya.Battleship.Constants;

namespace Voya.Battleship.Client
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
            _timing = timing;
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
                if (PendingCell >= 0)
                {
                    LastShot = UiText.Unconfirmed;
                }

                Changed?.Invoke();
            }

            if (_reconnectAllowed && !Connected && now - _lastConnect >= _timing.ConnectRetrySeconds)
            {
                Connect(now);
            }

            if (Connected && now - _lastHeartbeat >= _timing.HeartbeatSeconds)
            {
                _lastHeartbeat = now;
                Send(new Packet { Type = ProtocolTypes.Heartbeat });
            }

            if (Connected && PendingCell >= 0 && now - _lastFire >= _timing.ShotRetrySeconds)
            {
                _lastFire = now;
                Send(new Packet
                {
                    Type = ProtocolTypes.Fire, Sequence = _pendingSequence, TurnId = _pendingTurn, Cell = PendingCell
                });
            }
        }

        public void Connect(double now)
        {
            _reconnectAllowed = true;
            _transport.SetEnabled(_slot, true);
            _lastConnect = now;
            Status = ConnectionStatus.Connecting;
            Send(new Packet { Type = ProtocolTypes.Connect });
            Changed?.Invoke();
        }

        public void Disconnect()
        {
            _reconnectAllowed = false;
            _transport.SetEnabled(_slot, false);
        }

        public void Fire(int cell, double now)
        {
            if (!Connected || State == null || State.Winner >= 0 || State.ActivePlayer != _slot || PendingCell >= 0 ||
                cell < 0 || cell >= State.Size * State.Size || State.EnemyShots[cell] != 0)
            {
                return;
            }

            PendingCell = cell;
            _pendingTurn = State.TurnId;
            _pendingSequence = _store.NextSequence(_slot);
            _store.SavePending(_slot, _pendingSequence, _pendingTurn, cell);
            _lastFire = now;
            LastShot = UiText.ShotPending;
            Send(new Packet
            {
                Type = ProtocolTypes.Fire, Sequence = _pendingSequence, TurnId = _pendingTurn, Cell = cell
            });
            Changed?.Invoke();
        }

        private void Send(Packet packet)
        {
            packet.Slot = _slot;
            packet.Token = _store.Token(_slot);
            _transport.SendToServer(_slot, packet);
        }

        private void Receive(byte[] bytes)
        {
            Packet packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
            double now = Time.realtimeSinceStartupAsDouble;
            _lastReceived = now;
            if (packet.Type == ProtocolTypes.Welcome)
            {
                _store.SaveToken(_slot, packet.Token);
                Status = ConnectionStatus.Connected;
                ApplySnapshot(packet.Snapshot, now);
            }
            else
            {
                if (packet.Type == ProtocolTypes.Rejected)
                {
                    Status = ConnectionStatus.SessionRejected;
                    LastShot = packet.Reason;
                    _reconnectAllowed = false;
                }
                else
                {
                    if (packet.Type == ProtocolTypes.Snapshot)
                    {
                        if (_store.Token(_slot) == null)
                        {
                            return;
                        }

                        Status = ConnectionStatus.Connected;
                        ApplySnapshot(packet.Snapshot, now);
                    }
                    else
                    {
                        if (packet.Type == ProtocolTypes.Result)
                        {
                            if (_store.Token(_slot) == null)
                            {
                                return;
                            }

                            Status = ConnectionStatus.Connected;
                            if (packet.Sequence == _pendingSequence && PendingCell >= 0)
                            {
                                LastShot = packet.Accepted ? ((ShotMark)packet.ShotResult).ToString() : packet.Reason;
                                PendingCell = GameRules.NoPendingCell;
                                _store.ClearPending(_slot);
                            }

                            ApplySnapshot(packet.Snapshot, now);
                        }
                    }
                }
            }

            Changed?.Invoke();
        }

        public void ApplySnapshot(Snapshot snapshot, double now)
        {
            if (State != null && snapshot.Revision < State.Revision)
            {
                return;
            }

            if (State == null || snapshot.Revision > State.Revision || snapshot.TurnId == State.TurnId)
            {
                State = snapshot;
                _snapshotAt = now;
            }

            if (PendingCell >= 0 && (snapshot.TurnId != _pendingTurn || snapshot.Winner >= 0))
            {
                LastShot = snapshot.EnemyShots[PendingCell] != 0
                    ? ((ShotMark)snapshot.EnemyShots[PendingCell]).ToString()
                    : UiText.ShotNotExecuted;

                PendingCell = GameRules.NoPendingCell;
                _store.ClearPending(_slot);
            }
        }

        public double Remaining(double now)
        {
            return State == null ? 0 : Math.Max(0, State.Remaining - (now - _snapshotAt));
        }

        public void Dispose()
        {
            _transport.UnbindClient(_slot);
            Changed = null;
        }
    }
}
