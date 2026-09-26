using DuelGenesis.Cards;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.UI
{
    public class GenesisMainMenu : MonoBehaviour
    {
        private ThirdPersonPlayerController _player;
        private ThirdPersonCamera _camera;
        private DuelistProfile _profile;
        private GenesisWallet _wallet;
        private PlayerCollection _collection;
        private PlayerDeck _deck;
        private bool _open = true;
        private bool _showControls;

        public bool IsOpen => _open;

        private void Start()
        {
            Resolve();
            Open();
        }

        private void Update()
        {
            if (!_open) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                EnterGenesis();
        }

        private void Resolve()
        {
            if (_player == null) _player = UnityEngine.Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_camera == null) _camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (_profile == null) _profile = UnityEngine.Object.FindFirstObjectByType<DuelistProfile>();
            if (_wallet == null) _wallet = UnityEngine.Object.FindFirstObjectByType<GenesisWallet>();
            if (_collection == null) _collection = UnityEngine.Object.FindFirstObjectByType<PlayerCollection>();
            if (_deck == null) _deck = UnityEngine.Object.FindFirstObjectByType<PlayerDeck>();
        }

        public void Open()
        {
            Resolve();
            _open = true;
            _showControls = false;
            _player?.SetMovementEnabled(false);
            _camera?.SetLookEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void EnterGenesis()
        {
            Resolve();
            _open = false;
            _showControls = false;
            _player?.SetMovementEnabled(true);
            _camera?.SetLookEnabled(true);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnGUI()
        {
            if (!_open) return;
            Resolve();

            GUI.depth = -700;
            GenesisTheme.Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.015f, 0.02f, 0.045f, 1f));

            float width = Mathf.Min(760f, Screen.width - 60f);
            float height = Mathf.Min(560f, Screen.height - 60f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GenesisTheme.Box(panel, GenesisTheme.Background);

            GUIStyle logo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };
            GUIStyle subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Purple }
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(panel.x + 30f, panel.y + 28f, panel.width - 60f, 60f), "DUEL: GENESIS", logo);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 88f, panel.width - 60f, 30f), "GENESIS CITY // PLAYABLE VERTICAL SLICE v0.6", subtitle);

            string profileLine = _profile == null
                ? "Loading duelist profile..."
                : $"Lv.{_profile.Level} {_profile.Title}   •   Record {_profile.Wins}-{_profile.Losses}   •   Packs {_profile.PacksOpened}";
            string economyLine = $"{(_wallet != null ? _wallet.GenesisCredits : 0):N0} GC   •   {(_collection != null ? _collection.TotalCardCount : 0)} Cards   •   {(_deck != null ? _deck.MainDeckCount : 0)}-Card Main Deck";

            GUI.Label(new Rect(panel.x + 40f, panel.y + 132f, panel.width - 80f, 58f), profileLine + "\n" + economyLine, body);

            if (_showControls)
            {
                Rect controls = new Rect(panel.x + 70f, panel.y + 205f, panel.width - 140f, 170f);
                GenesisTheme.Box(controls, GenesisTheme.PanelAlt);
                GUI.Label(new Rect(controls.x + 16f, controls.y + 12f, controls.width - 32f, controls.height - 24f),
                    "WASD — Move     Mouse — Look     Space — Jump\nE — Interact / Duel     C — Collection     B — Deck Builder\nP — Duelist Profile     F1 — Help     Esc — Pause\n\nDuring duels, use the on-screen buttons to Summon, Set, activate effects and choose battle targets.", body);
            }
            else
            {
                GUI.Label(new Rect(panel.x + 70f, panel.y + 210f, panel.width - 140f, 135f),
                    "Explore Genesis City, open boosters, build your collection, create a legal deck and duel the CPU on the physical hologram table. Progress, currency, collection and your duel record save automatically.", body);
            }

            float buttonY = panel.yMax - 150f;
            if (GenesisTheme.Button(new Rect(panel.center.x - 150f, buttonY, 300f, 44f), "ENTER GENESIS", GenesisTheme.Cyan))
                EnterGenesis();

            if (GenesisTheme.Button(new Rect(panel.center.x - 150f, buttonY + 52f, 145f, 38f), _showControls ? "HIDE CONTROLS" : "CONTROLS", GenesisTheme.Purple))
                _showControls = !_showControls;

            if (GenesisTheme.Button(new Rect(panel.center.x + 5f, buttonY + 52f, 145f, 38f), "QUIT", GenesisTheme.Danger))
                QuitGame();

            GUI.Label(new Rect(panel.x + 30f, panel.yMax - 42f, panel.width - 60f, 24f), "ENTER or SPACE — Start", body);
        }
    }
}
