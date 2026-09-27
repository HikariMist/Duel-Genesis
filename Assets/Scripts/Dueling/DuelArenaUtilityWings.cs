using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Adds physical side pods to the approved compact duel arena without enlarging
    /// the central five-column field. Player layout follows the standard game mat:
    /// Field + Extra Deck on the player's left, GY + Main Deck on the player's right,
    /// with a small banished tray beside the GY. CPU hardware is mirrored.
    /// </summary>
    [DefaultExecutionOrder(5200)]
    public sealed class DuelArenaUtilityWings : MonoBehaviour
    {
        private const string HardwareRootName = "DG Arena Utility Wings";

        private static readonly Color ShellWhite = new Color(0.78f, 0.79f, 0.76f, 1f);
        private static readonly Color DarkFrame = new Color(0.028f, 0.034f, 0.046f, 1f);
        private static readonly Color SlotDark = new Color(0.045f, 0.065f, 0.085f, 1f);
        private static readonly Color PlayerRed = new Color(0.78f, 0.055f, 0.075f, 1f);
        private static readonly Color CpuBlue = new Color(0.035f, 0.32f, 0.86f, 1f);
        private static readonly Color UtilityGlow = new Color(0.22f, 0.70f, 0.92f, 1f);
        private static readonly Color BanishGlow = new Color(0.72f, 0.34f, 0.92f, 1f);

        private Transform _table;
        private Transform _hardwareRoot;
        private Transform _physicalRoot;
        private DuelTabletopPresentation _presentation;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelArenaUtilityWings>() != null)
                return;

            GameObject host = new GameObject("Duel Arena Utility Wings");
            host.AddComponent<DuelArenaUtilityWings>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolve)
                return;

            _nextResolve = Time.unscaledTime + 0.15f;
            Resolve();
            if (_table == null)
                return;

            EnsureHardware();
            EnsureUtilityZones();
        }

        private void Resolve()
        {
            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_table == null)
                return;

            if (_hardwareRoot == null)
                _hardwareRoot = _table.Find(HardwareRootName);

            if (_physicalRoot == null)
                _physicalRoot = _table.Find("DG Physical Tabletop");

            if (_presentation == null)
                _presentation = Object.FindFirstObjectByType<DuelTabletopPresentation>();
        }

        private void EnsureHardware()
        {
            if (_hardwareRoot != null)
                return;

            GameObject root = new GameObject(HardwareRootName);
            root.transform.SetParent(_table, false);
            _hardwareRoot = root.transform;

            BuildSideWing("Left Utility Wing", -1f);
            BuildSideWing("Right Utility Wing", 1f);

            // Player side: left = Field / Extra Deck.
            BuildTray("Player Field Tray", DuelTabletopLayout.FieldZonePosition(true), PlayerRed);
            BuildTray("Player Extra Deck Tray", DuelTabletopLayout.ExtraDeckPosition(true), PlayerRed);

            // Player side: right = GY / Main Deck. The existing table polish creates
            // the Deck and GY card trays, so the wing provides the structural housing.
            BuildBanishPocket("Player Banished Tray", DuelTabletopLayout.BanishedPosition(true), BanishGlow);

            // CPU is a 180-degree mirror from its own point of view.
            BuildTray("CPU Field Tray", DuelTabletopLayout.FieldZonePosition(false), CpuBlue);
            BuildTray("CPU Extra Deck Tray", DuelTabletopLayout.ExtraDeckPosition(false), CpuBlue);
            BuildBanishPocket("CPU Banished Tray", DuelTabletopLayout.BanishedPosition(false), BanishGlow);

            // Shared Extra Monster Zone recesses in the middle of the arena.
            BuildPassiveExtraMonsterPad(0);
            BuildPassiveExtraMonsterPad(1);
        }

        private void BuildSideWing(string name, float side)
        {
            float x = side * 5.62f;
            float y = DuelTabletopLayout.BoardSurfaceY - 0.13f;

            CreateBlock(name + " Dark Base", new Vector3(x, y - 0.08f, 0f), new Vector3(1.82f, 0.24f, 5.92f), DarkFrame, true, false);
            CreateBlock(name + " White Shell", new Vector3(x, y + 0.06f, 0f), new Vector3(1.62f, 0.18f, 5.72f), ShellWhite, true, false);
            CreateBlock(name + " Recess", new Vector3(x, DuelTabletopLayout.BoardSurfaceY + 0.010f, 0f), new Vector3(1.36f, 0.055f, 5.35f), SlotDark, false, false);

            // Side-color markers make it obvious which half belongs to which duelist.
            CreateBlock(name + " Player Accent", new Vector3(x, DuelTabletopLayout.BoardSurfaceY + 0.055f, -2.73f), new Vector3(1.40f, 0.045f, 0.10f), PlayerRed, false, true);
            CreateBlock(name + " CPU Accent", new Vector3(x, DuelTabletopLayout.BoardSurfaceY + 0.055f, 2.73f), new Vector3(1.40f, 0.045f, 0.10f), CpuBlue, false, true);
        }

        private void BuildTray(string name, Vector3 position, Color accent)
        {
            position.y = DuelTabletopLayout.BoardSurfaceY + 0.026f;
            CreateBlock(name + " Rim", position, new Vector3(1.28f, 0.075f, 1.49f), DarkFrame, false, false);
            CreateBlock(name + " Inset", position + Vector3.up * 0.045f, new Vector3(1.07f, 0.040f, 1.27f), SlotDark, false, false);
            CreateBlock(name + " Light", position + new Vector3(0f, 0.072f, -0.62f), new Vector3(0.94f, 0.020f, 0.045f), accent, false, true);
        }

        private void BuildBanishPocket(string name, Vector3 position, Color accent)
        {
            position.y = DuelTabletopLayout.BoardSurfaceY + 0.020f;
            CreateBlock(name + " Connector", new Vector3(position.x * 0.955f, position.y - 0.035f, position.z), new Vector3(0.58f, 0.11f, 1.20f), DarkFrame, true, false);
            CreateBlock(name + " Base", position, new Vector3(1.05f, 0.090f, 1.30f), DarkFrame, true, false);
            CreateBlock(name + " Inset", position + Vector3.up * 0.052f, new Vector3(0.84f, 0.035f, 1.08f), SlotDark, false, false);
            CreateBlock(name + " Edge Light", position + new Vector3(0f, 0.075f, -0.52f), new Vector3(0.70f, 0.020f, 0.045f), accent, false, true);
        }

        private void BuildPassiveExtraMonsterPad(int index)
        {
            Vector3 position = DuelTabletopLayout.ExtraMonsterZonePosition(index);
            CreateBlock(
                "Extra Monster Zone Pad " + (index + 1),
                position,
                new Vector3(1.08f, 0.025f, 0.76f),
                UtilityGlow,
                false,
                true);
        }

        private void EnsureUtilityZones()
        {
            if (_physicalRoot == null || _presentation == null)
                return;

            EnsureZone("Player Extra Deck Utility Zone", DuelTabletopLayout.ExtraDeckPosition(true), true, DuelTabletopZoneKind.ExtraDeck, PlayerRed);
            EnsureZone("Player Field Spell Utility Zone", DuelTabletopLayout.FieldZonePosition(true), true, DuelTabletopZoneKind.FieldSpell, PlayerRed);
            EnsureZone("Player Banished Utility Zone", DuelTabletopLayout.BanishedPosition(true), true, DuelTabletopZoneKind.Banished, BanishGlow);

            EnsureZone("CPU Extra Deck Utility Zone", DuelTabletopLayout.ExtraDeckPosition(false), false, DuelTabletopZoneKind.ExtraDeck, CpuBlue);
            EnsureZone("CPU Field Spell Utility Zone", DuelTabletopLayout.FieldZonePosition(false), false, DuelTabletopZoneKind.FieldSpell, CpuBlue);
            EnsureZone("CPU Banished Utility Zone", DuelTabletopLayout.BanishedPosition(false), false, DuelTabletopZoneKind.Banished, BanishGlow);
        }

        private void EnsureZone(string name, Vector3 position, bool playerSide, DuelTabletopZoneKind kind, Color color)
        {
            if (_physicalRoot.Find(name) != null)
                return;

            GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneObject.name = name;
            zoneObject.transform.SetParent(_physicalRoot, false);
            zoneObject.transform.localPosition = position;
            zoneObject.transform.localScale = DuelTabletopLayout.UtilityZoneScale;
            SetMaterial(zoneObject, color, true);

            DuelTabletopZone zone = zoneObject.AddComponent<DuelTabletopZone>();
            zone.Configure(_presentation, playerSide, kind, 0, zoneObject.GetComponent<Renderer>(), color);
        }

        private void CreateBlock(string name, Vector3 position, Vector3 scale, Color color, bool collider, bool emission)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(_hardwareRoot, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;

            if (!collider)
            {
                Collider existing = obj.GetComponent<Collider>();
                if (existing != null)
                    Object.Destroy(existing);
            }

            SetMaterial(obj, color, emission);
        }

        private static void SetMaterial(GameObject obj, Color color, bool emission)
        {
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
                    material.SetColor("_EmissionColor", color * 1.35f);
            }

            renderer.material = material;
        }
    }
}
