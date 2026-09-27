using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class NetworkProfile
    {
        [SerializeField]
        private int _latencyMs;
        [SerializeField]
        private int _jitterMs;
        [SerializeField, Range(0, 1)]
        private float _loss;
        [SerializeField, Range(0, 1)]
        private float _duplication;
        public int LatencyMs { get => _latencyMs; set => _latencyMs = value; }
        public int JitterMs { get => _jitterMs; set => _jitterMs = value; }
        public float Loss { get => _loss; set => _loss = value; }
        public float Duplication { get => _duplication; set => _duplication = value; }
    }
}
