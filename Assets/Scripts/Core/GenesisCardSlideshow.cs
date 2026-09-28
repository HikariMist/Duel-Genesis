using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Core
{
    /// <summary>
    /// A video wall: shows the art of the game's rarest cards (Ultra and Secret Rares), changing every few
    /// seconds with a quick fade through white. Screens with different <see cref="offset"/> show different cards.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class GenesisCardSlideshow : MonoBehaviour
    {
        public float seconds = 4.5f;
        public int offset;
        public float brightness = 1.4f;

        private static List<CardData> _featured;
        private Material _material;
        private int _index = -1;
        private float _next;

        private void Start()
        {
            _material = GetComponent<Renderer>().material;   // this screen's own copy
            _index = offset;
            _next = Time.time + 0.5f + offset * 0.37f;
        }

        private void Update()
        {
            if (_material == null) return;
            float fade = Mathf.Clamp01((_next - Time.time) / 0.35f);   // brighten just before the cut
            _material.SetColor("_EmissionColor", Color.white * Mathf.Lerp(brightness * 2.2f, brightness, fade));
            if (Time.time < _next) return;
            _next = Time.time + seconds;

            if (_featured == null || _featured.Count == 0)
            {
                if (!CardDatabase.IsReady) return;
                _featured = CardDatabase.All.Where(c => c != null && c.rarity >= CardRarity.UltraRare).OrderBy(c => c.cardName).ToList();
                if (_featured.Count == 0) _featured = CardDatabase.All.Where(c => c != null).Take(40).ToList();
            }
            for (int tries = 0; tries < 6; tries++)
            {
                _index = (_index + 5 + offset) % _featured.Count;
                Texture2D art = ProductionCardArtRegistry.LoadFace(_featured[_index].cardName);
                if (art == null) continue;
                _material.SetTexture("_BaseMap", art);
                _material.SetTexture("_EmissionMap", art);
                break;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession() => _featured = null;
    }
}
