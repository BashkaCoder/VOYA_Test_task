using System;
using UnityEngine;

namespace Voya.Battleship
{
    [Serializable]
    public class NetworkSettingsLimits
    {
        [SerializeField] private int _latencyMaxMs;
        [SerializeField] private int _jitterMaxMs;

        public int LatencyMaxMs => _latencyMaxMs;
        public int JitterMaxMs => _jitterMaxMs;

        public void Validate()
        {
            if (_latencyMaxMs <= 0 || _jitterMaxMs <= 0)
                throw new ArgumentException(RuleText.InvalidNetworkSettingsLimits);
        }
    }
}
