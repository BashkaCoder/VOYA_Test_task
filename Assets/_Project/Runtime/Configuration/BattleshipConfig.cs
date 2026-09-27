using UnityEngine;

namespace Voya.Battleship.Configuration
{
    [CreateAssetMenu(menuName = "VOYA/Battleship Config")]
    public class BattleshipConfig : ScriptableObject
    {
        [field: SerializeField] public int BoardSize { get; private set; }

        [field: SerializeField] public int[] Ships { get; private set; }

        [field: SerializeField] public float TurnSeconds { get; private set; }

        [field: SerializeField] public NetworkProfile NetworkStartMaximum { get; private set; }

        [field: SerializeField] public ClientTiming Timing { get; private set; }

        [field: SerializeField] public BoardPalette Palette { get; private set; }

        [field: SerializeField] public NetworkSettingsLimits NetworkSettingsLimits { get; private set; }

        [field: SerializeField] public float UiRefreshSeconds { get; private set; }

        [field: SerializeField] public int VisibleLogLines { get; private set; }

        [field: SerializeField] public bool LoggingEnabled { get; private set; }

        [field: SerializeField] public bool RunInBackground { get; private set; }
    }
}
