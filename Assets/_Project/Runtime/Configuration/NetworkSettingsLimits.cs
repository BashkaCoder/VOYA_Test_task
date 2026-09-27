using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class NetworkSettingsLimits
    {
        [SerializeField]
        private int _latencyMaxMs;
        [SerializeField]
        private int _jitterMaxMs;
        public int LatencyMaxMs => _latencyMaxMs;
        public int JitterMaxMs => _jitterMaxMs;
    }
}
