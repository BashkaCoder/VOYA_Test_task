using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

namespace Voya.Battleship
{
    [Serializable]
    public class NetworkProfile
    {
        [SerializeField] private int _latencyMs;
        [SerializeField] private int _jitterMs;
        [SerializeField, Range(0, 1)] private float _loss;
        [SerializeField, Range(0, 1)] private float _duplication;

        public int LatencyMs { get => _latencyMs; set => _latencyMs = value; }
        public int JitterMs { get => _jitterMs; set => _jitterMs = value; }
        public float Loss { get => _loss; set => _loss = value; }
        public float Duplication { get => _duplication; set => _duplication = value; }

        public void Validate()
        {
            if (_latencyMs < 0 || _jitterMs < 0 || _loss < 0 || _loss > 1 || _duplication < 0 || _duplication > 1)
                throw new ArgumentException(RuleText.InvalidNetworkProfile);
        }
        public NetworkProfile Copy()
        {
            return new NetworkProfile
            {
                _latencyMs = _latencyMs,
                _jitterMs = _jitterMs,
                _loss = _loss,
                _duplication = _duplication
            };
        }
    }
}
