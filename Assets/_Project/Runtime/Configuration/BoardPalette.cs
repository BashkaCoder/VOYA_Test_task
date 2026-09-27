using System;
using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [Serializable]
    public class BoardPalette
    {
        [field: SerializeField] public Color Unknown { get; private set; }

        [field: SerializeField] public Color Ship { get; private set; }

        [field: SerializeField] public Color Miss { get; private set; }

        [field: SerializeField] public Color Hit { get; private set; }

        [field: SerializeField] public Color Pending { get; private set; }
    }
}
