using System;
using NUnit.Framework;
using UnityEngine;

namespace Voya.Battleship.Tests
{
    public class BattleshipTests
    {
        private static Board Empty(int size = 3)
        {
            int[] cells = new int[size * size];
            for (int i = 0; i < cells.Length; i++) cells[i] = -1;
            cells[0] = 0;
            return new Board(size, new[] { 1 }, cells);
        }
        private static Match SmallMatch() => new Match(Empty(), Empty());

        [Test]
        public void PlacementFitsAndContainsExactFleet()
        {
            int[] fleet = { 3, 2, 2, 1 };
            for (int seed = 0; seed < 50; seed++)
            {
                Board board = FleetPlacement.Create(6, fleet, new System.Random(seed));
                int count = 0;
                foreach (int cell in board.ShipCells()) count += cell;
                Assert.AreEqual(8, count);
            }
        }

        [Test]
        public void FirstShipCellIsHitAndFinalCellIsSunk()
        {
            int[] cells = { 0, 0, -1, -1 };
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
            match.Timeout(); match.Timeout();
            Assert.AreEqual(0, match.ActivePlayer);
            Assert.AreEqual(3, match.TurnId);
            Assert.IsFalse(match.Fire(0, 1, 0, out _, out string reason));
            Assert.AreEqual("Stale turn", reason);
            Assert.AreEqual(3, match.Revision);
        }

        [Test]
        public void DuplicateCommandIsAppliedOnce()
        {
            BattleshipConfig config = ScriptableObject.CreateInstance<BattleshipConfig>();
            SimulatedTransport transport = new SimulatedTransport(new NetworkProfile(), _ => { });
            try
            {
                BattleshipServer server = new BattleshipServer(config, transport, 0, () => "test-token");
                server.Handle(new Packet { type = "Connect", slot = 0 }, 0);
                Packet fire = new Packet { type = "Fire", slot = 0, token = "test-token", sequence = 7, turnId = 1, cell = 0 };
                server.Handle(fire, 1);
                int revision = server.Match.Revision;
                server.Handle(fire, 1.1);
                Assert.AreEqual(2, revision);
                Assert.AreEqual(revision, server.Match.Revision);
                Assert.AreEqual(2, server.Match.TurnId);
            }
            finally { transport.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void ServerExpiresTurnBeforeProcessingDelayedShot()
        {
            BattleshipConfig config = ScriptableObject.CreateInstance<BattleshipConfig>();
            SimulatedTransport transport = new SimulatedTransport(new NetworkProfile(), _ => { });
            try
            {
                BattleshipServer server = new BattleshipServer(config, transport, 0, () => "test-token");
                server.Handle(new Packet { type = "Connect", slot = 0 }, 0);
                server.Handle(new Packet { type = "Fire", slot = 0, token = "test-token", sequence = 1, turnId = 1, cell = 0 }, 15.1);
                Assert.AreEqual(2, server.Match.Revision);
                Assert.AreEqual(1, server.Match.ActivePlayer);
                foreach (int mark in server.Match.View(0, 0).enemyShots) Assert.AreEqual(0, mark);
            }
            finally { transport.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void SerializedViewExcludesUnknownEnemyShipPositions()
        {
            Match match = SmallMatch();
            Snapshot view = match.View(0, 15);
            string json = JsonUtility.ToJson(view);
            Assert.IsFalse(json.Contains("enemyShips"));
            foreach (int mark in view.enemyShots) Assert.AreEqual(0, mark);
            Assert.AreEqual(1, view.ownShips[0]);
        }

        [Test]
        public void OlderSnapshotCannotRollClientBack()
        {
            SimulatedTransport transport = new SimulatedTransport(new NetworkProfile(), _ => { });
            ClientRuntime client = new ClientRuntime(0, transport, new SessionStore(), 0);
            try
            {
                Snapshot current = SmallMatch().View(0, 15);
                current.revision = 8;
                current.turnId = 8;
                Snapshot old = SmallMatch().View(0, 15);
                old.revision = 7;
                client.ApplySnapshot(current, 1);
                client.ApplySnapshot(old, 2);
                Assert.AreEqual(8, client.State.revision);
                Assert.AreEqual(8, client.State.turnId);
            }
            finally { client.Dispose(); transport.Dispose(); }
        }

        [Test]
        public void ResumeProjectionContainsCurrentAuthoritativeRevision()
        {
            Match match = SmallMatch();
            Snapshot missed = match.View(0, 15);
            match.Timeout(); match.Timeout();
            Snapshot resumed = match.View(0, 13);
            SimulatedTransport transport = new SimulatedTransport(new NetworkProfile(), _ => { });
            ClientRuntime client = new ClientRuntime(0, transport, new SessionStore(), 0);
            try
            {
                client.ApplySnapshot(missed, 0);
                client.ApplySnapshot(resumed, 2);
                Assert.AreEqual(match.Revision, client.State.revision);
                Assert.AreEqual(match.TurnId, client.State.turnId);
            }
            finally { client.Dispose(); transport.Dispose(); }
        }

        [Test]
        public void RecreatedClientStartsWithoutVolatileStateButKeepsSessionIdentity()
        {
            SessionStore store = new SessionStore();
            store.SaveToken(0, "persisted-token");
            SimulatedTransport transport = new SimulatedTransport(new NetworkProfile(), _ => { });
            ClientRuntime first = new ClientRuntime(0, transport, store, 0);
            first.ApplySnapshot(SmallMatch().View(0, 15), 0);
            first.Dispose();
            ClientRuntime recreated = new ClientRuntime(0, transport, store, 1);
            try
            {
                Assert.IsNull(recreated.State);
                Assert.AreEqual("persisted-token", store.Token(0));
                Assert.IsNull(store.Token(1));
                recreated.ApplySnapshot(SmallMatch().View(0, 14), 1);
                Assert.AreEqual(1, recreated.State.revision);
            }
            finally { recreated.Dispose(); transport.Dispose(); }
        }

        [Test]
        public void PendingCommandSurvivesRecreationAndResolvesFromAuthoritativeTurn()
        {
            SessionStore store = new SessionStore();
            store.SaveToken(0, "persisted-token");
            store.SavePending(0, 12, 1, 2);
            SimulatedTransport transport = new SimulatedTransport(new NetworkProfile(), _ => { });
            ClientRuntime old = new ClientRuntime(0, transport, store, 0);
            old.Dispose();
            ClientRuntime recreated = new ClientRuntime(0, transport, store, 1);
            try
            {
                Assert.IsNull(recreated.State);
                Assert.AreEqual(2, recreated.PendingCell);
                Snapshot current = SmallMatch().View(0, 15);
                current.turnId = 2;
                recreated.ApplySnapshot(current, 1);
                Assert.AreEqual(-1, recreated.PendingCell);
                Assert.IsNull(store.Pending(0));
                Assert.AreEqual("Shot did not execute", recreated.LastShot);
            }
            finally { recreated.Dispose(); transport.Dispose(); }
        }
    }
}
