#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// The Genesis Japanese Garden (open city block west of the plaza, south of the west boulevard) plus
    /// cherry blossoms scattered through the parks and torii gates at the park corners. Everything is modelled
    /// here in code (no downloads): torii, cherry trees, stone lanterns, an arched bridge, the pond, a
    /// white-walled garden wall and falling-petal particles. Meshes are saved to Assets/Art/Generated/Garden.
    /// Safe to re-run; run it after World > 10 (Nature), which it clears space in.
    /// </summary>
    public static class GenesisGarden
    {
        private const string Folder = "Assets/Art/Generated/Garden";
        public const string GardenName = "Japanese Garden";
        public const string BlossomName = "Cherry Blossoms";
        public const string ParkToriiName = "Park Torii";

        // The block between the west avenue (x -240/-120) and the west boulevard (z 0) / avenue (z -120).
        public static readonly Rect Block = new Rect(-228f, -108f, 96f, 94f);
        private static readonly Vector2 PondCentre = new Vector2(-180f, -66f);
        private const float PathX = -180f;
        private static readonly Vector2[] ParkTorii = { new Vector2(50f, 50f), new Vector2(-50f, 50f), new Vector2(50f, -50f), new Vector2(-50f, -50f) };

        /// <summary>True where the garden and the park torii sit, so other world tools leave the space clear.</summary>
        public static bool IsReserved(Vector2 p) =>
            (p.x > Block.xMin - 1f && p.x < Block.xMax + 1f && p.y > Block.yMin - 1f && p.y < Block.yMax + 2.5f) ||
            ParkTorii.Any(t => Vector2.Distance(t, p) < 5.5f);

        private static Material _red, _black, _stone, _bark, _pinkA, _pinkB, _pinkC, _carpet, _water, _bed, _lily, _lotus, _glow, _wood, _plaster, _tile, _gravel, _moss, _koi, _koiWhite, _petal;

        [MenuItem("Duel Genesis/World/12. Plant Japanese Garden (torii, pond, cherry blossoms)")]
        public static void Build()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "Garden");

            foreach (string n in new[] { GardenName, BlossomName, ParkToriiName })
            {
                Transform old = city.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            int cleared = ClearNatureInReserved(city.transform);
            Materials();

            // Meshes.
            Mesh bigTorii = Save(ToriiMesh(6.4f, 7.2f), "Torii_Large");
            Mesh smallTorii = Save(ToriiMesh(3.0f, 3.9f), "Torii_Path");
            Mesh parkTorii = Save(ToriiMesh(4.6f, 5.4f), "Torii_Park");
            var trees = new List<Mesh>();
            for (int i = 0; i < 6; i++) trees.Add(Save(CherryMesh(1000 + i * 17), "Cherry_" + i));
            Mesh carpet = Save(CarpetMesh(3.6f, 7), "PetalCarpet");
            Mesh lantern = Save(LanternMesh(), "StoneLantern");
            Mesh bridgeCol;
            Mesh bridge = Save(BridgeMesh(16f, 2.4f, 1.5f, out bridgeCol), "TaikoBridge");
            bridgeCol = Save(bridgeCol, "TaikoBridge_Collider");
            var stones = new List<Mesh>();
            for (int i = 0; i < 4; i++) stones.Add(Save(StoneMesh(300 + i * 7), "Stone_" + i));
            Mesh lily = Save(LilyMesh(), "LilyPad");
            Mesh koi = Save(KoiMesh(), "Koi");

            var rng = new System.Random(20260929);
            var garden = new GameObject(GardenName).transform;
            garden.SetParent(city.transform, false);
            int count = 0;

            // ---- wall with a gate gap on the boulevard side
            count += Wall(garden);

            // ---- the entrance torii and a tunnel of small torii down to the pond
            count += Place(garden, "Entrance Torii", bigTorii, new[] { _red, _black }, new Vector3(PathX, 0f, -18.5f), 0f, 1f, torii: 6.4f);
            for (float z = -23f; z > -48f; z -= 2.3f)
                count += Place(garden, "Path Torii", smallTorii, new[] { _red, _black }, new Vector3(PathX, 0f, z), 0f, 1f, torii: 3.0f);
            // Stone path: from the gate to the bridge, and on from the bridge to the rock garden.
            var path = new GameObject("Stone Path").transform;
            path.SetParent(garden, false);
            for (float z = -15.6f; z > -58.5f; z -= 1.35f) Slab(path, new Vector3(PathX, 0.03f, z), new Vector3(2.4f, 0.06f, 1.15f), rng);
            for (float z = -75.5f; z > -86f; z -= 1.35f) Slab(path, new Vector3(PathX, 0.03f, z), new Vector3(2.4f, 0.06f, 1.15f), rng);
            foreach (float s in new[] { -1f, 1f })
            {
                count += Place(garden, "Stone Lantern", lantern, new[] { _stone, _glow }, new Vector3(PathX + s * 3.6f, 0f, -20.5f), 0f, 1.15f, solidRadius: 0.4f);
                count += Place(garden, "Stone Lantern", lantern, new[] { _stone, _glow }, new Vector3(PathX + s * 2.4f, 0f, -57.5f), 0f, 1f, solidRadius: 0.4f);
                count += Place(garden, "Stone Lantern", lantern, new[] { _stone, _glow }, new Vector3(PathX + s * 2.4f, 0f, -76f), 0f, 1f, solidRadius: 0.4f);
            }

            // ---- the pond, the bridge over its waist and a torii standing in the water
            count += Pond(garden, stones, lily, koi, rng);
            var br = Place(garden, "Taiko Bridge", bridge, new[] { _wood, _red, _black }, new Vector3(PathX, 0f, PondCentre.y), 90f, 1f);
            GameObject brGo = garden.Find("Taiko Bridge").gameObject;
            brGo.AddComponent<MeshCollider>().sharedMesh = bridgeCol;
            count += br;
            count += Place(garden, "Water Torii", parkTorii, new[] { _red, _black }, new Vector3(-165.5f, 0.05f, -68f), 90f, 1f, torii: 4.6f);

            // ---- rock garden past the bridge: raked gravel, three stones, moss
            var zen = new GameObject("Rock Garden").transform;
            zen.SetParent(garden, false);
            Box(zen, "Raked Gravel", new Vector3(PathX, 0.025f, -95f), new Vector3(30f, 0.05f, 15f), _gravel, false);
            for (int i = 0; i < 12; i++) Box(zen, "Rake Line", new Vector3(PathX, 0.052f, -89f - i * 1.1f), new Vector3(29f, 0.012f, 0.12f), _stone, false);
            foreach (var (p, h) in new[] { (new Vector3(-189f, 0f, -94f), 1.8f), (new Vector3(-173f, 0f, -97f), 1.2f), (new Vector3(-182.5f, 0f, -99.5f), 0.8f) })
            {
                count += Place(zen, "Garden Rock", stones[rng.Next(stones.Count)], new[] { _stone, _moss }, p, rng.Next(360), h, solidRadius: h * 0.6f);
                Box(zen, "Moss", p + new Vector3(0f, 0.04f, 0f), new Vector3(h * 2.4f, 0.04f, h * 2.1f), _moss, false);
            }

            // ---- cherry trees round the garden, each with a carpet of fallen petals
            var used = new List<Vector2>();
            bool GardenFree(Vector2 p) =>
                Block.Contains(p) && p.x > Block.xMin + 3.5f && p.x < Block.xMax - 3.5f && p.y > Block.yMin + 3.5f && p.y < Block.yMax - 4f &&
                Mathf.Abs(p.x - PathX) > 6f &&                                     // the torii path and bridge line
                PondDistance(p) > 3.5f &&
                !(Mathf.Abs(p.x - PathX) < 19f && p.y < -85f) &&                    // rock garden
                used.All(u => Vector2.Distance(u, p) > 7f);
            int planted = 0;
            for (int t = 0; t < 600 && planted < 24; t++)
            {
                var p = new Vector2(Mathf.Lerp(Block.xMin, Block.xMax, (float)rng.NextDouble()), Mathf.Lerp(Block.yMin, Block.yMax, (float)rng.NextDouble()));
                if (!GardenFree(p)) continue;
                used.Add(p);
                count += Tree(garden, trees[rng.Next(trees.Count)], carpet, p, rng);
                planted++;
            }
            // A pair framing the gate, just inside the wall.
            foreach (float s in new[] { -1f, 1f }) count += Tree(garden, trees[rng.Next(trees.Count)], carpet, new Vector2(PathX + s * 8.5f, -21f), rng);
            count += Petals(garden, new Vector3(-180f, 9f, -60f), new Vector3(92f, 1f, 90f), 70f);

            // ---- torii at the corner of each park, facing the plaza
            var parkRoot = new GameObject(ParkToriiName).transform;
            parkRoot.SetParent(city.transform, false);
            foreach (Vector2 p in ParkTorii)
            {
                if (GenesisDuelCenter.InCardShopLot(p)) continue;
                float yaw = Quaternion.LookRotation(new Vector3(-p.x, 0f, -p.y)).eulerAngles.y;
                count += Place(parkRoot, "Park Torii", parkTorii, new[] { _red, _black }, new Vector3(p.x, 0f, p.y), yaw, 1f, torii: 4.6f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * s * 3.8f;
                    count += Place(parkRoot, "Stone Lantern", lantern, new[] { _stone, _glow }, new Vector3(p.x, 0f, p.y) + side, yaw, 1f, solidRadius: 0.4f);
                }
            }

            // ---- cherry blossoms scattered through the four parks and round the forest edge
            var blossoms = new GameObject(BlossomName).transform;
            blossoms.SetParent(city.transform, false);
            var taken = NatureSpots(city.transform);
            int scattered = 0;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                int n = 0;
                for (int t = 0; t < 300 && n < 7; t++)
                {
                    var p = new Vector2(sx * Mathf.Lerp(24f, 98f, (float)rng.NextDouble()), sz * Mathf.Lerp(24f, 98f, (float)rng.NextDouble()));
                    if (p.magnitude < 70f || GenesisDuelCenter.InCardShopLot(p) || IsReserved(p)) continue;
                    if (Mathf.Abs(p.x) < 30f && p.y > 56f && p.y < 130f) continue;   // Duel Center
                    if (taken.Any(u => Vector2.Distance(u, p) < 5.5f)) continue;
                    taken.Add(p);
                    scattered += Tree(blossoms, trees[rng.Next(trees.Count)], carpet, p, rng);
                    n++;
                }
                count += Petals(blossoms, new Vector3(sx * 61f, 8f, sz * 61f), new Vector3(70f, 1f, 70f), 18f);
            }
            for (int t = 0; t < 2000 && scattered < 60; t++)
            {
                // The inner side of the forest band, where it meets the city.
                float d = 372f + (float)rng.NextDouble() * 12f, along = Mathf.Lerp(-360f, 360f, (float)rng.NextDouble());
                int side = rng.Next(4);
                var p = side == 0 ? new Vector2(along, d) : side == 1 ? new Vector2(along, -d) : side == 2 ? new Vector2(d, along) : new Vector2(-d, along);
                if (OnRoad(p) || IsReserved(p) || taken.Any(u => Vector2.Distance(u, p) < 6f)) continue;
                taken.Add(p);
                scattered += Tree(blossoms, trees[rng.Next(trees.Count)], carpet, p, rng);
            }
            count += scattered;

            foreach (Transform tr in garden.GetComponentsInChildren<Transform>().Concat(blossoms.GetComponentsInChildren<Transform>()).Concat(parkRoot.GetComponentsInChildren<Transform>()))
                if (tr.GetComponent<ParticleSystem>() == null && tr.GetComponentInParent<LODGroup>() == null) GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis planted the Japanese Garden: {count} pieces ({planted + 2} garden cherry trees, {scattered} more across the parks and forest edge, torii at {ParkTorii.Count(p => !GenesisDuelCenter.InCardShopLot(p))} parks); cleared {cleared} Nature Kit pieces from the garden plot.");
        }

        [MenuItem("Duel Genesis/DEV/Capture Japanese Garden Screenshots")]
        public static void Capture()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Shots");
            Directory.CreateDirectory(dir);
            GenesisShots.Shot(dir, "garden_1_aerial", new Vector3(-138f, 38f, -8f), new Vector3(-182f, 0f, -62f), 60f);
            GenesisShots.Shot(dir, "garden_2_gate", new Vector3(-180f, 1.8f, -6f), new Vector3(-180f, 3f, -40f), 70f);
            GenesisShots.Shot(dir, "garden_3_pond", new Vector3(-196f, 3.2f, -52f), new Vector3(-172f, 1f, -70f), 72f);
            GenesisShots.Shot(dir, "garden_4_park", new Vector3(30f, 2.2f, -30f), new Vector3(50f, 3f, -50f), 72f);
            Debug.Log("Duel: Genesis captured Japanese garden screenshots into " + dir);
        }

        // =====================================================================================  scene pieces

        private static int Place(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos, float yaw, float scale, float torii = 0f, float solidRadius = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (torii > 0f)
                foreach (float s in new[] { -1f, 1f })
                {
                    var c = go.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(s * torii * 0.5f, 2f, 0f);
                    c.radius = torii * 0.06f;
                    c.height = 4f;
                }
            if (solidRadius > 0f)
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.radius = solidRadius / scale;
                c.height = 2f;
                c.center = new Vector3(0f, 1f, 0f);
            }
            return 1;
        }

        /// <summary>Realistic cherry trees (set up from the Sketchfab downloads) if present.</summary>
        public static GameObject[] CherryPrefabs() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ThirdParty/Sketchfab" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab") && (p.ToLowerInvariant().Contains("sakura") || p.ToLowerInvariant().Contains("cherry")))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(g => g != null).ToArray();

        private static int Tree(Transform parent, Mesh tree, Mesh carpet, Vector2 p, System.Random rng)
        {
            // Never the low-poly placeholder: a realistic sakura if we have one, otherwise a realistic scanned tree.
            GameObject[] sakura = CherryPrefabs();
            GameObject real = sakura.Length > 0 ? sakura[rng.Next(sakura.Length)] : GenesisRealTrees.Load("tree_small_02");
            if (real != null)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(real, parent);
                t.name = sakura.Length > 0 ? "Cherry Blossom Tree" : "Garden Tree";
                t.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                Bounds tb = GenesisWorldBuilder.RendererBounds(t);
                float h = 6.5f + (float)rng.NextDouble() * 2.5f;
                if (tb.size.y > 0.01f) t.transform.localScale *= h / tb.size.y;
                tb = GenesisWorldBuilder.RendererBounds(t);
                t.transform.position += new Vector3(p.x - tb.center.x, -tb.min.y, p.y - tb.center.z);
                if (t.GetComponentInChildren<Collider>() == null)
                {
                    var cc = t.AddComponent<CapsuleCollider>();
                    cc.radius = 0.35f / t.transform.lossyScale.x;
                    cc.height = 3f / t.transform.lossyScale.y;
                    cc.center = t.transform.InverseTransformPoint(new Vector3(p.x, 1.5f, p.y));
                }
                return 1;
            }
            float s = 0.85f + (float)rng.NextDouble() * 0.45f;
            var go = new GameObject("Cherry Blossom Tree");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.x, 0f, p.y);
            go.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            go.transform.localScale = Vector3.one * s;
            go.AddComponent<MeshFilter>().sharedMesh = tree;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { _bark, _pinkA, _pinkB, _pinkC };
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = 0.35f;
            c.height = 3f;
            c.center = new Vector3(0f, 1.5f, 0f);
            var petals = new GameObject("Fallen Petals");
            petals.transform.SetParent(go.transform, false);
            petals.transform.localPosition = new Vector3(0f, 0.02f / s, 0f);
            petals.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            petals.AddComponent<MeshFilter>().sharedMesh = carpet;
            var r = petals.AddComponent<MeshRenderer>();
            r.sharedMaterial = _carpet;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lod = go.AddComponent<LODGroup>();   // drop far-away trees
            lod.SetLODs(new[] { new LOD(0.012f, new Renderer[] { go.GetComponent<MeshRenderer>(), r }) });
            lod.RecalculateBounds();
            return 1;
        }

        private static void Slab(Transform parent, Vector3 at, Vector3 size, System.Random rng)
        {
            var go = Box(parent, "Path Stone", at, new Vector3(size.x * (0.92f + (float)rng.NextDouble() * 0.08f), size.y, size.z), _stone, false);
            go.transform.localRotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 5f, 0f);
        }

        private static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        /// <summary>A white plaster wall with a stone base and a black tile coping, gap on the boulevard for the gate.</summary>
        private static int Wall(Transform garden)
        {
            var root = new GameObject("Garden Wall").transform;
            root.SetParent(garden, false);
            float x0 = Block.xMin + 0.8f, x1 = Block.xMax - 0.8f, z0 = Block.yMin + 0.8f, z1 = Block.yMax - 0.8f;
            var runs = new List<(Vector2 a, Vector2 b)>
            {
                (new Vector2(x0, z1), new Vector2(PathX - 4.2f, z1)), (new Vector2(PathX + 4.2f, z1), new Vector2(x1, z1)),
                (new Vector2(x1, z1), new Vector2(x1, z0)), (new Vector2(x1, z0), new Vector2(x0, z0)), (new Vector2(x0, z0), new Vector2(x0, z1)),
            };
            var mb = new MB(3);
            foreach (var (a, b) in runs)
            {
                Vector3 A = new Vector3(a.x, 0f, a.y), B = new Vector3(b.x, 0f, b.y);
                Vector3 dir = (B - A).normalized;
                Vector3 mid = (A + B) * 0.5f;
                float len = Vector3.Distance(A, B) + 0.7f;
                Quaternion q = Quaternion.LookRotation(dir);
                mb.Box(mid + Vector3.up * 0.25f, new Vector3(0.75f, 0.5f, len), q, 0);
                mb.Box(mid + Vector3.up * 1.3f, new Vector3(0.55f, 1.6f, len), q, 1);
                // Tile coping: a pitched roof.
                Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.62f;
                Vector3 e = dir * (len * 0.5f + 0.15f);
                float y0 = 2.08f, y1 = 2.5f;
                Vector3 r0 = mid - e + Vector3.up * y1, r1 = mid + e + Vector3.up * y1;
                Vector3 l0 = mid - e - side + Vector3.up * y0, l1 = mid + e - side + Vector3.up * y0;
                Vector3 g0 = mid - e + side + Vector3.up * y0, g1 = mid + e + side + Vector3.up * y0;
                mb.Quad(r0, r1, g1, g0, 2, side + Vector3.up);
                mb.Quad(r0, r1, l1, l0, 2, -side + Vector3.up);
                mb.Quad(l0, l1, g1, g0, 2, Vector3.down);
                mb.Tri(r0, l0, g0, 2, -dir);
                mb.Tri(r1, l1, g1, 2, dir);
                var col = new GameObject("Wall Collider");
                col.transform.SetParent(root, false);
                col.transform.localPosition = mid + Vector3.up * 1.2f;
                col.transform.localRotation = q;
                col.AddComponent<BoxCollider>().size = new Vector3(0.7f, 2.4f, len);
            }
            var wall = new GameObject("Wall");
            wall.transform.SetParent(root, false);
            wall.AddComponent<MeshFilter>().sharedMesh = Save(mb.ToMesh("GardenWall"), "GardenWall");
            wall.AddComponent<MeshRenderer>().sharedMaterials = new[] { _stone, _plaster, _tile };
            return 1;
        }

        private static float PondRadius(float angle)
        {
            // An ellipse pinched at its waist (where the bridge crosses) with a wobbly shore.
            float rx = 18f, rz = 11f;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float r = 1f / Mathf.Sqrt(c * c / (rx * rx) + s * s / (rz * rz));
            r *= 1f - 0.34f * Mathf.Pow(Mathf.Abs(c) < 1f ? 1f - Mathf.Abs(c) : 0f, 3f);
            r *= 1f + 0.06f * Mathf.Sin(angle * 5f + 1.3f) + 0.04f * Mathf.Sin(angle * 9f);
            return r;
        }

        private static float PondDistance(Vector2 p)
        {
            Vector2 d = p - PondCentre;
            return d.magnitude - PondRadius(Mathf.Atan2(d.y, d.x));
        }

        private static int Pond(Transform garden, List<Mesh> stones, Mesh lily, Mesh koi, System.Random rng)
        {
            var root = new GameObject("Koi Pond").transform;
            root.SetParent(garden, false);
            root.localPosition = new Vector3(PondCentre.x, 0f, PondCentre.y);
            const int seg = 96;
            var bed = new MB(1);
            var water = new MB(1);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 P(float a, float k, float y) => new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PondRadius(a) * k + Vector3.up * y;
                bed.Tri(new Vector3(0f, 0.012f, 0f), P(a0, 1.02f, 0.012f), P(a1, 1.02f, 0.012f), 0, Vector3.up);
                water.Tri(new Vector3(0f, 0.16f, 0f), P(a0, 0.99f, 0.16f), P(a1, 0.99f, 0.16f), 0, Vector3.up);
            }
            var bedGo = new GameObject("Pond Bed");
            bedGo.transform.SetParent(root, false);
            bedGo.AddComponent<MeshFilter>().sharedMesh = Save(bed.ToMesh("PondBed"), "PondBed");
            bedGo.AddComponent<MeshRenderer>().sharedMaterial = _bed;
            var wGo = new GameObject("Pond Water");
            wGo.transform.SetParent(root, false);
            wGo.AddComponent<MeshFilter>().sharedMesh = Save(water.ToMesh("PondWater"), "PondWater");
            var wr = wGo.AddComponent<MeshRenderer>();
            wr.sharedMaterial = _water;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            int n = 2;

            // Shore stones and a low invisible wall, both broken where the bridge lands (angles ±90°).
            bool AtBridge(float a) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90f)) < 13f || Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, -90f)) < 13f;
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.085f)
            {
                if (AtBridge(a)) continue;
                float r = PondRadius(a) + 0.35f;
                Vector3 at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                n += Place(root, "Shore Stone", stones[rng.Next(stones.Count)], new[] { _stone, _moss }, at, rng.Next(360), 0.45f + (float)rng.NextDouble() * 0.5f);
            }
            for (int i = 0; i < seg; i += 2)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 2) * Mathf.PI * 2f / seg;
                if (AtBridge((a0 + a1) * 0.5f)) continue;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (PondRadius(a0) + 0.3f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (PondRadius(a1) + 0.3f);
                var col = new GameObject("Pond Edge");
                col.transform.SetParent(root, false);
                col.transform.localPosition = (p0 + p1) * 0.5f + Vector3.up * 0.6f;
                col.transform.localRotation = Quaternion.LookRotation(p1 - p0);
                col.AddComponent<BoxCollider>().size = new Vector3(0.4f, 1.2f, Vector3.Distance(p0, p1) + 0.3f);
            }
            // A few big stones standing in the water.
            for (int i = 0; i < 5; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                if (AtBridge(a)) continue;
                Vector3 at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PondRadius(a) * Mathf.Lerp(0.55f, 0.8f, (float)rng.NextDouble());
                n += Place(root, "Water Stone", stones[rng.Next(stones.Count)], new[] { _stone, _moss }, at, rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.6f);
            }
            // Lily pads (some with a lotus flower) and koi, clear of the bridge.
            for (int i = 0, placed = 0; i < 200 && placed < 22; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, k = Mathf.Lerp(0.2f, 0.88f, (float)rng.NextDouble());
                Vector3 at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PondRadius(a) * k;
                if (Mathf.Abs(at.x) < 3f) continue;
                at.y = 0.17f;
                n += Place(root, "Lily Pad", lily, new[] { _lily }, at, rng.Next(360), 0.5f + (float)rng.NextDouble() * 0.5f);
                if (rng.NextDouble() < 0.3)
                    n += Place(root, "Lotus", koi, new[] { _lotus }, at + Vector3.up * 0.1f, rng.Next(360), 0.35f);
                placed++;
            }
            for (int i = 0; i < 8; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PondRadius(a) * 0.55f;
                at.y = 0.09f;
                n += Place(root, "Koi", koi, new[] { rng.NextDouble() < 0.6 ? _koi : _koiWhite }, at, rng.Next(360), 0.9f);
            }
            return n;
        }

        private static int Petals(Transform parent, Vector3 at, Vector3 box, float rate)
        {
            var go = new GameObject("Falling Petals");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.86f), new Color(1f, 0.9f, 0.94f));
            main.gravityModifier = 0.012f;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.2f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.35f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
            rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
            rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.Local;
            r.sharedMaterial = _petal;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return 1;
        }

        private static int ClearNatureInReserved(Transform city)
        {
            Transform nature = city.Find("Nature");
            if (nature == null) return 0;
            int n = 0;
            foreach (Transform group in nature.Cast<Transform>().ToList())
                foreach (Transform t in group.Cast<Transform>().ToList())
                {
                    Vector3 p = t.position;
                    if (IsReserved(new Vector2(p.x, p.z))) { Object.DestroyImmediate(t.gameObject); n++; }
                }
            return n;
        }

        private static List<Vector2> NatureSpots(Transform city)
        {
            var list = new List<Vector2>();
            Transform nature = city.Find("Nature");
            if (nature == null) return list;
            foreach (Transform group in nature)
                foreach (Transform t in group)
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("flower") || n.Contains("grass")) continue;   // small stuff can sit under a tree
                    list.Add(new Vector2(t.position.x, t.position.z));
                }
            return list;
        }

        private static bool OnRoad(Vector2 p)
        {
            float[] lines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };
            return lines.Any(l => Mathf.Abs(p.x - l) < (l == 0f ? 17f : 14f) || Mathf.Abs(p.y - l) < (l == 0f ? 17f : 14f));
        }

        // =====================================================================================  materials

        private static void Materials()
        {
            _red = Mat("Vermilion", new Color(0.78f, 0.14f, 0.07f), 0.35f);
            _black = Mat("Lacquer Black", new Color(0.06f, 0.055f, 0.05f), 0.5f);
            _stone = Mat("Garden Stone", new Color(0.52f, 0.51f, 0.48f), 0.12f);
            _bark = Mat("Cherry Bark", new Color(0.25f, 0.17f, 0.15f), 0.1f);
            _pinkA = Mat("Blossom Pink", new Color(1f, 0.7f, 0.8f), 0.15f, twoSided: true);
            _pinkB = Mat("Blossom Pale", new Color(1f, 0.84f, 0.9f), 0.15f, twoSided: true);
            _pinkC = Mat("Blossom Deep", new Color(0.93f, 0.52f, 0.67f), 0.15f, twoSided: true);
            _carpet = Mat("Fallen Petals", new Color(0.98f, 0.76f, 0.84f), 0.05f);
            _bed = Mat("Pond Bed", new Color(0.09f, 0.13f, 0.12f), 0.2f);
            _water = Mat("Pond Water", new Color(0.18f, 0.42f, 0.44f, 0.62f), 0.96f, transparent: true);
            _lily = Mat("Lily Pad", new Color(0.2f, 0.45f, 0.18f), 0.35f, twoSided: true);
            _lotus = Mat("Lotus", new Color(1f, 0.75f, 0.85f), 0.3f, emission: new Color(0.25f, 0.12f, 0.16f));
            _glow = Mat("Lantern Glow", new Color(1f, 0.85f, 0.55f), 0.2f, emission: new Color(1.6f, 1.05f, 0.45f));
            _wood = Mat("Bridge Wood", new Color(0.42f, 0.27f, 0.17f), 0.25f);
            _plaster = Mat("White Plaster", new Color(0.92f, 0.9f, 0.85f), 0.1f);
            _tile = Mat("Roof Tile", new Color(0.16f, 0.17f, 0.19f), 0.45f);
            _gravel = Mat("Raked Gravel", new Color(0.8f, 0.78f, 0.72f), 0.05f);
            _moss = Mat("Moss", new Color(0.23f, 0.38f, 0.16f), 0.05f);
            _koi = Mat("Koi Orange", new Color(1f, 0.42f, 0.08f), 0.6f);
            _koiWhite = Mat("Koi White", new Color(0.95f, 0.93f, 0.9f), 0.6f);
            _petal = PetalParticleMaterial();
        }

        private static Material Mat(string name, Color c, float smooth, bool transparent = false, bool twoSided = false, Color? emission = null)
        {
            string path = $"{Folder}/{name}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            m.doubleSidedGI = twoSided;
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A petal-shaped alpha texture on an unlit URP particle material.</summary>
        private static Material PetalParticleMaterial()
        {
            string texPath = Folder + "/petal.png";
            if (!File.Exists(texPath))
            {
                var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float u = (x + 0.5f) / 16f - 1f, v = (y + 0.5f) / 16f - 1f;
                    float w = 0.62f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * (v > 0.75f ? 1f - (v - 0.75f) * 2.2f * (1f - Mathf.Abs(u) * 4f) : 1f);   // oval with a notch at the tip
                    float a = Mathf.Abs(u) < w ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                File.WriteAllBytes(texPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(texPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            string path = Folder + "/Petal Particle.mat";
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Mesh Save(Mesh mesh, string name)
        {
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // =====================================================================================  meshes

        /// <summary>A torii: pillars (w apart), tie beam, centre strut, and a two-layer top beam with upswept ends.</summary>
        private static Mesh ToriiMesh(float w, float h)
        {
            var mb = new MB(2);
            float r = w * 0.052f + 0.08f;
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Tube(new Vector3(s * w * 0.5f, 0f, 0f), new Vector3(s * w * 0.47f, h * 0.9f, 0f), r, r * 0.86f, 12, 0);
                mb.Tube(new Vector3(s * w * 0.5f, 0f, 0f), new Vector3(s * w * 0.5f, h * 0.07f, 0f), r * 1.18f, r * 1.14f, 12, 1);   // black foot
            }
            float nukiY = h * 0.7f;
            mb.Box(new Vector3(0f, nukiY, 0f), new Vector3(w + r * 4.5f, h * 0.055f, r * 1.1f), Quaternion.identity, 0);
            float shimaY = h * 0.86f;
            mb.Box(new Vector3(0f, (nukiY + shimaY) * 0.5f, 0f), new Vector3(r * 1.3f, shimaY - nukiY, r * 0.9f), Quaternion.identity, 0);
            // Shimaki (red) and kasagi (black), both curving up at the ends.
            float half = w * 0.5f + r * 5f;
            var pts = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float x = Mathf.Lerp(-half, half, i / 16f);
                float lift = Mathf.Pow(Mathf.Abs(x) / half, 3f) * h * 0.05f;
                pts.Add(new Vector3(x, lift, 0f));
            }
            mb.Sweep(pts.Select(p => p + Vector3.up * shimaY).ToList(), r * 1.5f, h * 0.06f, 0);
            var top = pts.Select(p => new Vector3(p.x * 1.12f, p.y * 1.5f, 0f) + Vector3.up * (shimaY + h * 0.06f)).ToList();
            mb.Sweep(top, r * 1.9f, h * 0.075f, 1);
            return mb.ToMesh("Torii");
        }

        /// <summary>A cherry tree: a leaning trunk that forks three times, with blossom clouds on the branch tips.</summary>
        private static Mesh CherryMesh(int seed)
        {
            var rng = new System.Random(seed);
            var mb = new MB(4);
            float F() => (float)rng.NextDouble();
            void Blossom(Vector3 at, float size)
            {
                int tone = 1 + rng.Next(3);
                mb.Blob(at, size, 0.72f, rng, tone);
                // A couple of smaller puffs round it so the cloud isn't a ball.
                for (int k = 0; k < 1; k++)
                    mb.Blob(at + new Vector3(F() - 0.5f, (F() - 0.3f) * 0.6f, F() - 0.5f) * size * 1.4f, size * (0.5f + F() * 0.3f), 0.75f, rng, 1 + rng.Next(3));
            }
            void Branch(Vector3 start, Vector3 dir, float len, float rad, int depth)
            {
                Vector3 bend = Vector3.Cross(dir, Vector3.up);
                if (bend.sqrMagnitude < 0.01f) bend = Vector3.right;
                Vector3 mid = start + dir * len * 0.5f + bend.normalized * (F() - 0.5f) * len * 0.18f;
                Vector3 end = start + dir * len + Vector3.up * (F() - 0.3f) * len * 0.15f;
                mb.Tube(start, mid, rad, rad * 0.86f, depth >= 2 ? 8 : 6, 0);
                mb.Tube(mid, end, rad * 0.86f, rad * 0.7f, depth >= 2 ? 8 : 6, 0);
                if (depth == 0) { Blossom(end, 0.95f + F() * 0.45f); return; }
                if (depth == 1 && F() < 0.6f) Blossom(mid, 0.8f + F() * 0.3f);
                int kids = depth == 3 ? 3 + rng.Next(2) : 2 + rng.Next(2);
                float az0 = F() * 360f;
                for (int k = 0; k < kids; k++)
                {
                    float az = az0 + k * 360f / kids + (F() - 0.5f) * 40f;
                    float tilt = depth == 3 ? 42f + F() * 18f : 22f + F() * 25f;
                    Vector3 outward = Quaternion.Euler(0f, az, 0f) * Vector3.forward;
                    Vector3 nd = Vector3.Slerp(dir, outward, tilt / 90f);
                    if (nd.y < 0.12f) nd.y = 0.12f;
                    Branch(end, nd.normalized, len * (0.68f + F() * 0.12f), rad * 0.66f, depth - 1);
                }
            }
            Vector3 lean = new Vector3(F() - 0.5f, 0f, F() - 0.5f) * 0.35f;
            Branch(Vector3.zero, (Vector3.up + lean).normalized, 2.3f + F() * 0.6f, 0.3f, 3);
            return mb.ToMesh("CherryTree");
        }

        private static Mesh CarpetMesh(float radius, int seed)
        {
            var rng = new System.Random(seed);
            var mb = new MB(1);
            const int seg = 28;
            var rs = Enumerable.Range(0, seg).Select(_ => radius * (0.75f + (float)rng.NextDouble() * 0.35f)).ToArray();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                mb.Tri(Vector3.zero, new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * rs[i], new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * rs[(i + 1) % seg], 0, Vector3.up);
            }
            return mb.ToMesh("PetalCarpet");
        }

        /// <summary>A kasuga-style stone lantern, about 1.7 m tall, with a glowing fire box.</summary>
        private static Mesh LanternMesh()
        {
            var mb = new MB(2);
            mb.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.2f, 0f), 0.4f, 0.36f, 6, 0);
            mb.Tube(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.92f, 0f), 0.14f, 0.12f, 8, 0);
            mb.Tube(new Vector3(0f, 0.92f, 0f), new Vector3(0f, 1.04f, 0f), 0.26f, 0.33f, 6, 0);
            mb.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.34f, 0.3f, 0.34f), Quaternion.identity, 1);
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * 0.25f;
                mb.Box(c + Vector3.up * 1.2f, new Vector3(0.08f, 0.32f, 0.08f), Quaternion.Euler(0f, 45f + i * 90f, 0f), 0);
            }
            mb.Tube(new Vector3(0f, 1.36f, 0f), new Vector3(0f, 1.62f, 0f), 0.5f, 0.05f, 6, 0);   // roof
            mb.Tube(new Vector3(0f, 1.33f, 0f), new Vector3(0f, 1.37f, 0f), 0.47f, 0.5f, 6, 0);
            mb.Blob(new Vector3(0f, 1.69f, 0f), 0.09f, 1.2f, new System.Random(4), 0);
            return mb.ToMesh("StoneLantern");
        }

        /// <summary>A red arched (taiko) bridge along local X; also a collision mesh of the deck plus side walls.</summary>
        private static Mesh BridgeMesh(float len, float width, float rise, out Mesh collider)
        {
            var mb = new MB(3);
            var col = new MB(1);
            const int n = 24;
            var deck = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                deck.Add(new Vector3(Mathf.Lerp(-len * 0.5f, len * 0.5f, t), 0.12f + rise * Mathf.Sin(Mathf.PI * t), 0f));
            }
            mb.Sweep(deck.Select(p => p - Vector3.up * 0.1f).ToList(), width, 0.2f, 0);
            col.Sweep(deck.Select(p => p - Vector3.up * 0.25f).ToList(), width, 0.35f, 0);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 off = new Vector3(0f, 0f, s * (width * 0.5f - 0.06f));
                for (int i = 1; i < n; i += 2)
                    mb.Box(deck[i] + off + Vector3.up * 0.45f, new Vector3(0.11f, 0.9f, 0.11f), Quaternion.identity, 1);
                mb.Sweep(deck.Select(p => p + off + Vector3.up * 0.9f).ToList(), 0.14f, 0.1f, 1);
                mb.Sweep(deck.Select(p => p + off + Vector3.up * 0.45f).ToList(), 0.08f, 0.06f, 1);
                foreach (int end in new[] { 0, n })
                {
                    mb.Box(deck[end] + off + Vector3.up * 0.55f, new Vector3(0.18f, 1.1f, 0.18f), Quaternion.identity, 1);
                    mb.Blob(deck[end] + off + Vector3.up * 1.2f, 0.13f, 1.3f, new System.Random(end + 3), 2);   // giboshi caps
                }
                col.Sweep(deck.Select(p => p + off + Vector3.up * 0.6f).ToList(), 0.15f, 1.2f, 0);
            }
            collider = col.ToMesh("TaikoBridgeCollider");
            return mb.ToMesh("TaikoBridge");
        }

        private static Mesh StoneMesh(int seed)
        {
            var mb = new MB(2);
            var rng = new System.Random(seed);
            mb.Blob(new Vector3(0f, 0.35f, 0f), 0.6f, 0.75f, rng, 0);
            mb.Blob(new Vector3(0.1f, 0.62f, 0.05f), 0.32f, 0.35f, rng, 1);   // moss cap
            return mb.ToMesh("Stone");
        }

        private static Mesh LilyMesh()
        {
            var mb = new MB(1);
            const int seg = 20;
            for (int i = 1; i < seg - 1; i++)   // a disc with a notch cut out
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                mb.Tri(Vector3.zero, new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f, new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f, 0, Vector3.up);
            }
            return mb.ToMesh("LilyPad");
        }

        private static Mesh KoiMesh()
        {
            var mb = new MB(1);
            var rng = new System.Random(9);
            mb.Blob(Vector3.zero, 0.22f, 0.45f, rng, 0);
            mb.Blob(new Vector3(0f, 0f, -0.26f), 0.12f, 0.3f, rng, 0);
            mb.Tri(new Vector3(0f, 0f, -0.34f), new Vector3(-0.14f, 0f, -0.52f), new Vector3(0.14f, 0f, -0.52f), 0, Vector3.up);
            return mb.ToMesh("Koi");
        }

        /// <summary>Flat-shaded mesh builder: every triangle gets its own vertices and is wound to face a hint direction.</summary>
        private sealed class MB
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<int>[] _sub;

            public MB(int submeshes) { _sub = new List<int>[submeshes]; for (int i = 0; i < submeshes; i++) _sub[i] = new List<int>(); }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, int s, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, outward) < 0f) { (b, c) = (c, b); n = -n; }
                n.Normalize();
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _n.Add(n); _n.Add(n); _n.Add(n);
                _sub[s].Add(i); _sub[s].Add(i + 1); _sub[s].Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int s, Vector3 outward) { Tri(a, b, c, s, outward); Tri(a, c, d, s, outward); }

            public void Box(Vector3 c, Vector3 size, Quaternion q, int s)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) => c + q * new Vector3(x * h.x, y * h.y, z * h.z);
                Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), s, X);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), s, -X);
                Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), s, Y);
                Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), s, -Y);
                Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), s, Z);
                Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), s, -Z);
            }

            public void Tube(Vector3 p0, Vector3 p1, float r0, float r1, int sides, int s)
            {
                Vector3 axis = (p1 - p0).normalized;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 w = Vector3.Cross(axis, u);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                    Quad(p0 + d0 * r0, p0 + d1 * r0, p1 + d1 * r1, p1 + d0 * r1, s, d0 + d1);
                    Tri(p0, p0 + d0 * r0, p0 + d1 * r0, s, -axis);
                    Tri(p1, p1 + d0 * r1, p1 + d1 * r1, s, axis);
                }
            }

            /// <summary>A box-section beam along a path (horizontal-ish), width across and height up.</summary>
            public void Sweep(List<Vector3> pts, float width, float height, int s)
            {
                Vector3[] Ring(int i)
                {
                    Vector3 t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, t).normalized * width * 0.5f;
                    Vector3 up = Vector3.Cross(t, side).normalized * height;
                    Vector3 p = pts[i];
                    return new[] { p - side, p + side, p + side + up, p - side + up };
                }
                Vector3 T0 = (pts[1] - pts[0]).normalized, T1 = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
                Vector3[] prev = Ring(0);
                Vector3 c0 = (prev[0] + prev[2]) * 0.5f;
                Quad(prev[0], prev[1], prev[2], prev[3], s, -T0);
                for (int i = 1; i < pts.Count; i++)
                {
                    Vector3[] cur = Ring(i);
                    Vector3 mid = (prev[0] + prev[2] + cur[0] + cur[2]) * 0.25f;
                    for (int k = 0; k < 4; k++)
                    {
                        int k1 = (k + 1) % 4;
                        Vector3 faceMid = (prev[k] + prev[k1] + cur[k] + cur[k1]) * 0.25f;
                        Quad(prev[k], prev[k1], cur[k1], cur[k], s, faceMid - mid);
                    }
                    prev = cur;
                }
                Quad(prev[0], prev[1], prev[2], prev[3], s, T1);
            }

            /// <summary>A lumpy icosphere (subdivided once), flattened by <paramref name="squash"/> in Y.</summary>
            public void Blob(Vector3 c, float radius, float squash, System.Random rng, int s)
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var verts = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                int[] f =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    verts.Add((verts[a] + verts[b]) * 0.5f);
                    cache[key] = verts.Count - 1;
                    return verts.Count - 1;
                }
                var tris = new List<int>();
                for (int i = 0; i < f.Length; i += 3)
                {
                    int a = f[i], b = f[i + 1], d = f[i + 2];
                    int ab = Mid(a, b), bd = Mid(b, d), da = Mid(d, a);
                    tris.AddRange(new[] { a, ab, da, b, bd, ab, d, da, bd, ab, bd, da });
                }
                var pts = verts.Select(v =>
                {
                    Vector3 n = v.normalized * radius * (0.82f + (float)rng.NextDouble() * 0.3f);
                    return c + new Vector3(n.x, n.y * squash, n.z);
                }).ToList();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = pts[tris[i]], b = pts[tris[i + 1]], d = pts[tris[i + 2]];
                    Tri(a, b, d, s, (a + b + d) / 3f - c);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(_v);
                m.SetNormals(_n);
                m.subMeshCount = _sub.Length;
                for (int i = 0; i < _sub.Length; i++) m.SetTriangles(_sub[i], i);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
#endif
