using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Turns the large physical monster-zone boxes into the primary click targets.
    /// This makes battle selection much easier than trying to click the small hologram/model itself.
    /// </summary>
    public sealed class DuelLargeZoneInput : MonoBehaviour
    {
        private DuelGameController _duel;
        private DuelPhysicalInputController _input;
        private Transform _tabletop;
        private bool _wired;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelLargeZoneInput>() != null)
                return;

            GameObject host = new GameObject("Duel Large Zone Input");
            host.AddComponent<DuelLargeZoneInput>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolve)
                return;

            _nextResolve = Time.unscaledTime + 0.20f;

            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
            if (_input == null)
                _input = Object.FindFirstObjectByType<DuelPhysicalInputController>();
            if (_tabletop == null)
            {
                GameObject table = GameObject.Find("Duel Table Prototype");
                if (table != null)
                    _tabletop = table.transform.Find("DG Physical Tabletop");
            }

            if (!_wired && _tabletop != null)
                WireMonsterZones();
        }

        private void WireMonsterZones()
        {
            for (int i = 0; i < 5; i++)
            {
                WireZone($"Player Monster Zone {i + 1}", true, i);
                WireZone($"CPU Monster Zone {i + 1}", false, i);
            }

            _wired = true;
        }

        private void WireZone(string name, bool playerSide, int index)
        {
            Transform zone = _tabletop.Find(name);
            if (zone == null)
                return;

            DuelLargeMonsterZoneTarget target = zone.GetComponent<DuelLargeMonsterZoneTarget>();
            if (target == null)
                target = zone.gameObject.AddComponent<DuelLargeMonsterZoneTarget>();
            target.Configure(playerSide, index);
        }
    }

    public sealed class DuelLargeMonsterZoneTarget : MonoBehaviour
    {
        private bool _playerSide;
        private int _index;
        private DuelGameController _duel;
        private DuelPhysicalInputController _input;
        private Renderer _renderer;
        private Color _baseColor;
        private bool _hasBaseColor;

        public void Configure(bool playerSide, int index)
        {
            _playerSide = playerSide;
            _index = index;
            _renderer = GetComponent<Renderer>();
            CacheBaseColor();
        }

        private void CacheBaseColor()
        {
            if (_renderer == null || _renderer.material == null)
                return;

            Material material = _renderer.material;
            if (material.HasProperty("_BaseColor"))
            {
                _baseColor = material.GetColor("_BaseColor");
                _hasBaseColor = true;
            }
            else if (material.HasProperty("_Color"))
            {
                _baseColor = material.GetColor("_Color");
                _hasBaseColor = true;
            }
        }

        private void Resolve()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
            if (_input == null)
                _input = Object.FindFirstObjectByType<DuelPhysicalInputController>();
        }

        private void OnMouseDown()
        {
            Resolve();
            if (_duel == null || _input == null || !_duel.IsActive)
                return;

            if (_playerSide)
            {
                if (_index < _duel.PlayerMonsters.Count)
                    _input.ClickMonster(true, _duel.PlayerMonsters[_index]);
            }
            else
            {
                if (_index < _duel.CpuMonsters.Count)
                    _input.ClickMonster(false, _duel.CpuMonsters[_index]);
            }
        }

        private void OnMouseEnter()
        {
            Resolve();
            if (_renderer == null || _renderer.material == null || _duel == null || !_duel.IsActive)
                return;

            Color hover = _playerSide
                ? new Color(0.10f, 0.90f, 1.00f, 1f)
                : new Color(1.00f, 0.20f, 0.38f, 1f);
            SetColor(hover);
        }

        private void OnMouseExit()
        {
            if (_hasBaseColor)
                SetColor(_baseColor);
        }

        private void SetColor(Color color)
        {
            if (_renderer == null || _renderer.material == null)
                return;

            Material material = _renderer.material;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * 1.5f);
        }
    }
}
