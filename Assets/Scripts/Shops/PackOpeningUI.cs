using System.Collections.Generic;
using DuelGenesis.Cards;
using DuelGenesis.Player;
using DuelGenesis.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Shops
{
    public class PackOpeningUI : MonoBehaviour
    {
        private readonly List<CardData> _lastPack = new();
        private bool _open;
        private int _revealedCount;
        private ThirdPersonPlayerController _playerController;
        private ThirdPersonCamera _thirdPersonCamera;
        private Vector2 _scroll;

        public bool IsOpen => _open;

        public void OpenPack(GameObject player)
        {
            if (_open || player == null) return;

            PlayerCollection collection = player.GetComponent<PlayerCollection>();
            if (collection == null)
                collection = player.AddComponent<PlayerCollection>();

            _lastPack.Clear();

            for (int i = 0; i < 5; i++)
            {
                CardData card = CardDatabase.GetRandomCard(i == 4);
                _lastPack.Add(card);
                collection.AddCard(card);
            }

            _revealedCount = 1;
            _open = true;
            _scroll = Vector2.zero;
            _playerController = player.GetComponent<ThirdPersonPlayerController>();
            _thirdPersonCamera = Object.FindFirstObjectByType<ThirdPersonCamera>();

            _playerController?.SetMovementEnabled(false);
            _thirdPersonCamera?.SetLookEnabled(false);
        }

        private void Update()
        {
            if (!_open) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool revealPressed =
                (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame);

            if (revealPressed && _revealedCount < _lastPack.Count)
            {
                _revealedCount++;
                return;
            }

            bool closePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (_revealedCount >= _lastPack.Count && keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                closePressed = true;

            if (closePressed)
                Close();
        }

        public void Close()
        {
            if (!_open) return;

            _open = false;
            _playerController?.SetMovementEnabled(true);
            _thirdPersonCamera?.SetLookEnabled(true);
            _playerController = null;
            _thirdPersonCamera = null;
        }

        private void OnGUI()
        {
            if (!_open) return;

            float width = Mathf.Min(920f, Screen.width - 40f);
            float height = Mathf.Min(680f, Screen.height - 40f);
            Rect windowRect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GenesisTheme.Box(windowRect, GenesisTheme.Background);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Cyan }
            };

            GUIStyle subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUIStyle cardName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(windowRect.x + 20f, windowRect.y + 18f, width - 40f, 42f), "GENESIS BOOSTER OPENING", title);
            GUI.Label(new Rect(windowRect.x + 20f, windowRect.y + 58f, width - 40f, 28f), $"Card {_revealedCount} of {_lastPack.Count}", subtitle);

            Rect scrollRect = new Rect(windowRect.x + 30f, windowRect.y + 100f, width - 60f, height - 170f);
            Rect contentRect = new Rect(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, _revealedCount * 120f));
            _scroll = GUI.BeginScrollView(scrollRect, _scroll, contentRect);

            for (int i = 0; i < _revealedCount && i < _lastPack.Count; i++)
            {
                CardData card = _lastPack[i];
                float y = i * 120f;
                Rect cardRect = new Rect(0f, y, contentRect.width, 110f);
                GenesisTheme.Box(cardRect, GenesisTheme.CardColor(card));

                Color old = GUI.contentColor;
                GUI.contentColor = GenesisTheme.RarityColor(card.rarity);
                GUI.Label(new Rect(16f, y + 8f, contentRect.width - 32f, 28f), $"{card.cardName}  •  {card.RarityLabel}", cardName);
                GUI.contentColor = Color.white;
                GUI.Label(new Rect(16f, y + 38f, contentRect.width - 32f, 22f), $"{card.kind}  |  {card.attribute}  |  {card.ShortStats}", body);
                GUI.Label(new Rect(16f, y + 63f, contentRect.width - 32f, 42f), card.effectText, body);
                GUI.contentColor = old;
            }

            GUI.EndScrollView();

            string instructions = _revealedCount < _lastPack.Count
                ? "SPACE / ENTER / LEFT CLICK — Reveal next card"
                : "ENTER or ESC — Close pack   •   Cards saved to your collection";

            GUI.Label(new Rect(windowRect.x + 20f, windowRect.yMax - 54f, width - 40f, 30f), instructions, subtitle);
        }
    }
}
