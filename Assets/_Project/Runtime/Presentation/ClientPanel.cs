using System;
using UnityEngine;
using UnityEngine.UI;
using Voya.Battleship.Configuration;
using Voya.Battleship.Protocol;
using Voya.Battleship.Client;
using Voya.Battleship.Constants;

namespace Voya.Battleship.Presentation
{
    public class ClientPanel : MonoBehaviour
    {
        [SerializeField] private Text _status;
        [SerializeField] private Text _timer;
        [SerializeField] private Text _shot;
        [SerializeField] private BoardView _ownBoard;
        [SerializeField] private BoardView _enemyBoard;
        [SerializeField] private NetworkSettingsView _networkSettings;
        [SerializeField] private Button _disconnectButton;
        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _recreateButton;

        private int _slot;
        private ClientRuntime _client;
        private BoardPalette _palette;

        public void Configure(int slot, NetworkProfile profile, BoardPalette palette, NetworkSettingsLimits limits, Action recreate)
        {
            _slot = slot;
            _palette = palette;
            _networkSettings.Bind(profile, limits);
            _enemyBoard.Bind(cell =>
            {
                _client?.Fire(cell, Time.realtimeSinceStartupAsDouble);
                Render(Time.realtimeSinceStartupAsDouble);
            });
            _disconnectButton.onClick.AddListener(() => _client?.Disconnect());
            _connectButton.onClick.AddListener(() => _client?.Connect(Time.realtimeSinceStartupAsDouble));
            _recreateButton.onClick.AddListener(() => recreate());
        }

        public void SetClient(ClientRuntime client) => _client = client;
        public void Render(double now)
        {
            Snapshot state = _client.State;
            string status = _client.Status switch
            {
                ConnectionStatus.Connected => UiText.Connected,
                ConnectionStatus.Disconnected => UiText.Disconnected,
                ConnectionStatus.SessionRejected => UiText.SessionRejected,
                _ => UiText.Connecting
            };
            _status.text = status + (state == null
                ? UiText.AwaitingState
                : state.Winner >= 0
                    ? state.Winner == _slot
                        ? UiText.Victory
                        : UiText.Defeat
                    : state.ActivePlayer == _slot
                        ? UiText.YourTurn
                        : UiText.OpponentTurn);
            _timer.text = state == null
                ? string.Empty
                : string.Format(UiText.TurnFormat, state.TurnId, _client.Remaining(now));
            _shot.text = _client.LastShot;
            _ownBoard.RenderOwn(state, _palette);
            bool canFire = _client.Connected && state is { Winner: < 0 } &&
                           state.ActivePlayer == _slot && _client.PendingCell < 0;
            _enemyBoard.RenderEnemy(state, _palette, _client.PendingCell, canFire);
        }
    }
}
