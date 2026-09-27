using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Voya.Battleship.Client;
using Voya.Battleship.Configuration;
using Voya.Battleship.Domain;
using Voya.Battleship.Protocol;
using Voya.Battleship.Server;
using Voya.Battleship.Constants;
using Voya.Battleship.Transport;

namespace Voya.Battleship.Tests
{
    public class BattleshipTests
    {
        private const string ConfigPath = "Assets/_Project/Config/BattleshipConfig.asset";
        private const string TestToken = "test-token";
        private const string PersistedToken = "persisted-token";
        private const string HiddenEnemyField = "EnemyShips";
        private static BattleshipConfig Config => AssetDatabase.LoadAssetAtPath<BattleshipConfig>(ConfigPath);

        private static Board Empty(int size = 3)
        {
            int[] cells = new int[size * size];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = -1;
            }

            cells[0] = 0;
            return new Board(size, new[] { 1 }, cells);
        }

        private static Match SmallMatch() => new Match(Empty(), Empty());

        [Test]
        public void StartupNetworkProfilesAreIndependentAndWithinConfiguredMaximums()
        {
            NetworkProfile maximum = Config.NetworkStartMaximum;
            using (SimulatedTransport transport = new SimulatedTransport(maximum, false, _ => { }))
            {
                NetworkProfile first = transport.Profile(0);
                NetworkProfile second = transport.Profile(1);
                Assert.AreNotSame(first, second);
                Assert.That(first.LatencyMs, Is.InRange(0, maximum.LatencyMs));
                Assert.That(second.LatencyMs, Is.InRange(0, maximum.LatencyMs));
                Assert.That(first.JitterMs, Is.InRange(0, maximum.JitterMs));
                Assert.That(second.JitterMs, Is.InRange(0, maximum.JitterMs));
                Assert.That(first.Loss, Is.InRange(0f, maximum.Loss));
                Assert.That(second.Loss, Is.InRange(0f, maximum.Loss));
                Assert.That(first.Duplication, Is.InRange(0f, maximum.Duplication));
                Assert.That(second.Duplication, Is.InRange(0f, maximum.Duplication));
                int secondLatency = second.LatencyMs;
                first.LatencyMs = maximum.LatencyMs;
                Assert.AreEqual(secondLatency, second.LatencyMs);
            }
        }
        [Test]
        public void PlacementFitsAndContainsExactFleet()
        {
            int[] fleet =
            {
                3,
                2,
                2,
                1
            };
            for (int sample = 0; sample < 50; sample++)
            {
                Board board = FleetPlacement.Create(6, fleet, new System.Random());
                int count = 0;
                foreach (int cell in board.ShipCells())
                {
                    count += cell;
                }

                Assert.AreEqual(8, count);
            }
        }

        [Test]
        public void FirstShipCellIsHitAndFinalCellIsSunk()
        {
            int[] cells =
            {
                0,
                0,
                -1,
                -1
            };
            Board board = new Board(2, new[] { 2 }, cells);
            Assert.AreEqual(ShotMark.Hit, board.Shoot(0));
            Assert.IsFalse(board.AllSunk());
            Assert.AreEqual(ShotMark.Sunk, board.Shoot(1));
            Assert.IsTrue(board.AllSunk());
        }

        [Test]
        public void MissHitSunkAndVictoryRespectAlternatingTurns()
        {
            Match match = SmallMatch();
            Assert.IsTrue(match.Fire(0, 1, 1, out ShotMark miss, out _));
            Assert.AreEqual(ShotMark.Miss, miss);
            Assert.AreEqual(1, match.ActivePlayer);
            Assert.IsTrue(match.Fire(1, 2, 1, out ShotMark anotherMiss, out _));
            Assert.AreEqual(ShotMark.Miss, anotherMiss);
            Assert.IsTrue(match.Fire(0, 3, 0, out ShotMark sunk, out _));
            Assert.AreEqual(ShotMark.Sunk, sunk);
            Assert.AreEqual(0, match.Winner);
        }

        [Test]
        public void OutOfTurnAndRepeatedCellCannotMutateMatch()
        {
            Match match = SmallMatch();
            Assert.IsFalse(match.Fire(1, 1, 0, out _, out _));
            Assert.AreEqual(1, match.Revision);
            Assert.IsTrue(match.Fire(0, 1, 1, out _, out _));
            Assert.IsTrue(match.Fire(1, 2, 1, out _, out _));
            Assert.IsFalse(match.Fire(0, 3, 1, out _, out _));
            Assert.AreEqual(3, match.Revision);
        }

        [Test]
        public void TimeoutAdvancesOnceAndOldTurnCannotFireWhenSamePlayerReturns()
        {
            Match match = SmallMatch();
            match.Timeout();
            match.Timeout();
            Assert.AreEqual(0, match.ActivePlayer);
            Assert.AreEqual(3, match.TurnId);
            Assert.IsFalse(match.Fire(0, 1, 0, out _, out string reason));
            Assert.AreEqual(RuleText.StaleTurn, reason);
            Assert.AreEqual(3, match.Revision);
        }

        [Test]
        public void DuplicateCommandIsAppliedOnce()
        {
            BattleshipConfig config = Config;
            SimulatedTransport transport = new SimulatedTransport(Config.NetworkStartMaximum, false, _ =>
            {
            });
            try
            {
                BattleshipServer server = new BattleshipServer(config, transport, 0, () => TestToken);
                server.Handle(new Packet { Type = ProtocolTypes.Connect, Slot = 0 }, 0);
                Packet fire = new Packet
                {
                    Type = ProtocolTypes.Fire,
                    Slot = 0,
                    Token = TestToken,
                    Sequence = 7,
                    TurnId = 1,
                    Cell = 0
                };
                server.Handle(fire, 1);
                int revision = server.Match.Revision;
                server.Handle(fire, 1.1);
                Assert.AreEqual(2, revision);
                Assert.AreEqual(revision, server.Match.Revision);
                Assert.AreEqual(2, server.Match.TurnId);
            }
            finally
            {
                transport.Dispose();
            }
        }

