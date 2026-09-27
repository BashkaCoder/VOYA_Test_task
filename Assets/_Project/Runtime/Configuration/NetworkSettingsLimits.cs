using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class NetworkSettingsLimits
    {
        [field: SerializeField] public int LatencyMaxMs { get; private set; }

        [field: SerializeField] public int JitterMaxMs { get; private set; }
    }
}
