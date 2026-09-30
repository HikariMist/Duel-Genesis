#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>Japanese city blocks: fills the open city's empty plots with Akihabara buildings (Zenrin "Japanese Otaku City").</summary>
    public static partial class GenesisCityBlocks
    {
        public const string AkibaModel = "Assets/ZRNAssets/005339_08932_25_14/Models/PQ_Remake_AKIHABARA.fbx";

        [MenuItem("Duel Genesis/DEV/Log Akihabara Model Parts")]
        public static void LogParts()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AkibaModel);
            if (model == null) { Debug.LogWarning("no model"); return; }
            var go = (GameObject)Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            var sb = new StringBuilder();
            void Walk(Transform t, int depth)
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) return;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                int tris = t.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).Sum(f => (int)(f.sharedMesh.triangles.Length / 3));
                sb.AppendLine($"{new string(' ', depth * 2)}{t.name} | kids {t.childCount} | size {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0} | centre {b.center.x:0} {b.center.y:0} {b.center.z:0} | tris {tris} | mats {string.Join(",", rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().Take(4))}");
                if (depth < 2) foreach (Transform c in t) Walk(c, depth + 1);
            }
            Walk(go.transform, 0);
            Object.DestroyImmediate(go);
            File.WriteAllText("Logs/DG-AkibaParts.txt", sb.ToString());
            Debug.Log("Duel: Genesis wrote Logs/DG-AkibaParts.txt");
        }

        public const string RootName = "Japanese City Blocks";
        private const float Scale = 10f;   // the Akihabara model is 1/10 scale (same as the archived map)
        private static readonly float[] Lines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };

        private sealed class Piece { public GameObject Source; public Vector3 Size; public bool Tall; }

        [MenuItem("Duel Genesis/World/15. Fill City Blocks With Japanese Buildings")]
        public static void Fill()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AkibaModel);
            if (model == null) { Debug.LogWarning("Duel: Genesis: the Japanese Otaku City model is missing: " + AkibaModel); return; }

            Transform old = city.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(city.transform, false);

            // A hidden copy of the whole Akihabara model to pick buildings from.
            var source = (GameObject)PrefabUtility.InstantiatePrefab(model);
            source.name = "Akihabara Source (temp)";
            source.transform.localScale = Vector3.one * Scale;
            var pieces = new List<Piece>();
            foreach (Transform group in source.transform)
            {
                if (!group.name.StartsWith("Block_") && group.name != "TallBuilding") continue;
                foreach (Transform b in group)
                {
                    var rs = b.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    Bounds bb = rs[0].bounds;
                    foreach (var r in rs) bb.Encapsulate(r.bounds);
                    Vector3 sz = bb.size;
                    float foot = Mathf.Max(sz.x, sz.z);
                    if (sz.y < 9f || foot < 7f || foot > 38f || Mathf.Min(sz.x, sz.z) < 5f) continue;
                    pieces.Add(new Piece { Source = b.gameObject, Size = sz, Tall = sz.y > 45f });
                }
            }
            var normal = pieces.Where(p => !p.Tall).ToList();
            var tall = pieces.Where(p => p.Tall).ToList();

            var nature = NatureObjects(city.transform);
            var rng = new System.Random(20260929);
            int placed = 0, blocks = 0, clearedNature = 0;
            for (int i = 0; i < Lines.Length - 1; i++)
            for (int j = 0; j < Lines.Length - 1; j++)
            {
                float x0 = Lines[i] + RoadHalf(Lines[i]), x1 = Lines[i + 1] - RoadHalf(Lines[i + 1]);
                float z0 = Lines[j] + RoadHalf(Lines[j]), z1 = Lines[j + 1] - RoadHalf(Lines[j + 1]);
                var rect = new Rect(x0, z0, x1 - x0, z1 - z0);
                if (Mathf.Abs(rect.center.x) < 120f && Mathf.Abs(rect.center.y) < 120f) continue;   // the four parks round the plaza
                if (GenesisGarden.IsReserved(rect.center)) continue;                                 // the Japanese garden
                var block = new GameObject($"Block {(char)('A' + i)}{j + 1}").transform;
                block.SetParent(root, false);
                blocks++;
                // Walk the four street fronts; north/south fronts run the full length, east/west fronts fit between them.
                placed += Front(block, rect, 0, normal, tall, rng, nature, ref clearedNature);
                placed += Front(block, rect, 1, normal, tall, rng, nature, ref clearedNature);
                placed += Front(block, rect, 2, normal, tall, rng, nature, ref clearedNature);
                placed += Front(block, rect, 3, normal, tall, rng, nature, ref clearedNature);
            }
            Object.DestroyImmediate(source);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis filled {blocks} city blocks with {placed} Japanese buildings (from {pieces.Count} Akihabara buildings, {tall.Count} towers); moved {clearedNature} plants out of their way.");
        }

        private static float RoadHalf(float line) => Mathf.Abs(line) < 0.01f ? 14f : 12f;

        /// <summary>True where buildings must not go: the plaza ring, the Duel Center, the card shop lot, the garden, the forest edge.</summary>
        private static bool Blocked(Vector2 p, float pad)
        {
            if (p.magnitude < 70f + pad) return true;
            if (GenesisDuelCenter.InColosseum(p, pad)) return true;
            if (GenesisDuelCenter.InCardShopLot(p)) return true;
            if (GenesisGarden.IsReserved(p)) return true;
            if (Mathf.Abs(p.x) > 364f - pad || Mathf.Abs(p.y) > 364f - pad) return true;
            return false;
        }

        /// <summary>Lines one street front of a block with buildings, fronts to the street, with small gaps and the odd alley.</summary>
        private static int Front(Transform block, Rect r, int side, List<Piece> normal, List<Piece> tall, System.Random rng, List<Transform> nature, ref int cleared)
        {
            // side 0 = north (z max, faces +Z), 1 = south (faces -Z), 2 = east (x max, faces +X), 3 = west (faces -X)
            bool alongX = side < 2;
            float start = alongX ? r.xMin : r.yMin + 20f, end = alongX ? r.xMax : r.yMax - 20f;
            float edge = side == 0 ? r.yMax : side == 1 ? r.yMin : side == 2 ? r.xMax : r.xMin;
            float inward = side == 0 || side == 2 ? -1f : 1f;
            Vector3 facing = side == 0 ? Vector3.forward : side == 1 ? Vector3.back : side == 2 ? Vector3.right : Vector3.left;
            float maxDepth = Mathf.Min(19f, (alongX ? r.height : r.width) * 0.5f - 3f);
            int n = 0;
            float t = start + 1f;
            while (t < end - 6f)
            {
                bool corner = t < start + 3f || t > end - 30f;
                var pool = corner && tall.Count > 0 && rng.NextDouble() < 0.12 ? tall : normal;
                Piece piece = null;
                bool swap = false;
                for (int tries = 0; tries < 12 && piece == null; tries++)
                {
                    var c = pool[rng.Next(pool.Count)];
                    bool s = rng.NextDouble() < 0.5;
                    float w = s ? c.Size.z : c.Size.x, d = s ? c.Size.x : c.Size.z;
                    if (d > maxDepth || t + w > end) { if (d > maxDepth) { s = !s; w = s ? c.Size.z : c.Size.x; d = s ? c.Size.x : c.Size.z; } }
                    if (d <= maxDepth && t + w <= end) { piece = c; swap = s; }
                }
                if (piece == null) break;
                float width = swap ? piece.Size.z : piece.Size.x, depth = swap ? piece.Size.x : piece.Size.z;
                float setback = 0.6f + (float)rng.NextDouble() * 1.2f;
                float along = t + width * 0.5f, across = edge + inward * (setback + depth * 0.5f);
                Vector2 centre = alongX ? new Vector2(along, across) : new Vector2(across, along);
                float pad = Mathf.Max(width, depth) * 0.5f;
                if (!Blocked(centre, pad))
                {
                    // Turn the model so the dimension we measured as its width runs along the street; flip at random for variety.
                    float yaw = (swap ? 90f : 0f) + (alongX ? 0f : 90f) + (rng.NextDouble() < 0.5 ? 180f : 0f);
                    Place(block, piece, centre, yaw);
                    cleared += ClearNature(nature, centre, alongX ? width : depth, alongX ? depth : width);
                    n++;
                }
                // Gaps: mostly a narrow slot between buildings, sometimes a lane, now and then an alley.
                double g = rng.NextDouble();
                t += width + (g < 0.7 ? 0.6f + (float)rng.NextDouble() * 1.4f : g < 0.92 ? 2.5f + (float)rng.NextDouble() * 2f : 6f + (float)rng.NextDouble() * 3f);
            }
            return n;
        }

        private static void Place(Transform parent, Piece piece, Vector2 centre, float yaw)
        {
            var holder = new GameObject(piece.Source.name.Replace("005339_08932_", "Building "));
            holder.transform.SetParent(parent, false);
            var copy = Object.Instantiate(piece.Source, holder.transform);
            copy.name = "Model";
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one * Scale;
            copy.transform.localPosition = Vector3.zero;
            Bounds b = GenesisWorldBuilder.RendererBounds(copy);
            copy.transform.position -= new Vector3(b.center.x - holder.transform.position.x, b.min.y - holder.transform.position.y, b.center.z - holder.transform.position.z);
            holder.transform.position = new Vector3(centre.x, 0f, centre.y);
            holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var col = holder.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, b.size.y * 0.5f, 0f);
            col.size = new Vector3(b.size.x, b.size.y, b.size.z);
            foreach (Transform t in holder.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static List<Transform> NatureObjects(Transform city)
        {
            var list = new List<Transform>();
            foreach (string n in new[] { "Nature", GenesisGarden.BlossomName })
            {
                Transform root = city.Find(n);
                if (root == null) continue;
                foreach (Transform g in root)
                {
                    if (g.childCount == 0 || g.GetComponent<Renderer>() != null || g.GetComponent<LODGroup>() != null) list.Add(g);
                    else foreach (Transform c in g) list.Add(c);
                }
            }
            Transform street = city.Find("Street Dressing");
            if (street != null) foreach (Transform c in street) list.Add(c);
            return list;
        }

        private static int ClearNature(List<Transform> nature, Vector2 c, float sx, float sz)
        {
            int n = 0;
            for (int i = nature.Count - 1; i >= 0; i--)
            {
                Transform t = nature[i];
                if (t == null) { nature.RemoveAt(i); continue; }
                Vector3 p = t.position;
                if (Mathf.Abs(p.x - c.x) < sx * 0.5f + 1.5f && Mathf.Abs(p.z - c.y) < sz * 0.5f + 1.5f)
                {
                    Object.DestroyImmediate(t.gameObject);
                    nature.RemoveAt(i);
                    n++;
                }
            }
            return n;
        }
    }
}
#endif
