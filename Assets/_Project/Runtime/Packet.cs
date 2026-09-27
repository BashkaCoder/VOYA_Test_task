using System;
using System.Collections.Generic;

namespace Voya.Battleship
{
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
