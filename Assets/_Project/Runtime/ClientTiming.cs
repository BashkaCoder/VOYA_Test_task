using System;
using UnityEngine;

namespace Voya.Battleship
{
    [Serializable]
    public class ClientTiming
    {
        [SerializeField] private float _watchdogSeconds;
        [SerializeField] private float _connectRetrySeconds;
        [SerializeField] private float _heartbeatSeconds;
        [SerializeField] private float _shotRetrySeconds;

        public float WatchdogSeconds => _watchdogSeconds;
        public float ConnectRetrySeconds => _connectRetrySeconds;
        public float HeartbeatSeconds => _heartbeatSeconds;
        public float ShotRetrySeconds => _shotRetrySeconds;

        public ClientTiming Copy()
        {
            return new ClientTiming
            {
                _watchdogSeconds = _watchdogSeconds,
                _connectRetrySeconds = _connectRetrySeconds,
                _heartbeatSeconds = _heartbeatSeconds,
                _shotRetrySeconds = _shotRetrySeconds
            };
        }

        public void Validate()
        {
            if (_watchdogSeconds <= 0 || _connectRetrySeconds <= 0 ||
                _heartbeatSeconds <= 0 || _shotRetrySeconds <= 0 ||
                _watchdogSeconds <= _heartbeatSeconds)
            {
                throw new ArgumentException(RuleText.InvalidClientTiming);
            }
        }
    }
}
