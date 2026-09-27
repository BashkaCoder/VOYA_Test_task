using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class NetworkProfile
    {
        [field: SerializeField] public int LatencyMs { get; set; }

        [field: SerializeField] public int JitterMs { get; set; }

        [field: SerializeField, Range(0, 1)] public float Loss { get; set; }

        [field: SerializeField, Range(0, 1)] public float Duplication { get; set; }
    }
}
