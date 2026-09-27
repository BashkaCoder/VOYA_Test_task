using Voya.Battleship.Domain;

namespace Voya.Battleship.Client
{
    public class SessionStore
    {
        private readonly string[] _tokens;
        private readonly int[] _sequences;
        private readonly PendingShot? [] _pending;
        public SessionStore()
        {
            _tokens = new string[GameRules.PlayerCount];
            _sequences = new int[GameRules.PlayerCount];
            _pending = new PendingShot? [GameRules.PlayerCount];
            for (int slot = 0; slot < _sequences.Length; slot++)
            {
                _sequences[slot] = GameRules.FirstCommandSequence;
            }
        }

        public string Token(int slot) => _tokens[slot];

        public void SaveToken(int slot, string token) => _tokens[slot] = token;

        public int NextSequence(int slot) => _sequences[slot]++;

        public PendingShot? Pending(int slot) => _pending[slot];

        public void SavePending(int slot, int sequence, int turnId, int cell) => _pending[slot] = new PendingShot
        {
            Sequence = sequence,
            TurnId = turnId,
            Cell = cell
        };

        public void ClearPending(int slot) => _pending[slot] = null;
    }
}