        [Test]
        public void ServerExpiresTurnBeforeProcessingDelayedShot()
        {
            BattleshipConfig config = Config;
            SimulatedTransport transport = new SimulatedTransport(Config.NetworkStartMaximum, false, _ =>
            {
            });
            try
            {
                BattleshipServer server = new BattleshipServer(config, transport, 0, () => TestToken);
                server.Handle(new Packet { Type = ProtocolTypes.Connect, Slot = 0 }, 0);
                server.Handle(new Packet { Type = ProtocolTypes.Fire, Slot = 0, Token = TestToken, Sequence = 1, TurnId = 1, Cell = 0 }, config.TurnSeconds + 0.1);
                Assert.AreEqual(2, server.Match.Revision);
                Assert.AreEqual(1, server.Match.ActivePlayer);
                foreach (int mark in server.Match.View(0, 0).EnemyShots)
                {
                    Assert.AreEqual(0, mark);
                }
            }
            finally
            {
                transport.Dispose();
            }
        }

        [Test]
        public void SerializedViewExcludesUnknownEnemyShipPositions()
        {
            Match match = SmallMatch();
            Snapshot view = match.View(0, 15);
            string json = JsonUtility.ToJson(view);
            Assert.IsFalse(json.Contains(HiddenEnemyField));
            foreach (int mark in view.EnemyShots)
            {
                Assert.AreEqual(0, mark);
            }

            Assert.AreEqual(1, view.OwnShips[0]);
        }

        [Test]
        public void OlderSnapshotCannotRollClientBack()
        {
            SimulatedTransport transport = new SimulatedTransport(Config.NetworkStartMaximum, false, _ =>
            {
            });
            ClientRuntime client = new ClientRuntime(0, transport, new SessionStore(), Config.Timing, 0);
            try
            {
                Snapshot current = SmallMatch().View(0, 15);
                current.Revision = 8;
                current.TurnId = 8;
                Snapshot old = SmallMatch().View(0, 15);
                old.Revision = 7;
                client.ApplySnapshot(current, 1);
                client.ApplySnapshot(old, 2);
                Assert.AreEqual(8, client.State.Revision);
                Assert.AreEqual(8, client.State.TurnId);
            }
            finally
            {
                client.Dispose();
                transport.Dispose();
            }
        }

        [Test]
        public void ResumeProjectionContainsCurrentAuthoritativeRevision()
        {
            Match match = SmallMatch();
            Snapshot missed = match.View(0, 15);
            match.Timeout();
            match.Timeout();
            Snapshot resumed = match.View(0, 13);
            SimulatedTransport transport = new SimulatedTransport(Config.NetworkStartMaximum, false, _ =>
            {
            });
            ClientRuntime client = new ClientRuntime(0, transport, new SessionStore(), Config.Timing, 0);
            try
            {
                client.ApplySnapshot(missed, 0);
                client.ApplySnapshot(resumed, 2);
                Assert.AreEqual(match.Revision, client.State.Revision);
                Assert.AreEqual(match.TurnId, client.State.TurnId);
            }
            finally
            {
                client.Dispose();
                transport.Dispose();
            }
        }

        [Test]
        public void RecreatedClientStartsWithoutVolatileStateButKeepsSessionIdentity()
        {
            SessionStore store = new SessionStore();
            store.SaveToken(0, PersistedToken);
            SimulatedTransport transport = new SimulatedTransport(Config.NetworkStartMaximum, false, _ =>
            {
            });
            ClientRuntime first = new ClientRuntime(0, transport, store, Config.Timing, 0);
            first.ApplySnapshot(SmallMatch().View(0, 15), 0);
            first.Dispose();
            ClientRuntime recreated = new ClientRuntime(0, transport, store, Config.Timing, 1);
            try
            {
                Assert.IsNull(recreated.State);
                Assert.AreEqual(PersistedToken, store.Token(0));
                Assert.IsNull(store.Token(1));
                recreated.ApplySnapshot(SmallMatch().View(0, 14), 1);
                Assert.AreEqual(1, recreated.State.Revision);
            }
            finally
            {
                recreated.Dispose();
                transport.Dispose();
            }
        }

        [Test]
        public void PendingCommandSurvivesRecreationAndResolvesFromAuthoritativeTurn()
        {
            SessionStore store = new SessionStore();
            store.SaveToken(0, PersistedToken);
            store.SavePending(0, 12, 1, 2);
            SimulatedTransport transport = new SimulatedTransport(Config.NetworkStartMaximum, false, _ =>
            {
            });
            ClientRuntime old = new ClientRuntime(0, transport, store, Config.Timing, 0);
            old.Dispose();
            ClientRuntime recreated = new ClientRuntime(0, transport, store, Config.Timing, 1);
            try
            {
                Assert.IsNull(recreated.State);
                Assert.AreEqual(2, recreated.PendingCell);
                Snapshot current = SmallMatch().View(0, 15);
                current.TurnId = 2;
                recreated.ApplySnapshot(current, 1);
                Assert.AreEqual(-1, recreated.PendingCell);
                Assert.IsNull(store.Pending(0));
                Assert.AreEqual(UiText.ShotNotExecuted, recreated.LastShot);
            }
            finally
            {
                recreated.Dispose();
                transport.Dispose();
            }
        }
    }
}
