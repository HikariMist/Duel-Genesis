using System.Collections.Generic;
using System.Reflection;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Master-Duel-style hand presentation anchored to the lower part of the screen.
    /// Uses the real duel hand and the production card renderer, so opening cards,
    /// draws, discards and played cards stay in sync with DuelGameController.
    /// </summary>
    public sealed class DuelMasterStyleHandHUD : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private DuelGameController _duel;
        private DuelPhysicalInputController _physicalInput;
        private FieldInfo _playerHandField;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelMasterStyleHandHUD>() != null)
                return;

            GameObject host = new GameObject("Duel Master Style Hand HUD");
            host.AddComponent<DuelMasterStyleHandHUD>();
        }

        private void Awake()
        {
            _playerHandField = typeof(DuelGameController).GetField("_playerHand", PrivateInstance);
        }

        private void Update()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
            if (_physicalInput == null)
                _physicalInput = Object.FindFirstObjectByType<DuelPhysicalInputController>();

            // The new hand is intentionally screen-space. Hide the old tiny 3D hand row
            // so the player only sees one clear, readable hand presentation.
            GameObject oldHand = GameObject.Find("DG Physical Player Hand");
            if (oldHand != null && oldHand.activeSelf)
                oldHand.SetActive(false);
        }

        private List<CardData> PlayerHand()
        {
            return _duel == null || _playerHandField == null
                ? null
                : _playerHandField.GetValue(_duel) as List<CardData>;
        }

        private void OnGUI()
        {
            if (_duel == null || !_duel.IsActive || _duel.IsDuelOver)
                return;

            List<CardData> hand = PlayerHand();
            if (hand == null || hand.Count == 0)
                return;

            GUI.depth = -800;

            const float cardWidth = 118f;
            const float cardHeight = 166f;
            float maxRowWidth = Mathf.Min(Screen.width - 80f, 920f);
            float spacing = hand.Count <= 1
                ? cardWidth
                : Mathf.Min(cardWidth * 0.78f, (maxRowWidth - cardWidth) / (hand.Count - 1));

            float rowWidth = cardWidth + spacing * Mathf.Max(0, hand.Count - 1);
            float startX = (Screen.width - rowWidth) * 0.5f;

            // Leave a clean strip beneath the cards for the contextual Summon/Set/Attack panel.
            float baseY = Screen.height - cardHeight - 126f;

            for (int i = 0; i < hand.Count; i++)
            {
                CardData card = hand[i];
                float arc = hand.Count <= 1 ? 0f : Mathf.Abs(i - (hand.Count - 1) * 0.5f) * 2.5f;
                Rect rect = new Rect(startX + spacing * i, baseY + arc, cardWidth, cardHeight);

                ProductionCardVisualDrawer.DrawCard(rect, card);

                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.001f);
                if (GUI.Button(rect, GUIContent.none))
                    _physicalInput?.ClickHandCard(card);
                GUI.color = old;
            }
        }
    }
}
