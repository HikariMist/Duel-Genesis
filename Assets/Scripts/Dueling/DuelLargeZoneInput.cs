using System.Collections.Generic;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Turns the large physical monster-zone boxes into easy click targets.
    /// Occupied-zone clicks resolve through DuelFieldSlotRegistry so a monster stays tied
    /// to the exact slot the player chose instead of its compact-list index.
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
            DuelLargeZoneInput existing = Object.FindFirstObjectByType<DuelLargeZoneInput>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.enabled = true;
                return;
            }

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

            Transform nextTabletop = null;
            GameObject table = GameObject.Find("Duel Table Prototype");
            if (table != null)
                nextTabletop = table.transform.Find("DG Physical Tabletop");

            if (nextTabletop != _tabletop)
            {
                _tabletop = nextTabletop;
                _wired = false;
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

            IReadOnlyList<DuelMonsterState> states = _playerSide ? _duel.PlayerMonsters : _duel.CpuMonsters;
            DuelMonsterState monster = MonsterInSlot(states, _playerSide, _index);
            if (monster != null)
                _input.ClickMonster(_playerSide, monster);
        }

        private static DuelMonsterState MonsterInSlot(IReadOnlyList<DuelMonsterState> states, bool playerSide, int slot)
        {
            for (int i = 0; states != null && i < states.Count; i++)
            {
                DuelMonsterState state = states[i];
                if (state != null && DuelFieldSlotRegistry.GetMonsterSlot(state, playerSide, states) == slot)
                    return state;
            }
            return null;
        }

        private void OnMouseEnter()
        {
            Resolve();
            if (_renderer == null || _renderer.material == null || _duel == null || !_duel.IsActive)
                return;

            Color hover = _playerSide
                ? new Color(0.72f, 0.78f, 0.80f, 1f)
                : new Color(0.72f, 0.78f, 0.80f, 1f);
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
                material.SetColor("_EmissionColor", color * 0.55f);
        }
    }
}
