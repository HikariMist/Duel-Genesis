#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Additive dressing pass for the open Genesis City. This never replaces Claude's existing
    /// Nature root, map, Duel Center, normal Kame shops, dueling code, or other gameplay systems.
    /// It owns only "City Dressing Expansion" and extra Kame houses whose names begin with
    /// RoadsideShopPrefix, so the pass is safe to rebuild.
    /// </summary>
    public static class GenesisWorldDressingExpansion
    {
        private const string RootName = "City Dressing Expansion";
        private const string RoadsideShopPrefix = "Kame Card House - Roadside";
        private const string Kit = GenesisAssetDownloads.NatureKitFolder + "/";
        private const float Half = 420f;
        private const float Walk = 4f;
        private const int RoadsideShopCount = 5;

        private static readonly float[] RoadLines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };
        private static readonly float[] BlockLines = { -420f, -360f, -240f, -120f, 0f, 120f, 240f, 360f, 420f };

        private static readonly string[] StreetTrees =
        {
            "tree_default", "tree_oak", "tree_detailed", "tree_fat", "tree_tall", "tree_plateau"
        };

        private static readonly string[] Bushes =
        {
            "plant_bush", "plant_bushLarge", "plant_bushDetailed", "plant_bushSmall"
        };

        private static readonly string[] Grasses =
        {
            "grass", "grass_large", "grass_leafs", "grass_leafsLarge"
        };

        private static readonly string[] Flowers =
        {
            "flower_redA", "flower_redB", "flower_redC",
            "flower_yellowA", "flower_yellowB", "flower_yellowC",
            "flower_purpleA", "flower_purpleB", "flower_purpleC"
        };

        private static readonly string[] Rocks =
        {
            "rock_largeA", "rock_largeB", "rock_largeC", "stone_largeA", "stone_largeB"
        };

        private static readonly string[] ForestBits =
        {
            "log", "log_large", "stump_round", "stump_old", "mushroom_redGroup", "mushroom_tanGroup"
        };

        private struct RoadsideSite
        {
            public Vector3 position;
            public Vector3 towardRoad;
        }

        [MenuItem("Duel Genesis/World/11. Expand Nature + Roadside Kame Card Houses")]
        public static void Build()
        {
            GameObject cityObject = GameObject.Find(GenesisWorldBuilder.CityRootName);
            if (cityObject == null || cityObject.transform.Find("Open World Marker") == null)
            {
                EditorUtility.DisplayDialog("Genesis City Dressing", "Build the Open Genesis City first (World > 9).", "OK");
                return;
            }

            Transform city = cityObject.transform;
            Transform map = city.Find("Map");
            if (map == null)
            {
                EditorUtility.DisplayDialog("Genesis City Dressing", "The open city's Map root was not found.", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "tree_default.fbx") == null)
            {
                EditorUtility.DisplayDialog(
                    "Genesis City Dressing",
                    "The Kenney Nature Kit is not imported yet. Run Duel Genesis > Downloads > Kenney Nature Kit first.",
                    "OK");
                return;
            }

            RemoveRoadsideKameGeneration(city);
            Transform old = city.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Transform root = Child(city, RootName);
            Transform accessRoot = Child(root, "Kame Road Access");
            Transform streetRoot = Child(root, "Street Trees + Verges");
            Transform clutterRoot = Child(root, "Plot Edge Clutter");
            Transform forestRoot = Child(root, "Forest Understory");

            Physics.SyncTransforms();
            var log = new StringBuilder();

            int houses = PlaceRoadsideKameHouses(city, map, accessRoot, log);
            Physics.SyncTransforms();

            var rng = new System.Random(20260929);
            var used = new List<Vector2>();
            int street = PlantStreetTrees(streetRoot, map, rng, used);
            int clutter = PlantPlotEdgeClutter(clutterRoot, map, rng, used);
            int forest = PlantForestUnderstory(forestRoot, map, rng, used);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root.gameObject;

            Debug.Log(
                $"Duel: Genesis city dressing complete: {houses} extra roadside Kame Card Houses, " +
                $"{street} street-tree/verge pieces, {clutter} plot-edge clutter pieces and {forest} forest-understory pieces.\n{log}");
        }

        // ================================================================== KAME HOUSES

        private static int PlaceRoadsideKameHouses(Transform city, Transform map, Transform accessRoot, StringBuilder log)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DMOImporter.ShopPrefabPath);
            if (prefab == null)
            {
                log.AppendLine("KAME: imported Kame Game Shop prefab is missing; extra houses skipped.");
                return 0;
            }

            System.Reflection.MethodInfo spawnShop = typeof(DMOImporter).GetMethod(
                "SpawnShop",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (spawnShop == null)
            {
                log.AppendLine("KAME: DMOImporter.SpawnShop was not found; extra houses skipped.");
                return 0;
            }

            Bounds bounds = MeasureShopBounds(city, prefab);
            float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f + 1.5f, 6f, 18f);
            float height = Mathf.Clamp(bounds.size.y, 3f, 20f);
            List<RoadsideSite> candidates = BuildRoadsideCandidates(map, radius, height);

            var anchors = city.Cast<Transform>()
                .Where(t => t.name.StartsWith("Kame Game Shop"))
                .Select(t => t.position)
                .ToList();
            anchors.Add(Vector3.zero);

            Material pavement = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/OpenWorld/OW Pavement.mat");
            int placed = 0;

            while (placed < RoadsideShopCount && candidates.Count > 0)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float spacing = anchors.Min(a => FlatDistance(a, candidates[i].position));
                    float outerBonus = Mathf.Min(40f, new Vector2(candidates[i].position.x, candidates[i].position.z).magnitude * 0.08f);
                    float score = spacing + outerBonus;
                    if (score > bestScore) { bestScore = score; bestIndex = i; }
                }

                if (bestIndex < 0 || bestScore < 58f) break;
                RoadsideSite chosen = candidates[bestIndex];
                candidates.RemoveAt(bestIndex);

                string name = $"{RoadsideShopPrefix} {placed + 1}";
                float yaw = Quaternion.LookRotation(chosen.towardRoad, Vector3.up).eulerAngles.y - 90f;
                GameObject shop = null;
                try
                {
                    shop = spawnShop.Invoke(null, new object[] { prefab, city, name, chosen.position, yaw }) as GameObject;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Duel: Genesis could not build {name}: {ex.GetBaseException().Message}");
                }
                if (shop == null) continue;

                placed++;
                anchors.Add(chosen.position);
                BuildRoadAccess(accessRoot, name, chosen, radius, pavement);
                Physics.SyncTransforms();
                candidates.RemoveAll(c => FlatDistance(c.position, chosen.position) < 72f);
                log.AppendLine($"{name}: {chosen.position}, front to {Compass(chosen.towardRoad)} roadway.");
            }

            if (placed < RoadsideShopCount)
                log.AppendLine($"KAME: placed {placed}/{RoadsideShopCount}; other road lots were blocked or too close to existing shops.");
            return placed;
        }

        private static Bounds MeasureShopBounds(Transform city, GameObject prefab)
        {
            Transform existing = city.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Kame Game Shop"));
            if (existing != null) return GenesisWorldBuilder.RendererBounds(existing.gameObject);

            GameObject temp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (temp == null) return new Bounds(Vector3.zero, new Vector3(14f, 8f, 14f));
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.position = new Vector3(0f, -500f, 0f);
            Physics.SyncTransforms();
            Bounds b = GenesisWorldBuilder.RendererBounds(temp);
            Object.DestroyImmediate(temp);
            return b;
        }

        private static List<RoadsideSite> BuildRoadsideCandidates(Transform map, float radius, float height)
        {
            var sites = new List<RoadsideSite>();
            foreach (float road in RoadLines)
            {
                float roadHalf = Mathf.Abs(road) < 0.01f ? 10f : 8f;
                float offset = roadHalf + Walk + radius + 2.5f;

                for (int s = 0; s < BlockLines.Length - 1; s++)
                {
                    float a = BlockLines[s], b = BlockLines[s + 1];
                    if (b - a < radius * 2f + 18f) continue;
                    foreach (float f in new[] { 0.32f, 0.50f, 0.68f })
                    {
                        float along = Mathf.Lerp(a, b, f);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            TryRoadsideCandidate(map, new Vector3(road + side * offset, 0f, along), side > 0 ? Vector3.left : Vector3.right, radius, height, sites);
                            TryRoadsideCandidate(map, new Vector3(along, 0f, road + side * offset), side > 0 ? Vector3.back : Vector3.forward, radius, height, sites);
                        }
                    }
                }
            }
            return sites;
        }

        private static void TryRoadsideCandidate(Transform map, Vector3 position, Vector3 towardRoad, float radius, float height, List<RoadsideSite> sites)
        {
            float distanceFromHub = new Vector2(position.x, position.z).magnitude;
            if (distanceFromHub < 92f || distanceFromHub > Half - radius - 10f) return;
            if (Mathf.Abs(position.x) < 46f && position.z > 48f && position.z < 148f) return;

            Vector3 alongRoad = Vector3.Cross(Vector3.up, towardRoad).normalized;
            float alongCoordinate = Mathf.Abs(alongRoad.x) > 0.5f ? position.x : position.z;
            if (DistanceToNearestRoadLine(alongCoordinate) < radius + 16f) return;

            if (!ShopSiteIsClear(map, position, radius, height, out float y)) return;
            position.y = y;
            sites.Add(new RoadsideSite { position = position, towardRoad = towardRoad });
        }

        private static bool ShopSiteIsClear(Transform map, Vector3 p, float radius, float height, out float groundY)
        {
            groundY = 0f;
            float? first = null;
            float sample = Mathf.Max(2f, radius * 0.72f);
            foreach (float x in new[] { -sample, 0f, sample })
            foreach (float z in new[] { -sample, 0f, sample })
            {
                if (!MapGroundAt(map, new Vector3(p.x + x, 0f, p.z + z), out float y)) return false;
                if (first == null) first = y;
                else if (Mathf.Abs(y - first.Value) > 0.35f) return false;
            }
            if (first == null) return false;
            groundY = first.Value;

            Collider[] blockers = Physics.OverlapBox(
                new Vector3(p.x, groundY + height * 0.5f, p.z),
                new Vector3(radius, height * 0.5f, radius),
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);
            return blockers.All(c => c == null || c.transform.IsChildOf(map));
        }

        private static void BuildRoadAccess(Transform parent, string name, RoadsideSite site, float radius, Material pavement)
        {
            if (pavement == null) return;
            const float gap = 3.5f;
            Vector3 centre = site.position + site.towardRoad * (radius + gap * 0.5f);
            GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = name + " - Road Access";
            path.transform.SetParent(parent, false);
            path.transform.position = new Vector3(centre.x, site.position.y + 0.035f, centre.z);
            path.transform.rotation = Quaternion.LookRotation(site.towardRoad, Vector3.up);
            path.transform.localScale = new Vector3(4.2f, 0.05f, gap + 1.5f);
            path.GetComponent<Renderer>().sharedMaterial = pavement;
            Collider col = path.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void RemoveRoadsideKameGeneration(Transform city)
        {
            foreach (Transform child in city.Cast<Transform>().Where(t => t.name.StartsWith(RoadsideShopPrefix)).ToList())
                Object.DestroyImmediate(child.gameObject);
        }

        // ================================================================== STREET TREES / VERGES

        private static int PlantStreetTrees(Transform parent, Transform map, System.Random rng, List<Vector2> used)
        {
            int count = 0, sequence = 0;
            foreach (float road in RoadLines)
            {
                float roadHalf = Mathf.Abs(road) < 0.01f ? 10f : 8f;
                float verge = roadHalf + Walk + 4.8f;

                for (float along = -392f; along <= 392f; along += 44f)
                {
                    float run = along + ((float)rng.NextDouble() - 0.5f) * 7f;
                    if (DistanceToNearestRoadLine(run) < 25f) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        count += PlantRoadTree(parent, map, new Vector2(road + side * verge, run), rng, used, sequence++);
                        count += PlantRoadTree(parent, map, new Vector2(run, road + side * verge), rng, used, sequence++);
                    }
                }
            }
            return count;
        }

        private static int PlantRoadTree(Transform parent, Transform map, Vector2 p, System.Random rng, List<Vector2> used, int sequence)
        {
            if (!WorldNatureAllowed(p) || !FarEnough(used, p, 6.5f)) return 0;
            if (!Put(parent, map, Pick(rng, StreetTrees), p, rng.Next(360), 5.8f + (float)rng.NextDouble() * 2.8f, 2.1f, true)) return 0;
            used.Add(p);
            int count = 1;

            if (sequence % 2 == 0 && Put(parent, map, Pick(rng, Bushes), p + RandomDisc(rng, 1.2f, 2.3f), rng.Next(360), 0.75f + (float)rng.NextDouble() * 0.6f, 0.45f)) count++;
            if (sequence % 3 == 0 && Put(parent, map, Pick(rng, Flowers), p + RandomDisc(rng, 1.2f, 2.6f), rng.Next(360), 0.38f + (float)rng.NextDouble() * 0.22f, 0.25f)) count++;
            return count;
        }

        // ================================================================== PLOT-EDGE CLUTTER

        private static int PlantPlotEdgeClutter(Transform parent, Transform map, System.Random rng, List<Vector2> used)
        {
            int count = 0;
            for (int ix = 0; ix < BlockLines.Length - 1; ix++)
            for (int iz = 0; iz < BlockLines.Length - 1; iz++)
            {
                float x0 = BlockLines[ix], x1 = BlockLines[ix + 1];
                float z0 = BlockLines[iz], z1 = BlockLines[iz + 1];
                if (x1 - x0 < 45f || z1 - z0 < 45f) continue;

                Vector2[] pockets =
                {
                    new Vector2(Mathf.Lerp(x0, x1, 0.20f), Mathf.Lerp(z0, z1, 0.22f)),
                    new Vector2(Mathf.Lerp(x0, x1, 0.80f), Mathf.Lerp(z0, z1, 0.78f)),
                    new Vector2(Mathf.Lerp(x0, x1, 0.22f), Mathf.Lerp(z0, z1, 0.78f)),
                    new Vector2(Mathf.Lerp(x0, x1, 0.78f), Mathf.Lerp(z0, z1, 0.22f)),
                };

                int clusters = (ix + iz) % 3 == 0 ? 3 : 2;
                for (int c = 0; c < clusters; c++)
                {
                    Vector2 p = pockets[(c + rng.Next(pockets.Length)) % pockets.Length] + RandomDisc(rng, 0f, 4f);
                    if (!WorldNatureAllowed(p) || !FarEnough(used, p, 5f)) continue;
                    count += PlantCluster(parent, map, p, rng, used, (ix * 17 + iz * 31 + c) % 4 == 0);
                }
            }
            return count;
        }

        private static int PlantCluster(Transform parent, Transform map, Vector2 p, System.Random rng, List<Vector2> used, bool rockOrLog)
        {
            if (!OpenForNature(map, p, 1.8f)) return 0;
            used.Add(p);
            int count = 0;

            if (Put(parent, map, Pick(rng, Bushes), p, rng.Next(360), 0.75f + (float)rng.NextDouble() * 0.75f, 0.55f)) count++;
            for (int i = 0; i < 3; i++)
                if (Put(parent, map, Pick(rng, Grasses), p + RandomDisc(rng, 0.8f, 2.6f), rng.Next(360), 0.35f + (float)rng.NextDouble() * 0.35f, 0.22f)) count++;
            if (Put(parent, map, Pick(rng, Flowers), p + RandomDisc(rng, 0.8f, 2.3f), rng.Next(360), 0.35f + (float)rng.NextDouble() * 0.22f, 0.20f)) count++;

            if (rockOrLog)
            {
                Vector2 q = p + RandomDisc(rng, 1.8f, 3.2f);
                string prop = rng.NextDouble() < 0.62 ? Pick(rng, Rocks) : Pick(rng, ForestBits);
                bool solid = prop.StartsWith("rock") || prop.StartsWith("stone");
                if (Put(parent, map, prop, q, rng.Next(360), 0.65f + (float)rng.NextDouble() * 0.75f, 0.55f, solid)) count++;
            }
            return count;
        }

        // ================================================================== FOREST UNDERSTORY

        private static int PlantForestUnderstory(Transform parent, Transform map, System.Random rng, List<Vector2> used)
        {
            int count = 0;
            const float edge = 370f;
            for (float along = -394f; along <= 394f; along += 22f)
            {
                if (DistanceToNearestRoadLine(along) < 20f) continue;
                foreach (Vector2 basePoint in new[]
                {
                    new Vector2(along, edge), new Vector2(along, -edge),
                    new Vector2(edge, along), new Vector2(-edge, along)
                })
                {
                    Vector2 p = basePoint + RandomDisc(rng, 0f, 5f);
                    if (!FarEnough(used, p, 4f) || !WorldNatureAllowed(p)) continue;
                    count += PlantCluster(parent, map, p, rng, used, true);

                    if (rng.NextDouble() < 0.34)
                    {
                        Vector2 tree = p + RandomDisc(rng, 3.5f, 6.5f);
                        string pine = Pick(rng, "tree_pineDefaultA", "tree_pineRoundA", "tree_pineTallB", "tree_cone_dark");
                        if (FarEnough(used, tree, 5f) && Put(parent, map, pine, tree, rng.Next(360), 5f + (float)rng.NextDouble() * 4f, 1.8f, true))
                        {
                            used.Add(tree);
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        // ================================================================== HELPERS

        private static bool Put(Transform parent, Transform map, string model, Vector2 at, float yaw, float height, float clearance, bool solid = false)
        {
            if (!OpenForNature(map, at, clearance)) return false;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + model + ".fbx");
            if (prefab == null || !MapGroundAt(map, new Vector3(at.x, 0f, at.y), out float groundY)) return false;

            GameObject go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (go == null) return false;
            go.name = "DG+ " + model;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = new Vector3(at.x, groundY - 100f, at.y);

            Bounds b = GenesisWorldBuilder.RendererBounds(go);
            if (b.size.y > 0.001f) go.transform.localScale *= height / b.size.y;
            b = GenesisWorldBuilder.RendererBounds(go);
            go.transform.position += new Vector3(at.x - b.center.x, groundY - b.min.y, at.y - b.center.z);

            if (solid)
            {
                b = GenesisWorldBuilder.RendererBounds(go);
                BoxCollider col = go.AddComponent<BoxCollider>();
                float w = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.25f, 0.30f, 1.2f);
                col.center = go.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.35f, b.center.z));
                Vector3 s = go.transform.lossyScale;
                col.size = new Vector3(
                    w / Mathf.Max(0.001f, s.x),
                    b.size.y * 0.70f / Mathf.Max(0.001f, s.y),
                    w / Mathf.Max(0.001f, s.z));
            }

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return true;
        }

        private static bool OpenForNature(Transform map, Vector2 p, float radius)
        {
            if (!MapGroundAt(map, new Vector3(p.x, 0f, p.y), out float y)) return false;
            Collider[] hits = Physics.OverlapSphere(new Vector3(p.x, y + 1.2f, p.y), radius, ~0, QueryTriggerInteraction.Ignore);
            return hits.All(c => c == null || c.transform.IsChildOf(map));
        }

        private static bool MapGroundAt(Transform map, Vector3 p, out float y)
        {
            y = 0f;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 120f, p.z), Vector3.down, 240f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (!h.collider.transform.IsChildOf(map)) continue;
                if (h.normal.y < 0.80f || h.point.y < -4f || h.point.y > 4f) continue;
                y = h.point.y;
                return true;
            }
            return false;
        }

        private static bool WorldNatureAllowed(Vector2 p)
        {
            if (Mathf.Abs(p.x) > Half - 8f || Mathf.Abs(p.y) > Half - 8f) return false;
            if (p.magnitude < 70f) return false;
            if (Mathf.Abs(p.x) < 42f && p.y > 50f && p.y < 148f) return false;
            return true;
        }

        private static float DistanceToNearestRoadLine(float coordinate)
        {
            float best = float.MaxValue;
            foreach (float line in RoadLines) best = Mathf.Min(best, Mathf.Abs(coordinate - line));
            return best;
        }

        private static bool FarEnough(List<Vector2> used, Vector2 p, float gap)
        {
            for (int i = 0; i < used.Count; i++)
                if (Vector2.Distance(used[i], p) < gap) return false;
            return true;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static Vector2 RandomDisc(System.Random rng, float min, float max)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Lerp(min, max, (float)rng.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static string Pick(System.Random rng, params string[] values) => values[rng.Next(values.Length)];

        private static string Compass(Vector3 d)
        {
            if (Mathf.Abs(d.x) > Mathf.Abs(d.z)) return d.x > 0f ? "east" : "west";
            return d.z > 0f ? "north" : "south";
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
#endif
