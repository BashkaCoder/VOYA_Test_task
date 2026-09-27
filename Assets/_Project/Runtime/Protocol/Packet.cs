using System;

namespace Voya.Battleship.Protocol
{
    [Serializable]
    public class Packet
    {
        public string Type;
        public int Slot;
        public string Token;
        public int Sequence;
        public int TurnId;
        public int Cell;
        public bool Accepted;
        public string Reason;
        public int ShotResult;
        public Snapshot Snapshot;
    }
}
