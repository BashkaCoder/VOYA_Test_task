namespace Voya.Battleship.Constants
{
    public static class TransportLogText
    {
        public const string Client = "Client";
        public const string Server = "Server";
        public const string Open = "OPEN";
        public const string SilentBreak = "SILENT BREAK";
        public const string EndpointFormat = "Transport {0} {1}";
        public const string MessageLabelFormat = "{0} #{1}";
        public const string SendFormat = "{0} {1} SEND {2}";
        public const string ReceiveFormat = "{0} {1} RECEIVE {2}";
        public const string DropFormat = "Transport {0} DROP {1}";
        public const string DuplicateFormat = "Transport {0} DUPLICATE {1}";
    }
}
