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
    /// Replaces every low-poly tree in the open city (Kenney Nature Kit trees, Polytope street/plaza trees) with the
    /// realistic Poly Haven trees baked by Production Assets > Bake Game-Ready Poly Haven Trees. Each new tree stands
    /// where the old one did, a little taller. Safe to re-run.
    /// </summary>
    public static class GenesisTreeSwap
    {
        private static readonly string[] Broadleaf = { "tree_small_02", "jacaranda_tree", "tree_small_02" };   // island_tree_03 sits on a sand mound: beach only
        private static readonly string[] Conifer = { "fir_tree_01" };

        [MenuItem("Duel Genesis/World/13. Swap Low-Poly Trees For Realistic Ones")]
        public static void Swap()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null) { Debug.LogWarning("Duel: Genesis: no city in the scene."); return; }
            var broad = Broadleaf.Select(GenesisRealTrees.Load).Where(p => p != null).ToArray();
            var conifer = Conifer.Select(GenesisRealTrees.Load).Where(p => p != null).ToArray();
            if (broad.Length == 0) { Debug.LogWarning("Duel: Genesis: bake the Poly Haven trees first (Production Assets)."); return; }
            if (conifer.Length == 0) conifer = broad;

            var targets = new List<(GameObject go, bool pine)>();
            foreach (Transform t in city.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                string src = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)) ?? "";
                string file = System.IO.Path.GetFileNameWithoutExtension(src).ToLowerInvariant();
                bool lowPolyTree = (src.Contains("Polytope") && file.Contains("tree")) || (src.Contains("NatureKit") && file.StartsWith("tree"));
                if (!lowPolyTree) continue;
                targets.Add((go, file.Contains("pine") || file.Contains("cone") || file.Contains("fir")));
            }

            var rng = new System.Random(20260929);
            int n = 0;
            foreach (var (go, pine) in targets)
            {
                Bounds b = GenesisWorldBuilder.RendererBounds(go);
                Transform parent = go.transform.parent;
                GameObject prefab = pine ? conifer[rng.Next(conifer.Length)] : broad[rng.Next(broad.Length)];
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                tree.name = "Tree (" + prefab.name + ")";
                tree.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                Bounds tb = GenesisWorldBuilder.RendererBounds(tree);
                float height = Mathf.Max(b.size.y * 1.35f, 7f) * (0.9f + (float)rng.NextDouble() * 0.25f);
                if (tb.size.y > 0.01f) tree.transform.localScale *= height / tb.size.y;
                tb = GenesisWorldBuilder.RendererBounds(tree);
                tree.transform.position += new Vector3(b.center.x - tb.center.x, b.min.y - tb.min.y, b.center.z - tb.center.z);
                Object.DestroyImmediate(go);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis swapped {n} low-poly trees for realistic Poly Haven trees.");
        }
    }
}
#endif
