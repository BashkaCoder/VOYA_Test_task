using System;

namespace Voya.Battleship.Protocol
{
    [Serializable]
    public class Snapshot
    {
        public int Revision;
        public int TurnId;
        public int ActivePlayer;
        public int Winner;
        public int Size;
        public double Remaining;
        public int[] OwnShips;
        public int[] OwnShots;
        public int[] EnemyShots;
    }
}
