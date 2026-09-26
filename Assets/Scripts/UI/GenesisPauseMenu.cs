using DuelGenesis.Dueling;
using DuelGenesis.Player;
using DuelGenesis.Shops;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.UI
{
    public class GenesisPauseMenu : MonoBehaviour
    {
        private bool _open;
        private bool _movementWasEnabled = true;
        private ThirdPersonPlayerController _player;
        private ThirdPersonCamera _camera;
        private DeckBuilderUI _deckBuilder;
        private PackOpeningUI _packOpening;
        private DuelPrototype _duel;
        private GenesisWelcomeUI _help;

        public bool IsOpen => _open;

        private void Update()
        {
            Resolve();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (_open)
            {
                Resume();
                return;
            }

            bool modalOpen = (_deckBuilder != null && _deckBuilder.IsOpen) ||
                             (_packOpening != null && _packOpening.IsOpen) ||
                             (_duel != null && _duel.IsActive);

            if (!modalOpen)
                Pause();
        }

        private void Resolve()
        {
            if (_player == null) _player = Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_camera == null) _camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (_deckBuilder == null) _deckBuilder = Object.FindFirstObjectByType<DeckBuilderUI>();
            if (_packOpening == null) _packOpening = Object.FindFirstObjectByType<PackOpeningUI>();
            if (_duel == null) _duel = Object.FindFirstObjectByType<DuelPrototype>();
            if (_help == null) _help = Object.FindFirstObjectByType<GenesisWelcomeUI>();
        }

        private void Pause()
        {
            Resolve();
            _open = true;
            _movementWasEnabled = _player == null || _player.MovementEnabled;
            Time.timeScale = 0f;
            _player?.SetMovementEnabled(false);
            _camera?.SetLookEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Resume()
        {
            _open = false;
            Time.timeScale = 1f;
            _player?.SetMovementEnabled(_movementWasEnabled);
            _camera?.SetLookEnabled(true);
        }

        private void ShowHelp()
        {
            Resume();
            _help?.ShowPersistent();
        }

        private void OnDisable()
        {
            if (_open)
            {
                Time.timeScale = 1f;
                _player?.SetMovementEnabled(_movementWasEnabled);
                _open = false;
            }
        }

        private void OnGUI()
        {
            if (!_open) return;

            GUI.depth = -500;
            Rect panel = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 180f, 480f, 360f);
            GenesisTheme.Box(panel, GenesisTheme.Background);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(panel.x + 20f, panel.y + 24f, panel.width - 40f, 46f), "DUEL: GENESIS", title);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 75f, panel.width - 56f, 45f), "PAUSED // GENESIS CITY PROTOTYPE DISTRICT", body);

            if (GenesisTheme.Button(new Rect(panel.x + 100f, panel.y + 140f, 280f, 42f), "RESUME", GenesisTheme.Cyan))
                Resume();

            if (GenesisTheme.Button(new Rect(panel.x + 100f, panel.y + 194f, 280f, 42f), "CONTROLS / CURRENT OBJECTIVE", GenesisTheme.Purple))
                ShowHelp();

            if (GenesisTheme.Button(new Rect(panel.x + 100f, panel.y + 248f, 280f, 42f), "QUIT GAME", GenesisTheme.Danger))
            {
                Time.timeScale = 1f;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            GUI.Label(new Rect(panel.x + 30f, panel.yMax - 48f, panel.width - 60f, 28f), "ESC — Resume", body);
        }
    }
}
