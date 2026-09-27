using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Builds the actual physical Duel: Genesis table around the duel board.
    /// The table exists in the overworld before a duel begins, while the live
    /// card zones/holograms activate on the inset play surface during a duel.
    /// </summary>
    public sealed class DuelTabletopPolish : MonoBehaviour
    {
        private const string SolidTableRootName = "DG Solid Duel Table Model";

        private static readonly Color BodyColor = new Color(0.055f, 0.060f, 0.075f, 1f);
        private static readonly Color EdgeColor = new Color(0.105f, 0.115f, 0.145f, 1f);
        private static readonly Color InsetColor = new Color(0.018f, 0.028f, 0.050f, 1f);
        private static readonly Color PlayerAccent = new Color(0.06f, 0.68f, 0.88f, 1f);
        private static readonly Color CpuAccent = new Color(0.70f, 0.14f, 0.62f, 1f);
        private static readonly Color MetalColor = new Color(0.16f, 0.17f, 0.21f, 1f);

        private Transform _table;
        private Transform _tabletop;
        private Transform _solidRoot;
        private bool _modelBuilt;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelTabletopPolish>() != null)
                return;

            GameObject host = new GameObject("Duel Tabletop Polish");
            host.AddComponent<DuelTabletopPolish>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolve)
                return;

            _nextResolve = Time.unscaledTime + 0.15f;
            Resolve();
            if (_table == null)
                return;

            EnsureSolidTableModel();

            if (_tabletop != null)
                HideZoneWords();

            PositionHands();
        }

        private void Resolve()
        {
            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_table != null && _tabletop == null)
                _tabletop = _table.Find("DG Physical Tabletop");
        }

        private void HideZoneWords()
        {
            TextMesh[] textMeshes = _tabletop.GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh text in textMeshes)
            {
                if (text != null && text.gameObject.name == "Zone Label")
                    text.gameObject.SetActive(false);
            }
        }

        private void EnsureSolidTableModel()
        {
            if (_modelBuilt && _solidRoot != null)
                return;

            Transform existing = _table.Find(SolidTableRootName);
            if (existing != null)
                Object.Destroy(existing.gameObject);

            // Clean up the old thin prototype frame if it was created earlier in this session.
            if (_tabletop != null)
            {
                DestroyNamedChild(_tabletop, "DG Table Frame");
                DestroyNamedChild(_tabletop, "Near Rail");
                DestroyNamedChild(_tabletop, "Far Rail");
                DestroyNamedChild(_tabletop, "Left Rail");
                DestroyNamedChild(_tabletop, "Right Rail");
            }

            GameObject root = new GameObject(SolidTableRootName);
            root.transform.SetParent(_table, false);
            _solidRoot = root.transform;

            BuildTableBody();
            BuildInsetPlayBed();
            BuildRaisedRim();
            BuildLegsAndSupports();
            BuildAccentLighting();
            BuildDeckPlatforms();

            _modelBuilt = true;
        }

        private void BuildTableBody()
        {
            float boardWidth = DuelTabletopLayout.BoardScale.x;
            float boardDepth = DuelTabletopLayout.BoardScale.z;
            float tableWidth = boardWidth + 1.05f;
            float tableDepth = boardDepth + 1.05f;

            // Thick tabletop slab. Its top sits just below the recessed duel surface.
            CreatePart(
                "Main Table Slab",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.23f, 0f),
                new Vector3(tableWidth, 0.42f, tableDepth),
                BodyColor,
                true,
                0.46f,
                0.08f);

            // Lower bevel-like layer gives the tabletop a visible silhouette from the duel camera.
            CreatePart(
                "Lower Table Lip",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.45f, 0f),
                new Vector3(tableWidth - 0.34f, 0.12f, tableDepth - 0.34f),
                EdgeColor,
                true,
                0.55f,
                0.14f);
        }

        private void BuildInsetPlayBed()
        {
            float width = DuelTabletopLayout.BoardScale.x + 0.10f;
            float depth = DuelTabletopLayout.BoardScale.z + 0.10f;

            // This dark surface remains visible in the overworld. During a duel the actual
            // zone mat sits a few centimeters above it, making the board feel physically inset.
            CreatePart(
                "Recessed Duel Bed",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.035f, 0f),
                new Vector3(width, 0.055f, depth),
                InsetColor,
                false,
                0.32f,
                0.02f);

            // Inner border around the recessed bed.
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            float borderY = DuelTabletopLayout.BoardSurfaceY + 0.005f;

            CreatePart("Inner Border Near", new Vector3(0f, borderY, -halfD - 0.08f), new Vector3(width + 0.18f, 0.07f, 0.16f), EdgeColor, false);
            CreatePart("Inner Border Far", new Vector3(0f, borderY, halfD + 0.08f), new Vector3(width + 0.18f, 0.07f, 0.16f), EdgeColor, false);
            CreatePart("Inner Border Left", new Vector3(-halfW - 0.08f, borderY, 0f), new Vector3(0.16f, 0.07f, depth), EdgeColor, false);
            CreatePart("Inner Border Right", new Vector3(halfW + 0.08f, borderY, 0f), new Vector3(0.16f, 0.07f, depth), EdgeColor, false);
        }

        private void BuildRaisedRim()
        {
            float outerWidth = DuelTabletopLayout.BoardScale.x + 0.92f;
            float outerDepth = DuelTabletopLayout.BoardScale.z + 0.92f;
            float halfW = outerWidth * 0.5f;
            float halfD = outerDepth * 0.5f;
            float y = DuelTabletopLayout.BoardSurfaceY + 0.10f;

            CreatePart("Raised Near Rail", new Vector3(0f, y, -halfD), new Vector3(outerWidth, 0.24f, 0.28f), EdgeColor, true, 0.58f, 0.18f);
            CreatePart("Raised Far Rail", new Vector3(0f, y, halfD), new Vector3(outerWidth, 0.24f, 0.28f), EdgeColor, true, 0.58f, 0.18f);
            CreatePart("Raised Left Rail", new Vector3(-halfW, y, 0f), new Vector3(0.28f, 0.24f, outerDepth - 0.28f), EdgeColor, true, 0.58f, 0.18f);
            CreatePart("Raised Right Rail", new Vector3(halfW, y, 0f), new Vector3(0.28f, 0.24f, outerDepth - 0.28f), EdgeColor, true, 0.58f, 0.18f);

            // Chunky corners make the silhouette read as furniture rather than four thin lines.
            Vector3 cornerScale = new Vector3(0.48f, 0.31f, 0.48f);
            CreatePart("Near Left Corner", new Vector3(-halfW, y + 0.02f, -halfD), cornerScale, MetalColor, true, 0.72f, 0.35f);
            CreatePart("Near Right Corner", new Vector3(halfW, y + 0.02f, -halfD), cornerScale, MetalColor, true, 0.72f, 0.35f);
            CreatePart("Far Left Corner", new Vector3(-halfW, y + 0.02f, halfD), cornerScale, MetalColor, true, 0.72f, 0.35f);
            CreatePart("Far Right Corner", new Vector3(halfW, y + 0.02f, halfD), cornerScale, MetalColor, true, 0.72f, 0.35f);
        }

        private void BuildLegsAndSupports()
        {
            float tableWidth = DuelTabletopLayout.BoardScale.x + 1.05f;
            float tableDepth = DuelTabletopLayout.BoardScale.z + 1.05f;
            float legX = tableWidth * 0.5f - 0.58f;
            float legZ = tableDepth * 0.5f - 0.58f;

            const float legHeight = 1.18f;
            const float legY = 0.59f;
            Vector3 legScale = new Vector3(0.50f, legHeight, 0.50f);

            CreatePart("Leg Near Left", new Vector3(-legX, legY, -legZ), legScale, MetalColor, true, 0.60f, 0.28f);
            CreatePart("Leg Near Right", new Vector3(legX, legY, -legZ), legScale, MetalColor, true, 0.60f, 0.28f);
            CreatePart("Leg Far Left", new Vector3(-legX, legY, legZ), legScale, MetalColor, true, 0.60f, 0.28f);
            CreatePart("Leg Far Right", new Vector3(legX, legY, legZ), legScale, MetalColor, true, 0.60f, 0.28f);

            // Apron beams under the slab make the body read as a supported real table.
            float apronY = DuelTabletopLayout.BoardSurfaceY - 0.53f;
            CreatePart("Apron Near", new Vector3(0f, apronY, -legZ), new Vector3(tableWidth - 1.0f, 0.28f, 0.22f), BodyColor, true);
            CreatePart("Apron Far", new Vector3(0f, apronY, legZ), new Vector3(tableWidth - 1.0f, 0.28f, 0.22f), BodyColor, true);
            CreatePart("Apron Left", new Vector3(-legX, apronY, 0f), new Vector3(0.22f, 0.28f, tableDepth - 1.0f), BodyColor, true);
            CreatePart("Apron Right", new Vector3(legX, apronY, 0f), new Vector3(0.22f, 0.28f, tableDepth - 1.0f), BodyColor, true);

            // Low stretchers are visible from the angled camera and reinforce the 3D depth.
            CreatePart("Lower Width Stretcher", new Vector3(0f, 0.34f, 0f), new Vector3(tableWidth - 1.55f, 0.16f, 0.18f), EdgeColor, true);
            CreatePart("Lower Depth Stretcher", new Vector3(0f, 0.34f, 0f), new Vector3(0.18f, 0.16f, tableDepth - 1.55f), EdgeColor, true);

            // Small feet visually anchor the legs to the floor.
            Vector3 footScale = new Vector3(0.68f, 0.10f, 0.68f);
            CreatePart("Foot Near Left", new Vector3(-legX, 0.05f, -legZ), footScale, BodyColor, true);
            CreatePart("Foot Near Right", new Vector3(legX, 0.05f, -legZ), footScale, BodyColor, true);
            CreatePart("Foot Far Left", new Vector3(-legX, 0.05f, legZ), footScale, BodyColor, true);
            CreatePart("Foot Far Right", new Vector3(legX, 0.05f, legZ), footScale, BodyColor, true);
        }

        private void BuildAccentLighting()
        {
            float width = DuelTabletopLayout.BoardScale.x + 0.25f;
            float depth = DuelTabletopLayout.BoardScale.z + 0.25f;
            float y = DuelTabletopLayout.BoardSurfaceY + 0.095f;
            float halfDepth = depth * 0.5f;

            // Player and opponent edge strips give the table a Duel: Genesis identity
            // without covering the field in text.
            CreatePart("Player Edge Light", new Vector3(0f, y, -halfDepth), new Vector3(width, 0.045f, 0.055f), PlayerAccent, false, 0.25f, 0.0f, true);
            CreatePart("CPU Edge Light", new Vector3(0f, y, halfDepth), new Vector3(width, 0.045f, 0.055f), CpuAccent, false, 0.25f, 0.0f, true);
        }

        private void BuildDeckPlatforms()
        {
            Vector3 playerDeck = DuelTabletopLayout.DeckPosition(true);
            Vector3 cpuDeck = DuelTabletopLayout.DeckPosition(false);
            Vector3 playerGrave = DuelTabletopLayout.GraveyardPosition(true);
            Vector3 cpuGrave = DuelTabletopLayout.GraveyardPosition(false);

            float y = DuelTabletopLayout.BoardSurfaceY + 0.005f;
            Vector3 trayScale = new Vector3(0.95f, 0.075f, 1.22f);

            playerDeck.y = y;
            cpuDeck.y = y;
            playerGrave.y = y;
            cpuGrave.y = y;

            CreatePart("Player Deck Tray", playerDeck, trayScale, new Color(0.045f, 0.12f, 0.15f, 1f), false);
            CreatePart("CPU Deck Tray", cpuDeck, trayScale, new Color(0.15f, 0.045f, 0.12f, 1f), false);
            CreatePart("Player Grave Tray", playerGrave, trayScale, new Color(0.07f, 0.075f, 0.095f, 1f), false);
            CreatePart("CPU Grave Tray", cpuGrave, trayScale, new Color(0.07f, 0.075f, 0.095f, 1f), false);
        }

        private GameObject CreatePart(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            bool keepCollider,
            float smoothness = 0.35f,
            float metallic = 0.08f,
            bool emission = false)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(_solidRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            if (!keepCollider)
            {
                Collider collider = part.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateMaterial(color, smoothness, metallic, emission);

            return part;
        }

        private static Material CreateMaterial(Color color, float smoothness, float metallic, bool emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color * 2.0f);
            }

            return material;
        }

        private void PositionHands()
        {
            if (_table == null)
                return;

            // Player hand is screen-space; keep the old world-space row hidden.
            Transform playerHand = _table.Find("DG Physical Player Hand");
            if (playerHand != null)
                playerHand.gameObject.SetActive(false);

            Transform cpuHand = _table.Find("DG Physical CPU Hand");
            if (cpuHand != null)
            {
                cpuHand.localPosition = new Vector3(0f, 0.18f, 1.00f);
                cpuHand.localScale = Vector3.one * 1.08f;
            }
        }

        private static void DestroyNamedChild(Transform parent, string childName)
        {
            if (parent == null)
                return;

            Transform child = parent.Find(childName);
            if (child != null)
                Object.Destroy(child.gameObject);
        }
    }
}
