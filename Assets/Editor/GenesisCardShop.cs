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
    /// The Genesis Card Shop: a modern glass showroom on the park block east of the plaza, with its own parking
    /// lot connected to the east boulevard by two driveways. Built from our own geometry plus the CC0 kits already
    /// in the project.
    ///   - front: the neon Dragon Gate Inn (daydev, CC BY 4.0) scaled up as a landmark, with a GENESIS CARDS sign,
    ///   - behind it a dark neon hall (Sci-fi bar look) with glass entrances either side of the Inn,
    ///     black fascia with the shop name, and a pylon sign at the lot entrance,
    ///   - inside: a hero plinth with a giant rotating card under spotlights, glass showcases, pack walls,
    ///     a service counter with a video wall, 8 playable duel tables and a trade lounge,
    ///   - outside: 32-space parking lot (two rows, painted bays, wheel stops, two accessible bays), lamp posts,
    ///     hedges, and driveways across the pavement onto the boulevard.
    /// Local +Z is the street front. Re-running it replaces the old shop and clears plants and props off its lot.
    /// </summary>
    public static partial class GenesisDuelCenter
    {
        public const string CardShopName = "Genesis Card Shop";
        public static readonly Vector3 CardShopSpot = new Vector3(78f, 0f, 54f);   // NE park block, facing the east boulevard
        public const float CardShopYaw = 180f;                                      // local +Z (front) faces world -Z

        /// <summary>The shop and its parking lot in world space, kept clear by the nature planter.</summary>
        public static bool InCardShopLot(Vector2 world) => world.x > 55f && world.x < 101f && world.y > 7f && world.y < 84f;

        [MenuItem("Duel Genesis/World/11. Build Genesis Card Shop (open city)")]
        public static void BuildCardShopMenu()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            GameObject shop = BuildCardShop(city.transform, CardShopSpot, CardShopYaw);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = shop;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("Duel Genesis/DEV/Teleport To Genesis Card Shop (Play Mode)")]
        public static void TeleportToCardShop()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Duel: Genesis: enter Play mode first."); return; }
            var player = Object.FindFirstObjectByType<DuelGenesis.Player.ThirdPersonPlayerController>();
            GameObject spot = GameObject.Find(CardShopName + " - Arrival Spot");
            if (player == null || spot == null) return;
            player.Teleport(spot.transform.position, spot.transform.rotation);
            Object.FindFirstObjectByType<DuelGenesis.Player.ThirdPersonCamera>()?.SnapBehind(spot.transform.forward);
        }

        private const float HallZ = -16f;    // hall centre (local z); the Inn stands in front of it
        private const float ShopW = 18f;     // half width
        private const float ShopD = 12f;     // half depth
        private const float ShopH = 9f;      // roof height

        public static GameObject BuildCardShop(Transform city, Vector3 ground, float yaw)
        {
            Transform old = city.Find(CardShopName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(CardShopName).transform;
            root.SetParent(city, false);
            root.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            int cleared = ClearLot(city, root, -22f, 22f, HallZ - ShopD - 1.5f, 45f);

            // ---- materials
            Material white = Mat("CS White", new Color(0.93f, 0.94f, 0.96f), smooth: 0.65f);
            Material charcoal = Mat("CS Charcoal", new Color(0.055f, 0.06f, 0.075f), metallic: 0.55f, smooth: 0.75f);
            Material floor = TexMat("CS Showroom Floor", "marble_01", new Color(0.93f, 0.94f, 0.97f), 0.8f, 2.5f);
            Material wood = TexMat("DC Wood", "wooden_panels", new Color(0.95f, 0.9f, 0.85f), 0.35f, 2f);
            Material paving = TexMat("CS Paving", "concrete_panels", new Color(0.82f, 0.82f, 0.84f), 0.3f, 2f);
            Material asphalt = Mat("CS Asphalt", new Color(0.12f, 0.125f, 0.14f), smooth: 0.2f);
            Material lineWhite = Mat("CS Line White", new Color(0.95f, 0.95f, 0.93f), smooth: 0.2f);
            Material lineBlue = Mat("CS Line Blue", new Color(0.12f, 0.35f, 0.85f), smooth: 0.2f);
            Material concrete = Mat("CS Concrete", new Color(0.62f, 0.62f, 0.6f), smooth: 0.15f);
            Material metal = Mat("DC Dark Metal", new Color(0.1f, 0.11f, 0.14f), metallic: 0.8f, smooth: 0.6f);
            Material glass = GlassMat();
            Material cyan = Mat("DC Glow Cyan", Cyan, emission: Cyan * 2.2f);
            Material magenta = Mat("DC Glow Magenta", Magenta, emission: Magenta * 2f);
            Material gold = Mat("DC Glow Gold", Gold, emission: Gold * 1.6f);
            Material downlight = Mat("DC Ceiling Light", Color.white, emission: new Color(1f, 0.96f, 0.9f) * 1.8f);
            Material screen = Mat("DC Screen", Color.white, smooth: 0.8f, emission: Color.white * 1.4f);
            Material board = Mat("DC Booth Board", new Color(0.05f, 0.06f, 0.1f), smooth: 0.5f);
            Material logo = LogoMat();
            string[] packIds = { "monster", "spell", "trap", "dark", "light", "earth", "fire", "water", "wind", "dragon", "spellcaster", "warrior" };
            var packs = new List<Material>();
            foreach (string id in packIds)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/DuelGenesis/Packs/pack_{id}.jpg");
                if (tex != null) packs.Add(Mat("DC Pack " + id, Color.white, tex: tex, emission: Color.white * 0.35f));
            }
            Material Pack(int i) => packs.Count > 0 ? packs[i % packs.Count] : gold;

            // =========================================================== the hall (behind the Dragon Gate Inn front)
            Transform hall = new GameObject("Genesis Hall").transform;
            hall.SetParent(root, false);
            hall.localPosition = new Vector3(0f, 0f, HallZ);
            Material darkFloor = Mat("CS Neon Floor", new Color(0.035f, 0.04f, 0.055f), metallic: 0.3f, smooth: 0.88f);
            Material violet = Mat("CS Glow Violet", new Color(0.55f, 0.25f, 1f), emission: new Color(0.55f, 0.25f, 1f) * 2.2f);

            Box(hall, "Hall Floor", new Vector3(0f, 0.03f, 0f), new Vector3(ShopW * 2f, 0.06f, ShopD * 2f), darkFloor);
            Box(hall, "Back Wall", new Vector3(0f, ShopH * 0.5f, -ShopD + 0.15f), new Vector3(ShopW * 2f, ShopH, 0.3f), charcoal);
            Box(hall, "West Wall", new Vector3(-ShopW + 0.15f, ShopH * 0.5f, 0f), new Vector3(0.3f, ShopH, ShopD * 2f), charcoal);
            Box(hall, "East Wall", new Vector3(ShopW - 0.15f, ShopH * 0.5f, 0f), new Vector3(0.3f, ShopH, ShopD * 2f), charcoal);
            Box(hall, "Roof", new Vector3(0f, ShopH + 0.2f, 0f), new Vector3(ShopW * 2f + 0.4f, 0.4f, ShopD * 2f + 0.4f), charcoal);
            Box(hall, "Ceiling", new Vector3(0f, ShopH - 0.02f, 0f), new Vector3(ShopW * 2f - 0.6f, 0.04f, ShopD * 2f - 0.6f), Mat("CS Ceiling", new Color(0.06f, 0.06f, 0.085f), smooth: 0.3f), collider: false);

            // Front wall with two glass entrances either side of the Inn.
            float doorHalf = 2.2f, doorH = 3.4f, doorX = 13f;
            float[] cutsL = { -ShopW + 0.3f, -doorX - doorHalf, -doorX + doorHalf, doorX - doorHalf, doorX + doorHalf, ShopW - 0.3f };
            for (int i = 0; i < cutsL.Length; i += 2)
            {
                float a = cutsL[i], b = cutsL[i + 1];
                Box(hall, "Front Wall", new Vector3((a + b) * 0.5f, ShopH * 0.5f, ShopD - 0.15f), new Vector3(b - a, ShopH, 0.3f), charcoal);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * doorX;
                Box(hall, "Door Header", new Vector3(x, (doorH + ShopH) * 0.5f, ShopD - 0.15f), new Vector3(doorHalf * 2f, ShopH - doorH, 0.3f), charcoal);
                Box(hall, "Door Glow Top", new Vector3(x, doorH + 0.05f, ShopD + 0.02f), new Vector3(doorHalf * 2f, 0.08f, 0.05f), s < 0 ? magenta : cyan, collider: false);
                foreach (float e in new[] { -1f, 1f })
                {
                    Box(hall, "Door Glow Side", new Vector3(x + e * (doorHalf + 0.04f), doorH * 0.5f, ShopD + 0.02f), new Vector3(0.08f, doorH, 0.05f), s < 0 ? magenta : cyan, collider: false);
                    Box(hall, "Sliding Door (open)", new Vector3(x + e * (doorHalf + 1f), doorH * 0.5f, ShopD - 0.35f), new Vector3(2f, doorH - 0.1f, 0.05f), glass, collider: false);
                }
                Sign(hall, "ENTER", new Vector3(x, doorH + 0.9f, ShopD + 0.03f), 180f, 0.05f, s < 0 ? Magenta : Cyan);
                Box(hall, "Entrance Mat", new Vector3(x, 0.065f, ShopD - 1.2f), new Vector3(doorHalf * 2f, 0.01f, 2.4f), s < 0 ? magenta : cyan, collider: false)
                    .GetComponent<Renderer>().sharedMaterial = Mat(s < 0 ? "CS Rug Magenta" : "CS Rug Cyan", (s < 0 ? Magenta : Cyan) * 0.35f, smooth: 0.1f);
            }
            // Neon trim on the outside of the hall: roof line and corners.
            Box(hall, "Roof Line Glow", new Vector3(0f, ShopH + 0.42f, ShopD + 0.22f), new Vector3(ShopW * 2f + 0.4f, 0.08f, 0.06f), magenta, collider: false);
            foreach (float s in new[] { -1f, 1f })
                Box(hall, "Corner Glow", new Vector3(s * (ShopW + 0.22f), ShopH * 0.5f, ShopD + 0.22f), new Vector3(0.08f, ShopH, 0.08f), cyan, collider: false);
            Sign(hall, "GENESIS  CARDS", new Vector3(-doorX, 7f, ShopD + 0.03f), 180f, 0.075f, Color.Lerp(Gold, Color.white, 0.3f));
            Sign(hall, "DUEL  ·  TRADE", new Vector3(doorX, 7f, ShopD + 0.03f), 180f, 0.075f, Color.Lerp(Cyan, Color.white, 0.3f));

            // Neon interior (Sci-fi bar look): strips at skirting, dado and ceiling height, louvers, coloured fill lights.
            foreach (float s in new[] { -1f, 1f })
            {
                Box(hall, "Wall Strip Low", new Vector3(s * (ShopW - 0.32f), 0.12f, 0f), new Vector3(0.04f, 0.06f, ShopD * 2f - 0.6f), cyan, collider: false);
                Box(hall, "Wall Strip Mid", new Vector3(s * (ShopW - 0.32f), 3.2f, 0f), new Vector3(0.04f, 0.05f, ShopD * 2f - 0.6f), magenta, collider: false);
                Box(hall, "Wall Strip High", new Vector3(s * (ShopW - 0.32f), ShopH - 0.35f, 0f), new Vector3(0.04f, 0.06f, ShopD * 2f - 0.6f), violet, collider: false);
                for (float z = -ShopD + 1.5f; z < ShopD - 1f; z += 0.9f)
                    if (Mathf.Abs(z - 1.2f) > 3.2f)   // leave the pack walls clear
                        Box(hall, "Louver", new Vector3(s * (ShopW - 0.4f), 6f, z), new Vector3(0.12f, 4.4f, 0.3f), Mat("CS Louver", new Color(0.09f, 0.1f, 0.14f), metallic: 0.5f, smooth: 0.6f), collider: false);
            }
            Box(hall, "Back Strip Low", new Vector3(0f, 0.12f, -ShopD + 0.32f), new Vector3(ShopW * 2f - 0.6f, 0.06f, 0.04f), cyan, collider: false);
            Box(hall, "Back Strip High", new Vector3(0f, ShopH - 0.35f, -ShopD + 0.32f), new Vector3(ShopW * 2f - 0.6f, 0.06f, 0.04f), violet, collider: false);
            foreach (float x in new[] { -12f, -6f, 0f, 6f, 12f })
                Box(hall, "Ceiling Light Bar", new Vector3(x, ShopH - 0.06f, 0f), new Vector3(0.18f, 0.04f, ShopD * 2f - 2f), x == 0f ? magenta : downlight, collider: false);
            foreach (float x in new[] { -11f, 0f, 11f })
                foreach (float z in new[] { -7f, 4f })
                    PointLight(hall, "Hall Light", new Vector3(x, ShopH - 1.5f, z), 14f, 7f, new Color(1f, 0.95f, 0.92f));
            foreach (Vector3 p in new[] { new Vector3(-15f, 3f, -8f), new Vector3(15f, 3f, -8f) })
                PointLight(hall, "Neon Fill", p, 12f, 5f, p.x < 0 ? Magenta : Cyan);

            // Hero plinth: a lit dais with a giant rotating card between the two entrances.
            Vector3 hero = new Vector3(0f, 0f, 6.5f);
            Box(hall, "Hero Plinth", hero + new Vector3(0f, 0.25f, 0f), new Vector3(3.6f, 0.5f, 3.6f), charcoal);
            Box(hall, "Hero Plinth Glow", hero + new Vector3(0f, 0.52f, 0f), new Vector3(3.64f, 0.04f, 3.64f), cyan, collider: false);
            var spinner = new GameObject("Hero Card").transform;
            spinner.SetParent(hall, false);
            spinner.localPosition = hero + new Vector3(0f, 2.3f, 0f);
            spinner.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 25f, 0f);
            Box(spinner, "Card Frame", Vector3.zero, new Vector3(2.2f, 3.2f, 0.08f), gold, collider: false);
            Quad(spinner, "Card Face A", new Vector3(0f, 0f, -0.05f), new Vector2(2f, 2.95f), 0f, screen).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 3;
            Quad(spinner, "Card Face B", new Vector3(0f, 0f, 0.05f), new Vector2(2f, 2.95f), 180f, screen).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 11;
            SpotLight(hall, "Hero Spot W", new Vector3(-4f, ShopH - 0.4f, 9f), hero + Vector3.up * 2f, new Color(1f, 0.6f, 0.9f));
            SpotLight(hall, "Hero Spot E", new Vector3(4f, ShopH - 0.4f, 9f), hero + Vector3.up * 2f, new Color(0.6f, 0.9f, 1f));

            // Showcases along the front, between the doors and the hero card.
            int card = 0;
            foreach (float s in new[] { -1f, 1f })
                foreach (float x in new[] { 4.2f, 8f })
                    Showcase(hall, new Vector3(s * x, 0f, 8.4f), charcoal, s < 0 ? magenta : cyan, glass, s < 0 ? magenta : cyan, Pack(card++), Pack(card++), Pack(card++));
            HangingSign(hall, "RARE SINGLES", new Vector3(-6.1f, 5.4f, 8.4f), board, magenta, 0f, 6f);
            HangingSign(hall, "NEW RELEASES", new Vector3(6.1f, 5.4f, 8.4f), board, cyan, 0f, 6f);

            // Pack counters: boosters on the west wall, duel mats on the east wall. Both open the shop counter UI.
            foreach (float s in new[] { -1f, 1f })
            {
                PackWall(hall, new Vector3(s * (ShopW - 0.3f), 0f, 1.2f), s < 0 ? 90f : -90f, board);
                Vector3 c = new Vector3(s * (ShopW - 2.6f), 0f, 1.2f);
                Box(hall, "Shop Counter", c + new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 1.1f, 5f), charcoal);
                Box(hall, "Shop Counter Glow", c + new Vector3(-s * 0.56f, 0.85f, 0f), new Vector3(0.02f, 0.08f, 4.8f), s < 0 ? magenta : cyan, collider: false);
                Box(hall, "Register", c + new Vector3(0f, 1.28f, 1.6f), new Vector3(0.45f, 0.35f, 0.55f), metal, collider: false);
                var terminal = new GameObject(s < 0 ? "Booster Counter Terminal" : "Duel Mat Counter Terminal");
                terminal.transform.SetParent(hall, false);
                terminal.transform.localPosition = c + new Vector3(0f, 1.1f, 0f);
                var tcol = terminal.AddComponent<BoxCollider>();
                tcol.center = new Vector3(0f, 0.4f, 0f);
                tcol.size = new Vector3(1.4f, 0.8f, 5f);
                terminal.AddComponent<DuelGenesis.Shops.CardShopTerminal>().shopName = CardShopName;
                HangingSign(hall, s < 0 ? "BOOSTER PACKS" : "DUEL MATS", new Vector3(c.x, 5.4f, c.z), board, s < 0 ? magenta : cyan, s < 0 ? 90f : -90f, 5.2f);
            }

            // Duel tables in the back corners under pendant lights.
            int seed = 700;
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float x in new[] { 8.8f, 13.6f })
                    foreach (float z in new[] { -9f, -4.8f })
                    {
                        ShopTable(hall, $"Table {seed - 699}", new Vector3(s * x, 0.06f, z), 0f, seed++);
                        Box(hall, "Pendant Cable", new Vector3(s * x, (ShopH + 3.2f) * 0.5f, z), new Vector3(0.02f, ShopH - 3.2f, 0.02f), metal, collider: false);
                        Box(hall, "Pendant", new Vector3(s * x, 3.1f, z), new Vector3(1.4f, 0.12f, 0.5f), charcoal, collider: false);
                        Box(hall, "Pendant Glow", new Vector3(s * x, 3.03f, z), new Vector3(1.3f, 0.02f, 0.42f), s < 0 ? magenta : cyan, collider: false);
                    }
                Box(hall, "Duel Zone Rug", new Vector3(s * 11.2f, 0.065f, -6.9f), new Vector3(8.6f, 0.01f, 7.6f), s < 0 ? magenta : cyan, collider: false)
                    .GetComponent<Renderer>().sharedMaterial = Mat(s < 0 ? "CS Rug Magenta" : "CS Rug Cyan", (s < 0 ? Magenta : Cyan) * 0.35f, smooth: 0.1f);
            }
            HangingSign(hall, "FREE PLAY", new Vector3(-11.2f, 5.4f, -2.4f), board, magenta, 0f, 5f);
            HangingSign(hall, "TOURNAMENT", new Vector3(11.2f, 5.4f, -2.4f), board, cyan, 0f, 5f);

            // The Duelist Bar: the Sci-fi bar room, back centre, with the trade lounge in front of it.
            GameObject barPrefab = GenesisSketchfabModels.Load("SciFiBar");
            if (barPrefab != null)
            {
                var bar = (GameObject)PrefabUtility.InstantiatePrefab(barPrefab, hall);
                bar.name = "Duelist Bar (Sci-fi bar by onerockett, CC BY 4.0)";
                bar.transform.localRotation = Quaternion.identity;
                bar.transform.localScale = Vector3.one * 9f;
                Physics.SyncTransforms();
                Bounds bb = LocalBounds(hall, bar);
                bar.transform.localPosition += new Vector3(-bb.center.x, -bb.min.y + 0.06f, -ShopD + 0.35f - bb.min.z);
                GameObjectUtility.SetStaticEditorFlags(bar, StaticEditorFlags.BatchingStatic);
                HangingSign(hall, "DUELIST BAR", new Vector3(0f, 5.4f, -2.2f), board, violet, 0f, 5f);
            }
            GameObject kTable = LoadKenney("tableRound"), kChair = LoadKenney("chairCushion");
            if (kTable != null && kChair != null)
            {
                TradeTable(hall, "Trade Table W", new Vector3(-5.8f, 0.06f, 1.6f), kTable, kChair);
                TradeTable(hall, "Trade Table E", new Vector3(5.8f, 0.06f, 1.6f), kTable, kChair);
            }

            // =========================================================== the Dragon Gate Inn front
            Box(root, "Forecourt", new Vector3(0f, 0.035f, (HallZ + ShopD + 15.25f) * 0.5f), new Vector3(ShopW * 2f + 2f, 0.07f, 15.25f - (HallZ + ShopD)), paving);
            Box(root, "Forecourt Curb", new Vector3(0f, 0.09f, 15.25f), new Vector3(ShopW * 2f + 2f, 0.18f, 0.2f), concrete);
            foreach (float s in new[] { -1f, 1f })
                Box(root, "Walkway Glow", new Vector3(s * 13f, 0.075f, (HallZ + ShopD + 15.25f) * 0.5f), new Vector3(0.08f, 0.01f, 15.25f - (HallZ + ShopD) - 0.5f), s < 0 ? magenta : cyan, collider: false);
            PlaceInn(root, HallZ + ShopD, gold);
            GameObject pot = LoadNature("pot_large"), bush = LoadNature("plant_bushDetailed");
            foreach (float x in new[] { -16.5f, -9.5f, 9.5f, 16.5f })
            {
                if (pot != null) PlaceSized(root, pot, "Forecourt Planter", new Vector3(x, 0.07f, 13.6f), 0f, 0.8f);
                if (bush != null) PlaceSized(root, bush, "Forecourt Planter Bush", new Vector3(x, 0.8f, 13.6f), 45f, 1.1f);
            }
            PointLight(root, "Forecourt Light W", new Vector3(-13f, 6f, 8f), 16f, 6f, Magenta);
            PointLight(root, "Forecourt Light E", new Vector3(13f, 6f, 8f), 16f, 6f, Cyan);

            // =========================================================== parking lot and driveways
            // Local z: front walk ends at 15.25; bays 15.3-20.8, aisle 20.8-28, bays 28-33.5; hedge strip 34.2-35.9; city pavement 36-40; road edge 44.
            const float lotFront = 15.3f, lotBack = 34.2f, lotHalf = 21f, bay = 2.8f, bayDepth = 5.5f;
            Box(root, "Parking Lot", new Vector3(0f, 0.02f, (lotFront + lotBack) * 0.5f), new Vector3(lotHalf * 2f, 0.04f, lotBack - lotFront), asphalt);
            float[] driveX = { -12f, 12f };
            const float driveHalf = 3.5f;
            bool InDrive(float x) => driveX.Any(d => Mathf.Abs(x - d) < driveHalf + 0.2f);
            foreach (float d in driveX)
            {
                Box(root, "Driveway", new Vector3(d, 0.045f, (lotBack + 44.3f) * 0.5f), new Vector3(driveHalf * 2f, 0.05f, 44.3f - lotBack), asphalt);
                Box(root, "Driveway Centre Line", new Vector3(d, 0.075f, (lotBack + 44.3f) * 0.5f), new Vector3(0.12f, 0.01f, 44.3f - lotBack - 1f), Mat("OW Road Paint Yellow", new Color(0.95f, 0.78f, 0.25f), smooth: 0.2f), collider: false);
                Sign(root, d < 0 ? "EXIT" : "ENTER", new Vector3(d, 0.08f, 31.2f), 180f, 0.09f, new Color(0.95f, 0.95f, 0.93f), flatOnFloor: true);
            }
            int bays = 0, accessible = 0;
            foreach ((float z0, float facing) in new[] { (lotFront, 1f), (28f, -1f) })
            {
                for (float x = -lotHalf + 0.6f; x <= lotHalf - 0.5f; x += bay)
                {
                    float cx = x + bay * 0.5f;
                    if (cx > lotHalf - 0.6f) break;
                    if (facing < 0f && InDrive(cx)) continue;           // keep the driveway lanes clear
                    Box(root, "Bay Line", new Vector3(x, 0.05f, z0 + bayDepth * 0.5f), new Vector3(0.12f, 0.01f, bayDepth), lineWhite, collider: false);
                    bool blue = facing > 0f && Mathf.Abs(cx) < 3f;           // accessible bays nearest the door
                    if (blue)
                    {
                        Box(root, "Accessible Bay", new Vector3(cx, 0.045f, z0 + bayDepth * 0.5f), new Vector3(bay - 0.2f, 0.01f, bayDepth - 0.2f), lineBlue, collider: false);
                        accessible++;
                    }
                    float stopZ = facing > 0f ? z0 + 0.6f : z0 + bayDepth - 0.6f;
                    Box(root, "Wheel Stop", new Vector3(cx, 0.08f, stopZ), new Vector3(1.8f, 0.15f, 0.2f), concrete);
                    bays++;
                }
                if (!(facing < 0f && InDrive(lotHalf - 0.5f)))
                    Box(root, "Bay Line", new Vector3(lotHalf - 0.5f, 0.05f, z0 + bayDepth * 0.5f), new Vector3(0.12f, 0.01f, bayDepth), lineWhite, collider: false);
            }
            // Aisle arrows (painted) and lot edge curb.
            Sign(root, "<  <  <", new Vector3(0f, 0.08f, 24.4f), 180f, 0.12f, new Color(0.95f, 0.95f, 0.93f), flatOnFloor: true);
            foreach (float s in new[] { -1f, 1f })
                Box(root, "Lot Side Curb", new Vector3(s * (lotHalf + 0.1f), 0.09f, (lotFront + lotBack) * 0.5f), new Vector3(0.2f, 0.18f, lotBack - lotFront), concrete);
            // Hedge strip between the lot and the street, broken by the driveways.
            Box(root, "Hedge Strip", new Vector3(0f, 0.03f, 35.02f), new Vector3(lotHalf * 2f, 0.06f, 1.65f), Mat("CS Lawn", new Color(0.24f, 0.42f, 0.18f), smooth: 0.1f));
            GameObject hedge = LoadNature("plant_bushLarge") ?? LoadNature("plant_bush");
            if (hedge != null)
                for (float x = -lotHalf + 1f; x <= lotHalf - 0.9f; x += 1.8f)
                    if (!InDrive(x)) PlaceSized(root, hedge, "Hedge", new Vector3(x, 0.06f, 35.02f), (x * 37f) % 360f, 0.85f);
            foreach (float d in driveX)
                foreach (float s in new[] { -1f, 1f })
                    Box(root, "Driveway Curb", new Vector3(d + s * (driveHalf + 0.1f), 0.09f, 35.02f), new Vector3(0.2f, 0.18f, 1.65f), concrete);

            // Lamp posts along the lot (POLYGON city pack), else simple poles.
            var lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/POLYGON city pack/Prefabs/Lamps/street_lamp 1 prefab.prefab");
            foreach (float x in new[] { -18f, 0f, 18f })
            {
                Vector3 p = new Vector3(x, 0.04f, 24.4f);
                if (lampPrefab != null && x != 0f) PlaceSized(root, lampPrefab, "Lot Lamp", p, 90f, 6f);
                PointLight(root, "Lot Light", p + Vector3.up * 5.5f, 18f, 6f, new Color(1f, 0.9f, 0.75f));
            }

            // Pylon sign by the entrance driveway.
            Vector3 pylon = new Vector3(18f, 0f, 35.02f);
            Box(root, "Pylon", pylon + new Vector3(0f, 3.5f, 0f), new Vector3(1.2f, 7f, 0.6f), charcoal);
            Box(root, "Pylon Glow L", pylon + new Vector3(-0.62f, 3.5f, 0f), new Vector3(0.05f, 7f, 0.62f), cyan, collider: false);
            Box(root, "Pylon Glow R", pylon + new Vector3(0.62f, 3.5f, 0f), new Vector3(0.05f, 7f, 0.62f), magenta, collider: false);
            if (logo != null)
            {
                Quad(root, "Pylon Logo Front", pylon + new Vector3(0f, 6.1f, 0.31f), new Vector2(1.05f, 0.98f), 180f, logo);
                Quad(root, "Pylon Logo Back", pylon + new Vector3(0f, 6.1f, -0.31f), new Vector2(1.05f, 0.98f), 0f, logo);
            }
            Sign(root, "G\nE\nN\nE\nS\nI\nS", new Vector3(pylon.x, 3.3f, pylon.z + 0.31f), 180f, 0.055f, Color.Lerp(Gold, Color.white, 0.3f));
            Sign(root, "C\nA\nR\nD\nS", new Vector3(pylon.x, 3.6f, pylon.z - 0.31f), 0f, 0.07f, Color.Lerp(Gold, Color.white, 0.3f));

            // Where the teleport lands: in the lot, looking at the entrance.
            var arrival = new GameObject(CardShopName + " - Arrival Spot").transform;
            arrival.SetParent(root, false);
            arrival.localPosition = new Vector3(-12f, 0.1f, 24.4f);
            arrival.localRotation = Quaternion.LookRotation(Vector3.back);

            Debug.Log($"Duel: Genesis built the {CardShopName} showroom at {ground}: 8 duel tables, 2 trade tables, {bays} parking bays ({accessible} accessible); cleared {cleared} props off the lot.");
            return root.gameObject;
        }

        /// <summary>Places the Dragon Gate Inn (daydev, CC BY 4.0) as the shop's front: its round base, dirt and outer rim are
        /// hidden, it is scaled up so the inn is ~15 m tall, and its back sits against the hall's front wall.</summary>
        private static void PlaceInn(Transform root, float hallFrontZ, Material gold)
        {
            GameObject prefab = GenesisSketchfabModels.Load("DragonGateInn");
            if (prefab == null) { Debug.LogWarning("Duel: Genesis: Dragon Gate Inn model not found (Production Assets > Set Up Sketchfab Models)."); return; }
            var inn = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            inn.name = "Dragon Gate Inn (by daydev, CC BY 4.0)";
            inn.transform.localRotation = Quaternion.identity;
            inn.transform.localScale = Vector3.one * 2f;
            foreach (Renderer r in inn.GetComponentsInChildren<Renderer>(true))
            {
                string m = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (m.StartsWith("Dirt") || m.StartsWith("Rock")) r.gameObject.SetActive(false);
            }
            Physics.SyncTransforms();
            // The inn itself (walls, roofs, windows) decides the placement; trees and signs come along.
            string[] building = { "WallRed", "Roof", "ChineseWindows", "Gold", "LionDoors", "GreenDark", "Wood_red" };
            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in inn.GetComponentsInChildren<Renderer>())
            {
                string m = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (!building.Any(k => m.StartsWith(k))) continue;
                Bounds lb = ToLocal(root, r.bounds);
                if (!any) { b = lb; any = true; } else b.Encapsulate(lb);
            }
            Bounds all = LocalBounds(root, inn);
            if (!any) b = all;
            inn.transform.localPosition += new Vector3(-b.center.x, -all.min.y, hallFrontZ + 0.4f - b.min.z);
            // A solid block for the building so players walk around it, not through it.
            Physics.SyncTransforms();
            b = new Bounds(b.center + new Vector3(-b.center.x, -all.min.y, hallFrontZ + 0.4f - b.min.z), b.size);
            var block = new GameObject("Inn Collider");
            block.transform.SetParent(root, false);
            block.transform.localPosition = b.center;
            block.AddComponent<BoxCollider>().size = new Vector3(b.size.x * 0.85f, b.size.y, b.size.z * 0.85f);
            // Our sign across the inn front.
            Vector3 front = new Vector3(0f, Mathf.Min(6.2f, b.max.y * 0.45f), b.max.z + 0.3f);
            Box(root, "Inn Sign Board", front, new Vector3(9f, 1.5f, 0.2f), Mat("CS Charcoal", new Color(0.055f, 0.06f, 0.075f), metallic: 0.55f, smooth: 0.75f), collider: false);
            Box(root, "Inn Sign Glow", front + new Vector3(0f, -0.8f, 0.05f), new Vector3(9f, 0.07f, 0.06f), gold, collider: false);
            Sign(root, "GENESIS  CARDS", front + new Vector3(0f, 0f, 0.11f), 180f, 0.09f, Color.Lerp(Gold, Color.white, 0.3f));
        }

        private static Bounds ToLocal(Transform space, Bounds world)
        {
            Vector3 c = world.center, e = world.extents;
            var b = new Bounds(space.InverseTransformPoint(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(space.InverseTransformPoint(c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return b;
        }

        private static Bounds LocalBounds(Transform space, GameObject go)
        {
            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                Bounds lb = ToLocal(space, r.bounds);
                if (!any) { b = lb; any = true; } else b.Encapsulate(lb);
            }
            return b;
        }

        private static GameObject LoadNature(string model) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(GenesisAssetDownloads.NatureKitFolder + "/" + model + ".fbx");

        /// <summary>A glass display case on a wood base with three lit cards inside.</summary>
        private static void Showcase(Transform root, Vector3 foot, Material wood, Material trim, Material glass, Material glow, Material a, Material b, Material c)
        {
            Box(root, "Showcase Base", foot + new Vector3(0f, 0.45f, 0f), new Vector3(3f, 0.9f, 1.1f), wood);
            Box(root, "Showcase Trim", foot + new Vector3(0f, 0.92f, 0f), new Vector3(3.04f, 0.05f, 1.14f), trim, collider: false);
            Box(root, "Showcase Glass", foot + new Vector3(0f, 1.3f, 0f), new Vector3(3f, 0.7f, 1.1f), glass, collider: false);
            Box(root, "Showcase Glow", foot + new Vector3(0f, 0.95f, 0f), new Vector3(2.9f, 0.02f, 1f), glow, collider: false);
            Material[] faces = { a, b, c };
            for (int k = 0; k < 3; k++)
            {
                var q = Quad(root, "Showcase Card", foot + new Vector3(-0.95f + k * 0.95f, 1.2f, 0.15f), new Vector2(0.42f, 0.6f), 180f, faces[k]);
                q.transform.localRotation *= Quaternion.Euler(-20f, 0f, 0f);
            }
        }

        private static void ShopTable(Transform root, string name, Vector3 local, float yaw, int seed)
        {
            var table = new GameObject(name);
            table.transform.SetParent(root, false);
            table.transform.localPosition = local;
            table.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var decor = table.AddComponent<DuelGenesis.Dueling.AmbientDuelTable>();
            decor.seed = seed;
            decor.dealCards = seed % 2 == 0;
            var col = table.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.38f, 0f);
            col.size = new Vector3(1.24f, 0.76f, 0.94f);
            var duel = table.AddComponent<DuelGenesis.Dueling.CityDuelTable>();
            duel.opponentSeed = seed * 37 + 11;
            duel.tableName = "Card Shop " + name;
        }

        /// <summary>Removes placed models (trees, plants, lamps, benches) standing on the lot; ground and pavements stay.</summary>
        private static int ClearLot(Transform city, Transform shop, float x0, float x1, float z0, float z1)
        {
            var doomed = new HashSet<GameObject>();
            foreach (Renderer r in city.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(shop)) continue;
                GameObject top = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
                if (top == null || top.transform == city) continue;
                if (top.name.Contains(RootName)) continue;   // never the Duel Center
                Vector3 p = shop.InverseTransformPoint(top.transform.position);
                Bounds b = r.bounds;
                Vector3 c = shop.InverseTransformPoint(b.center);
                if (c.x > x0 && c.x < x1 && c.z > z0 && c.z < z1 || p.x > x0 && p.x < x1 && p.z > z0 && p.z < z1) doomed.Add(top);
            }
            foreach (GameObject g in doomed) Object.DestroyImmediate(g);
            return doomed.Count;
        }
    }
}
#endif
