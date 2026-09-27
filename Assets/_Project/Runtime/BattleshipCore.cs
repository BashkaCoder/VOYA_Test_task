using System;
using System.Collections.Generic;

namespace Voya.Battleship
{
    public enum ShotMark { Unknown, Miss, Hit, Sunk }

    public class Board
    {
        private readonly int[] _ships;
        private readonly bool[] _hits;
        private readonly int[] _lengths;
        private readonly int[] _damage;
        public int Size { get; }
        public int CellCount => _ships.Length;

        public Board(int size, int[] lengths, int[] ships)
        {
            if (size < 2 || lengths == null || lengths.Length == 0 || ships == null || ships.Length != size * size)
                throw new ArgumentException("Invalid board configuration");
            Size = size;
            _lengths = (int[])lengths.Clone();
            _ships = (int[])ships.Clone();
            _hits = new bool[ships.Length];
            _damage = new int[lengths.Length];
            for (int i = 0; i < ships.Length; i++)
            {
                if (ships[i] < -1 || ships[i] >= lengths.Length) throw new ArgumentException("Invalid ship id");
            }
        }

        public int[] ShipCells()
        {
            int[] cells = new int[_ships.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = _ships[i] >= 0 ? 1 : 0;
            return cells;
        }

        public int[] IncomingMarks()
        {
            int[] marks = new int[_ships.Length];
            for (int i = 0; i < marks.Length; i++)
            {
                if (_hits[i]) marks[i] = _ships[i] < 0 ? (int)ShotMark.Miss :
                    _damage[_ships[i]] == _lengths[_ships[i]] ? (int)ShotMark.Sunk : (int)ShotMark.Hit;
            }
            return marks;
        }

        public bool WasShot(int cell) => _hits[cell];

        public ShotMark Shoot(int cell)
        {
            if (cell < 0 || cell >= _ships.Length || _hits[cell]) throw new ArgumentException("Invalid or repeated shot");
            _hits[cell] = true;
            int ship = _ships[cell];
            if (ship < 0) return ShotMark.Miss;
            _damage[ship]++;
            return _damage[ship] == _lengths[ship] ? ShotMark.Sunk : ShotMark.Hit;
        }

        public bool AllSunk()
        {
            for (int i = 0; i < _lengths.Length; i++) if (_damage[i] != _lengths[i]) return false;
            return true;
        }
    }

    public static class FleetPlacement
    {
        public static Board Create(int size, int[] lengths, Random random)
        {
            if (size < 2 || lengths == null || lengths.Length == 0 || random == null) throw new ArgumentException("Invalid fleet configuration");
            int[] cells = new int[size * size];
            for (int i = 0; i < cells.Length; i++) cells[i] = -1;
            for (int ship = 0; ship < lengths.Length; ship++)
            {
                int length = lengths[ship];
                if (length < 1 || length > size) throw new ArgumentException("Invalid ship length");
                List<int[]> choices = new List<int[]>();
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) for (int direction = 0; direction < 2; direction++)
                {
                    int dx = direction == 0 ? 1 : 0;
                    int dy = direction == 0 ? 0 : 1;
                    if (x + dx * (length - 1) >= size || y + dy * (length - 1) >= size) continue;
                    int[] candidate = new int[length];
                    bool free = true;
                    for (int n = 0; n < length; n++)
                    {
                        candidate[n] = (y + dy * n) * size + x + dx * n;
                        if (cells[candidate[n]] >= 0) free = false;
                    }
                    if (free) choices.Add(candidate);
                }
                if (choices.Count == 0) throw new ArgumentException("Fleet cannot fit");
                foreach (int cell in choices[random.Next(choices.Count)]) cells[cell] = ship;
            }
            return new Board(size, lengths, cells);
        }
    }

    public class Match
    {
        private readonly Board[] _boards;
        private readonly int[][] _enemyMarks;
        public int ActivePlayer { get; private set; }
        public int TurnId { get; private set; } = 1;
        public int Revision { get; private set; } = 1;
        public int Winner { get; private set; } = -1;
        public int Size => _boards[0].Size;

        public Match(Board a, Board b)
        {
            if (a.Size != b.Size) throw new ArgumentException("Board sizes differ");
            _boards = new[] { a, b };
            _enemyMarks = new[] { new int[a.CellCount], new int[a.CellCount] };
        }

        public bool Fire(int player, int turnId, int cell, out ShotMark result, out string reason)
        {
            result = ShotMark.Unknown;
            reason = null;
            if (Winner >= 0) reason = "Match ended";
            else if (player != ActivePlayer) reason = "Out of turn";
            else if (turnId != TurnId) reason = "Stale turn";
            else if (cell < 0 || cell >= Size * Size) reason = "Invalid cell";
            else if (_boards[1 - player].WasShot(cell)) reason = "Cell already resolved";
            if (reason != null) return false;
            result = _boards[1 - player].Shoot(cell);
            _enemyMarks[player][cell] = (int)result;
            if (result == ShotMark.Sunk)
            {
                int[] incoming = _boards[1 - player].IncomingMarks();
                for (int i = 0; i < incoming.Length; i++)
                    if (incoming[i] == (int)ShotMark.Sunk && _enemyMarks[player][i] == (int)ShotMark.Hit)
                        _enemyMarks[player][i] = (int)ShotMark.Sunk;
            }
            if (_boards[1 - player].AllSunk()) Winner = player;
            else { ActivePlayer = 1 - player; TurnId++; }
            Revision++;
            return true;
        }

        public bool Timeout()
        {
            if (Winner >= 0) return false;
            ActivePlayer = 1 - ActivePlayer;
            TurnId++;
            Revision++;
            return true;
        }

        public Snapshot View(int player, double remaining)
        {
            return new Snapshot
            {
                revision = Revision, turnId = TurnId, activePlayer = ActivePlayer, winner = Winner,
                size = Size, remaining = remaining, ownShips = _boards[player].ShipCells(),
                ownShots = _boards[player].IncomingMarks(), enemyShots = (int[])_enemyMarks[player].Clone()
            };
        }
    }

    [Serializable]
    public class Snapshot
    {
        public int revision;
        public int turnId;
        public int activePlayer;
        public int winner;
        public int size;
        public double remaining;
        public int[] ownShips;
        public int[] ownShots;
        public int[] enemyShots;
    }

    [Serializable]
    public class Packet
    {
        public string type;
        public int slot;
        public string token;
        public int sequence;
        public int turnId;
        public int cell;
        public bool accepted;
        public string reason;
        public int shotResult;
        public Snapshot snapshot;
    }
}
