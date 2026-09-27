using System;
using System.Collections.Generic;

namespace Voya.Battleship
{
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
                throw new ArgumentException(RuleText.InvalidBoard);
            Size = size;
            _lengths = (int[])lengths.Clone();
            _ships = (int[])ships.Clone();
            _hits = new bool[ships.Length];
            _damage = new int[lengths.Length];
            for (int i = 0; i < ships.Length; i++)
            {
                if (ships[i] < GameRules.NoShip || ships[i] >= lengths.Length) throw new ArgumentException(RuleText.InvalidShipId);
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
            if (cell < 0 || cell >= _ships.Length || _hits[cell]) throw new ArgumentException(RuleText.InvalidShot);
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
}
