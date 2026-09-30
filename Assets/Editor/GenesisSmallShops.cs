#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Four smaller card shops spread round the far corners of the city, each with its own look, a working
    /// pack counter and a duel table or two:
    ///   - Pagoda Packs (south-west): a round glass shop under three tiered roofs with a spinning card on the spire,
    ///   - Booster Box (south-east): a pill-shaped glass box with a giant card on a pole out front,
    ///   - Crescent Cards (north-west): an open-air crescent arcade of counters round a courtyard card statue,
    ///   - Card Tower (east): a slim three-storey glass tower crowned by a spinning card.
    /// Buildings and plants on each site are cleared first. Safe to re-run.
    /// </summary>
    public static partial class GenesisDuelCenter
    {
        public const string SmallShopsName = "Small Card Shops";

        private static readonly (string name, Vector2 site, float yaw, float clear)[] SmallShops =
        {
            ("Pagoda Packs", new Vector2(-300f, -145f), 0f, 13f),
            ("Booster Box", new Vector2(180f, -213f), 180f, 14f),
            ("Crescent Cards", new Vector2(-148f, 300f), 90f, 16f),
            ("Card Tower", new Vector2(300f, 145f), 180f, 11f),
        };

        [MenuItem("Duel Genesis/World/16. Build Small Card Shops (4 around the city)")]
        public static void BuildSmallShops()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            Transform old = city.transform.Find(SmallShopsName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var all = new GameObject(SmallShopsName).transform;
            all.SetParent(city.transform, false);
            EnsureFolder(VaultFolder);
            _meshCount = 10000;   // mesh asset names apart from the Card Vault's

            var m = new ShopMats();
            int cleared = 0;
            var log = new List<string>();
            foreach (var (name, site, yaw, clear) in SmallShops)
            {
                cleared += ClearSite(city.transform, site, clear);
                var root = new GameObject(name).transform;
                root.SetParent(all, false);
                root.SetPositionAndRotation(new Vector3(site.x, 0f, site.y), Quaternion.Euler(0f, yaw, 0f));
                switch (name)
                {
                    case "Pagoda Packs": Pagoda(root, m); break;
                    case "Booster Box": BoosterBox(root, m); break;
                    case "Crescent Cards": Crescent(root, m); break;
                    case "Card Tower": Tower(root, m); break;
                }
                var arrival = new GameObject(name + " - Arrival Spot").transform;
                arrival.SetParent(root, false);
                arrival.localPosition = new Vector3(0f, 0.1f, clear + 3f);
                arrival.localRotation = Quaternion.LookRotation(Vector3.back);
                log.Add($"{name} at ({site.x:0}, {site.y:0}), {Vector2.Distance(site, new Vector2(CardShopSpot.x, CardShopSpot.z)):0} m from the main shop");
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis built {SmallShops.Length} small card shops (cleared {cleared} buildings/plants off their sites):\n" + string.Join("\n", log));
        }

        private static int _nextSmallShop;

        [MenuItem("Duel Genesis/DEV/Teleport To Next Small Card Shop (Play Mode)")]
        public static void TeleportToSmallShop()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Duel: Genesis: enter Play mode first."); return; }
            var player = Object.FindFirstObjectByType<DuelGenesis.Player.ThirdPersonPlayerController>();
            var (name, _, _, _) = SmallShops[_nextSmallShop++ % SmallShops.Length];
            GameObject spot = GameObject.Find(name + " - Arrival Spot");
            if (player == null || spot == null) return;
            player.Teleport(spot.transform.position, spot.transform.rotation);
            Object.FindFirstObjectByType<DuelGenesis.Player.ThirdPersonCamera>()?.SnapBehind(spot.transform.forward);
        }

        [MenuItem("Duel Genesis/DEV/Capture Small Card Shop Screenshots")]
        public static void CaptureSmallShops()
        {
            string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "Shots");
            System.IO.Directory.CreateDirectory(dir);
            int i = 0;
            foreach (var (name, _, _, clear) in SmallShops)
            {
                GameObject shop = GameObject.Find(name);
                if (shop == null) continue;
                Transform t = shop.transform;
                i++;
                GenesisShots.Shot(dir, $"small_{i}_front", t.TransformPoint(new Vector3(clear * 0.6f, 2.2f, clear + 12f)), t.TransformPoint(new Vector3(0f, 5f, 0f)), 70f);
                GenesisShots.Shot(dir, $"small_{i}_inside", t.TransformPoint(new Vector3(0f, 1.9f, 3f)), t.TransformPoint(new Vector3(0f, 1.6f, -6f)), 85f);
            }
            Debug.Log("Duel: Genesis captured small card shop screenshots into " + dir);
        }

        private sealed class ShopMats
        {
            public readonly Material Marble = TexMat("CS Vault Marble", "marble_01", new Color(0.95f, 0.95f, 0.97f), 0.85f, 3f);
            public readonly Material Paving = TexMat("CS Paving", "concrete_panels", new Color(0.82f, 0.82f, 0.84f), 0.3f, 2f);
            public readonly Material Stone = TexMat("CS Vault Plinth", "concrete_panels", new Color(0.7f, 0.7f, 0.72f), 0.35f, 2f);
            public readonly Material Wood = TexMat("DC Wood", "wooden_panels", new Color(0.95f, 0.9f, 0.85f), 0.45f, 2f);
            public readonly Material White = Mat("CS Vault White", new Color(0.94f, 0.94f, 0.95f), smooth: 0.7f);
            public readonly Material Plaster = Mat("SS Plaster", new Color(0.93f, 0.9f, 0.84f), smooth: 0.15f);
            public readonly Material Lacquer = Mat("SS Lacquer Red", new Color(0.62f, 0.08f, 0.06f), smooth: 0.65f);
            public readonly Material Tile = Mat("SS Roof Tile", new Color(0.12f, 0.13f, 0.15f), metallic: 0.3f, smooth: 0.55f);
            public readonly Material Bronze = Mat("CS Vault Bronze", new Color(0.42f, 0.3f, 0.18f), metallic: 0.95f, smooth: 0.72f);
            public readonly Material Charcoal = Mat("CS Charcoal", new Color(0.055f, 0.06f, 0.075f), metallic: 0.55f, smooth: 0.75f);
            public readonly Material Glass = GlassMat();
            public readonly Material Cyan = Mat("DC Glow Cyan", GenesisDuelCenter.Cyan, emission: GenesisDuelCenter.Cyan * 2.2f);
            public readonly Material Magenta = Mat("DC Glow Magenta", GenesisDuelCenter.Magenta, emission: GenesisDuelCenter.Magenta * 2f);
            public readonly Material Gold = Mat("DC Glow Gold", GenesisDuelCenter.Gold, emission: GenesisDuelCenter.Gold * 1.6f);
            public readonly Material Warm = Mat("CS Vault Warm Light", new Color(1f, 0.92f, 0.8f), emission: new Color(1f, 0.9f, 0.75f) * 2.2f);
            public readonly Material Board = Mat("DC Booth Board", new Color(0.05f, 0.06f, 0.1f), smooth: 0.5f);
            public readonly Material Ceiling = Mat("CS Vault Ceiling", new Color(0.9f, 0.88f, 0.84f), smooth: 0.2f);
            public Material Pack(int i)
            {
                string[] ids = { "monster", "spell", "trap", "dark", "light", "earth", "fire", "water", "wind", "dragon", "spellcaster", "warrior" };
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/DuelGenesis/Packs/pack_{ids[i % ids.Length]}.jpg");
                return tex != null ? Mat("DC Pack " + ids[i % ids.Length], Color.white, tex: tex, emission: Color.white * 0.35f) : Gold;
            }
        }

        // ------------------------------------------------------------------ helpers (positions are in the shop's local space)

        private static GameObject At(Transform p, string name, Mesh mesh, Material mat, Vector3 at, bool collider = false, bool shadows = true) =>
            Solid(p, name, mesh, mat, collider, shadows, at - VaultCentre);

        private static GameObject Rod(Transform p, string name, Vector3 a, Vector3 b, float r, Material mat, bool collider = false) =>
            Solid(p, name, Tube(a, b, r, 16), mat, collider);

        private static void Counter(Transform p, string shopName, Vector3 at, float yaw, float length, ShopMats m, Material glow)
        {
            var c = new GameObject("Counter").transform;
            c.SetParent(p, false);
            c.localPosition = at;
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);   // local +Z faces the customers
            Box(c, "Counter Body", new Vector3(0f, 0.55f, 0f), new Vector3(length, 1.1f, 0.8f), m.Wood);
            Box(c, "Counter Top", new Vector3(0f, 1.13f, 0f), new Vector3(length + 0.1f, 0.06f, 0.9f), m.Marble);
            Box(c, "Counter Glow", new Vector3(0f, 0.2f, 0.41f), new Vector3(length, 0.06f, 0.02f), glow, collider: false);
            var terminal = new GameObject("Card Shop Terminal");
            terminal.transform.SetParent(c, false);
            terminal.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var col = terminal.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.4f, 0f);
            col.size = new Vector3(length, 0.8f, 1.2f);
            terminal.AddComponent<DuelGenesis.Shops.CardShopTerminal>().shopName = shopName;
            GameObject kiosk = SubwayProp("Ticket_Vending_01");
            if (kiosk != null) PlaceSized(c, kiosk, "Register Kiosk", new Vector3(length * 0.35f, 1.16f, 0f), 0f, 0.8f);
        }

        private static void PackBoard(Transform p, Vector3 at, float yaw, float width, int first, ShopMats m, Material glow)
        {
            var w = new GameObject("Pack Board").transform;
            w.SetParent(p, false);
            w.localPosition = at;
            w.localRotation = Quaternion.Euler(0f, yaw, 0f);   // local +Z faces the room
            Box(w, "Board", new Vector3(0f, 2f, 0f), new Vector3(width, 2.8f, 0.1f), m.Board);
            Box(w, "Board Glow", new Vector3(0f, 3.45f, 0.06f), new Vector3(width, 0.05f, 0.03f), glow, collider: false);
            int cols = Mathf.Max(2, Mathf.FloorToInt(width / 1.05f));
            for (int i = 0; i < cols * 2; i++)
                Quad(w, "Pack", new Vector3((i % cols - (cols - 1) * 0.5f) * 1.0f, i < cols ? 2.65f : 1.35f, 0.07f), new Vector2(0.78f, 1.12f), 180f, m.Pack(first + i));
        }

        /// <summary>A spinning double-sided card (pack art faces), for rooftops and courtyards.</summary>
        private static void SpinningCard(Transform p, Vector3 at, float scale, ShopMats m, int packA, int packB)
        {
            var spinner = new GameObject("Spinning Card").transform;
            spinner.SetParent(p, false);
            spinner.localPosition = at;
            spinner.localScale = Vector3.one * scale;
            spinner.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 30f, 0f);
            Box(spinner, "Card Frame", Vector3.zero, new Vector3(2.2f, 3.2f, 0.08f), m.Gold, collider: false);
            Quad(spinner, "Card Face A", new Vector3(0f, 0f, -0.05f), new Vector2(2f, 2.95f), 0f, m.Pack(packA));
            Quad(spinner, "Card Face B", new Vector3(0f, 0f, 0.05f), new Vector2(2f, 2.95f), 180f, m.Pack(packB));
        }

        /// <summary>A giant card slab with real art (see the Card Vault crown), facing local +Z.</summary>
        private static GameObject GiantCard(Transform p, Vector3 at, float yaw, float height, string art, string frame, ShopMats m)
        {
            float w = height * 0.686f;
            Mesh slab = SaveMesh(CardSlab(w, height, 0.25f, height * 0.045f), "SmallCardSlab_" + height.ToString("0.0"));
            var go = new GameObject("Giant Card - " + art);
            go.transform.SetParent(p, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = slab;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { CardMaterial(art, frame) ?? m.Gold, CardMaterial("Card Back Anime 1", null) ?? m.Bronze, m.Gold };
            return go;
        }

        private static int ClearSite(Transform city, Vector2 site, float radius)
        {
            int n = 0;
            foreach (string rootName in new[] { GenesisCityBlocks.RootName, "Nature", GenesisGarden.BlossomName, "Street Dressing" })
            {
                Transform root = city.Find(rootName);
                if (root == null) continue;
                var doomed = new List<GameObject>();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root || t.parent == null) continue;
                    // Top-level items only: a building holder, a tree, a plant (not their meshes or block groups).
                    bool item = rootName == GenesisCityBlocks.RootName ? t.GetComponent<BoxCollider>() != null && t.parent.parent == root
                              : t.parent == root ? t.childCount == 0 || t.GetComponent<LODGroup>() != null || t.GetComponent<Renderer>() != null
                              : t.parent.parent == root;
                    if (!item) continue;
                    Bounds b = t.GetComponentInChildren<Renderer>() != null ? GenesisWorldBuilder.RendererBounds(t.gameObject) : new Bounds(t.position, Vector3.one);
                    Vector2 c = new Vector2(b.center.x, b.center.z);
                    float reach = radius + Mathf.Max(b.extents.x, b.extents.z);
                    if (Vector2.Distance(c, site) < reach) doomed.Add(t.gameObject);
                }
                foreach (var g in doomed) if (g != null) { Object.DestroyImmediate(g); n++; }
            }
            return n;
        }

        // ------------------------------------------------------------------ 1. Pagoda Packs

        private static void Pagoda(Transform s, ShopMats m)
        {
            const float R = 6f, door = 22f;
            At(s, "Forecourt", Ring(0f, 11f, 0f, 0.04f, 0f, 360f, 64), m.Paving, Vector3.zero);
            At(s, "Plinth", Ring(0f, R + 0.8f, 0f, 0.28f, 0f, 360f, 64), m.Stone, Vector3.zero, collider: true);
            At(s, "Floor", Ring(0f, R, 0.28f, 0.3f, 0f, 360f, 64), m.Marble, Vector3.zero, collider: true);
            At(s, "Glass Wall", Ring(R - 0.03f, R, 0.3f, 4.2f, door, 360f - door, 64), m.Glass, Vector3.zero, collider: true, shadows: false);
            for (float a = door; a <= 360f - door + 0.1f; a += (360f - 2f * door) / 14f)
                Rod(s, "Lacquer Post", Radial(a) * R + Vector3.up * 0.3f, Radial(a) * R + Vector3.up * 4.2f, 0.11f, m.Lacquer, true);
            // Three tiered roofs with glowing eaves, rising to a spire and a spinning card.
            (float r, float y0, float drumR, float drumTop)[] tiers = { (8.4f, 4.2f, 4.4f, 6.3f), (6.4f, 6.3f, 3f, 8f), (4.4f, 8f, 1.2f, 9.2f) };
            foreach (var (r, y0, drumR, drumTop) in tiers)
            {
                At(s, "Roof Tier", Ring(0f, r, y0, y0 + 0.4f, 0f, 360f, 80), m.Tile, Vector3.zero);
                At(s, "Eave Glow", Ring(r, r + 0.08f, y0 + 0.05f, y0 + 0.35f, 0f, 360f, 80), m.Magenta, Vector3.zero);
                At(s, "Eave Soffit", Ring(r - 0.3f, r - 0.1f, y0 - 0.04f, y0, 0f, 360f, 80), m.Warm, Vector3.zero);
                At(s, "Tier Wall", Ring(drumR - 0.1f, drumR, y0 + 0.4f, drumTop, 0f, 360f, 64), m.Plaster, Vector3.zero);
            }
            At(s, "Ceiling", Ring(0f, R - 0.05f, 4.15f, 4.2f, 0f, 360f, 64), m.Ceiling, Vector3.zero);
            Rod(s, "Spire", new Vector3(0f, 9.2f, 0f), new Vector3(0f, 11.2f, 0f), 0.14f, m.Gold);
            SpinningCard(s, new Vector3(0f, 13.4f, 0f), 1.3f, m, 2, 5);
            Sign(s, "PAGODA  PACKS", new Vector3(0f, 3.75f, R + 0.25f), 180f, 0.05f, Color.Lerp(Gold, Color.white, 0.3f));
            // Inside: a curved counter at the back, a pack board behind it, one duel table.
            Counter(s, "Pagoda Packs", new Vector3(0f, 0.3f, -3.3f), 0f, 4.2f, m, m.Magenta);
            PackBoard(s, new Vector3(0f, 0.3f, -R + 0.5f), 0f, 5f, 0, m, m.Magenta);
            ShopTable(s, "Pagoda Table", new Vector3(-2.9f, 0.3f, 1.3f), 90f, 811);
            PointLight(s, "Pagoda Light", new Vector3(0f, 3.6f, 0f), 10f, 4f, new Color(1f, 0.92f, 0.82f));
            PointLight(s, "Pagoda Glow", new Vector3(0f, 7f, 6f), 12f, 3f, GenesisDuelCenter.Magenta);
            foreach (float x in new[] { -7f, 7f }) LampPost(s, new Vector3(x, 0.04f, 8.5f), m.Bronze, m.Warm);
        }

        // ------------------------------------------------------------------ 2. Booster Box

        private static void BoosterBox(Transform s, ShopMats m)
        {
            const float L = 8f, halfD = 4.3f, H = 4.8f;   // straight length between the round ends, half depth
            At(s, "Forecourt", Ring(0f, 13f, 0f, 0.04f, 0f, 360f, 64), m.Paving, Vector3.zero);
            At(s, "Plinth", Stadium(L + 2f * halfD + 0.8f, 2f * halfD + 0.8f, 0f, 0.28f, 24), m.Stone, Vector3.zero, collider: true);
            At(s, "Floor", Stadium(L + 2f * halfD, 2f * halfD, 0.28f, 0.3f, 24), m.Marble, Vector3.zero, collider: true);
            foreach (float sx in new[] { -1f, 1f })   // the round ends
                At(s, "Glass End", Ring(halfD - 0.03f, halfD, 0.3f, H, sx > 0 ? 0f : 180f, sx > 0 ? 180f : 360f, 32), m.Glass, new Vector3(sx * L * 0.5f, 0f, 0f), collider: true, shadows: false);
            Box(s, "Glass Back", new Vector3(0f, (0.3f + H) * 0.5f, -halfD + 0.015f), new Vector3(L, H - 0.3f, 0.03f), m.Glass);
            foreach (float sx in new[] { -1f, 1f })   // front glass either side of the doors
                Box(s, "Glass Front", new Vector3(sx * (L * 0.25f + 0.9f), (0.3f + H) * 0.5f, halfD - 0.015f), new Vector3(L * 0.5f - 1.8f, H - 0.3f, 0.03f), m.Glass);
            Box(s, "Door Header", new Vector3(0f, H - 0.5f, halfD - 0.02f), new Vector3(3.6f, 1f, 0.12f), m.Charcoal, collider: false);
            At(s, "Roof", Stadium(L + 2f * halfD + 1.2f, 2f * halfD + 1.2f, H, H + 0.45f, 24), m.White, Vector3.zero);
            At(s, "Roof Glow", Stadium(L + 2f * halfD + 1.3f, 2f * halfD + 1.3f, H + 0.1f, H + 0.35f, 24), m.Cyan, Vector3.zero);
            At(s, "Ceiling", Stadium(L + 2f * halfD - 0.1f, 2f * halfD - 0.1f, H - 0.05f, H, 24), m.Ceiling, Vector3.zero);
            Sign(s, "BOOSTER  BOX", new Vector3(0f, H + 0.22f, halfD + 0.74f), 180f, 0.06f, Color.Lerp(GenesisDuelCenter.Cyan, Color.white, 0.4f));
            // A giant card on a pole by the street.
            Rod(s, "Card Pole", new Vector3(8f, 0f, 7.5f), new Vector3(8f, 3.2f, 7.5f), 0.18f, m.Bronze, true);
            GiantCard(s, new Vector3(8f, 3.2f + 3.1f, 7.5f), -15f, 6.2f, "Summoned Skull", "Normal Monster Card Background", m);
            PointLight(s, "Card Pole Light", new Vector3(8f, 6f, 10f), 10f, 3f, new Color(1f, 0.92f, 0.8f));
            // Inside: counter along the back, pack boards on the back glass, two tables.
            Counter(s, "Booster Box", new Vector3(0f, 0.3f, -2.4f), 0f, 5f, m, m.Cyan);
            PackBoard(s, new Vector3(0f, 0.3f, -halfD + 0.25f), 0f, 6.4f, 3, m, m.Cyan);
            ShopTable(s, "Box Table W", new Vector3(-6.8f, 0.3f, 0f), 0f, 821);
            ShopTable(s, "Box Table E", new Vector3(6.8f, 0.3f, 0f), 0f, 822);
            PointLight(s, "Box Light", new Vector3(0f, 4.2f, 0f), 12f, 4f, new Color(1f, 0.94f, 0.88f));
        }

        // ------------------------------------------------------------------ 3. Crescent Cards

        private static void Crescent(Transform s, ShopMats m)
        {
            const float rIn = 6.5f, rOut = 12f, a0 = 105f, a1 = 255f, H = 4.6f;   // the arc wraps the back of a courtyard open to the street
            At(s, "Courtyard", Ring(0f, rOut + 1.5f, 0f, 0.06f, 0f, 360f, 96), m.Paving, Vector3.zero, collider: true);
            At(s, "Arcade Floor", Ring(rIn - 0.5f, rOut, 0.06f, 0.2f, a0, a1, 64), m.Marble, Vector3.zero, collider: true);
            At(s, "Back Wall", Ring(rOut - 0.35f, rOut, 0.2f, H, a0, a1, 64), m.Plaster, Vector3.zero, collider: true);
            At(s, "Back Wall Glow", Ring(rOut - 0.37f, rOut - 0.35f, 3.6f, 3.68f, a0, a1, 64), m.Gold, Vector3.zero);
            At(s, "Arcade Roof", Ring(rIn - 1f, rOut + 0.4f, H, H + 0.45f, a0 - 4f, a1 + 4f, 64), m.White, Vector3.zero);
            At(s, "Roof Edge Glow", Ring(rIn - 1.08f, rIn - 1f, H + 0.05f, H + 0.4f, a0 - 4f, a1 + 4f, 64), m.Cyan, Vector3.zero);
            At(s, "Roof Soffit", Ring(rIn - 0.8f, rIn - 0.6f, H - 0.04f, H, a0, a1, 64), m.Warm, Vector3.zero);
            for (float a = a0; a <= a1 + 0.1f; a += (a1 - a0) / 8f)
                Rod(s, "Arcade Column", Radial(a) * (rIn - 0.4f) + Vector3.up * 0.2f, Radial(a) * (rIn - 0.4f) + Vector3.up * H, 0.16f, m.Bronze, true);
            // Three counters (stalls) under the arcade, pack boards on the back wall behind each.
            int stall = 0;
            foreach (float a in new[] { 140f, 180f, 220f })
            {
                Vector3 p = Radial(a) * 9.4f + Vector3.up * 0.2f;
                float yaw = a + 180f;   // face the courtyard
                Counter(s, "Crescent Cards", p, yaw, 3.4f, m, stall == 1 ? m.Gold : stall == 0 ? m.Magenta : m.Cyan);
                PackBoard(s, Radial(a) * (rOut - 0.45f) + Vector3.up * 0.2f, yaw, 3.8f, stall * 4, m, stall == 1 ? m.Gold : stall == 0 ? m.Magenta : m.Cyan);
                PointLight(s, "Stall Light", Radial(a) * 9f + Vector3.up * 4f, 8f, 3.5f, new Color(1f, 0.93f, 0.84f));
                stall++;
            }
            // The courtyard: a giant card statue on a round plinth, two duel tables either side.
            At(s, "Statue Plinth", Ring(0f, 1.6f, 0.06f, 0.9f, 0f, 360f, 48), m.Stone, Vector3.zero, collider: true);
            At(s, "Statue Glow", Ring(1.6f, 1.65f, 0.1f, 0.8f, 0f, 360f, 48), m.Gold, Vector3.zero);
            GiantCard(s, new Vector3(0f, 0.9f + 2.6f, 0f), 0f, 5.2f, "Dark Magician", "Normal Monster Card Background", m);
            GiantCard(s, new Vector3(0f, 0.9f + 2.6f, -0.2f), 180f, 5.2f, "Blue-Eyes White Dragon", "Normal Monster Card Background", m);
            ShopTable(s, "Courtyard Table N", new Vector3(-4.2f, 0.06f, 3.2f), 30f, 831);
            ShopTable(s, "Courtyard Table S", new Vector3(4.2f, 0.06f, 3.2f), -30f, 832);
            Sign(s, "CRESCENT  CARDS", new Vector3(0f, H + 0.22f, -rIn + 1.25f), 180f, 0.06f, Color.Lerp(Gold, Color.white, 0.3f));
            SpotLight(s, "Statue Spot", new Vector3(0f, 7f, 6f), new Vector3(0f, 3.5f, 0f), new Color(1f, 0.95f, 0.88f));
            foreach (float x in new[] { -9f, 9f }) LampPost(s, new Vector3(x, 0.06f, 9f), m.Bronze, m.Warm);
        }

        // ------------------------------------------------------------------ 4. Card Tower

        private static void Tower(Transform s, ShopMats m)
        {
            const float R = 5f, door = 24f, H = 12.6f;
            At(s, "Forecourt", Ring(0f, 9.5f, 0f, 0.04f, 0f, 360f, 64), m.Paving, Vector3.zero);
            At(s, "Plinth", Ring(0f, R + 0.6f, 0f, 0.28f, 0f, 360f, 64), m.Stone, Vector3.zero, collider: true);
            At(s, "Floor", Ring(0f, R, 0.28f, 0.3f, 0f, 360f, 64), m.Marble, Vector3.zero, collider: true);
            At(s, "Glass Ground", Ring(R - 0.03f, R, 0.3f, 4.2f, door, 360f - door, 64), m.Glass, Vector3.zero, collider: true, shadows: false);
            At(s, "Glass Upper", Ring(R - 0.03f, R, 4.2f, H, 0f, 360f, 64), m.Glass, Vector3.zero, shadows: false);
            foreach (float y in new[] { 4.2f, 8.4f })
            {
                At(s, "Floor Slab", Ring(0f, R - 0.05f, y - 0.1f, y, 0f, 360f, 64), m.Ceiling, Vector3.zero);
                At(s, "Floor Band", Ring(R, R + 0.12f, y - 0.1f, y + 0.25f, 0f, 360f, 64), m.Bronze, Vector3.zero);
                At(s, "Floor Band Glow", Ring(R + 0.12f, R + 0.15f, y, y + 0.12f, 0f, 360f, 64), m.Cyan, Vector3.zero);
                PackBoard(s, new Vector3(0f, y, -R + 0.45f), 0f, 4.2f, (int)y, m, m.Cyan);   // displays seen through the glass upstairs
                PointLight(s, "Upper Light", new Vector3(0f, y + 3.2f, 0f), 8f, 3f, new Color(1f, 0.94f, 0.88f));
            }
            for (float a = 0f; a < 360f; a += 30f)
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) > door)
                    Rod(s, "Fin", Radial(a) * (R + 0.12f) + Vector3.up * 0.3f, Radial(a) * (R + 0.12f) + Vector3.up * H, 0.06f, m.Bronze);
                else
                    Rod(s, "Fin", Radial(a) * (R + 0.12f) + Vector3.up * 4.2f, Radial(a) * (R + 0.12f) + Vector3.up * H, 0.06f, m.Bronze);
            At(s, "Crown", Ring(0f, R + 0.5f, H, H + 0.5f, 0f, 360f, 64), m.White, Vector3.zero);
            At(s, "Crown Glow", Ring(R + 0.5f, R + 0.58f, H + 0.05f, H + 0.45f, 0f, 360f, 64), m.Magenta, Vector3.zero);
            Rod(s, "Crown Mast", new Vector3(0f, H + 0.5f, 0f), new Vector3(0f, H + 2f, 0f), 0.12f, m.Gold);
            SpinningCard(s, new Vector3(0f, H + 4.2f, 0f), 1.25f, m, 7, 9);
            Sign(s, "CARD  TOWER", new Vector3(0f, 4.55f, R + 0.2f), 180f, 0.05f, Color.Lerp(GenesisDuelCenter.Magenta, Color.white, 0.4f));
            Box(s, "Door Canopy", new Vector3(0f, 3.4f, R + 1.2f), new Vector3(4.2f, 0.18f, 2.4f), m.White, collider: false);
            Box(s, "Door Canopy Glow", new Vector3(0f, 3.3f, R + 2.42f), new Vector3(4.2f, 0.06f, 0.04f), m.Magenta, collider: false);
            Counter(s, "Card Tower", new Vector3(0f, 0.3f, -2.4f), 0f, 3.6f, m, m.Magenta);
            PackBoard(s, new Vector3(0f, 0.3f, -R + 0.45f), 0f, 4.2f, 6, m, m.Magenta);
            ShopTable(s, "Tower Table", new Vector3(2.6f, 0.3f, 1.2f), 90f, 841);
            PointLight(s, "Tower Light", new Vector3(0f, 3.5f, 0f), 9f, 4f, new Color(1f, 0.93f, 0.85f));
        }
    }
}
#endif
