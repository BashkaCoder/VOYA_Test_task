using System;
using System.Collections.Generic;

namespace Voya.Battleship
{
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
}
