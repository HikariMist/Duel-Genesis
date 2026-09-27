using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// A furnished duel table where two NPCs are mid-duel: the same table, mat and arena as the
    /// playable table, with a few face-up monsters (and their holograms) and set cards on each side.
    /// Placed by the Genesis City builder; builds itself at runtime.
    /// </summary>
    public sealed class AmbientDuelTable : MonoBehaviour
    {
        [Tooltip("Random seed so each ambient table shows a different, stable board.")]
        public int seed = 1;

        private readonly List<(DuelCard card, MeshRenderer renderer, MaterialPropertyBlock block)> _faces = new();
        private readonly List<(MonsterHologram hologram, Vector3 position, int player)> _holograms = new();
        private Transform _root;
        private float _nextShuffle;

        private void Start()
        {
            _root = DuelTableBuilder.BuildDecorative(transform);
        }

        private void Update()
        {
            if (_root == null) return;
            if (_faces.Count == 0 && CardDatabase.All != null && CardDatabase.All.Count > 0)
                DealBoard();

            foreach (var f in _faces)
            {
                if (f.block.GetTexture("_BaseMap") != null) continue;
                if (CardFaceCompositor.TryGetFace(f.card.Data, out Texture2D face) && face != null)
                {
                    f.block.SetTexture("_BaseMap", face);
                    f.block.SetTexture("_MainTex", face);
                    f.renderer.SetPropertyBlock(f.block, 0);
                }
            }
            foreach (var h in _holograms)
                if (h.hologram != null) h.hologram.SetPose(h.position, DuelMatLayout.Yaw(h.player), false);
        }

        private void DealBoard()
        {
            var rng = new System.Random(seed * 7919 + 13);
            List<CardData> monsters = CardDatabase.All.Where(c => c.kind == CardKind.Monster && CardModelRegistry.LoadPrefab(c) != null && c.level <= 8).ToList();
            List<CardData> backrow = CardDatabase.All.Where(c => c.kind != CardKind.Monster).ToList();
            if (monsters.Count == 0) return;

            Transform cards = new GameObject("Ambient Cards").transform;
            cards.SetParent(_root, false);
            int uid = 90000 + seed * 100;
            for (int player = 0; player < 2; player++)
            {
                int count = 1 + rng.Next(3);
                var slots = Enumerable.Range(0, 5).OrderBy(_ => rng.Next()).Take(count).ToList();
                foreach (int slot in slots)
                {
                    CardData data = monsters[rng.Next(monsters.Count)];
                    var card = new DuelCard(uid++, data, player) { FaceUp = true, Zone = DuelZone.Monster, Slot = slot };
                    Vector3 pos = DuelMatLayout.MonsterZone(player, slot);
                    PlaceCard(cards, card, pos, player, faceUp: true);
                    MonsterHologram h = MonsterHologram.TryCreate(cards, card);
                    if (h != null) _holograms.Add((h, pos + new Vector3(0f, DuelMatLayout.CardThicknessMm * 0.001f, 0f), player));
                }

                int sets = rng.Next(3);
                for (int i = 0; i < sets && backrow.Count > 0; i++)
                {
                    var card = new DuelCard(uid++, backrow[rng.Next(backrow.Count)], player) { Zone = DuelZone.SpellTrap, Slot = i + 1 };
                    PlaceCard(cards, card, DuelMatLayout.SpellTrapZone(player, i + 1), player, faceUp: false);
                }
                // Deck stack.
                var deckCard = new DuelCard(uid++, monsters[0], player) { Zone = DuelZone.Deck };
                GameObject deck = PlaceCard(cards, deckCard, DuelMatLayout.DeckZone(player), player, faceUp: false);
                deck.transform.localScale = new Vector3(1f, 60f, 1f); // ~20 mm stack of 40 cards
                deck.transform.localPosition = DuelMatLayout.DeckZone(player) + new Vector3(0f, DuelMatLayout.CardThicknessMm * 0.001f * 30f, 0f);
            }
        }

        private GameObject PlaceCard(Transform parent, DuelCard card, Vector3 position, int player, bool faceUp)
        {
            GameObject go = new GameObject("Card " + card.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position + new Vector3(0f, DuelMatLayout.CardThicknessMm * 0.0005f, 0f);
            go.transform.localRotation = Quaternion.Euler(faceUp ? 0f : 180f, DuelMatLayout.Yaw(player), 0f);
            go.AddComponent<MeshFilter>().sharedMesh = DuelVisualResources.CardMesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { DuelVisualResources.FaceMaterial, DuelVisualResources.BackMaterial, DuelVisualResources.EdgeMaterial };
            if (faceUp)
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block, 0);
                _faces.Add((card, r, block));
            }
            return go;
        }
    }
}
