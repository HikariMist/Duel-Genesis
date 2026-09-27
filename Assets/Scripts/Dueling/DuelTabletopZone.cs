using UnityEngine;

namespace DuelGenesis.Dueling
{
    public enum DuelTabletopZoneKind
    {
        Monster,
        SpellTrap,
        Deck,
        Graveyard,
        ExtraDeck,
        FieldSpell,
        Banished,
        ExtraMonster
    }

    /// <summary>
    /// Lightweight world-space interaction surface for a tabletop duel zone.
    /// Gameplay actions are reported to the presentation/input layer; the duel engine
    /// remains authoritative.
    /// </summary>
    public sealed class DuelTabletopZone : MonoBehaviour
    {
        private DuelTabletopPresentation _owner;
        private Renderer _renderer;
        private Material _material;
        private Color _baseColor;
        private bool _occupied;
        private bool _hovered;
        private bool _selected;

        public bool PlayerSide { get; private set; }
        public int Index { get; private set; }
        public DuelTabletopZoneKind Kind { get; private set; }
        public bool Occupied => _occupied;

        public void Configure(
            DuelTabletopPresentation owner,
            bool playerSide,
            DuelTabletopZoneKind kind,
            int index,
            Renderer zoneRenderer,
            Color baseColor)
        {
            _owner = owner;
            PlayerSide = playerSide;
            Kind = kind;
            Index = index;
            _renderer = zoneRenderer;
            _baseColor = baseColor;

            if (_renderer != null)
                _material = _renderer.material;

            RefreshColor();
        }

        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            RefreshColor();
        }

        public void SetOccupied(bool occupied)
        {
            if (_occupied == occupied) return;
            _occupied = occupied;
            RefreshColor();
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            RefreshColor();
        }

        private void OnMouseEnter()
        {
            _hovered = true;
            RefreshColor();
        }

        private void OnMouseExit()
        {
            _hovered = false;
            RefreshColor();
        }

        private void OnMouseDown()
        {
            if (_owner != null && _owner.IsTabletopActive)
                _owner.OnZoneClicked(this);
        }

        private void RefreshColor()
        {
            if (_material == null) return;

            Color color = _baseColor;
            if (_occupied)
                color = Color.Lerp(color, Color.white, 0.10f);
            if (_hovered)
                color = Color.Lerp(color, Color.white, 0.30f);
            if (_selected)
                color = Color.Lerp(color, new Color(1f, 0.78f, 0.18f, 1f), 0.58f);

            if (_material.HasProperty("_BaseColor"))
                _material.SetColor("_BaseColor", color);
            if (_material.HasProperty("_Color"))
                _material.SetColor("_Color", color);
            if (_material.HasProperty("_EmissionColor"))
            {
                _material.EnableKeyword("_EMISSION");
                float emission = _hovered || _selected ? 0.95f : 0.12f;
                _material.SetColor("_EmissionColor", color * emission);
            }
        }
    }
}
