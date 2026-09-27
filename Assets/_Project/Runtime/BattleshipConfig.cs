using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

namespace Voya.Battleship
{
    [CreateAssetMenu(menuName = AssetMenuPaths.BattleshipConfig)]
    public class BattleshipConfig : ScriptableObject
    {
        [SerializeField] private int _boardSize;
        [SerializeField] private int[] _ships;
        [SerializeField] private float _turnSeconds;
        [SerializeField] private NetworkProfile _network;
        [SerializeField] private ClientTiming _clientTiming;
        [SerializeField] private BoardPalette _palette;
        [SerializeField] private NetworkSettingsLimits _networkSettingsLimits;
        [SerializeField] private float _uiRefreshSeconds;
        [SerializeField] private int _visibleLogLines;
        [SerializeField] private bool _loggingEnabled;
        [SerializeField] private bool _runInBackground;

        public int BoardSize => _boardSize;
        public int[] Ships => (int[])_ships.Clone();
        public float TurnSeconds => _turnSeconds;
        public NetworkProfile Network => _network.Copy();
        public ClientTiming Timing => _clientTiming.Copy();
        public BoardPalette Palette => _palette;
        public NetworkSettingsLimits NetworkSettingsLimits => _networkSettingsLimits;
        public float UiRefreshSeconds => _uiRefreshSeconds;
        public int VisibleLogLines => _visibleLogLines;
        public bool LoggingEnabled => _loggingEnabled;
        public bool RunInBackground => _runInBackground;

        public void Validate()
        {
            if (_boardSize < 2 || _turnSeconds <= 0 || _ships == null || _ships.Length == 0 ||
                _network == null || _clientTiming == null || _palette == null || _networkSettingsLimits == null ||
                _uiRefreshSeconds <= 0 || _visibleLogLines <= 0)
            {
                throw new ArgumentException(RuleText.InvalidMatchConfig);
            }
            _network.Validate();
            _clientTiming.Validate();
            _networkSettingsLimits.Validate();
            FleetPlacement.Create(_boardSize, _ships, new Random());
        }
    }
}
