using DuelGenesis.Cards;
using DuelGenesis.Dueling;
using DuelGenesis.Economy;
using DuelGenesis.Player;
using DuelGenesis.Progression;
using DuelGenesis.Shops;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.UI
{
    public class GenesisProfilePanel : MonoBehaviour
    {
        private bool _open;
        private ThirdPersonPlayerController _player;
        private ThirdPersonCamera _camera;
        private DuelistProfile _profile;
        private GenesisWallet _wallet;
        private PlayerCollection _collection;
        private PlayerDeck _deck;
        private DuelGameController _duel;
        private DeckBuilderUI _deckBuilder;
        private PackOpeningUI _packOpening;
        private GenesisPauseMenu _pause;
        private GenesisMainMenu _mainMenu;

        public bool IsOpen => _open;

        private void Update()
        {
            Resolve();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.pKey.wasPressedThisFrame)
                return;

            if (_open)
            {
                Close();
                return;
            }

            bool blocked = (_mainMenu != null && _mainMenu.IsOpen) ||
                           (_pause != null && _pause.IsOpen) ||
                           (_duel != null && _duel.IsActive) ||
                           (_deckBuilder != null && _deckBuilder.IsOpen) ||
                           (_packOpening != null && _packOpening.IsOpen);
            if (!blocked)
                Open();
        }

        private void Resolve()
        {
            if (_player == null) _player = UnityEngine.Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_camera == null) _camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (_profile == null) _profile = UnityEngine.Object.FindFirstObjectByType<DuelistProfile>();
            if (_wallet == null) _wallet = UnityEngine.Object.FindFirstObjectByType<GenesisWallet>();
            if (_collection == null) _collection = UnityEngine.Object.FindFirstObjectByType<PlayerCollection>();
            if (_deck == null) _deck = UnityEngine.Object.FindFirstObjectByType<PlayerDeck>();
            if (_duel == null) _duel = UnityEngine.Object.FindFirstObjectByType<DuelGameController>();
            if (_deckBuilder == null) _deckBuilder = UnityEngine.Object.FindFirstObjectByType<DeckBuilderUI>();
            if (_packOpening == null) _packOpening = UnityEngine.Object.FindFirstObjectByType<PackOpeningUI>();
            if (_pause == null) _pause = UnityEngine.Object.FindFirstObjectByType<GenesisPauseMenu>();
            if (_mainMenu == null) _mainMenu = UnityEngine.Object.FindFirstObjectByType<GenesisMainMenu>();
        }

        public void Open()
        {
            Resolve();
            _open = true;
            _player?.SetMovementEnabled(false);
            _camera?.SetLookEnabled(false);
        }

        public void Close()
        {
            _open = false;
            _player?.SetMovementEnabled(true);
            _camera?.SetLookEnabled(true);
        }

        private void OnGUI()
        {
            if (!_open) return;
            Resolve();

            GUI.depth = -550;
            float width = Mathf.Min(720f, Screen.width - 60f);
            float height = Mathf.Min(600f, Screen.height - 60f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GenesisTheme.Box(panel, GenesisTheme.Background);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };
            GUIStyle section = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = GenesisTheme.Purple }
            };
            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, 42f), "DUELIST PROFILE", title);

            int level = _profile != null ? _profile.Level : 1;
            int xp = _profile != null ? _profile.XP : 0;
            int next = _profile != null ? _profile.XPToNextLevel : 100;
            float progress = _profile != null ? _profile.LevelProgress01 : 0f;
            string titleText = _profile != null ? _profile.Title : "Rookie Duelist";
            int wins = _profile != null ? _profile.Wins : 0;
            int losses = _profile != null ? _profile.Losses : 0;
            int packs = _profile != null ? _profile.PacksOpened : 0;

            Rect identity = new Rect(panel.x + 34f, panel.y + 78f, panel.width - 68f, 125f);
            GenesisTheme.Box(identity, GenesisTheme.PanelAlt);
            GUI.Label(new Rect(identity.x + 18f, identity.y + 10f, identity.width - 36f, 28f), $"LEVEL {level} — {titleText}", section);
            GUI.Label(new Rect(identity.x + 18f, identity.y + 44f, identity.width - 36f, 24f), $"XP {xp:N0} / {next:N0}", body);
            Rect bar = new Rect(identity.x + 18f, identity.y + 76f, identity.width - 36f, 24f);
            GenesisTheme.Box(bar, new Color(0.08f, 0.10f, 0.16f, 1f));
            GenesisTheme.Box(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Mathf.Clamp01(progress), bar.height - 4f), GenesisTheme.Cyan);

            float top = panel.y + 220f;
            GUI.Label(new Rect(panel.x + 34f, top, panel.width - 68f, 28f), "CAREER", section);
            GUI.Label(new Rect(panel.x + 48f, top + 34f, panel.width - 96f, 64f),
                $"Wins: {wins}     Losses: {losses}     Total Duels: {wins + losses}\nBoosters Opened: {packs}", body);

            top += 112f;
            GUI.Label(new Rect(panel.x + 34f, top, panel.width - 68f, 28f), "COLLECTION & ECONOMY", section);
            int cards = _collection != null ? _collection.TotalCardCount : 0;
            int unique = _collection != null ? _collection.UniqueCardCount : 0;
            int deckCount = _deck != null ? _deck.MainDeckCount : 0;
            bool legal = _deck != null && _deck.Validate(_collection, out _);
            int gc = _wallet != null ? _wallet.GenesisCredits : 0;
            GUI.Label(new Rect(panel.x + 48f, top + 34f, panel.width - 96f, 88f),
                $"Genesis Credits: {gc:N0} GC\nCollection: {cards} cards / {unique} unique\nMain Deck: {deckCount} cards — {(legal ? "LEGAL" : "INCOMPLETE")}\nAuto-save: ACTIVE", body);

            GUI.Label(new Rect(panel.x + 34f, panel.yMax - 78f, panel.width - 68f, 26f),
                "Press P to close. Progress is stored automatically on this device.", body);

            if (GenesisTheme.Button(new Rect(panel.center.x - 90f, panel.yMax - 44f, 180f, 30f), "CLOSE PROFILE", GenesisTheme.Purple))
                Close();
        }
    }
}
