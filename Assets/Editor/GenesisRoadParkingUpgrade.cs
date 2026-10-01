#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Non-destructive street finishing pass for the open Genesis City.
    /// Keeps all existing road geometry, buildings, shops, landmarks and gameplay objects in place,
    /// then layers better markings, crossings, intersection details and parking lots into clear block centres.
    /// </summary>
    public static class GenesisRoadParkingUpgrade
    {
        private const string RootName = "Road & Parking Upgrade";
        private const string MatFolder = "Assets/Art/Generated/OpenWorld";
        private const float Half = 420f;
        private const float Avenue = 16f;
        private const float Boulevard = 20f;
        private const float ParkingWidth = 46f;
        private const float ParkingDepth = 28f;
        private const int MaxParkingLots = 12;

        private static readonly float[] StreetLines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };
        private static readonly float[] BlockCentres = { -300f, -180f, -60f, 60f, 180f, 300f };

        private static Material _asphalt;
        private static Material _white;
        private static Material _yellow;
        private static Material _curb;
        private static Material _disabledBlue;
        private static Material _stopRed;

        [MenuItem("Duel Genesis/World/25. Upgrade Roads & Parking")]
        public static void UpgradeMenu()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null)
            {
                Debug.LogWarning("Duel: Genesis: build/open the Open Genesis City first (World > 9).");
                return;
            }

            Apply(city.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        public static void Apply(Transform city)
        {
            if (city == null || city.Find("Open World Marker") == null) return;
            Materials();

            Transform map = city.Find("Map");
            if (map == null)
            {
                Debug.LogWarning("Duel: Genesis: Map root not found; road/parking upgrade skipped.");
                return;
            }

            Transform old = map.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Transform root = Child(map, RootName);
            Transform markings = Child(root, "Road Markings");
            Transform crossings = Child(root, "Crosswalks & Stop Lines");
            Transform parking = Child(root, "Parking Lots");

            int dashCount = AddBoulevardLaneDashes(markings);
            int crossingCount = AddCrosswalks(crossings);
            int lotCount = AddParkingLots(city, parking);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            Selection.activeGameObject = root.gameObject;
            Debug.Log($"Duel: Genesis road upgrade complete: {dashCount} lane dashes, {crossingCount} upgraded intersections and {lotCount} marked parking lots. Existing roads, buildings, shops and gameplay routes were not moved.");
        }

        private static int AddBoulevardLaneDashes(Transform parent)
        {
            int count = 0;
            const float dashLength = 5f;
            const float gap = 5f;
            const float laneOffset = 5f;

            // The 20 m central boulevards support two lanes in each direction. Keep the existing yellow
            // centre line, but add realistic white broken lane separators without changing the road footprint.
            for (float p = -Half + 5f; p <= Half - 5f; p += dashLength + gap)
            {
                float mid = p + dashLength * 0.5f;
                if (SkipRoadPaint(0f, mid)) continue;
                foreach (float x in new[] { -laneOffset, laneOffset })
                {
                    Box(parent, "Boulevard N/S Lane Dash", new Vector3(x, 0.038f, mid),
                        new Vector3(0.16f, 0.008f, dashLength), _white);
                    count++;
                }
            }

            for (float p = -Half + 5f; p <= Half - 5f; p += dashLength + gap)
            {
                float mid = p + dashLength * 0.5f;
                if (SkipRoadPaint(mid, 0f)) continue;
                foreach (float z in new[] { -laneOffset, laneOffset })
                {
                    Box(parent, "Boulevard E/W Lane Dash", new Vector3(mid, 0.038f, z),
                        new Vector3(dashLength, 0.008f, 0.16f), _white);
                    count++;
                }
            }
            return count;
        }

        private static bool SkipRoadPaint(float x, float z)
        {
            // Keep the central plaza/ring readable and keep dashed paint out of all intersections.
            if (new Vector2(x, z).magnitude < 72f) return true;
            foreach (float line in StreetLines)
            {
                if (Mathf.Abs(x - line) < 13f || Mathf.Abs(z - line) < 13f) return true;
            }
            return false;
        }

        private static int AddCrosswalks(Transform parent)
        {
            int intersections = 0;
            foreach (float x in StreetLines)
            foreach (float z in StreetLines)
            {
                if (new Vector2(x, z).magnitude < 82f) continue;

                // Fully mark the boulevard junctions, plus every other outer grid junction. This gives
                // the city visual rhythm without creating thousands of tiny renderers.
                bool boulevardJunction = Mathf.Abs(x) < 0.01f || Mathf.Abs(z) < 0.01f;
                bool majorOuter = IsMajorGridLine(x) && IsMajorGridLine(z);
                if (!boulevardJunction && !majorOuter) continue;

                float verticalWidth = Mathf.Abs(x) < 0.01f ? Boulevard : Avenue;
                float horizontalWidth = Mathf.Abs(z) < 0.01f ? Boulevard : Avenue;
                float northSouthOffset = horizontalWidth * 0.5f + 2.2f;
                float eastWestOffset = verticalWidth * 0.5f + 2.2f;

                Zebra(parent, new Vector3(x, 0.044f, z - northSouthOffset), verticalWidth - 1.6f, true);
                Zebra(parent, new Vector3(x, 0.044f, z + northSouthOffset), verticalWidth - 1.6f, true);
                Zebra(parent, new Vector3(x - eastWestOffset, 0.044f, z), horizontalWidth - 1.6f, false);
                Zebra(parent, new Vector3(x + eastWestOffset, 0.044f, z), horizontalWidth - 1.6f, false);

                // Stop bars sit just before each crossing.
                Box(parent, "Stop Line", new Vector3(x, 0.045f, z - northSouthOffset - 2.3f),
                    new Vector3(verticalWidth - 1.2f, 0.009f, 0.38f), _white);
                Box(parent, "Stop Line", new Vector3(x, 0.045f, z + northSouthOffset + 2.3f),
                    new Vector3(verticalWidth - 1.2f, 0.009f, 0.38f), _white);
                Box(parent, "Stop Line", new Vector3(x - eastWestOffset - 2.3f, 0.045f, z),
                    new Vector3(0.38f, 0.009f, horizontalWidth - 1.2f), _white);
                Box(parent, "Stop Line", new Vector3(x + eastWestOffset + 2.3f, 0.045f, z),
                    new Vector3(0.38f, 0.009f, horizontalWidth - 1.2f), _white);
                intersections++;
            }
            return intersections;
        }

        private static bool IsMajorGridLine(float v)
        {
            return Mathf.Abs(Mathf.Abs(v) - 240f) < 0.01f || Mathf.Abs(Mathf.Abs(v) - 360f) < 0.01f;
        }

        private static void Zebra(Transform parent, Vector3 centre, float roadWidth, bool crossesVerticalRoad)
        {
            const int stripes = 7;
            const float crossingDepth = 3.4f;
            float stripeWidth = roadWidth / (stripes * 2f - 1f);
            float start = -roadWidth * 0.5f + stripeWidth * 0.5f;
            for (int i = 0; i < stripes; i++)
            {
                float across = start + i * stripeWidth * 2f;
                Vector3 p = centre + (crossesVerticalRoad ? Vector3.right : Vector3.forward) * across;
                Vector3 size = crossesVerticalRoad
                    ? new Vector3(stripeWidth, 0.008f, crossingDepth)
                    : new Vector3(crossingDepth, 0.008f, stripeWidth);
                Box(parent, "Crosswalk Stripe", p, size, _white);
            }
        }

        private static int AddParkingLots(Transform city, Transform parent)
        {
            int built = 0;
            var candidates = new List<Vector2>();

            // City-block centres are intentionally left open by GenesisCityBlocks, making them good places
            // for parking without taking frontage away from the Japanese buildings.
            foreach (float z in BlockCentres)
            foreach (float x in BlockCentres)
            {
                Vector2 p = new Vector2(x, z);
                if (Mathf.Abs(x) < 120f && Mathf.Abs(z) < 120f) continue;
                candidates.Add(p);
            }

            // Symmetric deterministic order: nearer useful districts first, then farther blocks.
            candidates = candidates.OrderBy(p => p.sqrMagnitude).ThenBy(p => p.y).ThenBy(p => p.x).ToList();

            foreach (Vector2 p in candidates)
            {
                if (built >= MaxParkingLots) break;
                float yaw = Mathf.Abs(p.x) > Mathf.Abs(p.y) ? 90f : 0f;
                Vector2 footprint = Mathf.Abs(yaw) < 1f
                    ? new Vector2(ParkingWidth, ParkingDepth)
                    : new Vector2(ParkingDepth, ParkingWidth);

                if (Reserved(p, Mathf.Max(footprint.x, footprint.y) * 0.55f)) continue;
                if (!AreaClear(city, p, footprint)) continue;

                BuildParkingLot(parent, p, yaw, ++built);
            }
            return built;
        }

        private static bool Reserved(Vector2 p, float pad)
        {
            if (p.magnitude < 82f + pad) return true;
            if (GenesisDuelCenter.InColosseum(p, pad + 4f)) return true;
            if (GenesisDuelCenter.InCardShopLot(p)) return true;
            if (GenesisGarden.IsReserved(p)) return true;
            if (Mathf.Abs(p.x) > 360f - pad || Mathf.Abs(p.y) > 360f - pad) return true;
            return false;
        }

        private static bool AreaClear(Transform city, Vector2 p, Vector2 size)
        {
            Bounds target = new Bounds(new Vector3(p.x, 4f, p.y), new Vector3(size.x + 5f, 8f, size.y + 5f));
            foreach (Renderer r in city.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                if (r.transform.IsChildOf(city.Find("Map/" + RootName))) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("ground") || n.Contains("road") || n.Contains("pavement") || n.Contains("curb") ||
                    n.Contains("paving") || n.Contains("plot") || n.Contains("grass")) continue;
                Bounds b = r.bounds;
                if (b.max.y < 0.28f) continue;
                if (b.Intersects(target)) return false;
            }
            return true;
        }

        private static void BuildParkingLot(Transform parent, Vector2 p, float yaw, int index)
        {
            Transform lot = Child(parent, $"Parking Lot {index:00}");
            lot.localPosition = new Vector3(p.x, 0f, p.y);
            lot.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(lot, "Asphalt", new Vector3(0f, 0.019f, 0f), new Vector3(ParkingWidth, 0.038f, ParkingDepth), _asphalt);

            // Perimeter lines make the lot read clearly from street level and from the minimap.
            Box(lot, "Parking Edge", new Vector3(0f, 0.043f, -ParkingDepth * 0.5f + 0.35f), new Vector3(ParkingWidth - 0.7f, 0.008f, 0.16f), _white);
            Box(lot, "Parking Edge", new Vector3(0f, 0.043f, ParkingDepth * 0.5f - 0.35f), new Vector3(ParkingWidth - 0.7f, 0.008f, 0.16f), _white);
            Box(lot, "Parking Edge", new Vector3(-ParkingWidth * 0.5f + 0.35f, 0.043f, 0f), new Vector3(0.16f, 0.008f, ParkingDepth - 0.7f), _white);
            Box(lot, "Parking Edge", new Vector3(ParkingWidth * 0.5f - 0.35f, 0.043f, 0f), new Vector3(0.16f, 0.008f, ParkingDepth - 0.7f), _white);

            const float stallWidth = 4.6f;
            const float stallDepth = 7.3f;
            const int stallsPerRow = 9;
            float rowZ = ParkingDepth * 0.5f - stallDepth * 0.5f - 0.7f;
            float startX = -(stallsPerRow * stallWidth) * 0.5f;

            for (int row = 0; row < 2; row++)
            {
                float z = row == 0 ? -rowZ : rowZ;
                float innerEdge = z + (row == 0 ? stallDepth * 0.5f : -stallDepth * 0.5f);

                for (int i = 0; i <= stallsPerRow; i++)
                {
                    float x = startX + i * stallWidth;
                    Box(lot, "Stall Line", new Vector3(x, 0.046f, z), new Vector3(0.12f, 0.009f, stallDepth), _white);
                }
                Box(lot, "Stall Head Line", new Vector3(0f, 0.046f, innerEdge), new Vector3(stallsPerRow * stallWidth, 0.009f, 0.12f), _white);

                // Concrete wheel stops keep the rows visually grounded without adding colliders.
                for (int i = 0; i < stallsPerRow; i++)
                {
                    float x = startX + (i + 0.5f) * stallWidth;
                    float stopZ = z + (row == 0 ? -stallDepth * 0.5f + 0.75f : stallDepth * 0.5f - 0.75f);
                    Box(lot, "Wheel Stop", new Vector3(x, 0.12f, stopZ), new Vector3(2.2f, 0.18f, 0.28f), _curb);
                }
            }

            // Two accessible spaces nearest the central aisle.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = startX + (stallsPerRow / 2f) * stallWidth;
                float z = side * rowZ;
                Box(lot, "Accessible Space Marker", new Vector3(x, 0.05f, z), new Vector3(2.0f, 0.01f, 2.0f), _disabledBlue);
            }

            // Directional centre aisle and a small red no-parking box at each end improve readability.
            Box(lot, "Aisle Centre Dash", new Vector3(0f, 0.044f, 0f), new Vector3(ParkingWidth - 7f, 0.008f, 0.12f), _yellow);
            Box(lot, "No Parking End", new Vector3(-ParkingWidth * 0.5f + 2f, 0.045f, 0f), new Vector3(2.5f, 0.009f, 3.2f), _stopRed);
            Box(lot, "No Parking End", new Vector3(ParkingWidth * 0.5f - 2f, 0.045f, 0f), new Vector3(2.5f, 0.009f, 3.2f), _stopRed);
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Materials()
        {
            _asphalt = Mat("OW Parking Asphalt", new Color(0.115f, 0.12f, 0.135f), 0.18f);
            _white = Mat("OW Road Paint Bright White", new Color(0.97f, 0.97f, 0.94f), 0.24f);
            _yellow = Mat("OW Road Paint Safety Yellow", new Color(0.98f, 0.76f, 0.12f), 0.22f);
            _curb = Mat("OW Parking Concrete", new Color(0.56f, 0.57f, 0.59f), 0.16f);
            _disabledBlue = Mat("OW Accessible Parking Blue", new Color(0.08f, 0.36f, 0.72f), 0.22f);
            _stopRed = Mat("OW No Parking Red", new Color(0.58f, 0.08f, 0.07f), 0.2f);
        }

        private static Material Mat(string name, Color color, float smooth)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "OpenWorld");

            string path = $"{MatFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smooth);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif
