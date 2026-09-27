using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Voya.Battleship
{
    public class BattleshipScene : LifetimeScope
    {
        [SerializeField] private BattleshipConfig _config;
        [SerializeField] private ClientPanel[] _panels;
        [SerializeField] private NetworkLogView _logView;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _loggingButton;

        private SessionStore _store;
        private SimulatedTransport _transport;
        private BattleshipServer _server;
        private ClientRuntime[] _clients;
        private double _nextDraw;
        private bool _ready;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_config == null) throw new InvalidOperationException(RuleText.MissingConfig);
            _config.Validate();
            if (_panels == null || _panels.Length != GameRules.PlayerCount ||
                _panels[0] == null || _panels[1] == null || _logView == null ||
                _restartButton == null || _loggingButton == null)
                throw new InvalidOperationException(RuleText.MissingSceneReferences);
            for (int slot = 0; slot < GameRules.PlayerCount; slot++) _panels[slot].Validate(_config.BoardSize);
            builder.Register<SessionStore>(Lifetime.Singleton);
        }

        private void Start()
        {
            Application.runInBackground = _config.RunInBackground;
            _store = Container.Resolve<SessionStore>();
            _logView.Configure(_config.VisibleLogLines);
            _transport = new SimulatedTransport(_config.Network, _config.LoggingEnabled, _logView.Append);
            _server = new BattleshipServer(_config, _transport, Time.realtimeSinceStartupAsDouble);
            _clients = new ClientRuntime[GameRules.PlayerCount];
            for (int slot = 0; slot < GameRules.PlayerCount; slot++)
            {
                int capturedSlot = slot;
                _panels[slot].Configure(slot, _transport.Profile(slot), _config.Palette, _config.NetworkSettingsLimits, () => CreateClient(capturedSlot));
                CreateClient(slot);
            }
            _restartButton.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            _loggingButton.onClick.AddListener(ToggleLogging);
            _ready = true;
            Draw();
        }

        private void Update()
        {
            if (!_ready) return;
            double now = Time.realtimeSinceStartupAsDouble;
            _server.Tick(now);
            foreach (ClientRuntime client in _clients) client?.Tick(now);
            if (now >= _nextDraw)
            {
                _nextDraw = now + _config.UiRefreshSeconds;
                Draw();
            }
        }

        protected override void OnDestroy()
        {
            _ready = false;
            if (_clients != null) foreach (ClientRuntime client in _clients) client?.Dispose();
            _transport?.Dispose();
            base.OnDestroy();
        }

        private void CreateClient(int slot)
        {
            if (_clients[slot] != null)
            {
                _clients[slot].Changed -= Draw;
                _clients[slot].Dispose();
            }
            _clients[slot] = new ClientRuntime(slot, _transport, _store, _config.Timing, Time.realtimeSinceStartupAsDouble);
            _clients[slot].Changed += Draw;
            _panels[slot].SetClient(_clients[slot]);
            Draw();
        }

        private void ToggleLogging()
        {
            _transport.Logging = !_transport.Logging;
            _logView.Append(_transport.Logging ? UiText.LoggingEnabled : UiText.LoggingDisabled);
        }

        private void Draw()
        {
            if (!_ready) return;
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (ClientPanel panel in _panels) panel.Render(now);
        }
    }
}
