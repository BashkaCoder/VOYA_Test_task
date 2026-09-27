namespace Voya.Battleship
{
    public static class ProtocolTypes
    {
        public const string Connect = "Connect";
        public const string Welcome = "Welcome";
        public const string Rejected = "Rejected";
        public const string Heartbeat = "Heartbeat";
        public const string Snapshot = "Snapshot";
        public const string Fire = "Fire";
        public const string Result = "Result";
        public const string GuidFormat = "N";
        public const string CommandKeyFormat = "{0}:{1}";
    }
}
