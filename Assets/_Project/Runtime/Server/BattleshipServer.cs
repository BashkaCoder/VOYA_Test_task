using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Voya.Battleship.Configuration;
using Voya.Battleship.Domain;
using Voya.Battleship.Protocol;
using Voya.Battleship.Transport;
using Voya.Battleship.Constants;
using Random = System.Random;

namespace Voya.Battleship.Server
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

        public BattleshipServer(BattleshipConfig config, SimulatedTransport transport, double now, Func<string> tokenFactory)
        {
            _tokens = new string[GameRules.PlayerCount];
            _confirmed = new bool[GameRules.PlayerCount];
            _processed = new Dictionary<string, Packet>();
            _transport = transport;
            _tokenFactory = tokenFactory;
            _turnSeconds = config.TurnSeconds;
            _deadline = now + _turnSeconds;
            Random placementRandom = new Random(Guid.NewGuid().GetHashCode());
            _match = new Match(FleetPlacement.Create(config.BoardSize, config.Ships, placementRandom), FleetPlacement.Create(config.BoardSize, config.Ships, placementRandom));
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
            if (packet.Slot != endpointSlot)
            {
                return;
            }

            Handle(packet, now);
        }

        public void Handle(Packet packet, double now)
        {
            Tick(now);
            if (packet.Slot < 0 || packet.Slot >= GameRules.PlayerCount)
            {
                return;
            }

            if (packet.Type == ProtocolTypes.Connect)
            {
                if (_tokens[packet.Slot] == null)
                {
                    if (!string.IsNullOrEmpty(packet.Token))
                    {
                        Reject(packet.Slot, RuleText.UnknownSession);
                        return;
                    }

                    _tokens[packet.Slot] = _tokenFactory();
                }

                if (string.IsNullOrEmpty(packet.Token) && _confirmed[packet.Slot])
                {
                    Reject(packet.Slot, RuleText.SessionTokenRequired);
                    return;
                }

                if (!string.IsNullOrEmpty(packet.Token) && packet.Token != _tokens[packet.Slot])
                {
                    Reject(packet.Slot, RuleText.InvalidSessionToken);
                    return;
                }

                if (!string.IsNullOrEmpty(packet.Token))
                {
                    _confirmed[packet.Slot] = true;
                }

                _transport.SendToClient(packet.Slot, new Packet { Type = ProtocolTypes.Welcome, Slot = packet.Slot, Token = _tokens[packet.Slot], Snapshot = View(packet.Slot, now) });
                return;
            }

            if (packet.Token != _tokens[packet.Slot] || string.IsNullOrEmpty(packet.Token))
            {
                return;
            }

            _confirmed[packet.Slot] = true;
            if (packet.Type == ProtocolTypes.Heartbeat)
            {
                SendSnapshot(packet.Slot, now);
                return;
            }

            if (packet.Type != ProtocolTypes.Fire)
            {
                return;
            }

            string key = string.Format(ProtocolTypes.CommandKeyFormat, packet.Token, packet.Sequence);
            if (_processed.TryGetValue(key, out Packet prior))
            {
                _transport.SendToClient(packet.Slot, prior);
                SendSnapshot(packet.Slot, now);
                return;
            }

            bool accepted = _match.Fire(packet.Slot, packet.TurnId, packet.Cell, out ShotMark mark, out string reason);
            Packet result = new Packet
            {
                Type = ProtocolTypes.Result,
                Slot = packet.Slot,
                Sequence = packet.Sequence,
                Accepted = accepted,
                Reason = reason,
                ShotResult = (int)mark,
                Snapshot = View(packet.Slot, now)
            };
            _processed[key] = result;
            _transport.SendToClient(packet.Slot, result);
            if (accepted)
            {
                if (_match.Winner < 0)
                {
                    _deadline = now + _turnSeconds;
                }

                Broadcast(now);
            }
        }

        private void Reject(int slot, string reason) => _transport.SendToClient(slot, new Packet { Type = ProtocolTypes.Rejected, Slot = slot, Reason = reason });
        private Snapshot View(int player, double now) => _match.View(player, Math.Max(0, _deadline - now));
        private void SendSnapshot(int player, double now)
        {
            if (_tokens[player] != null)
            {
                _transport.SendToClient(player, new Packet { Type = ProtocolTypes.Snapshot, Slot = player, Snapshot = View(player, now) });
            }
        }

        private void Broadcast(double now)
        {
            for (int slot = 0; slot < GameRules.PlayerCount; slot++)
            {
                SendSnapshot(slot, now);
            }
        }
    }
}
