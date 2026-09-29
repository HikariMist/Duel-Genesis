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
    /// Plants the Kenney Nature Kit (CC0) into the open Genesis City:
    ///   - four parks on the blocks diagonal to the plaza (tree groves, bushes, flower beds, rocks, an obelisk),
    ///   - a pine forest band round the edge of the map, so the city ends in woods instead of a void.
    /// Building plots elsewhere stay empty for future buildings. Safe to re-run.
    /// </summary>
    public static class GenesisNature
    {
        private const string Kit = GenesisAssetDownloads.NatureKitFolder + "/";
        private const float Half = 420f;

        [MenuItem("Duel Genesis/World/10. Plant The Nature Kit (parks + forest edge)")]
        public static void Plant()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "tree_default.fbx") == null) { Debug.LogWarning("Duel: Genesis: download the Nature Kit first (Downloads)."); return; }

            Transform old = city.transform.Find("Nature");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Nature").transform;
            root.SetParent(city.transform, false);
            var rng = new System.Random(20260928);
            int count = 0;

            // ---- parks on the four blocks diagonal to the plaza
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var park = new GameObject($"Park {(sz > 0 ? "N" : "S")}{(sx > 0 ? "E" : "W")}").transform;
                park.SetParent(root, false);
                float x0 = 20f, x1 = 102f;   // inside the curbs of the block between the boulevard and the first avenue
                Vector2 centre = new Vector2(sx * (x0 + x1) * 0.5f, sz * (x0 + x1) * 0.5f);
                bool Allowed(Vector2 p) =>
                    p.magnitude > 68f &&                                         // clear of the ring road
                    !(Mathf.Abs(p.x) < 28f && p.y > 58f && p.y < 128f) &&        // clear of the Duel Center and its forecourt
                    Vector2.Distance(p, centre) > 9f;                            // the obelisk clearing
                Vector2 Rand() => new Vector2(sx * Mathf.Lerp(x0, x1, (float)rng.NextDouble()), sz * Mathf.Lerp(x0, x1, (float)rng.NextDouble()));
                var used = new List<Vector2>();
                Vector2? Spot(float gap)
                {
                    for (int t = 0; t < 40; t++)
                    {
                        Vector2 p = Rand();
                        if (Allowed(p) && used.All(u => Vector2.Distance(u, p) > gap)) { used.Add(p); return p; }
                    }
                    return null;
                }

                count += Put(park, "statue_obelisk", centre, 0f, 4.5f, solid: true);
                for (int i = 0; i < 10; i++)   // a flower ring round the obelisk
                {
                    float a = i * 36f * Mathf.Deg2Rad;
                    count += Put(park, Pick(rng, "flower_redA", "flower_yellowA", "flower_purpleA", "flower_redB", "flower_yellowB"), centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5.5f, a * 57f, 0.6f);
                }
                string[] trees = { "tree_default", "tree_oak", "tree_detailed", "tree_fat", "tree_tall", "tree_plateau", "tree_default_fall", "tree_oak_fall" };
                for (int i = 0; i < 22; i++) { var p = Spot(7f); if (p != null) count += Put(park, Pick(rng, trees), p.Value, rng.Next(360), 6f + (float)rng.NextDouble() * 3f, solid: true); }
                for (int i = 0; i < 26; i++) { var p = Spot(3f); if (p != null) count += Put(park, Pick(rng, "plant_bush", "plant_bushLarge", "plant_bushDetailed", "plant_bushSmall"), p.Value, rng.Next(360), 1f + (float)rng.NextDouble() * 0.8f); }
                for (int i = 0; i < 14; i++)   // flower beds: little clusters
                {
                    var p = Spot(4f);
                    if (p == null) continue;
                    string f = Pick(rng, "flower_redA", "flower_redB", "flower_redC", "flower_yellowA", "flower_yellowB", "flower_yellowC", "flower_purpleA", "flower_purpleB", "flower_purpleC");
                    for (int k = 0; k < 5; k++)
                        count += Put(park, f, p.Value + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 2.2f, rng.Next(360), 0.5f);
                }
                for (int i = 0; i < 20; i++) { var p = Spot(2f); if (p != null) count += Put(park, Pick(rng, "grass", "grass_large", "grass_leafs", "grass_leafsLarge"), p.Value, rng.Next(360), 0.5f); }
                for (int i = 0; i < 8; i++) { var p = Spot(4f); if (p != null) count += Put(park, Pick(rng, "rock_largeA", "rock_largeB", "rock_largeC", "stone_largeA", "stone_largeB"), p.Value, rng.Next(360), 0.8f + (float)rng.NextDouble(), solid: true); }
                for (int i = 0; i < 4; i++) { var p = Spot(4f); if (p != null) count += Put(park, Pick(rng, "log", "log_large", "stump_round", "stump_old", "mushroom_redGroup", "mushroom_tanGroup"), p.Value, rng.Next(360), 0.6f); }
            }

            // ---- pine forest round the map edge
            var forest = new GameObject("Forest Edge").transform;
            forest.SetParent(root, false);
            string[] pines = { "tree_pineDefaultA", "tree_pineDefaultB", "tree_pineRoundA", "tree_pineRoundC", "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_cone", "tree_cone_dark" };
            for (float d = Half - 8f; d > Half - 40f; d -= 11f)
                for (float t = -Half + 6f; t < Half - 6f; t += 12f)
                    foreach (var p in new[] { new Vector2(t, d), new Vector2(t, -d), new Vector2(d, t), new Vector2(-d, t) })
                    {
                        Vector2 j = p + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 6f;
                        if (OnRoad(j)) continue;
                        count += Put(forest, Pick(rng, pines), j, rng.Next(360), 8f + (float)rng.NextDouble() * 6f, solid: true);
                    }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis planted the Nature Kit: {count} pieces (4 parks + the forest edge).");
        }

        /// <summary>True on the boulevards and avenues that run out to the map edge (kept clear).</summary>
        private static bool OnRoad(Vector2 p)
        {
            float[] lines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };
            return lines.Any(l => Mathf.Abs(p.x - l) < (l == 0f ? 16f : 13f) || Mathf.Abs(p.y - l) < (l == 0f ? 16f : 13f));
        }

        private static string Pick(System.Random rng, params string[] names) => names[rng.Next(names.Length)];

        /// <summary>Places a kit model sized to <paramref name="height"/> metres, standing on the ground. Returns 1 if placed.</summary>
        private static int Put(Transform parent, string model, Vector2 at, float yaw, float height, bool solid = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + model + ".fbx");
            if (prefab == null) return 0;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localPosition = Vector3.zero;
            Bounds b = GenesisWorldBuilder.RendererBounds(go);
            if (b.size.y > 0.001f) go.transform.localScale *= height / b.size.y;
            b = GenesisWorldBuilder.RendererBounds(go);
            go.transform.position += new Vector3(at.x - b.center.x, -b.min.y, at.y - b.center.z);
            if (solid)
            {
                b = GenesisWorldBuilder.RendererBounds(go);
                var col = go.AddComponent<BoxCollider>();
                float w = Mathf.Min(b.size.x, b.size.z) * 0.25f;
                col.center = go.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.35f, b.center.z));
                Vector3 s = go.transform.lossyScale;
                col.size = new Vector3(Mathf.Max(0.3f, w) / s.x, b.size.y * 0.7f / s.y, Mathf.Max(0.3f, w) / s.z);
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return 1;
        }
    }
}
#endif
