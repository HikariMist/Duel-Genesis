using System;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Styles the runtime duel surface after a classic physical Yu-Gi-Oh game mat.
    /// No printed labels: the location and color treatment of the seven columns identifies
    /// Field / Monster / GY and Extra Deck / S-T / Deck just like the supplied reference.
    /// </summary>
    [DefaultExecutionOrder(6400)]
    public sealed class DuelClassicFieldStyler : MonoBehaviour
    {
        private const string GraphicsRootName = "DG Classic Mat Graphics";

        private static readonly Color MatColor = new Color(0.060f, 0.115f, 0.165f, 1f);
        private static readonly Color MatEdge = new Color(0.26f, 0.36f, 0.43f, 1f);
        private static readonly Color MonsterBase = new Color(0.105f, 0.145f, 0.175f, 1f);
        private static readonly Color MonsterEdge = new Color(0.48f, 0.50f, 0.48f, 1f);
        private static readonly Color SpellBase = new Color(0.055f, 0.175f, 0.225f, 1f);
        private static readonly Color SpellEdge = new Color(0.12f, 0.62f, 0.77f, 1f);
        private static readonly Color FieldBase = new Color(0.055f, 0.215f, 0.125f, 1f);
        private static readonly Color FieldEdge = new Color(0.20f, 0.72f, 0.43f, 1f);
        private static readonly Color ExtraBase = new Color(0.14f, 0.16f, 0.205f, 1f);
        private static readonly Color ExtraEdge = new Color(0.65f, 0.72f, 0.80f, 1f);
        private static readonly Color GraveBase = new Color(0.105f, 0.135f, 0.155f, 1f);
        private static readonly Color GraveEdge = new Color(0.42f, 0.49f, 0.54f, 1f);
        private static readonly Color DeckBase = new Color(0.18f, 0.125f, 0.085f, 1f);
        private static readonly Color DeckEdge = new Color(0.68f, 0.48f, 0.28f, 1f);
        private static readonly Color BanishBase = new Color(0.14f, 0.095f, 0.17f, 1f);
        private static readonly Color BanishEdge = new Color(0.48f, 0.34f, 0.58f, 1f);

        private Transform _table;
        private Transform _tabletop;
        private Transform _graphicsRoot;
        private int _lastZoneCount = -1;
        private float _nextSync;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            DuelClassicFieldStyler existing = Object.FindFirstObjectByType<DuelClassicFieldStyler>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.enabled = true;
                return;
            }

            GameObject host = new GameObject("Duel Classic Field Styler");
            host.AddComponent<DuelClassicFieldStyler>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextSync)
                return;

            _nextSync = Time.unscaledTime + 0.20f;
            Resolve();
            if (_tabletop == null)
                return;

            DuelTabletopZone[] zones = _tabletop.GetComponentsInChildren<DuelTabletopZone>(true);
            if (_graphicsRoot == null || zones.Length != _lastZoneCount)
                RebuildGraphics(zones);

            StyleZones(zones);
        }

        private void Resolve()
        {
            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            Transform nextTabletop = _table != null ? _table.Find("DG Physical Tabletop") : null;
            if (nextTabletop != _tabletop)
            {
                _tabletop = nextTabletop;
                _graphicsRoot = null;
                _lastZoneCount = -1;
            }

            if (_tabletop != null && _graphicsRoot == null)
                _graphicsRoot = _tabletop.Find(GraphicsRootName);
        }

        private void RebuildGraphics(DuelTabletopZone[] zones)
        {
            if (_tabletop == null)
                return;

            Transform old = _tabletop.Find(GraphicsRootName);
            if (old != null)
                Object.Destroy(old.gameObject);

            HideOldOutlines();

            GameObject root = new GameObject(GraphicsRootName);
            root.transform.SetParent(_tabletop, false);
            _graphicsRoot = root.transform;

            CreateBlock(
                "Classic Mat Surface",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.014f, 0f),
                new Vector3(DuelTabletopLayout.BoardScale.x - 0.10f, 0.018f, DuelTabletopLayout.BoardScale.z - 0.10f),
                MatColor,
                false);

            CreateBoardBorder();
            CreateSeam("Center Seam", 0f);
            CreateSeam("Player Row Seam", (DuelTabletopLayout.PlayerMonsterZ + DuelTabletopLayout.PlayerBackrowZ) * 0.5f);
            CreateSeam("CPU Row Seam", (DuelTabletopLayout.CpuMonsterZ + DuelTabletopLayout.CpuBackrowZ) * 0.5f);

            for (int i = 0; i < zones.Length; i++)
            {
                DuelTabletopZone zone = zones[i];
                if (zone == null || zone.Kind == DuelTabletopZoneKind.ExtraMonster)
                    continue;

                CreateZoneBorder(zone, ZoneEdgeColor(zone.Kind));
            }

            _lastZoneCount = zones.Length;
        }

        private void HideOldOutlines()
        {
            Transform[] children = _tabletop.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == _tabletop)
                    continue;

                if (child.name.IndexOf("Outline", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    child.name.IndexOf(GraphicsRootName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void StyleZones(DuelTabletopZone[] zones)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                DuelTabletopZone zone = zones[i];
                if (zone == null)
                    continue;

                if (zone.Kind == DuelTabletopZoneKind.ExtraMonster)
                {
                    zone.gameObject.SetActive(false);
                    continue;
                }

                zone.gameObject.SetActive(true);
                zone.SetBaseColor(ZoneBaseColor(zone.Kind));
            }
        }

        private void CreateBoardBorder()
        {
            float halfX = DuelTabletopLayout.BoardScale.x * 0.5f;
            float halfZ = DuelTabletopLayout.BoardScale.z * 0.5f;
            float y = DuelTabletopLayout.BoardSurfaceY + 0.035f;
            const float edge = 0.045f;

            CreateBlock("Mat Border Near", new Vector3(0f, y, -halfZ), new Vector3(DuelTabletopLayout.BoardScale.x, 0.016f, edge), MatEdge, false);
            CreateBlock("Mat Border Far", new Vector3(0f, y, halfZ), new Vector3(DuelTabletopLayout.BoardScale.x, 0.016f, edge), MatEdge, false);
            CreateBlock("Mat Border Left", new Vector3(-halfX, y, 0f), new Vector3(edge, 0.016f, DuelTabletopLayout.BoardScale.z), MatEdge, false);
            CreateBlock("Mat Border Right", new Vector3(halfX, y, 0f), new Vector3(edge, 0.016f, DuelTabletopLayout.BoardScale.z), MatEdge, false);
        }

        private void CreateSeam(string name, float z)
        {
            CreateBlock(
                name,
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.032f, z),
                new Vector3(DuelTabletopLayout.BoardScale.x - 0.20f, 0.012f, 0.026f),
                new Color(MatEdge.r, MatEdge.g, MatEdge.b, 0.65f),
                false);
        }

        private void CreateZoneBorder(DuelTabletopZone zone, Color color)
        {
            Vector3 position = zone.transform.localPosition;
            Vector3 scale = zone.transform.localScale;
            float y = position.y + 0.025f;
            float halfX = scale.x * 0.5f;
            float halfZ = scale.z * 0.5f;
            const float edge = 0.032f;
            string prefix = (zone.PlayerSide ? "P " : "C ") + zone.Kind + " " + zone.Index;

            CreateBlock(prefix + " Border Near", new Vector3(position.x, y, position.z - halfZ), new Vector3(scale.x, 0.014f, edge), color, true);
            CreateBlock(prefix + " Border Far", new Vector3(position.x, y, position.z + halfZ), new Vector3(scale.x, 0.014f, edge), color, true);
            CreateBlock(prefix + " Border Left", new Vector3(position.x - halfX, y, position.z), new Vector3(edge, 0.014f, scale.z), color, true);
            CreateBlock(prefix + " Border Right", new Vector3(position.x + halfX, y, position.z), new Vector3(edge, 0.014f, scale.z), color, true);
        }

        private void CreateBlock(string name, Vector3 position, Vector3 scale, Color color, bool emission)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(_graphicsRoot, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;

            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                return;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color * 0.45f);
            }
            renderer.material = material;
        }

        private static Color ZoneBaseColor(DuelTabletopZoneKind kind)
        {
            return kind switch
            {
                DuelTabletopZoneKind.Monster => MonsterBase,
                DuelTabletopZoneKind.SpellTrap => SpellBase,
                DuelTabletopZoneKind.FieldSpell => FieldBase,
                DuelTabletopZoneKind.ExtraDeck => ExtraBase,
                DuelTabletopZoneKind.Graveyard => GraveBase,
                DuelTabletopZoneKind.Deck => DeckBase,
                DuelTabletopZoneKind.Banished => BanishBase,
                _ => MonsterBase
            };
        }

        private static Color ZoneEdgeColor(DuelTabletopZoneKind kind)
        {
            return kind switch
            {
                DuelTabletopZoneKind.Monster => MonsterEdge,
                DuelTabletopZoneKind.SpellTrap => SpellEdge,
                DuelTabletopZoneKind.FieldSpell => FieldEdge,
                DuelTabletopZoneKind.ExtraDeck => ExtraEdge,
                DuelTabletopZoneKind.Graveyard => GraveEdge,
                DuelTabletopZoneKind.Deck => DeckEdge,
                DuelTabletopZoneKind.Banished => BanishEdge,
                _ => MatEdge
            };
        }
    }
}
