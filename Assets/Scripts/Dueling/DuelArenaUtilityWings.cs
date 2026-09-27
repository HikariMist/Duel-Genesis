using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Builds the physical utility wings around the compact arena. This class owns only
    /// the decorative hardware; DuelTabletopPresentation owns the actual clickable zones.
    /// </summary>
    [DefaultExecutionOrder(5200)]
    public sealed class DuelArenaUtilityWings : MonoBehaviour
    {
        private const string HardwareRootName = "DG Arena Utility Wings";

        private static readonly Color ShellWhite = new Color(0.80f, 0.82f, 0.84f, 1f);
        private static readonly Color ShellMid = new Color(0.30f, 0.34f, 0.40f, 1f);
        private static readonly Color DarkFrame = new Color(0.022f, 0.030f, 0.045f, 1f);
        private static readonly Color SlotDark = new Color(0.028f, 0.052f, 0.072f, 1f);
        private static readonly Color PlayerRed = new Color(0.88f, 0.055f, 0.075f, 1f);
        private static readonly Color CpuBlue = new Color(0.035f, 0.34f, 0.92f, 1f);
        private static readonly Color FieldGreen = new Color(0.10f, 0.76f, 0.42f, 1f);
        private static readonly Color GraveGlow = new Color(0.30f, 0.64f, 0.82f, 1f);
        private static readonly Color BanishGlow = new Color(0.72f, 0.30f, 0.95f, 1f);
        private static readonly Color ExtraGlow = new Color(0.84f, 0.72f, 0.22f, 1f);

        private Transform _table;
        private Transform _hardwareRoot;
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
        }

        private void Resolve()
        {
            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_table != null && _hardwareRoot == null)
                _hardwareRoot = _table.Find(HardwareRootName);
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

            // Player's left: Field Zone and Extra Deck.
            BuildTray("Player Field Spell Bay", DuelTabletopLayout.FieldZonePosition(true), FieldGreen, true);
            BuildTray("Player Extra Deck Bay", DuelTabletopLayout.ExtraDeckPosition(true), ExtraGlow, true);

            // Player's right: Graveyard, Main Deck and the smaller Banished pocket.
            BuildTray("Player Graveyard Bay", DuelTabletopLayout.GraveyardPosition(true), GraveGlow, true);
            BuildTray("Player Main Deck Bay", DuelTabletopLayout.DeckPosition(true), PlayerRed, true);
            BuildBanishPocket("Player Banished Bay", DuelTabletopLayout.BanishedPosition(true), BanishGlow);

            // CPU is mirrored from its own viewpoint.
            BuildTray("CPU Field Spell Bay", DuelTabletopLayout.FieldZonePosition(false), FieldGreen, true);
            BuildTray("CPU Extra Deck Bay", DuelTabletopLayout.ExtraDeckPosition(false), ExtraGlow, true);
            BuildTray("CPU Graveyard Bay", DuelTabletopLayout.GraveyardPosition(false), GraveGlow, true);
            BuildTray("CPU Main Deck Bay", DuelTabletopLayout.DeckPosition(false), CpuBlue, true);
            BuildBanishPocket("CPU Banished Bay", DuelTabletopLayout.BanishedPosition(false), BanishGlow);

            BuildExtraMonsterRecess(0);
            BuildExtraMonsterRecess(1);
        }

        private void BuildSideWing(string name, float side)
        {
            float x = side * 5.62f;
            float y = DuelTabletopLayout.BoardSurfaceY - 0.13f;

            // Structural bridge from the main arena into the side pod.
            CreateBlock(name + " Connector", new Vector3(side * 4.96f, y - 0.02f, 0f), new Vector3(0.82f, 0.24f, 5.20f), DarkFrame, true, false);
            CreateBlock(name + " Dark Base", new Vector3(x, y - 0.08f, 0f), new Vector3(1.94f, 0.26f, 6.05f), DarkFrame, true, false);
            CreateBlock(name + " White Shell", new Vector3(x, y + 0.06f, 0f), new Vector3(1.72f, 0.19f, 5.84f), ShellWhite, true, false);
            CreateBlock(name + " Inner Metal", new Vector3(x, y + 0.115f, 0f), new Vector3(1.49f, 0.075f, 5.56f), ShellMid, false, false);
            CreateBlock(name + " Recess", new Vector3(x, DuelTabletopLayout.BoardSurfaceY + 0.008f, 0f), new Vector3(1.38f, 0.050f, 5.34f), SlotDark, false, false);

            // Duelist ownership strips at the near/far ends of each wing.
            CreateBlock(name + " Player Accent", new Vector3(x, DuelTabletopLayout.BoardSurfaceY + 0.060f, -2.74f), new Vector3(1.43f, 0.038f, 0.10f), PlayerRed, false, true);
            CreateBlock(name + " CPU Accent", new Vector3(x, DuelTabletopLayout.BoardSurfaceY + 0.060f, 2.74f), new Vector3(1.43f, 0.038f, 0.10f), CpuBlue, false, true);
        }

        private void BuildTray(string name, Vector3 position, Color accent, bool sideRails)
        {
            position.y = DuelTabletopLayout.BoardSurfaceY + 0.020f;

            CreateBlock(name + " Outer Rim", position, new Vector3(1.34f, 0.105f, 1.56f), DarkFrame, false, false);
            CreateBlock(name + " Metal Rim", position + Vector3.up * 0.047f, new Vector3(1.20f, 0.055f, 1.42f), ShellMid, false, false);
            CreateBlock(name + " Inset", position + Vector3.up * 0.079f, new Vector3(1.04f, 0.030f, 1.24f), SlotDark, false, false);

            CreateBlock(name + " Near Light", position + new Vector3(0f, 0.103f, -0.61f), new Vector3(0.88f, 0.018f, 0.042f), accent, false, true);
            CreateBlock(name + " Far Light", position + new Vector3(0f, 0.103f, 0.61f), new Vector3(0.88f, 0.018f, 0.042f), accent, false, true);

            if (sideRails)
            {
                CreateBlock(name + " Left Rail", position + new Vector3(-0.54f, 0.090f, 0f), new Vector3(0.045f, 0.070f, 1.10f), ShellWhite, false, false);
                CreateBlock(name + " Right Rail", position + new Vector3(0.54f, 0.090f, 0f), new Vector3(0.045f, 0.070f, 1.10f), ShellWhite, false, false);
            }
        }

        private void BuildBanishPocket(string name, Vector3 position, Color accent)
        {
            position.y = DuelTabletopLayout.BoardSurfaceY + 0.018f;

            // Small physical bump-out attached to the Graveyard side.
            float connectorX = position.x > 0f ? position.x - 0.58f : position.x + 0.58f;
            CreateBlock(name + " Connector", new Vector3(connectorX, position.y - 0.030f, position.z), new Vector3(0.74f, 0.11f, 1.12f), DarkFrame, true, false);
            CreateBlock(name + " Base", position, new Vector3(1.06f, 0.105f, 1.34f), DarkFrame, true, false);
            CreateBlock(name + " Rim", position + Vector3.up * 0.046f, new Vector3(0.94f, 0.052f, 1.20f), ShellMid, false, false);
            CreateBlock(name + " Inset", position + Vector3.up * 0.078f, new Vector3(0.80f, 0.028f, 1.04f), SlotDark, false, false);
            CreateBlock(name + " Edge Light", position + new Vector3(0f, 0.098f, -0.50f), new Vector3(0.67f, 0.018f, 0.040f), accent, false, true);
        }

        private void BuildExtraMonsterRecess(int index)
        {
            Vector3 position = DuelTabletopLayout.ExtraMonsterZonePosition(index);
            position.y = DuelTabletopLayout.BoardSurfaceY + 0.005f;
            CreateBlock("Extra Monster Recess " + (index + 1), position, new Vector3(1.28f, 0.050f, 0.92f), DarkFrame, false, false);
            CreateBlock("Extra Monster Inner " + (index + 1), position + Vector3.up * 0.032f, new Vector3(1.12f, 0.024f, 0.78f), SlotDark, false, false);
            CreateBlock("Extra Monster Glow " + (index + 1), position + new Vector3(0f, 0.052f, -0.36f), new Vector3(0.92f, 0.018f, 0.040f), ExtraGlow, false, true);
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
                    material.SetColor("_EmissionColor", color * 1.30f);
            }

            renderer.material = material;
        }
    }
}
