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
    /// Genesis City, open layout. The dense Akihabara map is archived (kept in the scene, switched off) and
    /// replaced by an open floor plan that buildings can be added to over time:
    ///   - a big central plaza (the spawn hub) with open-air duel tables, benches, lamps and trees,
    ///   - four tree-lined boulevards running out from the plaza and a ring road around it,
    ///   - a grid of avenues every 120 m, with pavements framing empty grass building plots,
    ///   - the Genesis Duel Center closing the north boulevard, its entrance facing the plaza.
    /// The new root is still called "DG City" (with a "Map" child), so the sky hologram, city lighting and
    /// the other World tools keep working. Re-running rebuilds it; the archived map is never touched.
    /// </summary>
    public static class GenesisOpenWorld
    {
        public const string ArchivedName = "DG City (Akihabara, archived)";
        private const string MatFolder = "Assets/Art/Generated/OpenWorld";
        private const string Polygon = "Assets/POLYGON city pack/Prefabs/";
        private const string Nature = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";

        private const float Half = 420f;          // the map is 840 m square
        private const float PlazaR = 50f;         // paved hub
        private const float RingW = 12f;          // ring road around it
        private const float Boulevard = 20f;      // four roads out of the plaza
        private const float Avenue = 16f;         // the grid
        private const float Walk = 4f;            // pavement along every road
        private static readonly float[] GridLines = { -360f, -240f, -120f, 120f, 240f, 360f };

        private static Material _grass, _asphalt, _pavement, _paving, _paint, _curb, _cyan, _gold;

        [MenuItem("Duel Genesis/World/9. Build Open Genesis City (archive the Akihabara map)")]
        public static void Build()
        {
            var log = new StringBuilder();

            // 1. Archive the old map: renamed and switched off, never deleted.
            GameObject current = FindRoot(GenesisWorldBuilder.CityRootName);
            if (current != null && current.transform.Find("Open World Marker") == null)
            {
                GameObject oldArchive = FindRoot(ArchivedName);
                if (oldArchive != null) Object.DestroyImmediate(current);   // a half-built open city: rebuild it
                else
                {
                    current.name = ArchivedName;
                    current.SetActive(false);
                    log.AppendLine("Archived the Akihabara map (switched off, nothing deleted).");
                }
            }
            else if (current != null) Object.DestroyImmediate(current);    // rebuilding the open city

            Materials();
            var city = new GameObject(GenesisWorldBuilder.CityRootName).transform;
            new GameObject("Open World Marker").transform.SetParent(city, false);
            Transform map = Child(city, "Map");

            // 2. Ground: one grass slab with the only collider the streets need.
            Box(map, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(Half * 2f, 1f, Half * 2f), _grass, collider: true);
            foreach (var (pos, size) in new[]
                     {
                         (new Vector3(0f, 10f, Half), new Vector3(Half * 2f, 20f, 1f)), (new Vector3(0f, 10f, -Half), new Vector3(Half * 2f, 20f, 1f)),
                         (new Vector3(Half, 10f, 0f), new Vector3(1f, 20f, Half * 2f)), (new Vector3(-Half, 10f, 0f), new Vector3(1f, 20f, Half * 2f)),
                     })
            {
                var wall = new GameObject("Map Edge");
                wall.transform.SetParent(map, false);
                wall.transform.localPosition = pos;
                wall.AddComponent<BoxCollider>().size = size;
            }

            // 3. Roads.
            Transform roads = Child(map, "Roads");
            float ringOuter = PlazaR + RingW;
            Disc(roads, "Ring Road", Vector3.zero, ringOuter, 0.015f, _asphalt);
            DiscRing(roads, "Ring Road Edge Line", ringOuter - 0.6f, 0.02f, 0.25f, _paint, 96);
            foreach (float s in new[] { 1f, -1f })
            {
                Road(roads, "Boulevard N/S", new Vector3(0f, 0f, s * (ringOuter + Half) * 0.5f), Half - ringOuter, Boulevard, alongZ: true);
                Road(roads, "Boulevard E/W", new Vector3(s * (ringOuter + Half) * 0.5f, 0f, 0f), Half - ringOuter, Boulevard, alongZ: false);
            }
            foreach (float g in GridLines)
            {
                Road(roads, "Avenue (x)", new Vector3(g, 0f, 0f), Half * 2f, Avenue, alongZ: true);
                Road(roads, "Avenue (z)", new Vector3(0f, 0f, g), Half * 2f, Avenue, alongZ: false);
            }

            // 4. Pavements around every block; the grass inside is an empty building plot.
            Transform plots = Child(map, "Building Plots");
            var lines = new List<float> { -Half, 0f, Half };
            lines.AddRange(GridLines);
            lines.Sort();
            int plotCount = 0;
            for (int i = 0; i < lines.Count - 1; i++)
            for (int j = 0; j < lines.Count - 1; j++)
            {
                float x0 = lines[i] + RoadHalf(lines[i]), x1 = lines[i + 1] - RoadHalf(lines[i + 1]);
                float z0 = lines[j] + RoadHalf(lines[j]), z1 = lines[j + 1] - RoadHalf(lines[j + 1]);
                if (x1 - x0 < 20f || z1 - z0 < 20f) continue;
                var block = new Rect(x0, z0, x1 - x0, z1 - z0);
                plotCount++;
                Transform plot = Child(plots, $"Plot {(char)('A' + i)}{j + 1}");
                Pavement(plot, block, ringOuter);
            }

            // 5. The central plaza: the spawn hub.
            Transform plaza = Child(city, "Central Plaza");
            BuildPlaza(plaza, log);

            // 6. Boulevard trees and lamps.
            Transform street = Child(city, "Street Dressing");
            int trees = 0, lamps = 0;
            string[] treeKinds = { "Trees/PT_Fruit_Tree_01_green.prefab", "Trees/PT_Pine_Tree_03_green.prefab", "Trees/PT_Fruit_Tree_01_green.prefab" };
            for (float d = ringOuter + 14f; d < Half - 12f; d += 22f)
            foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                if (dir == Vector3.forward && d > 60f && d < 140f) continue;   // the Duel Center's forecourt
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector3 p = dir * d + side * s * (Boulevard * 0.5f + Walk * 0.5f);
                    if (NearGridRoad(p, 6f)) continue;
                    if (Spawn(street, Nature + treeKinds[trees % treeKinds.Length], p, (d * 37f) % 360f, height: 6f) != null) trees++;
                    Vector3 lp = dir * (d + 11f) + side * s * (Boulevard * 0.5f + 0.6f);
                    if (!NearGridRoad(lp, 6f) && Spawn(street, Polygon + "Lamps/street_lamp 1 prefab.prefab", lp, Quaternion.LookRotation(-side * s).eulerAngles.y + 90f, height: 6f) != null) lamps++;
                }
            }

            // 7. The Genesis Duel Center (the Genesis Colosseum) closes the north boulevard, its portal facing the plaza.
            GameObject hall = GenesisDuelCenter.BuildColosseum(city, GenesisDuelCenter.ColosseumCentre, GenesisDuelCenter.ColosseumYaw);
            GenesisDuelCenter.BuildCardShop(city, GenesisDuelCenter.CardShopSpot, GenesisDuelCenter.CardShopYaw);
            Box(map, "Duel Center Forecourt", new Vector3(0f, 0.012f, 71f), new Vector3(40f, 0.024f, 18f), _paving);

            // 8. Spawn: the plaza's south side, looking north across it to the Duel Center.
            GameObject player = GameObject.Find("Player_Hikari_Blockout");
            if (player != null)
            {
                Vector3 p = player.transform.position;
                player.transform.SetPositionAndRotation(new Vector3(0f, Mathf.Max(p.y, 0.05f), -26f), Quaternion.identity);
                log.AppendLine("Player spawn moved to the plaza (0, 0, -26), facing the Duel Center.");
            }

            foreach (Transform t in map.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = city.gameObject;
            Debug.Log($"Duel: Genesis built the open city: {Half * 2f:0} m square, {plotCount} empty building plots, {trees} trees, {lamps} street lamps, Duel Center {(hall != null ? "placed" : "MISSING")}.\n{log}");
        }

        [MenuItem("Duel Genesis/World/9c. Rebuild Just The Duel Center (open city)")]
        public static void RebuildDuelCenter()
        {
            GameObject city = FindRoot(GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            GenesisDuelCenter.BuildColosseum(city.transform, GenesisDuelCenter.ColosseumCentre, GenesisDuelCenter.ColosseumYaw);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        [MenuItem("Duel Genesis/World/9b. Restore The Akihabara Map")]
        public static void Restore()
        {
            GameObject archived = FindRoot(ArchivedName);
            if (archived == null) { Debug.LogWarning("Duel: Genesis: no archived Akihabara map in this scene."); return; }
            GameObject open = FindRoot(GenesisWorldBuilder.CityRootName);
            if (open != null) { open.name = "DG City (open layout, off)"; open.SetActive(false); }
            archived.name = GenesisWorldBuilder.CityRootName;
            archived.SetActive(true);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Duel: Genesis restored the Akihabara map (the open layout is switched off).");
        }

        // ------------------------------------------------------------------ plaza

        private static void BuildPlaza(Transform plaza, StringBuilder log)
        {
            Disc(plaza, "Plaza Paving", Vector3.zero, PlazaR, 0.03f, _paving);
            DiscRing(plaza, "Plaza Curb", PlazaR - 0.4f, 0.12f, 0.8f, _curb, 128);
            DiscRing(plaza, "Plaza Glow Ring", 20f, 0.035f, 0.35f, _cyan, 96);
            DiscRing(plaza, "Plaza Inner Ring", 34f, 0.034f, 0.5f, _pavement, 96);
            for (int i = 0; i < 8; i++)   // spokes of lighter paving
            {
                Quaternion r = Quaternion.Euler(0f, i * 45f + 22.5f, 0f);
                var spoke = Box(plaza, "Plaza Spoke", r * new Vector3(0f, 0.034f, 35f), new Vector3(1.2f, 0.01f, 28f), _pavement);
                spoke.transform.localRotation = r;
            }

            // The Duel Genesis crest on the ground, in front of the spawn.
            var logoTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/DuelGenesis/DuelGenesisLogo.png");
            if (logoTex != null)
            {
                Material logo = Mat("OW Plaza Crest", Color.white, tex: logoTex, emission: Color.white * 0.5f);
                logo.SetFloat("_AlphaClip", 1f);
                logo.SetFloat("_Cutoff", 0.35f);
                logo.EnableKeyword("_ALPHATEST_ON");
                var crest = GameObject.CreatePrimitive(PrimitiveType.Quad);
                crest.name = "Plaza Crest";
                Object.DestroyImmediate(crest.GetComponent<Collider>());
                crest.transform.SetParent(plaza, false);
                crest.transform.localPosition = new Vector3(0f, 0.045f, -10f);
                crest.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                crest.transform.localScale = new Vector3(12f * 1.075f, 12f, 1f);
                crest.GetComponent<Renderer>().sharedMaterial = logo;
            }

            // Four open-air duel tables on the glow ring (east, west and the two southern diagonals).
            int tables = 0;
            foreach (float a in new[] { 90f, 270f, 135f, 225f })
            {
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 26f;
                var table = new GameObject("Plaza Duel Table " + (++tables));
                table.transform.SetParent(plaza, false);
                table.transform.localPosition = p;
                table.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                var decor = table.AddComponent<DuelGenesis.Dueling.AmbientDuelTable>();
                decor.seed = 800 + tables;
                decor.dealCards = true;
                var col = table.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.38f, 0f);
                col.size = new Vector3(1.24f, 0.76f, 0.94f);
                var duel = table.AddComponent<DuelGenesis.Dueling.CityDuelTable>();
                duel.opponentSeed = 900 + tables * 13;
                duel.tableName = "Plaza Table " + tables;
                var beacon = new GameObject("Beacon - DUEL TABLE");
                beacon.transform.SetParent(table.transform, false);
                beacon.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                var gb = beacon.AddComponent<DuelGenesis.Core.GenesisBeacon>();
                gb.label = "DUEL TABLE";
                gb.beamHeight = 18f;
                gb.labelHeight = 3.2f;
            }

            // Lamps with warm light, benches facing in, and a ring of trees at the edge.
            int lamps = 0;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 38f;
                GameObject lamp = Spawn(plaza, Polygon + "Lamps/street_lamp 1 prefab.prefab", p, a + 90f, height: 6f);
                if (lamp == null) continue;
                lamps++;
                Bounds lb = GenesisWorldBuilder.RendererBounds(lamp);
                var lg = new GameObject("Lamp Light");
                lg.transform.SetParent(lamp.transform, true);
                lg.transform.position = new Vector3(lb.center.x, lb.max.y - 0.4f, lb.center.z);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 16f;
                l.intensity = 4f;
                l.color = new Color(1f, 0.84f, 0.62f);
                l.shadows = LightShadows.None;
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f + 11.25f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 20f || Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 20f) continue;   // keep north/south walkways open
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 42f;
                Spawn(plaza, Polygon + "Props/bench prefab.prefab", p, a + 180f, length: 1.9f);
                Vector3 t = Quaternion.Euler(0f, a + 11.25f, 0f) * Vector3.forward * 46f;
                if (Mathf.Abs(Mathf.DeltaAngle(a + 11.25f, 90f)) > 8f && Mathf.Abs(Mathf.DeltaAngle(a + 11.25f, 270f)) > 8f)
                    Spawn(plaza, Nature + (i % 2 == 0 ? "Trees/PT_Fruit_Tree_01_green.prefab" : "Trees/PT_Pine_Tree_03_green.prefab"), t, a * 7f, height: 6.5f);
            }
            log.AppendLine($"Plaza: {tables} open-air duel tables, {lamps} lit lamps, benches and trees.");
        }

        // ------------------------------------------------------------------ roads and blocks

        private static float RoadHalf(float line) =>
            Mathf.Abs(line) >= Half - 0.01f ? 0f : (Mathf.Abs(line) < 0.01f ? Boulevard * 0.5f : Avenue * 0.5f) + Walk;

        private static bool NearGridRoad(Vector3 p, float margin) =>
            GridLines.Any(g => Mathf.Abs(p.x - g) < Avenue * 0.5f + margin || Mathf.Abs(p.z - g) < Avenue * 0.5f + margin);

        private static void Road(Transform parent, string name, Vector3 centre, float length, float width, bool alongZ)
        {
            Vector3 size = alongZ ? new Vector3(width, 0.02f, length) : new Vector3(length, 0.02f, width);
            Box(parent, name, centre + Vector3.up * 0.01f, size, _asphalt);
            // Edge lines and a centre line.
            foreach (float o in new[] { -width * 0.5f + 0.5f, width * 0.5f - 0.5f, 0f })
            {
                Vector3 off = alongZ ? new Vector3(o, 0f, 0f) : new Vector3(0f, 0f, o);
                Vector3 ls = alongZ ? new Vector3(o == 0f ? 0.3f : 0.18f, 0.005f, length) : new Vector3(length, 0.005f, o == 0f ? 0.3f : 0.18f);
                Box(parent, name + " Line", centre + off + Vector3.up * 0.022f, ls, o == 0f ? _gold : _paint);
            }
        }

        /// <summary>Pavement just inside the block edge and a low curb round the empty plot, both kept off the ring road.</summary>
        private static void Pavement(Transform plot, Rect block, float ringOuter)
        {
            const float y = 0.028f;
            Clipped(plot, "Pavement S", new Vector3(block.center.x, y, block.yMin + Walk * 0.5f), new Vector3(block.width, 0.03f, Walk), _pavement, ringOuter);
            Clipped(plot, "Pavement N", new Vector3(block.center.x, y, block.yMax - Walk * 0.5f), new Vector3(block.width, 0.03f, Walk), _pavement, ringOuter);
            Clipped(plot, "Pavement W", new Vector3(block.xMin + Walk * 0.5f, y, block.center.y), new Vector3(Walk, 0.03f, block.height - Walk * 2f), _pavement, ringOuter);
            Clipped(plot, "Pavement E", new Vector3(block.xMax - Walk * 0.5f, y, block.center.y), new Vector3(Walk, 0.03f, block.height - Walk * 2f), _pavement, ringOuter);
            var r = new Rect(block.xMin + Walk, block.yMin + Walk, block.width - Walk * 2f, block.height - Walk * 2f);
            Clipped(plot, "Plot Curb", new Vector3(r.center.x, 0.06f, r.yMin), new Vector3(r.width, 0.12f, 0.3f), _curb, ringOuter);
            Clipped(plot, "Plot Curb", new Vector3(r.center.x, 0.06f, r.yMax), new Vector3(r.width, 0.12f, 0.3f), _curb, ringOuter);
            Clipped(plot, "Plot Curb", new Vector3(r.xMin, 0.06f, r.center.y), new Vector3(0.3f, 0.12f, r.height), _curb, ringOuter);
            Clipped(plot, "Plot Curb", new Vector3(r.xMax, 0.06f, r.center.y), new Vector3(0.3f, 0.12f, r.height), _curb, ringOuter);
        }

        /// <summary>A strip that stays outside the plaza's ring road: strips that reach it are cut into 8 m pieces and the covered ones dropped.</summary>
        private static void Clipped(Transform parent, string name, Vector3 c, Vector3 s, Material m, float ringOuter)
        {
            float keepOut = ringOuter + 2f;
            float nearX = Mathf.Clamp(0f, c.x - s.x * 0.5f, c.x + s.x * 0.5f), nearZ = Mathf.Clamp(0f, c.z - s.z * 0.5f, c.z + s.z * 0.5f);
            if (new Vector2(nearX, nearZ).magnitude > keepOut) { Box(parent, name, c, s, m); return; }
            bool alongX = s.x >= s.z;
            float len = alongX ? s.x : s.z;
            int pieces = Mathf.Max(1, Mathf.CeilToInt(len / 8f));
            float piece = len / pieces;
            for (int i = 0; i < pieces; i++)
            {
                float o = -len * 0.5f + piece * (i + 0.5f);
                Vector3 pc = c + (alongX ? new Vector3(o, 0f, 0f) : new Vector3(0f, 0f, o));
                float nearest = new Vector2(Mathf.Max(0f, Mathf.Abs(pc.x) - (alongX ? piece * 0.5f : s.x * 0.5f)), Mathf.Max(0f, Mathf.Abs(pc.z) - (alongX ? s.z * 0.5f : piece * 0.5f))).magnitude;
                if (nearest < keepOut) continue;
                Box(parent, name, pc, alongX ? new Vector3(piece, s.y, s.z) : new Vector3(s.x, s.y, piece), m);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject FindRoot(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material m, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Disc(Transform parent, string name, Vector3 centre, float radius, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre + Vector3.up * height * 0.5f;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        /// <summary>A flat ring made of short box segments.</summary>
        private static void DiscRing(Transform parent, string name, float radius, float y, float width, Material m, int segments)
        {
            var ring = Child(parent, name);
            float seg = 2f * Mathf.PI * radius / segments * 1.04f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                var b = Box(ring, "Segment", Quaternion.Euler(0f, a, 0f) * Vector3.forward * radius + Vector3.up * y, new Vector3(seg, Mathf.Max(0.01f, y * 0.5f), width), m);
                b.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
        }

        private static GameObject Spawn(Transform parent, string path, Vector3 foot, float yaw, float height = 0f, float length = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Bounds b = GenesisWorldBuilder.RendererBounds(go);
            float scale = 1f;
            if (height > 0f && b.size.y > 0.001f) scale = height / b.size.y;
            else if (length > 0f) scale = length / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
            go.transform.localScale *= scale;
            b = GenesisWorldBuilder.RendererBounds(go);
            Vector3 target = parent.TransformPoint(foot);
            go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            return go;
        }

        private static void Materials()
        {
            _grass = Mat("OW Grass", new Color(0.34f, 0.5f, 0.28f), smooth: 0.1f);
            _asphalt = Mat("OW Asphalt", new Color(0.16f, 0.17f, 0.19f), smooth: 0.25f);
            _pavement = Mat("OW Pavement", new Color(0.68f, 0.68f, 0.7f), smooth: 0.2f);
            _paving = Mat("OW Plaza Paving", new Color(0.8f, 0.77f, 0.72f), smooth: 0.3f);
            _paint = Mat("OW Road Paint", new Color(0.92f, 0.92f, 0.9f), smooth: 0.2f);
            _gold = Mat("OW Road Paint Yellow", new Color(0.95f, 0.78f, 0.25f), smooth: 0.2f);
            _curb = Mat("OW Curb", new Color(0.55f, 0.56f, 0.6f), smooth: 0.2f);
            _cyan = Mat("OW Glow Cyan", new Color(0.25f, 0.9f, 1f), emission: new Color(0.25f, 0.9f, 1f) * 2f);
        }

        private static Material Mat(string name, Color colour, float smooth = 0.5f, Color? emission = null, Texture tex = null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "OpenWorld");
            string path = $"{MatFolder}/{name}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", smooth);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (tex != null) m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
#endif
