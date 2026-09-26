using DuelGenesis.Cards;
using DuelGenesis.Core;
using DuelGenesis.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.UI
{
    public class GenesisWelcomeUI : MonoBehaviour
    {
        private bool _visible = true;
        private float _hideAt;
        private DuelistProfile _profile;
        private PlayerCollection _collection;
        private PlayerDeck _deck;

        private void Start()
        {
            _hideAt = Time.unscaledTime + 8f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                if (_visible) Hide();
                else ShowPersistent();
            }

            if (_visible && Time.unscaledTime >= _hideAt)
                _visible = false;
        }

        public void ShowPersistent()
        {
            _visible = true;
            _hideAt = float.PositiveInfinity;
        }

        public void Hide()
        {
            _visible = false;
        }

        private void Resolve()
        {
            if (_profile == null) _profile = Object.FindFirstObjectByType<DuelistProfile>();
            if (_collection == null) _collection = Object.FindFirstObjectByType<PlayerCollection>();
            if (_deck == null) _deck = Object.FindFirstObjectByType<PlayerDeck>();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            Resolve();

            float width = Mathf.Min(720f, Screen.width - 60f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, 36f, width, 210f);
            GenesisTheme.Box(panel, GenesisTheme.Background);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };

            bool legal = _deck != null && _deck.Validate(_collection, out _);
            string objective = legal
                ? "CURRENT OBJECTIVE: Walk to the neon Duel Table and press E to start a duel."
                : "CURRENT OBJECTIVE: Open packs at the Card Shop and build a legal 40–60 card deck with B.";

            string profile = _profile == null
                ? ""
                : $"LEVEL {_profile.Level} • {_profile.Title} • Record {_profile.Wins}-{_profile.Losses}";

            GUI.Label(new Rect(panel.x + 16f, panel.y + 14f, panel.width - 32f, 38f), "DUEL: GENESIS — PLAYABLE VERTICAL SLICE", title);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 58f, panel.width - 56f, 112f),
                $"{profile}\n\n{objective}\n\nWASD Move  •  Mouse Look  •  E Interact  •  C Collection  •  B Deck Builder  •  F1 Help", body);

            string check = GenesisRuntimeDiagnostics.LastPassed ? "SYSTEM CHECK: PASS" : "SYSTEM CHECK: " + GenesisRuntimeDiagnostics.LastReport;
            GUI.Label(new Rect(panel.x + 24f, panel.yMax - 34f, panel.width - 48f, 24f), check, body);
        }
    }
}
