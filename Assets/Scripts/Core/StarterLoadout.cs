using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Core
{
    public class StarterLoadout : MonoBehaviour
    {
        private const string ClaimedKey = "DUEL_GENESIS_STARTER_V1";

        private static readonly string[] StarterCardIds =
        {
            "DG001", "DG002", "DG003", "DG004", "DG005",
            "DG006", "DG007", "DG008", "DG009", "DG010",
            "DG011", "DG012", "DG013", "DG014", "DG015",
            "DG016", "DG017", "DG018", "DG019", "DG020"
        };

        public static int StarterDeckSize => StarterCardIds.Length * 2;

        private void Start()
        {
            GrantIfNeeded();
        }

        public void GrantIfNeeded()
        {
            if (PlayerPrefs.GetInt(ClaimedKey, 0) == 1)
                return;

            PlayerCollection collection = Object.FindFirstObjectByType<PlayerCollection>();
            PlayerDeck deck = Object.FindFirstObjectByType<PlayerDeck>();
            if (collection == null || deck == null)
                return;

            // Existing development saves are never overwritten or duplicated.
            if (collection.TotalCardCount > 0 || deck.MainDeckCount > 0)
            {
                MarkClaimed();
                return;
            }

            foreach (string id in StarterCardIds)
            {
                CardData card = CardDatabase.GetById(id);
                if (card == null)
                {
                    Debug.LogError($"Starter loadout missing card {id}.");
                    continue;
                }

                collection.AddCard(card, 2);
                deck.AddCard(card, collection);
                deck.AddCard(card, collection);
            }

            MarkClaimed();
            Debug.Log($"Duel: Genesis starter loadout granted — {deck.MainDeckCount} card legal Main Deck plus starting collection.");
        }

        private static void MarkClaimed()
        {
            PlayerPrefs.SetInt(ClaimedKey, 1);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        public static void ClearClaimFlagForTesting()
        {
            PlayerPrefs.DeleteKey(ClaimedKey);
            PlayerPrefs.Save();
        }
#endif
    }
}
