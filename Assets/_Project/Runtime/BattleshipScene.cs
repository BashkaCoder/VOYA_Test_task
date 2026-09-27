using System;
using System.Collections.Generic;
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
        private SessionStore _store;
        private SimulatedTransport _transport;
        private BattleshipServer _server;
        private readonly ClientRuntime[] _clients = new ClientRuntime[2];
        private readonly Text[] _statuses = new Text[2];
        private readonly Text[] _timers = new Text[2];
        private readonly Text[] _shotLabels = new Text[2];
        private readonly Button[][] _enemyButtons = new Button[2][];
        private readonly Image[][] _enemyImages = new Image[2][];
        private readonly Text[][] _enemyTexts = new Text[2][];
        private readonly Image[][] _ownImages = new Image[2][];
        private readonly Text[][] _ownTexts = new Text[2][];
        private Text _logText;
        private readonly List<string> _logs = new List<string>();
        private Font _font;
        private double _nextDraw;
        [NonSerialized] private bool _ready;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_config == null) throw new InvalidOperationException("BattleshipConfig is required");
            _config.Validate();
            builder.RegisterInstance(_config);
            builder.Register<SessionStore>(Lifetime.Singleton);
        }
        private void Start()
        {
            Application.runInBackground = true;
            _store = Container.Resolve<SessionStore>();
            _transport = new SimulatedTransport(_config.Network, Log);
            _server = new BattleshipServer(_config, _transport, Time.realtimeSinceStartupAsDouble);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
            for (int slot = 0; slot < 2; slot++) CreateClient(slot);
            _ready = true;
            Draw();
        }
        private void Update()
        {
            if (!_ready) return;
            double now = Time.realtimeSinceStartupAsDouble;
            _server.Tick(now);
            for (int slot = 0; slot < 2; slot++) _clients[slot]?.Tick(now);
            if (now >= _nextDraw) { _nextDraw = now + 0.1; Draw(); }
        }
        protected override void OnDestroy()
        {
            _ready = false;
            for (int slot = 0; slot < 2; slot++) _clients[slot]?.Dispose();
            _transport?.Dispose();
            base.OnDestroy();
        }
        private void CreateClient(int slot)
        {
            _clients[slot]?.Dispose();
            _clients[slot] = new ClientRuntime(slot, _transport, _store, Time.realtimeSinceStartupAsDouble);
            _clients[slot].Changed += Draw;
        }
        private void Log(string line)
        {
            _logs.Add(line);
            if (_logs.Count > 9) _logs.RemoveAt(0);
            if (_logText != null) _logText.text = string.Join("\n", _logs);
        }

        private void BuildUi()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Main scene Canvas is required");
            GameObject canvasObject = canvas.gameObject;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            Panel(canvasObject.transform, "Background", new Vector2(800, 450), new Vector2(1600, 900), new Color(0.07f, 0.1f, 0.15f));
            Text title = Label(canvasObject.transform, "NETWORK BATTLESHIP", new Vector2(500, 855), new Vector2(600, 36), 26);
            title.alignment = TextAnchor.MiddleCenter;
            Button restart = ButtonAt(canvasObject.transform, "Restart Scene", new Vector2(1190, 855), new Vector2(155, 34), () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            Button logging = ButtonAt(canvasObject.transform, "Logs: on/off", new Vector2(1360, 855), new Vector2(160, 34), () => { _transport.Logging = !_transport.Logging; Log("Logging " + (_transport.Logging ? "enabled" : "disabled")); });
            for (int slot = 0; slot < 2; slot++) BuildClientPanel(canvasObject.transform, slot);
            Panel(canvasObject.transform, "Log background", new Vector2(800, 100), new Vector2(1520, 180), new Color(0.12f, 0.17f, 0.23f));
            _logText = Label(canvasObject.transform, "", new Vector2(800, 100), new Vector2(1490, 166), 14);
            _logText.alignment = TextAnchor.UpperLeft;
        }
        private void BuildClientPanel(Transform parent, int slot)
        {
            float origin = slot == 0 ? 405 : 1195;
            Panel(parent, "Client " + slot, new Vector2(origin, 520), new Vector2(750, 625), new Color(0.13f, 0.19f, 0.27f));
            Text heading = Label(parent, "CLIENT " + (slot == 0 ? "A" : "B"), new Vector2(origin, 806), new Vector2(300, 32), 23);
            heading.alignment = TextAnchor.MiddleCenter;
            _statuses[slot] = Label(parent, "Connecting", new Vector2(origin, 770), new Vector2(700, 26), 17);
            _statuses[slot].alignment = TextAnchor.MiddleCenter;
            _timers[slot] = Label(parent, "", new Vector2(origin, 743), new Vector2(700, 26), 17);
            _timers[slot].alignment = TextAnchor.MiddleCenter;
            _shotLabels[slot] = Label(parent, "", new Vector2(origin, 711), new Vector2(700, 26), 16);
            _shotLabels[slot].alignment = TextAnchor.MiddleCenter;
            Label(parent, "OWN BOARD", new Vector2(origin - 175, 672), new Vector2(250, 24), 17).alignment = TextAnchor.MiddleCenter;
            Label(parent, "TARGET BOARD", new Vector2(origin + 175, 672), new Vector2(250, 24), 17).alignment = TextAnchor.MiddleCenter;
            int size = _config.BoardSize;
            int count = size * size;
            float spacing = Mathf.Min(46, 270f / size);
            _enemyButtons[slot] = new Button[count]; _enemyImages[slot] = new Image[count]; _enemyTexts[slot] = new Text[count];
            _ownImages[slot] = new Image[count]; _ownTexts[slot] = new Text[count];
            for (int cell = 0; cell < count; cell++)
            {
                int c = cell;
                float x = (cell % size - (size - 1) * 0.5f) * spacing;
                float y = 624 - (cell / size) * spacing;
                GameObject own = Cell(parent, new Vector2(origin - 175 + x, y), spacing - 4);
                _ownImages[slot][cell] = own.GetComponent<Image>();
                _ownTexts[slot][cell] = own.GetComponentInChildren<Text>();
                GameObject enemy = Cell(parent, new Vector2(origin + 175 + x, y), spacing - 4);
                _enemyImages[slot][cell] = enemy.GetComponent<Image>();
                _enemyTexts[slot][cell] = enemy.GetComponentInChildren<Text>();
                Button button = enemy.AddComponent<Button>();
                button.onClick.AddListener(() => { _clients[slot].Fire(c, Time.realtimeSinceStartupAsDouble); Draw(); });
                _enemyButtons[slot][cell] = button;
            }
            ButtonAt(parent, "Disconnect", new Vector2(origin - 223, 314), new Vector2(135, 34), () => _clients[slot].Disconnect());
            ButtonAt(parent, "Connect", new Vector2(origin - 72, 314), new Vector2(135, 34), () => _clients[slot].Connect(Time.realtimeSinceStartupAsDouble));
            ButtonAt(parent, "Recreate client", new Vector2(origin + 144, 314), new Vector2(190, 34), () => CreateClient(slot));
            NetworkProfile profile = _transport.Profile(slot);
            SliderAt(parent, "Latency ms", new Vector2(origin - 180, 265), 0, 1000, profile.latencyMs, value => profile.latencyMs = (int)value);
            SliderAt(parent, "Jitter ms", new Vector2(origin + 175, 265), 0, 700, profile.jitterMs, value => profile.jitterMs = (int)value);
            SliderAt(parent, "Loss %", new Vector2(origin - 180, 225), 0, 100, profile.loss * 100, value => profile.loss = value / 100);
            SliderAt(parent, "Duplicate %", new Vector2(origin + 175, 225), 0, 100, profile.duplication * 100, value => profile.duplication = value / 100);
        }
        private void Draw()
        {
            if (!_ready) return;
            double now = Time.realtimeSinceStartupAsDouble;
            for (int slot = 0; slot < 2; slot++)
            {
                ClientRuntime client = _clients[slot];
                if (client == null) continue;
                Snapshot state = client.State;
                _statuses[slot].text = client.Status + (state == null ? " | awaiting state" : state.winner >= 0 ?
                    state.winner == slot ? " | VICTORY" : " | DEFEAT" : state.activePlayer == slot ? " | YOUR TURN" : " | opponent turn");
                _timers[slot].text = state == null ? "" : $"Turn {state.turnId} | {client.Remaining(now):0.0}s";
                _shotLabels[slot].text = client.LastShot;
                for (int cell = 0; cell < _config.BoardSize * _config.BoardSize; cell++)
                {
                    int ownShip = state?.ownShips[cell] ?? 0;
                    int ownShot = state?.ownShots[cell] ?? 0;
                    int enemyShot = state?.enemyShots[cell] ?? 0;
                    _ownImages[slot][cell].color = ownShot >= 2 ? new Color(0.89f, 0.35f, 0.3f) : ownShot == 1 ?
                        new Color(0.55f, 0.61f, 0.69f) : ownShip == 1 ? new Color(0.31f, 0.76f, 0.69f) : new Color(0.21f, 0.34f, 0.47f);
                    _ownTexts[slot][cell].text = ownShot == 1 ? "•" : ownShot >= 2 ? "X" : ownShip == 1 ? "■" : "";
                    _enemyImages[slot][cell].color = client.PendingCell == cell ? new Color(0.96f, 0.75f, 0.35f) : enemyShot >= 2 ?
                        new Color(0.89f, 0.35f, 0.3f) : enemyShot == 1 ? new Color(0.55f, 0.61f, 0.69f) : new Color(0.21f, 0.34f, 0.47f);
                    _enemyTexts[slot][cell].text = client.PendingCell == cell ? "…" : enemyShot == 1 ? "•" : enemyShot == 2 ? "X" : enemyShot == 3 ? "S" : "";
                    _enemyButtons[slot][cell].interactable = client.Connected && state != null && state.winner < 0 && state.activePlayer == slot && client.PendingCell < 0 && enemyShot == 0;
                }
            }
        }
        private GameObject Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = Rect(parent, name, position, size);
            go.AddComponent<Image>().color = color;
            return go;
        }
        private GameObject Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position - new Vector2(800, 450);
            rect.sizeDelta = size;
            return go;
        }
        private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            GameObject go = Rect(parent, "Label", position, size);
            Text text = go.AddComponent<Text>();
            text.font = _font; text.fontSize = fontSize; text.text = value; text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }
        private Button ButtonAt(Transform parent, string title, Vector2 position, Vector2 size, Action click)
        {
            GameObject go = Panel(parent, title, position, size, new Color(0.27f, 0.43f, 0.59f));
            Button button = go.AddComponent<Button>();
            button.onClick.AddListener(() => click());
            Text label = Label(go.transform, title, new Vector2(800, 450), size, 15);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private GameObject Cell(Transform parent, Vector2 position, float width)
        {
            GameObject go = Panel(parent, "Cell", position, new Vector2(width, width), new Color(0.21f, 0.34f, 0.47f));
            Text label = Label(go.transform, "", new Vector2(800, 450), new Vector2(width, width), 24);
            label.alignment = TextAnchor.MiddleCenter;
            return go;
        }
        private void SliderAt(Transform parent, string title, Vector2 position, float min, float max, float initial, Action<float> change)
        {
            Text label = Label(parent, title + ": " + initial.ToString("0"), position + new Vector2(-80, 12), new Vector2(155, 24), 14);
            GameObject track = Panel(parent, title + " track", position + new Vector2(75, 12), new Vector2(110, 12), new Color(0.3f, 0.37f, 0.45f));
            GameObject handle = Panel(track.transform, "Handle", new Vector2(800, 450), new Vector2(12, 24), Color.white);
            Slider slider = track.AddComponent<Slider>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.minValue = min; slider.maxValue = max; slider.value = initial;
            slider.onValueChanged.AddListener(value => { change(value); label.text = title + ": " + value.ToString("0"); });
        }
    }
}
