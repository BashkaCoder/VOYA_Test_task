using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class ClientTiming
    {
        [SerializeField]
        private float _watchdogSeconds;
        [SerializeField]
        private float _connectRetrySeconds;
        [SerializeField]
        private float _heartbeatSeconds;
        [SerializeField]
        private float _shotRetrySeconds;
        public float WatchdogSeconds => _watchdogSeconds;
        public float ConnectRetrySeconds => _connectRetrySeconds;
        public float HeartbeatSeconds => _heartbeatSeconds;
        public float ShotRetrySeconds => _shotRetrySeconds;
    }
}
