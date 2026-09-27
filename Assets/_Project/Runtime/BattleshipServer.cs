using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

namespace Voya.Battleship
{
    public class BattleshipServer
    {
        private readonly Match _match;
        private readonly SimulatedTransport _transport;
        private readonly string[] _tokens;
        private readonly bool[] _confirmed;
        private readonly Dictionary<string, Packet> _processed;
        private readonly double _turnSeconds;
        private readonly Func<string> _tokenFactory;
        private double _deadline;
        public Match Match => _match;

        public BattleshipServer(BattleshipConfig config, SimulatedTransport transport, double now, Func<string> tokenFactory = null)
        {
            _tokens = new string[GameRules.PlayerCount];
            _confirmed = new bool[GameRules.PlayerCount];
            _processed = new Dictionary<string, Packet>();
            _transport = transport;
            _tokenFactory = tokenFactory ?? (() => Guid.NewGuid().ToString(ProtocolTypes.GuidFormat));
            _turnSeconds = config.TurnSeconds;
            _deadline = now + _turnSeconds;
            Random placementRandom = new Random(Guid.NewGuid().GetHashCode());
            _match = new Match(FleetPlacement.Create(config.BoardSize, config.Ships, placementRandom),
                FleetPlacement.Create(config.BoardSize, config.Ships, placementRandom));
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
            if (packet.slot < 0 || packet.slot >= GameRules.PlayerCount) return;
            if (packet.type == ProtocolTypes.Connect)
            {
                if (_tokens[packet.slot] == null)
                {
                    if (!string.IsNullOrEmpty(packet.token)) { Reject(packet.slot, RuleText.UnknownSession); return; }
                    _tokens[packet.slot] = _tokenFactory();
                }
                if (string.IsNullOrEmpty(packet.token) && _confirmed[packet.slot]) { Reject(packet.slot, RuleText.SessionTokenRequired); return; }
                if (!string.IsNullOrEmpty(packet.token) && packet.token != _tokens[packet.slot]) { Reject(packet.slot, RuleText.InvalidSessionToken); return; }
                if (!string.IsNullOrEmpty(packet.token)) _confirmed[packet.slot] = true;
                _transport.SendToClient(packet.slot, new Packet { type = ProtocolTypes.Welcome, slot = packet.slot, token = _tokens[packet.slot], snapshot = View(packet.slot, now) });
                return;
            }
            if (packet.token != _tokens[packet.slot] || string.IsNullOrEmpty(packet.token)) return;
            _confirmed[packet.slot] = true;
            if (packet.type == ProtocolTypes.Heartbeat) { SendSnapshot(packet.slot, now); return; }
            if (packet.type != ProtocolTypes.Fire) return;
            string key = string.Format(ProtocolTypes.CommandKeyFormat, packet.token, packet.sequence);
            if (_processed.TryGetValue(key, out Packet prior)) { _transport.SendToClient(packet.slot, prior); SendSnapshot(packet.slot, now); return; }
            bool accepted = _match.Fire(packet.slot, packet.turnId, packet.cell, out ShotMark mark, out string reason);
            Packet result = new Packet { type = ProtocolTypes.Result, slot = packet.slot, sequence = packet.sequence, accepted = accepted,
                reason = reason, shotResult = (int)mark, snapshot = View(packet.slot, now) };
            _processed[key] = result;
            _transport.SendToClient(packet.slot, result);
            if (accepted)
            {
                if (_match.Winner < 0) _deadline = now + _turnSeconds;
                Broadcast(now);
            }
        }
        private void Reject(int slot, string reason) => _transport.SendToClient(slot, new Packet { type = ProtocolTypes.Rejected, slot = slot, reason = reason });
        private Snapshot View(int player, double now) => _match.View(player, Math.Max(0, _deadline - now));
        private void SendSnapshot(int player, double now)
        {
            if (_tokens[player] != null) _transport.SendToClient(player, new Packet { type = ProtocolTypes.Snapshot, slot = player, snapshot = View(player, now) });
        }
        private void Broadcast(double now) { for (int slot = 0; slot < GameRules.PlayerCount; slot++) SendSnapshot(slot, now); }
    }
}
