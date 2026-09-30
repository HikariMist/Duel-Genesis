using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Screen-space duel interface and the human player's interaction flow:
    /// click a card -> contextual actions -> (choose a zone / an attack target) -> engine call.
    /// Engine choices (tributes, targets, discards, chain responses) appear as a prompt with the
    /// candidate cards, which can be picked in the prompt or directly on the table.
    /// </summary>
    public sealed class DuelHud : MonoBehaviour
    {
        private enum Mode { Idle, Menu, PlaceCard, AttackTarget, PileView, Confirm }

        private DuelGameController _controller;
        private DuelEngine _engine;
        private DuelBoardView _board;
        private int _me;
        private int _cpu;

        private Canvas _canvas;
        private RectTransform _root;
        private Mode _mode = Mode.Idle;

        // Player panels
        private readonly Text[] _lpText = new Text[2];
        private readonly Image[] _lpFill = new Image[2];
        private readonly Text[] _counters = new Text[2];
        private readonly float[] _lpShown = new float[2];
        private readonly Image[] _lpFlash = new Image[2];

        // Phase track
        private readonly List<(DuelPhase phase, Image pill, Text label)> _phasePills = new();
        private Button _primaryButton;
        private Button _endTurnButton;
        private Text _turnLabel;
        private Image _turnPill;

        // Inspector
        private CanvasGroup _inspectorGroup;
        private RawImage _inspectorImage;
        private Text _inspectorName;
        private Text _inspectorType;
        private Text _inspectorStats;
        private Text _inspectorEffect;
        private Text _inspectorStatus;
        private DuelCard _inspected;
        private DuelCard _pinned;

        // Log
        private Text _logText;
        private readonly List<string> _logLines = new();

        // Menus / prompts
        private RectTransform _menu;
        private DuelCard _menuCard;
        private RectTransform _prompt;
        private Text _promptTitle;
        private Text _promptBody;
        private RectTransform _promptStrip;
        private Button _promptConfirm;
        private Button _promptCancel;
        private Button _promptPass;
        private DuelChoice _shownChoice;
        private readonly List<DuelCard> _selected = new();
        private readonly List<(DuelCard card, Image frame)> _stripItems = new();
        private string _pileTitle;

        // Placement / attack state
        private Func<int, bool> _placeAction;
        private DuelZone _placeZoneKind;
        private readonly HashSet<int> _placeSlots = new();
        private DuelMonsterState _attacker;

        // Banner / popups / result
        private Text _banner;
        private Text _bannerSub;
        private float _bannerTime;
        private float _bannerDuration;
        private readonly List<(Text text, float life, Vector3 world)> _popups = new();
        private RectTransform _result;
        private RectTransform _confirm;
        private readonly Queue<(string title, string sub, Color color)> _bannerQueue = new();

        private float _playableRefresh;
        private readonly HashSet<int> _playable = new();

        // ============================================================== lifecycle

        public static DuelHud Create(DuelGameController controller)
        {
            GameObject go = new GameObject("DG Duel HUD");
            DuelHud hud = go.AddComponent<DuelHud>();
            hud._controller = controller;
            hud._engine = controller.Engine;
            hud._board = controller.Board;
            hud._me = controller.HumanIndex;
            hud._cpu = 1 - controller.HumanIndex;
            hud.Build();
            return hud;
        }

        private void OnEnable()
        {
            if (_engine != null) _engine.EventRaised += OnEngineEvent;
        }

        private void Start()
        {
            _engine.EventRaised -= OnEngineEvent;
            _engine.EventRaised += OnEngineEvent;
            _board.CardClicked += OnCardClicked;
            _board.ZoneClicked += OnZoneClicked;
            _board.RightClicked += CancelMode;
            _board.CardHighlight = CardHighlight;
            _board.ZoneHighlight = ZoneHighlight;
            for (int i = 0; i < 2; i++) _lpShown[i] = _engine.Duelists[i].LifePoints;
            foreach (DuelEvent e in _engine.History.ToList()) OnEngineEvent(e);
        }

        private void OnDestroy()
        {
            if (_engine != null) _engine.EventRaised -= OnEngineEvent;
            if (_board != null)
            {
                _board.CardClicked -= OnCardClicked;
                _board.ZoneClicked -= OnZoneClicked;
                _board.RightClicked -= CancelMode;
                _board.CardHighlight = null;
                _board.ZoneHighlight = null;
            }
        }

        // ============================================================== build

        private void Build()
        {
            UiKit.EnsureEventSystem();
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // the whole 1920x1080 layout always fits, whatever the window shape
            gameObject.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)transform;

            BuildPlayerPanel(_cpu, top: true);
            BuildPlayerPanel(_me, top: false);
            BuildTurnIndicator();
            BuildPhaseTrack();
            BuildInspector();
            BuildLog();
            BuildToolbar();
            BuildMenu();
            BuildPrompt();
            BuildBanner();
        }

        private void BuildPlayerPanel(int player, bool top)
        {
            Color accent = player == _me ? DuelVisualResources.Cyan : DuelVisualResources.Violet;
            Image panel = UiKit.Panel(_root, top ? "Opponent Panel" : "Player Panel", UiKit.PanelColor);
            UiKit.Place(panel.rectTransform, top ? new Vector2(0f, 1f) : new Vector2(0f, 0f), top ? new Vector2(0f, 1f) : new Vector2(0f, 0f),
                top ? new Vector2(24f, -24f) : new Vector2(24f, 24f), new Vector2(470f, 118f));
            panel.transform.Find("Border").GetComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.55f);

            Image bar = UiKit.Fill(panel.transform, "Accent", accent);
            UiKit.Place(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(5f, 94f));

            string name = _engine.Duelists[player].Name;
            Text title = UiKit.Label(panel.transform, "Name", name.ToUpperInvariant(), 19, accent, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -10f), new Vector2(430f, 26f));

            Text lp = UiKit.Label(panel.transform, "LP", "8000", 46, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(lp.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -32f), new Vector2(220f, 52f));
            UiKit.Glow(lp, new Color(accent.r, accent.g, accent.b, 0.5f));
            _lpText[player] = lp;
            Text lpTag = UiKit.Label(panel.transform, "LP Tag", "LP", 18, UiKit.MutedText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(lpTag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -46f), new Vector2(40f, 30f));

            Image lpBack = UiKit.Fill(panel.transform, "LP Back", new Color(1f, 1f, 1f, 0.08f));
            UiKit.Place(lpBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f, -52f), new Vector2(230f, 12f));
            Image lpFill = UiKit.Fill(lpBack.transform, "LP Fill", accent);
            lpFill.rectTransform.anchorMin = Vector2.zero;
            lpFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            lpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            lpFill.rectTransform.offsetMin = Vector2.zero;
            lpFill.rectTransform.offsetMax = Vector2.zero;
            _lpFill[player] = lpFill;

            Image flash = UiKit.Fill(panel.transform, "Damage Flash", new Color(1f, 0.2f, 0.2f, 0f));
            UiKit.Stretch(flash.rectTransform, 4f, 4f, 4f, 4f);
            _lpFlash[player] = flash;

            Text counters = UiKit.Label(panel.transform, "Counters", "", 15, UiKit.MutedText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(counters.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 8f), new Vector2(440f, 26f));
            _counters[player] = counters;
        }

        private void BuildTurnIndicator()
        {
            _turnPill = UiKit.Panel(_root, "Turn Indicator", UiKit.PanelColor);
            UiKit.Place(_turnPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(420f, 48f));
            _turnLabel = UiKit.Label(_turnPill.transform, "Label", "", 21, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_turnLabel.rectTransform);
        }

        private void BuildPhaseTrack()
        {
            RectTransform track = UiKit.Rect(_root, "Phase Track");
            UiKit.Place(track, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 40f), new Vector2(170f, 470f));

            (DuelPhase phase, string code, string name)[] phases =
            {
                (DuelPhase.Draw, "DP", "Draw"),
                (DuelPhase.Standby, "SP", "Standby"),
                (DuelPhase.Main1, "M1", "Main 1"),
                (DuelPhase.Battle, "BP", "Battle"),
                (DuelPhase.Main2, "M2", "Main 2"),
                (DuelPhase.End, "EP", "End")
            };
            for (int i = 0; i < phases.Length; i++)
            {
                var entry = phases[i];
                Image pill = UiKit.Panel(track, "Phase " + entry.code, UiKit.PanelColor);
                UiKit.Place(pill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * 50f), new Vector2(170f, 44f));
                Text label = UiKit.Label(pill.transform, "Label", $"<b>{entry.code}</b>  <size=15>{entry.name}</size>", 20, UiKit.MutedText, TextAnchor.MiddleCenter);
                UiKit.Stretch(label.rectTransform);
                Button button = pill.gameObject.AddComponent<Button>();
                button.targetGraphic = pill;
                DuelPhase target = entry.phase;
                button.onClick.AddListener(() => OnPhasePillClicked(target));
                _phasePills.Add((entry.phase, pill, label));
            }

            _primaryButton = UiKit.Button(track, "Primary Phase Button", "BATTLE ▶", DuelVisualResources.Cyan, OnPrimaryPhaseButton, 21);
            UiKit.Place((RectTransform)_primaryButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -318f), new Vector2(170f, 62f));
            _endTurnButton = UiKit.Button(track, "End Turn Button", "END TURN", DuelVisualResources.Violet, () => { CloseTransient(); _engine.EndTurn(_me); }, 17);
            UiKit.Place((RectTransform)_endTurnButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -388f), new Vector2(170f, 44f));
        }

        private void BuildInspector()
        {
            Image panel = UiKit.Panel(_root, "Card Inspector", UiKit.PanelColor);
            UiKit.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 20f), new Vector2(330f, 700f));
            _inspectorGroup = panel.gameObject.AddComponent<CanvasGroup>();
            _inspectorGroup.alpha = 0f;
            _inspectorGroup.blocksRaycasts = false;

            _inspectorImage = UiKit.Rect(panel.transform, "Card").gameObject.AddComponent<RawImage>();
            UiKit.Place(_inspectorImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(290f, 290f / ProductionCardVisualDrawer.CardAspect));
            _inspectorImage.raycastTarget = false;

            _inspectorName = UiKit.Label(panel.transform, "Name", "", 20, UiKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(_inspectorName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -444f), new Vector2(296f, 28f));
            _inspectorType = UiKit.Label(panel.transform, "Type", "", 15, DuelVisualResources.Cyan, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(_inspectorType.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -472f), new Vector2(296f, 22f));
            _inspectorStats = UiKit.Label(panel.transform, "Stats", "", 17, DuelVisualResources.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(_inspectorStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -494f), new Vector2(296f, 24f));
            _inspectorEffect = UiKit.Label(panel.transform, "Effect", "", 14, UiKit.TextColor, TextAnchor.UpperLeft);
            _inspectorEffect.resizeTextForBestFit = true;
            _inspectorEffect.resizeTextMinSize = 10;
            _inspectorEffect.resizeTextMaxSize = 15;
            UiKit.Place(_inspectorEffect.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(296f, 140f));
            _inspectorStatus = UiKit.Label(panel.transform, "Status", "", 13, UiKit.MutedText, TextAnchor.LowerLeft, FontStyle.Italic);
            UiKit.Place(_inspectorStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(296f, 22f));
        }

        private void BuildLog()
        {
            Image panel = UiKit.Panel(_root, "Duel Log", new Color(0.02f, 0.03f, 0.07f, 0.66f));
            UiKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -76f), new Vector2(430f, 190f));
            _logText = UiKit.Label(panel.transform, "Lines", "", 15, UiKit.TextColor, TextAnchor.LowerLeft);
            UiKit.Stretch(_logText.rectTransform, 14f, 10f, 14f, 10f);
        }

        private void BuildToolbar()
        {
            RectTransform bar = UiKit.Rect(_root, "Toolbar");
            UiKit.Place(bar, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(430f, 42f));
            Button view = UiKit.Button(bar, "View", "VIEW (V)", DuelVisualResources.Cyan, () => _board.ToggleCameraView(), 15);
            UiKit.Place((RectTransform)view.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(130f, 40f));
            Button holo = UiKit.Button(bar, "Holograms", "MODELS (H)", DuelVisualResources.Cyan, () => _board.HologramsEnabled = !_board.HologramsEnabled, 15);
            UiKit.Place((RectTransform)holo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(140f, 40f));
            Button surrender = UiKit.Button(bar, "Surrender", "SURRENDER", DuelVisualResources.Magenta, ShowSurrenderConfirm, 15);
            UiKit.Place((RectTransform)surrender.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(140f, 40f));
        }

        private void BuildMenu()
        {
            _menu = UiKit.Panel(_root, "Card Menu", new Color(0.03f, 0.05f, 0.11f, 0.96f)).rectTransform;
            _menu.gameObject.SetActive(false);
        }

        private void BuildPrompt()
        {
            _prompt = UiKit.Panel(_root, "Prompt", new Color(0.03f, 0.05f, 0.11f, 0.95f)).rectTransform;
            UiKit.Place(_prompt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(1000f, 350f));
            _prompt.Find("Border").GetComponent<Image>().color = new Color(1f, 0.78f, 0.3f, 0.7f);
            _promptTitle = UiKit.Label(_prompt, "Title", "", 26, DuelVisualResources.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_promptTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(950f, 34f));
            _promptBody = UiKit.Label(_prompt, "Body", "", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            UiKit.Place(_promptBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -48f), new Vector2(950f, 48f));

            _promptStrip = UiKit.Rect(_prompt, "Strip");
            UiKit.Place(_promptStrip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(950f, 176f));

            _promptConfirm = UiKit.Button(_prompt, "Confirm", "CONFIRM", DuelVisualResources.Cyan, OnPromptConfirm, 18);
            UiKit.Place((RectTransform)_promptConfirm.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(190f, 48f));
            _promptPass = UiKit.Button(_prompt, "Pass", "DON'T ACTIVATE", DuelVisualResources.Violet, OnPromptPass, 16);
            UiKit.Place((RectTransform)_promptPass.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-226f, 16f), new Vector2(190f, 48f));
            _promptCancel = UiKit.Button(_prompt, "Cancel", "CANCEL", DuelVisualResources.Magenta, OnPromptCancel, 16);
            UiKit.Place((RectTransform)_promptCancel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(160f, 48f));
            _prompt.gameObject.SetActive(false);
        }

        private void BuildBanner()
        {
            _banner = UiKit.Label(_root, "Banner", "", 76, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 110f));
            Outline outline = _banner.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
            _bannerSub = UiKit.Label(_root, "Banner Sub", "", 26, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(1200f, 40f));
            _bannerSub.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            _banner.color = Color.clear;
            _bannerSub.color = Color.clear;
        }

        // ============================================================== per-frame

        private void Update()
        {
            if (_engine == null) return;
            UiKit.EnsureEventSystem();
            LogClickDiagnostics();
            HandleKeys();
            RefreshPlayable();
            UpdatePlayerPanels();
            UpdatePhaseTrack();
            UpdateInspector();
            UpdateChoicePrompt();
            UpdateBanner();
            UpdatePopups();
            if (_engine.IsOver && _result == null && _board.IsSettled && _bannerQueue.Count == 0 && Time.unscaledTime > _bannerTime + 0.6f)
                ShowResult();
        }

        private void LogClickDiagnostics()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var systems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            var current = UnityEngine.EventSystems.EventSystem.current;
            Debug.Log($"[DuelHud] click pos={mouse.position.ReadValue()} eventSystems={systems.Length} current={(current != null ? current.name : "none")} " +
                      $"module={(current != null && current.currentInputModule != null ? current.currentInputModule.GetType().Name : "none")} " +
                      $"overUI={(current != null && current.IsPointerOverGameObject())} lock={Cursor.lockState} canAct={HumanCanAct} " +
                      $"busy={_engine.IsBusy} choice={_engine.PendingChoice?.Title ?? "-"} mode={_mode} phase={_engine.Phase}");
        }

        private bool HumanCanAct => !_engine.IsOver && !_engine.IsBusy && _engine.TurnPlayer == _me;

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_mode != Mode.Idle || _pinned != null) CancelMode();
                else ShowSurrenderConfirm();
            }
            if (keyboard.spaceKey.wasPressedThisFrame && _mode == Mode.Idle && _primaryButton.interactable)
                OnPrimaryPhaseButton();
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                _controller.ToggleAutopilot();
                PushBanner(_controller.Autopilot ? "AUTOPILOT ON" : "AUTOPILOT OFF", "The CPU plays your side (F8 to toggle)", DuelVisualResources.Gold);
            }
        }

        private void RefreshPlayable()
        {
            if (Time.unscaledTime < _playableRefresh) return;
            _playableRefresh = Time.unscaledTime + 0.2f;
            _playable.Clear();
            if (!HumanCanAct) return;
            foreach (DuelCard card in _engine.AllCards())
                if (ActionsFor(card).Any(a => a.enabled)) _playable.Add(card.Uid);
        }

        private void UpdatePlayerPanels()
        {
            for (int p = 0; p < 2; p++)
            {
                DuelistState d = _engine.Duelists[p];
                _lpShown[p] = Mathf.MoveTowards(_lpShown[p], d.LifePoints, Mathf.Max(40f, Mathf.Abs(_lpShown[p] - d.LifePoints) * 4f) * Time.unscaledDeltaTime);
                _lpText[p].text = Mathf.RoundToInt(_lpShown[p]).ToString();
                _lpFill[p].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_lpShown[p] / DuelRules.StartingLifePoints), 1f);
                _counters[p].text = $"HAND {d.Hand.Count}    DECK {d.Deck.Count}    GY {d.Graveyard.Count}    BANISHED {d.Banished.Count}    EXTRA {d.ExtraDeck.Count}";
                Color f = _lpFlash[p].color;
                _lpFlash[p].color = new Color(f.r, f.g, f.b, Mathf.MoveTowards(f.a, 0f, Time.unscaledDeltaTime * 1.2f));
            }
        }

        private void UpdatePhaseTrack()
        {
            bool mine = _engine.TurnPlayer == _me;
            Color accent = mine ? DuelVisualResources.Cyan : DuelVisualResources.Violet;
            foreach (var (phase, pill, label) in _phasePills)
            {
                bool current = _engine.Phase == phase;
                pill.color = current ? new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.55f, 0.98f) : UiKit.PanelColor;
                label.color = current ? Color.white : (int)phase < (int)_engine.Phase ? new Color(0.45f, 0.5f, 0.6f) : UiKit.MutedText;
                pill.GetComponent<Button>().interactable = mine && HumanCanAct && IsReachable(phase);
            }

            _turnLabel.text = _engine.IsOver ? "DUEL OVER" : $"TURN {_engine.TurnNumber}  ·  {(mine ? "YOUR TURN" : "OPPONENT'S TURN")}";
            _turnLabel.color = accent;
            _turnPill.transform.Find("Border").GetComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.7f);

            string primary = "WAIT";
            bool enabled = HumanCanAct && _mode != Mode.PlaceCard && _mode != Mode.AttackTarget;
            if (mine)
            {
                primary = _engine.Phase switch
                {
                    DuelPhase.Main1 => _engine.TurnNumber > 1 ? "BATTLE ▶" : "END TURN ▶",
                    DuelPhase.Battle => "MAIN 2 ▶",
                    DuelPhase.Main2 => "END TURN ▶",
                    _ => "…"
                };
            }
            else primary = "OPPONENT…";
            UiKit.SetButtonLabel(_primaryButton, primary);
            _primaryButton.interactable = enabled && mine;
            _endTurnButton.gameObject.SetActive(mine && (_engine.Phase == DuelPhase.Main1 && _engine.TurnNumber > 1 || _engine.Phase == DuelPhase.Battle));
            _endTurnButton.interactable = enabled;
        }

        private bool IsReachable(DuelPhase phase)
        {
            return _engine.Phase switch
            {
                DuelPhase.Main1 => phase == DuelPhase.Battle && _engine.TurnNumber > 1 || phase == DuelPhase.Main2 && _engine.TurnNumber > 1 || phase == DuelPhase.End,
                DuelPhase.Battle => phase == DuelPhase.Main2 || phase == DuelPhase.End,
                DuelPhase.Main2 => phase == DuelPhase.End,
                _ => false
            };
        }

        private void OnPhasePillClicked(DuelPhase target)
        {
            if (!HumanCanAct || !IsReachable(target)) return;
            CloseTransient();
            if (target == DuelPhase.End) { _engine.EndTurn(_me); return; }
            int guard = 4;
            while (_engine.Phase != target && guard-- > 0 && _engine.AdvancePhase(_me)) { }
        }

        private void OnPrimaryPhaseButton()
        {
            if (!HumanCanAct) return;
            CloseTransient();
            _engine.AdvancePhase(_me);
        }

        // ============================================================== inspector

        private void UpdateInspector()
        {
            DuelCard card = _board.HoveredCard ?? _menuCard ?? _pinned ?? HoveredStripCard();
            if (card == null)
            {
                _inspectorGroup.alpha = Mathf.MoveTowards(_inspectorGroup.alpha, 0f, Time.unscaledDeltaTime * 4f);
                return;
            }
            _inspectorGroup.alpha = Mathf.MoveTowards(_inspectorGroup.alpha, 1f, Time.unscaledDeltaTime * 8f);
            if (card == _inspected && _inspectorImage.texture != null) { UpdateInspectorStats(card); return; }
            _inspected = card;

            bool hidden = IsHiddenFromHuman(card);
            if (hidden)
            {
                _inspectorImage.texture = CardFaceCompositor.CachedCardBack ?? ProductionCardArtRegistry.LoadCardBack();
                _inspectorName.text = card.Zone == DuelZone.Hand ? "Opponent's hand" : card.Zone == DuelZone.Deck ? "Deck" : "Face-down card";
                _inspectorType.text = string.Empty;
                _inspectorStats.text = string.Empty;
                _inspectorEffect.text = card.Zone == DuelZone.Deck ? $"{_engine.Me(card.Owner).Deck.Count} cards remaining." : "You can't see this card.";
                _inspectorStatus.text = string.Empty;
                return;
            }

            CardData data = card.Data;
            _inspectorImage.texture = CardFaceCompositor.TryGetFace(data, out Texture2D face) ? face : null;
            if (_inspectorImage.texture == null) _inspected = null;   // retry next frame
            _inspectorName.text = data.cardName;
            _inspectorType.text = data.kind == CardKind.Monster
                ? $"{(string.IsNullOrEmpty(data.attribute) ? "" : data.attribute + "  ·  ")}{(data.ResolvedFrameKind == CardFrameKind.XyzMonster ? "Rank" : "Level")} {data.level}  ·  {data.typeLine}"
                : $"{data.typeLine} {data.kind} Card";
            _inspectorEffect.text = (data.effectText ?? string.Empty).Replace("\r\n", "\n");
            bool implemented = data.kind == CardKind.Monster || CardEffects.Get(data) != null;
            _inspectorStatus.text = data.kind == CardKind.Monster
                ? (DuelRules.CanEverBeNormalSummoned(data) ? "Can be Normal Summoned / Set." : "Cannot be Normal Summoned — needs a Special Summon.")
                : implemented ? "<color=#4fe3a0>✔ Effect active in Duel: Genesis</color>" : "<color=#ff9c6a>Effect not implemented yet — can only be Set.</color>";
            UpdateInspectorStats(card);
        }

        private void UpdateInspectorStats(DuelCard card)
        {
            if (IsHiddenFromHuman(card)) return;
            if (!card.IsMonster) { _inspectorStats.text = string.Empty; return; }
            DuelMonsterState m = _engine.FindMonster(card);
            if (m != null && m.IsFaceUp)
            {
                int atk = _engine.GetAttack(m), def = _engine.GetDefense(m);
                string a = atk != card.Data.attack ? $"<color=#{(atk > card.Data.attack ? "7dffb0" : "ff8a7a")}>{atk}</color>" : atk.ToString();
                string d = def != card.Data.defense ? $"<color=#{(def > card.Data.defense ? "7dffb0" : "ff8a7a")}>{def}</color>" : def.ToString();
                _inspectorStats.text = $"ATK {a}   DEF {d}   {(m.IsAttackPosition ? "· ATK Position" : "· DEF Position")}";
            }
            else _inspectorStats.text = $"ATK {card.Data.attack}   DEF {card.Data.defense}";
        }

        private bool IsHiddenFromHuman(DuelCard card)
        {
            if (card.Owner == _me && card.Zone != DuelZone.Deck) return false;
            if (card.Zone == DuelZone.Hand || card.Zone == DuelZone.Deck || card.Zone == DuelZone.ExtraDeck) return true;
            if (card.OnField && !card.FaceUp) return card.Controller != _me;
            return false;
        }

        private DuelCard HoveredStripCard()
        {
            return null;
        }

        // ============================================================== highlights (board callbacks)

        private (Color, float) CardHighlight(DuelCard card)
        {
            DuelChoice choice = _engine.PendingChoice;
            if (choice != null && choice.Player == _me && choice.Candidates.Contains(card))
                return _selected.Contains(card) ? (DuelVisualResources.Cyan, 1f) : (DuelVisualResources.Gold, 0.75f);

            if (_mode == Mode.AttackTarget)
            {
                if (_attacker != null && card == _attacker.Card) return (DuelVisualResources.Cyan, 1f);
                if (card.Zone == DuelZone.Monster && card.Controller == _cpu) return (new Color(1f, 0.3f, 0.25f), 0.85f);
            }

            if (_engine.CurrentAttacker != null && card == _engine.CurrentAttacker.Card) return (new Color(1f, 0.45f, 0.2f), 0.9f);
            if (_engine.CurrentAttackTarget != null && card == _engine.CurrentAttackTarget.Card) return (new Color(1f, 0.2f, 0.2f), 0.9f);
            if (_menuCard == card) return (DuelVisualResources.Cyan, 0.9f);
            if (_mode == Mode.Idle && _playable.Contains(card.Uid)) return (DuelVisualResources.Cyan, 0.42f);
            return (Color.clear, 0f);
        }

        private (Color, float) ZoneHighlight(DuelZoneView zone)
        {
            if (_mode == Mode.PlaceCard && zone.Player == _me && zone.Zone == _placeZoneKind && _placeSlots.Contains(zone.Slot))
                return (DuelVisualResources.Cyan, zone == _board.HoveredZone ? 1f : 0.6f);
            if (_mode == Mode.AttackTarget && zone.Player == _cpu && zone.Zone == DuelZone.Monster && _engine.Me(_cpu).MonsterCount == 0)
                return (new Color(1f, 0.3f, 0.25f), 0.4f);
            return (Color.clear, 0f);
        }

        // ============================================================== clicks

        private void OnCardClicked(DuelCard card)
        {
            DuelChoice choice = _engine.PendingChoice;
            if (choice != null && choice.Player == _me)
            {
                if (choice.Candidates.Contains(card)) ToggleSelection(card, choice);
                return;
            }

            if (_mode == Mode.AttackTarget)
            {
                DuelMonsterState target = _engine.FindMonster(card);
                if (target != null && card.Controller == _cpu && _attacker != null)
                {
                    DuelMonsterState attacker = _attacker;
                    CancelMode();
                    _engine.DeclareAttack(_me, attacker, target);
                }
                else CancelMode();
                return;
            }

            if (_mode == Mode.PlaceCard)
            {
                if (card.OnField && card.Controller == _me && card.Zone == _placeZoneKind) return;
                CancelMode();
                return;
            }

            if (card.Zone == DuelZone.Graveyard || card.Zone == DuelZone.Banished || card.Zone == DuelZone.ExtraDeck && card.Owner == _me)
            {
                ShowPile(card.Owner, card.Zone);
                return;
            }

            OpenMenu(card);
        }

        private void OnZoneClicked(DuelZoneView zone)
        {
            if (_mode == Mode.PlaceCard)
            {
                if (zone.Player == _me && zone.Zone == _placeZoneKind && _placeSlots.Contains(zone.Slot))
                {
                    Func<int, bool> action = _placeAction;
                    CancelMode();
                    action?.Invoke(zone.Slot);
                }
                return;
            }
            if (_mode == Mode.AttackTarget && zone.Player == _cpu && _attacker != null && _engine.Me(_cpu).MonsterCount == 0)
            {
                DuelMonsterState attacker = _attacker;
                CancelMode();
                _engine.DeclareAttack(_me, attacker, null);
                return;
            }
            if (zone.Zone == DuelZone.Graveyard || zone.Zone == DuelZone.Banished || zone.Zone == DuelZone.ExtraDeck && zone.Player == _me)
                ShowPile(zone.Player, zone.Zone);
            else CancelMode();
        }

        private void CancelMode()
        {
            _mode = Mode.Idle;
            _attacker = null;
            _placeAction = null;
            _placeSlots.Clear();
            _menuCard = null;
            _pinned = null;
            _menu.gameObject.SetActive(false);
            if (_confirm != null) { Destroy(_confirm.gameObject); _confirm = null; }
            if (_shownChoice == null && _prompt.gameObject.activeSelf) _prompt.gameObject.SetActive(false);
        }

        private void CloseTransient()
        {
            if (_mode == Mode.Menu || _mode == Mode.PileView) CancelMode();
        }

        // ============================================================== actions

        private struct CardAction
        {
            public string label;
            public bool enabled;
            public string reason;
            public Action run;
        }

        private IEnumerable<(string label, bool enabled, string reason, Action run)> ActionsFor(DuelCard card)
        {
            var list = new List<(string, bool, string, Action)>();
            if (card == null || _engine.IsOver) return list;

            if (card.Zone == DuelZone.Hand && card.Owner == _me)
            {
                if (card.IsMonster)
                {
                    bool summon = _engine.CanNormalSummon(_me, card, false);
                    string why = summon ? "" : _engine.WhyCannotNormalSummon(_me, card);
                    int tributes = _engine.TributesRequired(card);
                    list.Add((tributes > 0 ? $"Tribute Summon ({tributes})" : "Normal Summon", summon, why, () => BeginSummon(card, false)));
                    list.Add((tributes > 0 ? $"Tribute Set ({tributes})" : "Set (face-down DEF)", summon, why, () => BeginSummon(card, true)));
                }
                else
                {
                    if (card.IsSpell)
                    {
                        bool can = _engine.CanActivate(_me, card);
                        list.Add(("Activate", can, can ? "" : _engine.WhyCannotActivate(_me, card), () => BeginActivateFromHand(card)));
                    }
                    bool set = _engine.CanSetSpellTrap(_me, card);
                    list.Add(("Set", set, set ? "" : "Only during your Main Phase, with a free zone.", () => BeginSetSpellTrap(card)));
                }
            }
            else if (card.Zone == DuelZone.Monster && card.Controller == _me)
            {
                DuelMonsterState m = _engine.FindMonster(card);
                if (_engine.Phase == DuelPhase.Battle && _engine.TurnPlayer == _me)
                {
                    bool can = _engine.CanAttack(_me, m);
                    string why = m.HasAttacked ? "Already attacked this turn." : !m.IsAttackPosition ? "Only Attack Position monsters can attack." :
                        _engine.OpponentCannotAttack(_me) ? "Attacks are prevented this turn." : "Cannot attack now.";
                    list.Add((_engine.Me(_cpu).MonsterCount == 0 ? "Attack Directly" : "Attack", can, can ? "" : why, () => BeginAttack(m)));
                }
                if (m.IsFaceUp && MonsterAbilities.Get(card, MonsterAbilityKind.Ignition) != null)
                {
                    bool can = _engine.CanUseMonsterEffect(_me, m);
                    list.Add(("Use Effect", can, can ? "" : "Once per turn, in your Main Phase, when it has a target.", () => _engine.UseMonsterEffect(_me, m)));
                }
                if (m.IsFaceDown)
                {
                    bool can = _engine.CanFlipSummon(_me, m);
                    list.Add(("Flip Summon", can, can ? "" : "Not the turn it was Set, once per turn, Main Phase only.", () => _engine.FlipSummon(_me, m)));
                }
                else if (_engine.IsMainPhase)
                {
                    bool can = _engine.CanChangePosition(_me, m);
                    list.Add((m.IsAttackPosition ? "Change to DEF" : "Change to ATK", can,
                        can ? "" : "Once per turn; not the turn it was summoned or after it attacked.", () => _engine.ChangePosition(_me, m)));
                }
            }
            else if (card.OnField && card.Controller == _me && !card.IsMonster)
            {
                DuelBackrowState s = _engine.FindBackrow(card);
                if (s != null && s.FaceDown)
                {
                    bool can = _engine.CanActivate(_me, card);
                    list.Add(("Activate", can, can ? "" : _engine.WhyCannotActivate(_me, card), () => _engine.Activate(_me, card)));
                }
                else if (s != null && CardEffects.Get(card.Data)?.HasFaceUpEffect == true)
                {
                    bool can = _engine.CanActivate(_me, card);
                    list.Add(("Use Effect", can, can ? "" : "Only during your Main Phase, when its requirements are met.", () => _engine.Activate(_me, card)));
                }
            }
            return list;
        }

        private void OpenMenu(DuelCard card)
        {
            CancelMode();
            _menuCard = card;
            _pinned = card;
            var actions = ActionsFor(card).ToList();
            if (actions.Count == 0 || !HumanCanAct)
            {
                _mode = Mode.Idle;
                _menuCard = null;
                return;
            }

            _mode = Mode.Menu;
            foreach (Transform child in _menu) if (child.name != "Border") Destroy(child.gameObject);

            Text title = UiKit.Label(_menu, "Title", card.Name, 17, DuelVisualResources.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(262f, 26f));
            float y = -40f;
            foreach (var action in actions)
            {
                var captured = action;
                Button button = UiKit.Button(_menu, action.label, action.label, action.enabled ? DuelVisualResources.Cyan : new Color(0.4f, 0.4f, 0.45f), () =>
                {
                    CancelMode();
                    captured.run();
                }, 17);
                button.interactable = action.enabled;
                UiKit.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(270f, 42f));
                y -= 46f;
                if (!action.enabled && !string.IsNullOrEmpty(action.reason))
                {
                    Text reason = UiKit.Label(_menu, "Reason", action.reason, 13, new Color(1f, 0.65f, 0.5f), TextAnchor.UpperLeft, FontStyle.Italic);
                    UiKit.Place(reason.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, y + 2f), new Vector2(262f, 34f));
                    y -= 34f;
                }
            }

            float height = -y + 8f;
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, mouse, null, out Vector2 local);
            Vector2 half = _root.rect.size * 0.5f;
            float x = Mathf.Clamp(local.x + 24f, -half.x + 10f, half.x - 300f);
            float yPos = Mathf.Clamp(local.y + height * 0.5f, -half.y + height + 10f, half.y - 10f);
            UiKit.Place(_menu, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(x, yPos), new Vector2(290f, height));
            _menu.gameObject.SetActive(true);
            _menu.SetAsLastSibling();
        }

        private void BeginSummon(DuelCard card, bool set)
        {
            int tributes = _engine.TributesRequired(card);
            if (tributes > 0)
            {
                _engine.NormalSummon(_me, card, set);   // the engine asks which monsters to Tribute
                return;
            }
            BeginPlacement(DuelZone.Monster, Enumerable.Range(0, 5).Where(i => _engine.Me(_me).Monsters[i] == null),
                slot => _engine.NormalSummon(_me, card, set, slot), set ? $"Choose a Monster Zone to Set {card.Name}." : $"Choose a Monster Zone for {card.Name}.");
        }

        private void BeginActivateFromHand(DuelCard card)
        {
            if (DuelRules.IsFieldSpell(card.Data)) { _engine.Activate(_me, card); return; }
            BeginPlacement(DuelZone.SpellTrap, Enumerable.Range(0, 5).Where(i => _engine.Me(_me).SpellTraps[i] == null),
                slot => _engine.Activate(_me, card, slot), $"Choose a Spell & Trap Zone for {card.Name}.");
        }

        private void BeginSetSpellTrap(DuelCard card)
        {
            if (DuelRules.IsFieldSpell(card.Data)) { _engine.SetSpellTrap(_me, card); return; }
            BeginPlacement(DuelZone.SpellTrap, Enumerable.Range(0, 5).Where(i => _engine.Me(_me).SpellTraps[i] == null),
                slot => _engine.SetSpellTrap(_me, card, slot), $"Choose a Spell & Trap Zone to Set {card.Name}.");
        }

        private void BeginPlacement(DuelZone kind, IEnumerable<int> freeSlots, Func<int, bool> action, string hint)
        {
            var slots = freeSlots.ToList();
            if (slots.Count <= 1)
            {
                action(slots.Count == 1 ? slots[0] : -1);
                return;
            }
            _mode = Mode.PlaceCard;
            _placeZoneKind = kind;
            _placeSlots.Clear();
            foreach (int s in slots) _placeSlots.Add(s);
            _placeAction = action;
            ShowHint(hint + "  (Right-click to cancel)");
        }

        private void BeginAttack(DuelMonsterState attacker)
        {
            if (_engine.Me(_cpu).MonsterCount == 0)
            {
                _engine.DeclareAttack(_me, attacker, null);
                return;
            }
            _mode = Mode.AttackTarget;
            _attacker = attacker;
            ShowHint($"Choose a monster for {attacker.Name} to attack.  (Right-click to cancel)");
        }

        private void ShowHint(string text) => AddLog("<color=#8fdcff>" + text + "</color>");

        // ============================================================== engine choices

        private void UpdateChoicePrompt()
        {
            DuelChoice choice = _engine.PendingChoice;
            bool mine = choice != null && choice.Player == _me;
            if (!mine)
            {
                if (_shownChoice != null)
                {
                    _shownChoice = null;
                    _selected.Clear();
                    if (_mode != Mode.PileView) _prompt.gameObject.SetActive(false);
                }
                return;
            }

            if (_shownChoice != choice)
            {
                CancelMode();
                _shownChoice = choice;
                _selected.Clear();
                if (choice.Kind == DuelChoiceKind.SelectOption)
                {
                    ShowOptionPrompt(choice);
                    return;
                }
                _promptConfirm.gameObject.SetActive(true);
                ShowPromptFor(choice.Title, choice.Prompt + SelectionHint(choice), choice.Candidates, selectable: true);
                _promptPass.gameObject.SetActive(choice.IsResponseWindow || choice.MinCount == 0);
                UiKit.SetButtonLabel(_promptPass, choice.IsResponseWindow ? "DON'T ACTIVATE" : "SKIP");
                UiKit.SetButtonLabel(_promptConfirm, choice.IsResponseWindow ? "ACTIVATE" : "CONFIRM");
                _promptCancel.gameObject.SetActive(choice.CanCancel);
                if (choice.IsResponseWindow) PushBanner("CHAIN?", choice.Prompt, DuelVisualResources.Gold);
            }

            if (choice.Kind == DuelChoiceKind.SelectOption) return;
            _promptConfirm.interactable = _selected.Count >= Mathf.Max(1, choice.MinCount) && _selected.Count <= choice.MaxCount;
            foreach (var (card, frame) in _stripItems)
                frame.color = _selected.Contains(card) ? DuelVisualResources.Cyan : new Color(1f, 0.78f, 0.3f, 0.5f);
        }

        /// <summary>"Choose an effect" style choices: one button per option.</summary>
        private void ShowOptionPrompt(DuelChoice choice)
        {
            ShowPromptFor(choice.Title, choice.Prompt, new List<DuelCard>(), selectable: false);
            _promptConfirm.gameObject.SetActive(false);
            _promptPass.gameObject.SetActive(false);
            _promptCancel.gameObject.SetActive(false);
            int count = choice.Options?.Count ?? 0;
            const float w = 300f, h = 52f, gap = 14f;
            int perRow = Mathf.Min(3, Mathf.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Button b = UiKit.Button(_promptStrip, "Option " + i, choice.Options[i], DuelVisualResources.Cyan, () =>
                {
                    if (_engine.PendingChoice == choice) _engine.SubmitOption(index);
                }, 17);
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, count - row * perRow);
                float x = -(inRow * (w + gap) - gap) * 0.5f + col * (w + gap) + w * 0.5f;
                float y = (count > perRow ? 34f : 0f) - row * (h + gap);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
            }
        }

        private static string SelectionHint(DuelChoice choice)
        {
            if (choice.IsResponseWindow) return "\nActivate one of your Set cards in response?";
            return choice.MinCount == choice.MaxCount
                ? $"\nSelect {choice.MinCount} card{(choice.MinCount == 1 ? "" : "s")} — here or on the table."
                : $"\nSelect up to {choice.MaxCount}.";
        }

        private void ToggleSelection(DuelCard card, DuelChoice choice)
        {
            if (_selected.Contains(card)) { _selected.Remove(card); return; }
            if (choice.MaxCount == 1) _selected.Clear();
            if (_selected.Count < choice.MaxCount) _selected.Add(card);
        }

        private void OnPromptConfirm()
        {
            if (_mode == Mode.PileView) { CancelMode(); return; }
            DuelChoice choice = _engine.PendingChoice;
            if (choice == null) return;
            _engine.SubmitChoice(_selected.ToList());
        }

        private void OnPromptPass()
        {
            DuelChoice choice = _engine.PendingChoice;
            if (choice == null) return;
            _selected.Clear();
            _engine.SubmitChoice(new List<DuelCard>());
        }

        private void OnPromptCancel()
        {
            if (_mode == Mode.PileView) { CancelMode(); return; }
            _engine.CancelChoice();
        }

        private void ShowPromptFor(string title, string body, List<DuelCard> cards, bool selectable)
        {
            _promptTitle.text = title;
            _promptBody.text = body;
            foreach (Transform child in _promptStrip) Destroy(child.gameObject);
            _stripItems.Clear();

            const float w = 112f, h = 112f / 0.686f;
            float gap = 10f;
            int maxVisible = 8;
            float total = Mathf.Min(cards.Count, maxVisible) * (w + gap) - gap;
            for (int i = 0; i < cards.Count && i < 16; i++)
            {
                DuelCard card = cards[i];
                int row = i / maxVisible;
                int col = i % maxVisible;
                float scale = cards.Count > maxVisible ? 0.5f : 1f;
                Image frame = UiKit.Panel(_promptStrip, "Pick " + card.Name, new Color(1f, 0.78f, 0.3f, 0.5f), border: false);
                float x = cards.Count > maxVisible ? -475f + col * (w * scale + gap) * 2.2f + row * (w * scale + gap) : -total * 0.5f + col * (w + gap);
                float y = cards.Count > maxVisible ? (row == 0 ? 44f : -44f) : 0f;
                UiKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, y), new Vector2(w * scale + 8f, h * scale + 8f));
                RawImage image = UiKit.Rect(frame.transform, "Face").gameObject.AddComponent<RawImage>();
                UiKit.Stretch(image.rectTransform, 4f, 4f, 4f, 4f);
                image.texture = IsHiddenFromHuman(card) ? CardFaceCompositor.CachedCardBack : CardFaceCompositor.GetFace(card.Data);
                image.raycastTarget = false;
                if (selectable)
                {
                    Button button = frame.gameObject.AddComponent<Button>();
                    DuelCard captured = card;
                    button.onClick.AddListener(() =>
                    {
                        if (_engine.PendingChoice != null) ToggleSelection(captured, _engine.PendingChoice);
                    });
                }
                else
                {
                    Button button = frame.gameObject.AddComponent<Button>();
                    DuelCard captured = card;
                    button.onClick.AddListener(() => _pinned = captured);
                }
                _stripItems.Add((card, frame));
            }
            if (cards.Count > 16)
            {
                Text more = UiKit.Label(_promptStrip, "More", $"+{cards.Count - 16} more", 16, UiKit.MutedText, TextAnchor.MiddleRight);
                UiKit.Place(more.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(160f, 24f));
            }

            _prompt.gameObject.SetActive(true);
            _prompt.SetAsLastSibling();
        }

        private void ShowPile(int owner, DuelZone zone)
        {
            DuelistState d = _engine.Me(owner);
            List<DuelCard> cards = zone switch
            {
                DuelZone.Graveyard => d.Graveyard,
                DuelZone.Banished => d.Banished,
                DuelZone.ExtraDeck => d.ExtraDeck,
                _ => new List<DuelCard>()
            };
            if (_engine.PendingChoice != null && _engine.PendingChoice.Player == _me) return;
            CancelMode();
            _mode = Mode.PileView;
            string whose = owner == _me ? "Your" : "Opponent's";
            string name = zone == DuelZone.Graveyard ? "Graveyard" : zone == DuelZone.Banished ? "Banished cards" : "Extra Deck";
            ShowPromptFor($"{whose} {name}", cards.Count == 0 ? "Empty." : $"{cards.Count} card{(cards.Count == 1 ? "" : "s")} (newest last). Click a card to inspect it.",
                Enumerable.Reverse(cards).ToList(), selectable: false);
            _promptPass.gameObject.SetActive(false);
            _promptCancel.gameObject.SetActive(false);
            UiKit.SetButtonLabel(_promptConfirm, "CLOSE");
            _promptConfirm.interactable = true;
        }

        // ============================================================== events -> log, banners, popups

        private void OnEngineEvent(DuelEvent e)
        {
            switch (e.Type)
            {
                case DuelEventType.DuelStarted:
                    PushBanner("DUEL!", e.Player == _me ? "You go first." : "Your opponent goes first.", DuelVisualResources.Gold);
                    break;
                case DuelEventType.TurnStarted:
                    CancelMode();
                    PushBanner(e.Player == _me ? "YOUR TURN" : "OPPONENT'S TURN", $"Turn {e.Amount}",
                        e.Player == _me ? DuelVisualResources.Cyan : DuelVisualResources.Violet);
                    break;
                case DuelEventType.PhaseChanged:
                    if (e.Phase == DuelPhase.Battle || e.Phase == DuelPhase.End || e.Phase == DuelPhase.Main2)
                        AddLog($"<color=#9aa6c8>— {e.Text} —</color>");
                    if (_engine.Phase == DuelPhase.Battle && e.Player == _me) PushBanner("BATTLE PHASE", "", new Color(1f, 0.45f, 0.3f));
                    return;
                case DuelEventType.Damage:
                    _lpFlash[e.Player].color = new Color(1f, 0.15f, 0.1f, 0.45f);
                    Popup($"-{e.Amount}", new Color(1f, 0.35f, 0.3f), e.Player);
                    break;
                case DuelEventType.LifeGained:
                    Popup($"+{e.Amount}", new Color(0.4f, 1f, 0.6f), e.Player);
                    break;
                case DuelEventType.LifePaid:
                    Popup($"-{e.Amount}", new Color(1f, 0.75f, 0.35f), e.Player);
                    break;
                case DuelEventType.CardActivated:
                    if (e.Player != _me) PushBanner(e.Card != null ? e.Card.Name.ToUpperInvariant() : "ACTIVATE", "Opponent activated a card", DuelVisualResources.Magenta);
                    break;
                case DuelEventType.CardDrawn:
                    if (e.Turn == 0 || e.Player != _engine.TurnPlayer) return;   // opening hands
                    break;
                case DuelEventType.DuelEnded:
                    CancelMode();
                    break;
            }

            if (e.Type == DuelEventType.CardDrawn && e.Player == _me && e.Card != null)
                AddLog($"You drew <b>{e.Card.Name}</b>.");
            else if (!string.IsNullOrEmpty(e.Text) && e.Type != DuelEventType.EffectResolved)
                AddLog(e.Player == _me ? $"<color=#bfeaff>{e.Text}</color>" : e.Player == _cpu ? $"<color=#e2c7ff>{e.Text}</color>" : e.Text);
        }

        private void AddLog(string line)
        {
            _logLines.Add(line);
            while (_logLines.Count > 8) _logLines.RemoveAt(0);
            if (_logText != null) _logText.text = string.Join("\n", _logLines);
        }

        private void PushBanner(string title, string sub, Color color)
        {
            _bannerQueue.Enqueue((title, sub, color));
        }

        private void UpdateBanner()
        {
            float t = Time.unscaledTime - _bannerTime;
            if (t > _bannerDuration && _bannerQueue.Count > 0)
            {
                var (title, sub, color) = _bannerQueue.Dequeue();
                _banner.text = title;
                _bannerSub.text = sub;
                _banner.color = color;
                _bannerTime = Time.unscaledTime;
                _bannerDuration = _bannerQueue.Count > 0 ? 0.8f : 1.25f;
                t = 0f;
            }

            float alpha = t < 0.15f ? t / 0.15f : t > _bannerDuration - 0.3f ? Mathf.Clamp01((_bannerDuration - t) / 0.3f) : 1f;
            if (t > _bannerDuration) alpha = 0f;
            Color c = _banner.color;
            _banner.color = new Color(c.r, c.g, c.b, alpha);
            _bannerSub.color = new Color(1f, 1f, 1f, alpha);
            float scale = 1f + Mathf.Max(0f, 0.15f - t) * 1.5f;
            _banner.rectTransform.localScale = Vector3.one * scale;
        }

        private void Popup(string text, Color color, int player)
        {
            Text label = UiKit.Label(_root, "Popup", text, 54, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            RectTransform panel = (RectTransform)_lpText[player].transform.parent;
            Vector2 anchor = player == _me ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
            UiKit.Place(label.rectTransform, anchor, new Vector2(0.5f, 0.5f), player == _me ? new Vector2(560f, 90f) : new Vector2(560f, -80f), new Vector2(300f, 70f));
            _popups.Add((label, 1.4f, Vector3.zero));
        }

        private void UpdatePopups()
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var (text, life, world) = _popups[i];
                life -= Time.unscaledDeltaTime;
                if (life <= 0f)
                {
                    Destroy(text.gameObject);
                    _popups.RemoveAt(i);
                    continue;
                }
                text.rectTransform.anchoredPosition += new Vector2(0f, 38f * Time.unscaledDeltaTime);
                Color c = text.color;
                text.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(life / 0.6f));
                _popups[i] = (text, life, world);
            }
        }

        // ============================================================== surrender & result

        private void ShowSurrenderConfirm()
        {
            if (_engine.IsOver || _confirm != null) return;
            _confirm = UiKit.Panel(_root, "Surrender Confirm", new Color(0.03f, 0.04f, 0.09f, 0.97f)).rectTransform;
            UiKit.Place(_confirm, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 220f));
            Text text = UiKit.Label(_confirm, "Text", "<b>Surrender this duel?</b>\nYou will receive no Genesis Credits.", 22, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(480f, 100f));
            Button yes = UiKit.Button(_confirm, "Yes", "SURRENDER", DuelVisualResources.Magenta, () => { CancelMode(); _controller.Surrender(); }, 18);
            UiKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 22f), new Vector2(200f, 52f));
            Button no = UiKit.Button(_confirm, "No", "KEEP DUELING", DuelVisualResources.Cyan, CancelMode, 18);
            UiKit.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(10f, 22f), new Vector2(200f, 52f));
            _confirm.SetAsLastSibling();
        }

        private void ShowResult()
        {
            _prompt.gameObject.SetActive(false);
            _menu.gameObject.SetActive(false);
            _result = UiKit.Rect(_root, "Result");
            UiKit.Stretch(_result);
            Image dim = _result.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0.02f, 0.72f);

            bool won = _engine.Winner == _me;
            bool draw = _engine.Winner < 0;
            Color color = won ? DuelVisualResources.Gold : draw ? UiKit.TextColor : DuelVisualResources.Magenta;

            RawImage logo = UiKit.Rect(_result, "Logo").gameObject.AddComponent<RawImage>();
            logo.texture = DuelVisualResources.Logo;
            logo.raycastTarget = false;
            float logoAspect = logo.texture != null ? logo.texture.width / (float)logo.texture.height : 2.65f;
            float resultLogoW = Mathf.Min(520f, 250f * logoAspect);
            UiKit.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(resultLogoW, resultLogoW / logoAspect));

            Text title = UiKit.Label(_result, "Title", won ? "VICTORY" : draw ? "DRAW" : "DEFEAT", 110, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 140f));
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            Text reason = UiKit.Label(_result, "Reason",
                $"{_engine.EndReason}\n<color=#ffd36a>+{_controller.LastReward} Genesis Credits</color>   ·   Turns played: {_engine.TurnNumber}",
                24, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Place(reason.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1100f, 80f));

            Button again = UiKit.Button(_result, "Rematch", "DUEL AGAIN", DuelVisualResources.Cyan, () => _controller.Rematch(), 22);
            UiKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, -150f), new Vector2(300f, 64f));
            Button leave = UiKit.Button(_result, "Return", "RETURN TO GENESIS CITY", DuelVisualResources.Violet, () => _controller.CloseDuel(), 20);
            UiKit.Place((RectTransform)leave.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, -150f), new Vector2(340f, 64f));
        }
    }
}
