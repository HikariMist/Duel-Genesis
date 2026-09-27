using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Builds a complete physical duel table around the active duel board.
    /// The model is generated automatically in the overworld and is intentionally
    /// styled after a classic anime duel arena blended with a modern Master-Duel-like table.
    /// </summary>
    public sealed class DuelTabletopPolish : MonoBehaviour
    {
        private const string SolidTableRootName = "DG Solid Duel Table Model";

        private static readonly Color ShellWhite = new Color(0.78f, 0.79f, 0.76f, 1f);
        private static readonly Color ShellShadow = new Color(0.26f, 0.28f, 0.31f, 1f);
        private static readonly Color DarkFrame = new Color(0.055f, 0.060f, 0.075f, 1f);
        private static readonly Color DeepFrame = new Color(0.022f, 0.026f, 0.035f, 1f);
        private static readonly Color PlayfieldColor = new Color(0.055f, 0.105f, 0.125f, 1f);
        private static readonly Color PlayerRed = new Color(0.72f, 0.055f, 0.075f, 1f);
        private static readonly Color PlayerRedGlow = new Color(1.00f, 0.055f, 0.075f, 1f);
        private static readonly Color CpuBlue = new Color(0.035f, 0.28f, 0.78f, 1f);
        private static readonly Color CpuBlueGlow = new Color(0.04f, 0.58f, 1.00f, 1f);
        private static readonly Color WarmLight = new Color(1.00f, 0.68f, 0.22f, 1f);
        private static readonly Color ConsoleGreen = new Color(0.24f, 1.00f, 0.42f, 1f);

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

            // Remove the earlier thin-frame prototype if this play session already made one.
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
            BuildRecessedPlayfield();
            BuildArenaRails();
            BuildCornerHousings();
            BuildPlayerConsole(true);
            BuildPlayerConsole(false);
            BuildDeckAndGravePlatforms();
            BuildLegsAndUnderFrame();
            BuildLightingDetails();

            _modelBuilt = true;
        }

        private void BuildTableBody()
        {
            float boardWidth = DuelTabletopLayout.BoardScale.x;
            float boardDepth = DuelTabletopLayout.BoardScale.z;
            float tableWidth = boardWidth + 1.45f;
            float tableDepth = boardDepth + 1.35f;

            // Dark structural core.
            CreateChamferedBox(
                "Lower Structural Body",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.35f, 0f),
                tableWidth,
                tableDepth,
                0.44f,
                0.48f,
                DarkFrame,
                true,
                0.50f,
                0.32f);

            // Light shell layer produces the chunky white arena silhouette from the reference.
            CreateChamferedBox(
                "Upper White Shell",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.14f, 0f),
                tableWidth - 0.16f,
                tableDepth - 0.16f,
                0.28f,
                0.44f,
                ShellWhite,
                true,
                0.62f,
                0.18f);

            // Black under-lip separates the futuristic shell from the legs.
            CreateChamferedBox(
                "Black Underside Lip",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.53f, 0f),
                tableWidth - 0.50f,
                tableDepth - 0.50f,
                0.15f,
                0.32f,
                DeepFrame,
                true,
                0.30f,
                0.12f);
        }

        private void BuildRecessedPlayfield()
        {
            float width = DuelTabletopLayout.BoardScale.x + 0.10f;
            float depth = DuelTabletopLayout.BoardScale.z + 0.10f;

            CreateChamferedBox(
                "Recessed Duel Surface",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.005f, 0f),
                width,
                depth,
                0.075f,
                0.24f,
                PlayfieldColor,
                false,
                0.30f,
                0.04f);

            // Thin center seam and player/opponent half accents make the field read as one arena.
            CreateBlock(
                "Center Field Divider",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.040f, 0f),
                new Vector3(width - 0.35f, 0.018f, 0.035f),
                ShellShadow,
                false,
                Vector3.zero,
                0.25f,
                0.02f);

            CreateBlock(
                "Player Inner Glow",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.046f, -3.24f),
                new Vector3(width - 0.58f, 0.022f, 0.045f),
                PlayerRedGlow,
                false,
                Vector3.zero,
                0.30f,
                0.0f,
                true);

            CreateBlock(
                "CPU Inner Glow",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.046f, 3.24f),
                new Vector3(width - 0.58f, 0.022f, 0.045f),
                CpuBlueGlow,
                false,
                Vector3.zero,
                0.30f,
                0.0f,
                true);
        }

        private void BuildArenaRails()
        {
            float boardWidth = DuelTabletopLayout.BoardScale.x;
            float boardDepth = DuelTabletopLayout.BoardScale.z;
            float railZ = boardDepth * 0.5f + 0.33f;
            float sideX = boardWidth * 0.5f + 0.34f;
            float railY = DuelTabletopLayout.BoardSurfaceY + 0.15f;

            // Split front/back rails leave room for the raised center consoles.
            CreateChamferedBox("Player Rail Left", new Vector3(-2.48f, railY, -railZ), 4.55f, 0.58f, 0.34f, 0.16f, PlayerRed, true, 0.72f, 0.42f);
            CreateChamferedBox("Player Rail Right", new Vector3(2.48f, railY, -railZ), 4.55f, 0.58f, 0.34f, 0.16f, PlayerRed, true, 0.72f, 0.42f);
            CreateChamferedBox("CPU Rail Left", new Vector3(-2.48f, railY, railZ), 4.55f, 0.58f, 0.34f, 0.16f, CpuBlue, true, 0.72f, 0.42f);
            CreateChamferedBox("CPU Rail Right", new Vector3(2.48f, railY, railZ), 4.55f, 0.58f, 0.34f, 0.16f, CpuBlue, true, 0.72f, 0.42f);

            // White side rails and dark inner strips mimic the industrial arena shell.
            CreateChamferedBox("Left White Rail", new Vector3(-sideX, railY, 0f), 0.64f, boardDepth + 0.32f, 0.34f, 0.15f, ShellWhite, true, 0.62f, 0.18f);
            CreateChamferedBox("Right White Rail", new Vector3(sideX, railY, 0f), 0.64f, boardDepth + 0.32f, 0.34f, 0.15f, ShellWhite, true, 0.62f, 0.18f);

            CreateBlock("Left Inner Rail Light", new Vector3(-sideX + 0.27f, railY + 0.02f, 0f), new Vector3(0.035f, 0.10f, boardDepth - 0.65f), WarmLight, false, Vector3.zero, 0.25f, 0f, true);
            CreateBlock("Right Inner Rail Light", new Vector3(sideX - 0.27f, railY + 0.02f, 0f), new Vector3(0.035f, 0.10f, boardDepth - 0.65f), WarmLight, false, Vector3.zero, 0.25f, 0f, true);
        }

        private void BuildCornerHousings()
        {
            float x = DuelTabletopLayout.BoardScale.x * 0.5f + 0.37f;
            float z = DuelTabletopLayout.BoardScale.z * 0.5f + 0.35f;
            float y = DuelTabletopLayout.BoardSurfaceY + 0.17f;

            BuildCorner("Player Left Corner", new Vector3(-x, y, -z), PlayerRed, -35f);
            BuildCorner("Player Right Corner", new Vector3(x, y, -z), PlayerRed, 35f);
            BuildCorner("CPU Left Corner", new Vector3(-x, y, z), CpuBlue, 35f);
            BuildCorner("CPU Right Corner", new Vector3(x, y, z), CpuBlue, -35f);
        }

        private void BuildCorner(string name, Vector3 position, Color accent, float yaw)
        {
            CreateChamferedBox(name + " Shell", position, 0.92f, 0.92f, 0.46f, 0.20f, ShellWhite, true, 0.70f, 0.25f, yaw);
            CreateBlock(name + " Dark Face", position + new Vector3(0f, 0.01f, 0f), new Vector3(0.55f, 0.49f, 0.55f), DeepFrame, false, new Vector3(0f, yaw, 0f), 0.30f, 0.10f);
            CreateBlock(name + " Accent", position + new Vector3(0f, 0.245f, 0f), new Vector3(0.30f, 0.035f, 0.30f), accent, false, new Vector3(0f, yaw + 45f, 0f), 0.55f, 0.18f, true);
        }

        private void BuildPlayerConsole(bool playerSide)
        {
            float direction = playerSide ? -1f : 1f;
            float z = direction * (DuelTabletopLayout.BoardScale.z * 0.5f + 0.39f);
            float frontZ = direction * (DuelTabletopLayout.BoardScale.z * 0.5f + 0.80f);
            float y = DuelTabletopLayout.BoardSurfaceY + 0.23f;
            Color accent = playerSide ? PlayerRed : CpuBlue;
            Color glow = playerSide ? PlayerRedGlow : CpuBlueGlow;
            string side = playerSide ? "Player" : "CPU";

            CreateChamferedBox(side + " Console Base", new Vector3(0f, y, z), 1.38f, 0.95f, 0.55f, 0.22f, DeepFrame, true, 0.46f, 0.44f);
            CreateChamferedBox(side + " Console Shell", new Vector3(0f, y + 0.09f, z - direction * 0.04f), 1.03f, 0.67f, 0.42f, 0.18f, accent, false, 0.78f, 0.42f);
            CreateChamferedBox(side + " Console Top", new Vector3(0f, y + 0.31f, z - direction * 0.03f), 0.66f, 0.43f, 0.13f, 0.10f, ShellShadow, false, 0.50f, 0.35f);

            CreateBlock(side + " Console Main Light", new Vector3(0f, y + 0.05f, frontZ), new Vector3(0.70f, 0.13f, 0.055f), glow, false, Vector3.zero, 0.32f, 0f, true);
            CreateBlock(side + " Console Status Light", new Vector3(0f, y + 0.32f, z - direction * 0.24f), new Vector3(0.24f, 0.045f, 0.18f), ConsoleGreen, false, Vector3.zero, 0.42f, 0f, true);
        }

        private void BuildDeckAndGravePlatforms()
        {
            BuildCardTray("Player Deck Housing", DuelTabletopLayout.DeckPosition(true), PlayerRed);
            BuildCardTray("CPU Deck Housing", DuelTabletopLayout.DeckPosition(false), CpuBlue);
            BuildCardTray("Player Grave Housing", DuelTabletopLayout.GraveyardPosition(true), ShellShadow);
            BuildCardTray("CPU Grave Housing", DuelTabletopLayout.GraveyardPosition(false), ShellShadow);
        }

        private void BuildCardTray(string name, Vector3 position, Color accent)
        {
            position.y = DuelTabletopLayout.BoardSurfaceY + 0.005f;
            CreateChamferedBox(name + " Base", position, 1.08f, 1.34f, 0.095f, 0.13f, DeepFrame, false, 0.28f, 0.08f);
            CreateChamferedBox(name + " Rim", position + new Vector3(0f, 0.055f, 0f), 0.94f, 1.18f, 0.055f, 0.11f, accent, false, 0.52f, 0.22f);
        }

        private void BuildLegsAndUnderFrame()
        {
            float tableWidth = DuelTabletopLayout.BoardScale.x + 1.45f;
            float tableDepth = DuelTabletopLayout.BoardScale.z + 1.35f;
            float legX = tableWidth * 0.5f - 0.72f;
            float legZ = tableDepth * 0.5f - 0.72f;

            BuildLeg("Near Left", new Vector3(-legX, 0f, -legZ), true);
            BuildLeg("Near Right", new Vector3(legX, 0f, -legZ), true);
            BuildLeg("Far Left", new Vector3(-legX, 0f, legZ), false);
            BuildLeg("Far Right", new Vector3(legX, 0f, legZ), false);

            float apronY = 1.03f;
            CreateBlock("Near Underframe Beam", new Vector3(0f, apronY, -legZ), new Vector3(tableWidth - 1.25f, 0.34f, 0.26f), DarkFrame, true, Vector3.zero, 0.38f, 0.26f);
            CreateBlock("Far Underframe Beam", new Vector3(0f, apronY, legZ), new Vector3(tableWidth - 1.25f, 0.34f, 0.26f), DarkFrame, true, Vector3.zero, 0.38f, 0.26f);
            CreateBlock("Left Underframe Beam", new Vector3(-legX, apronY, 0f), new Vector3(0.26f, 0.34f, tableDepth - 1.25f), DarkFrame, true, Vector3.zero, 0.38f, 0.26f);
            CreateBlock("Right Underframe Beam", new Vector3(legX, apronY, 0f), new Vector3(0.26f, 0.34f, tableDepth - 1.25f), DarkFrame, true, Vector3.zero, 0.38f, 0.26f);

            CreateBlock("Center Width Brace", new Vector3(0f, 0.36f, 0f), new Vector3(tableWidth - 1.80f, 0.18f, 0.22f), DeepFrame, true, Vector3.zero, 0.26f, 0.20f);
            CreateBlock("Center Depth Brace", new Vector3(0f, 0.36f, 0f), new Vector3(0.22f, 0.18f, tableDepth - 1.80f), DeepFrame, true, Vector3.zero, 0.26f, 0.20f);
        }

        private void BuildLeg(string name, Vector3 floorPosition, bool playerSide)
        {
            Color accent = playerSide ? PlayerRedGlow : CpuBlueGlow;
            Vector3 legPosition = floorPosition + new Vector3(0f, 0.59f, 0f);

            CreateChamferedBox(name + " Leg", legPosition, 0.66f, 0.66f, 1.18f, 0.13f, DarkFrame, true, 0.44f, 0.32f);
            CreateChamferedBox(name + " Leg Shoulder", floorPosition + new Vector3(0f, 1.14f, 0f), 0.86f, 0.86f, 0.24f, 0.15f, ShellShadow, true, 0.54f, 0.24f);
            CreateChamferedBox(name + " Foot", floorPosition + new Vector3(0f, 0.07f, 0f), 0.84f, 0.84f, 0.14f, 0.12f, DeepFrame, true, 0.28f, 0.12f);

            float outwardZ = floorPosition.z < 0f ? -0.345f : 0.345f;
            CreateBlock(name + " Vertical Light", floorPosition + new Vector3(0f, 0.62f, outwardZ), new Vector3(0.16f, 0.64f, 0.035f), accent, false, Vector3.zero, 0.24f, 0f, true);
        }

        private void BuildLightingDetails()
        {
            float x = DuelTabletopLayout.BoardScale.x * 0.5f + 0.26f;
            float y = DuelTabletopLayout.BoardSurfaceY + 0.20f;

            CreateBlock("Left Warm Rail Lamp", new Vector3(-x, y, 0f), new Vector3(0.055f, 0.12f, 1.25f), WarmLight, false, Vector3.zero, 0.22f, 0f, true);
            CreateBlock("Right Warm Rail Lamp", new Vector3(x, y, 0f), new Vector3(0.055f, 0.12f, 1.25f), WarmLight, false, Vector3.zero, 0.22f, 0f, true);
        }

        private GameObject CreateBlock(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            bool keepCollider,
            Vector3 localEuler,
            float smoothness = 0.35f,
            float metallic = 0.08f,
            bool emission = false)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(_solidRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
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

        private GameObject CreateChamferedBox(
            string name,
            Vector3 localPosition,
            float width,
            float depth,
            float height,
            float cornerCut,
            Color color,
            bool keepCollider,
            float smoothness = 0.35f,
            float metallic = 0.08f,
            float yaw = 0f)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(_solidRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            MeshFilter filter = part.AddComponent<MeshFilter>();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();

            Mesh mesh = BuildChamferedBoxMesh(width, depth, height, cornerCut);
            filter.sharedMesh = mesh;
            renderer.material = CreateMaterial(color, smoothness, metallic, false);

            if (keepCollider)
            {
                MeshCollider collider = part.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            return part;
        }

        private static Mesh BuildChamferedBoxMesh(float width, float depth, float height, float cornerCut)
        {
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            float halfH = height * 0.5f;
            float cut = Mathf.Clamp(cornerCut, 0.01f, Mathf.Min(halfW, halfD) * 0.80f);

            Vector3[] ring =
            {
                new Vector3(-halfW + cut, 0f, -halfD),
                new Vector3( halfW - cut, 0f, -halfD),
                new Vector3( halfW,       0f, -halfD + cut),
                new Vector3( halfW,       0f,  halfD - cut),
                new Vector3( halfW - cut, 0f,  halfD),
                new Vector3(-halfW + cut, 0f,  halfD),
                new Vector3(-halfW,       0f,  halfD - cut),
                new Vector3(-halfW,       0f, -halfD + cut)
            };

            Vector3[] vertices = new Vector3[18];
            for (int i = 0; i < 8; i++)
            {
                vertices[i] = ring[i] + Vector3.up * halfH;
                vertices[i + 8] = ring[i] - Vector3.up * halfH;
            }
            vertices[16] = Vector3.up * halfH;
            vertices[17] = Vector3.down * halfH;

            int[] triangles = new int[96];
            int t = 0;
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;

                triangles[t++] = 16;
                triangles[t++] = next;
                triangles[t++] = i;

                triangles[t++] = 17;
                triangles[t++] = i + 8;
                triangles[t++] = next + 8;

                triangles[t++] = i;
                triangles[t++] = next;
                triangles[t++] = next + 8;

                triangles[t++] = i;
                triangles[t++] = next + 8;
                triangles[t++] = i + 8;
            }

            Mesh mesh = new Mesh();
            mesh.name = "DG Chamfered Box Mesh";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
                    material.SetColor("_EmissionColor", color * 2.5f);
            }

            return material;
        }

        private void PositionHands()
        {
            if (_table == null)
                return;

            Transform playerHand = _table.Find("DG Physical Player Hand");
            if (playerHand != null)
                playerHand.gameObject.SetActive(false);

            Transform cpuHand = _table.Find("DG Physical CPU Hand");
            if (cpuHand != null)
            {
                cpuHand.localPosition = new Vector3(0f, 0.20f, 1.05f);
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
