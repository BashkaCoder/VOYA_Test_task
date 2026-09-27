using Voya.Battleship.Protocol;
using Voya.Battleship.Constants;

namespace Voya.Battleship.Domain
{
    public class Match
    {
        private readonly Board[] _boards;
        private readonly int[][] _enemyMarks;
        public int ActivePlayer { get; private set; }
        public int TurnId { get; private set; }
        public int Revision { get; private set; }
        public int Winner { get; private set; }
        public int Size => _boards[0].Size;

        public Match(Board a, Board b)
        {
            ActivePlayer = GameRules.FirstPlayer;
            TurnId = GameRules.InitialTurnId;
            Revision = GameRules.InitialRevision;
            Winner = GameRules.NoWinner;
            _boards = new[]
            {
                a,
                b
            };
            _enemyMarks = new[]
            {
                new int[a.CellCount],
                new int[a.CellCount]
            };
        }

        public bool Fire(int player, int turnId, int cell, out ShotMark result, out string reason)
        {
            result = ShotMark.Unknown;
            reason = null;
            if (Winner >= 0)
            {
                reason = RuleText.MatchEnded;
            }
            else
            {
                if (player != ActivePlayer)
                {
                    reason = RuleText.OutOfTurn;
                }
                else
                {
                    if (turnId != TurnId)
                    {
                        reason = RuleText.StaleTurn;
                    }
                    else
                    {
                        if (cell < 0 || cell >= Size * Size)
                        {
                            reason = RuleText.InvalidCell;
                        }
                        else
                        {
                            if (_boards[1 - player].WasShot(cell))
                            {
                                reason = RuleText.CellResolved;
                            }
                        }
                    }
                }
            }

            if (reason != null)
            {
                return false;
            }

            result = _boards[1 - player].Shoot(cell);
            _enemyMarks[player][cell] = (int)result;
            if (result == ShotMark.Sunk)
            {
                int[] incoming = _boards[1 - player].IncomingMarks();
                for (int i = 0; i < incoming.Length; i++)
                {
                    if (incoming[i] == (int)ShotMark.Sunk && _enemyMarks[player][i] == (int)ShotMark.Hit)
                    {
                        _enemyMarks[player][i] = (int)ShotMark.Sunk;
                    }
                }
            }

            if (_boards[1 - player].AllSunk())
            {
                Winner = player;
            }
            else
            {
                ActivePlayer = 1 - player;
                TurnId++;
            }

            Revision++;
            return true;
        }

        public void Timeout()
        {
            if (Winner >= 0)
            {
                return;
            }

            ActivePlayer = 1 - ActivePlayer;
            TurnId++;
            Revision++;
        }

        public Snapshot View(int player, double remaining)
        {
            return new Snapshot
            {
                Revision = Revision,
                TurnId = TurnId,
                ActivePlayer = ActivePlayer,
                Winner = Winner,
                Size = Size,
                Remaining = remaining,
                OwnShips = _boards[player].ShipCells(),
                OwnShots = _boards[player].IncomingMarks(),
                EnemyShots = (int[])_enemyMarks[player].Clone()
            };
        }
    }
}
