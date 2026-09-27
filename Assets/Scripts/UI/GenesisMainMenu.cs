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
        private Texture2D _titleLogo;
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

            // Put the supplied logo at Assets/Resources/UI/DuelGenesisLogo.png.
            // Resources.Load omits the Resources folder and file extension.
            if (_titleLogo == null)
                _titleLogo = Resources.Load<Texture2D>("UI/DuelGenesisLogo");
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

            float width = Mathf.Min(920f, Screen.width - 40f);
            float height = Mathf.Min(720f, Screen.height - 40f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GenesisTheme.Box(panel, GenesisTheme.Background);

            GUIStyle fallbackLogo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
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

            float logoHeight = Mathf.Clamp(panel.height * 0.31f, 140f, 225f);
            Rect logoRect = new Rect(panel.x + 42f, panel.y + 10f, panel.width - 84f, logoHeight);

            if (_titleLogo != null)
            {
                Color previous = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(logoRect, _titleLogo, ScaleMode.ScaleToFit, true);
                GUI.color = previous;
            }
            else
            {
                GUI.Label(logoRect, "DUEL: GENESIS", fallbackLogo);
            }

            float subtitleY = logoRect.yMax - 2f;
            GUI.Label(new Rect(panel.x + 30f, subtitleY, panel.width - 60f, 30f),
                "GENESIS CITY // PLAYABLE VERTICAL SLICE v0.6", subtitle);

            string profileLine = _profile == null
                ? "Loading duelist profile..."
                : $"Lv.{_profile.Level} {_profile.Title}   •   Record {_profile.Wins}-{_profile.Losses}   •   Packs {_profile.PacksOpened}";
            string economyLine = $"{(_wallet != null ? _wallet.GenesisCredits : 0):N0} GC   •   {(_collection != null ? _collection.TotalCardCount : 0)} Cards   •   {(_deck != null ? _deck.MainDeckCount : 0)}-Card Main Deck";

            float infoY = subtitleY + 34f;
            GUI.Label(new Rect(panel.x + 40f, infoY, panel.width - 80f, 58f), profileLine + "\n" + economyLine, body);

            float buttonY = panel.yMax - 150f;
            float contentY = infoY + 66f;
            float contentHeight = Mathf.Max(80f, buttonY - contentY - 12f);

            if (_showControls)
            {
                Rect controls = new Rect(panel.x + 70f, contentY, panel.width - 140f, contentHeight);
                GenesisTheme.Box(controls, GenesisTheme.PanelAlt);
                GUI.Label(new Rect(controls.x + 16f, controls.y + 10f, controls.width - 32f, controls.height - 20f),
                    "WASD — Move     Mouse — Look     Space — Jump\nE — Interact / Duel     C — Collection     B — Deck Builder\nP — Duelist Profile     F1 — Help     Esc — Pause\n\nDuring duels, click your cards and field zones to Summon, Set, activate effects and choose battle targets.", body);
            }
            else
            {
                GUI.Label(new Rect(panel.x + 70f, contentY + 4f, panel.width - 140f, Mathf.Max(60f, contentHeight - 8f)),
                    "Explore Genesis City, open boosters, build your collection, create a legal deck and duel on the physical hologram table. Progress, currency, collection and your duel record save automatically.", body);
            }

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
