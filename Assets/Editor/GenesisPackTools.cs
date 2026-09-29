#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuelGenesis.Cards;
using DuelGenesis.Shops;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>Pack-counter shortcuts and a pull-rate check for the rarity system.</summary>
    public static class GenesisPackTools
    {
        [MenuItem("Duel Genesis/DEV/Open Pack Counter Here (Play Mode)")]
        public static void OpenCounter()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Duel: Genesis: enter Play mode first."); return; }
            var player = Object.FindAnyObjectByType<DuelGenesis.Player.ThirdPersonPlayerController>();
            if (player == null) return;
            var terminal = Object.FindAnyObjectByType<CardShopTerminal>();
            if (terminal != null) terminal.Interact(player.gameObject);
        }

        [MenuItem("Duel Genesis/DEV/Simulate 1000 Packs Of Each Type (Pull Rates)")]
        public static void Simulate()
        {
            if (!CardDatabase.IsReady)
            {
                Debug.LogWarning("Duel: Genesis: the card catalog is loaded in Play mode; enter Play mode and run this again.");
                return;
            }
            var log = new StringBuilder("Duel: Genesis pull rates over 1000 packs of each type:\n");
            CardRarity[] order = { CardRarity.Common, CardRarity.Rare, CardRarity.SuperRare, CardRarity.UltraRare, CardRarity.SecretRare };
            foreach (GenesisPackType pack in GenesisPacks.All)
            {
                List<CardData> pool = pack.Pool();
                var inPool = order.ToDictionary(r => r, r => pool.Count(c => c.rarity == r));
                var pulled = order.ToDictionary(r => r, r => 0);
                int packsWithSuper = 0, packsWithUltra = 0, packsWithSecret = 0;
                for (int i = 0; i < 1000; i++)
                {
                    List<CardData> cards = GenesisPacks.Roll(pack);
                    if (cards == null) break;
                    foreach (CardData c in cards) pulled[c.rarity]++;
                    CardRarity best = cards.Max(c => c.rarity);
                    if (best >= CardRarity.SuperRare) packsWithSuper++;
                    if (best >= CardRarity.UltraRare) packsWithUltra++;
                    if (best >= CardRarity.SecretRare) packsWithSecret++;
                }
                log.AppendLine($"{pack.displayName,-12} pool {pool.Count} ({string.Join(" / ", order.Select(r => inPool[r]))} C/R/SR/UR/ScR) | " +
                               $"packs with Super+ {packsWithSuper / 10f:0.#}%, Ultra+ {packsWithUltra / 10f:0.#}%, Secret {packsWithSecret / 10f:0.#}%");
            }
            Debug.Log(log.ToString());
        }
    }
}
#endif
