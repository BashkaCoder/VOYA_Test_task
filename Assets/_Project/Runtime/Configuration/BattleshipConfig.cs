using UnityEngine;
using Voya.Battleship.Constants;

namespace Voya.Battleship.Configuration
{
    [CreateAssetMenu(menuName = AssetPaths.BattleshipConfig)]
    public class BattleshipConfig : ScriptableObject
    {
        [SerializeField]
        private int _boardSize;
        [SerializeField]
        private int[] _ships;
        [SerializeField]
        private float _turnSeconds;
        [SerializeField]
        private NetworkProfile _networkStartMaximum;
        [SerializeField]
        private ClientTiming _clientTiming;
        [SerializeField]
        private BoardPalette _palette;
        [SerializeField]
        private NetworkSettingsLimits _networkSettingsLimits;
        [SerializeField]
        private float _uiRefreshSeconds;
        [SerializeField]
        private int _visibleLogLines;
        [SerializeField]
        private bool _loggingEnabled;
        [SerializeField]
        private bool _runInBackground;
        public int BoardSize => _boardSize;
        public int[] Ships => (int[])_ships.Clone();
        public float TurnSeconds => _turnSeconds;
        public NetworkProfile NetworkStartMaximum => _networkStartMaximum;
        public ClientTiming Timing => _clientTiming;
        public BoardPalette Palette => _palette;
        public NetworkSettingsLimits NetworkSettingsLimits => _networkSettingsLimits;
        public float UiRefreshSeconds => _uiRefreshSeconds;
        public int VisibleLogLines => _visibleLogLines;
        public bool LoggingEnabled => _loggingEnabled;
        public bool RunInBackground => _runInBackground;
    }
}
