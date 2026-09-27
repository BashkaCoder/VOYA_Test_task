using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class ClientTiming
    {
        [field: SerializeField] public float WatchdogSeconds { get; private set; }

        [field: SerializeField] public float ConnectRetrySeconds { get; private set; }

        [field: SerializeField] public float HeartbeatSeconds { get; private set; }

        [field: SerializeField] public float ShotRetrySeconds { get; private set; }
    }
}
