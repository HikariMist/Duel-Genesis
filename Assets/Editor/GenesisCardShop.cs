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
    ///   - double-height hall behind a full glass curtain wall, big white canopy with LED edge and downlights,
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
        public static bool InCardShopLot(Vector2 world) => world.x > 55f && world.x < 101f && world.y > 7f && world.y < 68f;

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
            int cleared = ClearLot(city, root, -22f, 22f, -13.5f, 45f);

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

            // =========================================================== building shell
            Box(root, "Showroom Floor", new Vector3(0f, 0.03f, 0f), new Vector3(ShopW * 2f, 0.06f, ShopD * 2f), floor);
            Box(root, "Back Wall", new Vector3(0f, ShopH * 0.5f, -ShopD + 0.15f), new Vector3(ShopW * 2f, ShopH, 0.3f), white);
            Box(root, "West Wall", new Vector3(-ShopW + 0.15f, ShopH * 0.5f, 0f), new Vector3(0.3f, ShopH, ShopD * 2f), white);
            Box(root, "East Wall", new Vector3(ShopW - 0.15f, ShopH * 0.5f, 0f), new Vector3(0.3f, ShopH, ShopD * 2f), white);
            // Charcoal cladding outside the side walls and a plinth band.
            foreach (float s in new[] { -1f, 1f })
            {
                Box(root, "Side Cladding", new Vector3(s * (ShopW + 0.05f), ShopH * 0.5f, 0f), new Vector3(0.12f, ShopH, ShopD * 2f), charcoal, collider: false);
                Box(root, "Corner Fin", new Vector3(s * (ShopW + 0.1f), ShopH * 0.5f, ShopD + 0.1f), new Vector3(0.35f, ShopH + 0.4f, 0.35f), charcoal);
                Box(root, "Corner Fin Glow", new Vector3(s * (ShopW + 0.1f), ShopH * 0.5f, ShopD + 0.3f), new Vector3(0.08f, ShopH, 0.05f), magenta, collider: false);
            }
            Box(root, "Back Cladding", new Vector3(0f, ShopH * 0.5f, -ShopD - 0.05f), new Vector3(ShopW * 2f, ShopH, 0.12f), charcoal, collider: false);
            Box(root, "Roof", new Vector3(0f, ShopH + 0.2f, 0f), new Vector3(ShopW * 2f + 0.4f, 0.4f, ShopD * 2f + 0.4f), white);

            // ---- glass curtain wall with a central entrance, black mullions and a transom
            float doorHalf = 2.4f, doorH = 3.3f;
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = doorHalf, x1 = ShopW - 0.3f;
                Box(root, "Curtain Glass", new Vector3(s * (x0 + x1) * 0.5f, 4f, ShopD), new Vector3(x1 - x0, 8f, 0.06f), glass);
            }
            Box(root, "Curtain Glass Over Door", new Vector3(0f, (doorH + 8f) * 0.5f, ShopD), new Vector3(doorHalf * 2f, 8f - doorH, 0.06f), glass);
            for (float x = -ShopW + 3f; x <= ShopW - 2.9f; x += 3f)
                if (Mathf.Abs(x) > doorHalf + 0.2f)
                    Box(root, "Mullion", new Vector3(x, 4f, ShopD + 0.05f), new Vector3(0.1f, 8f, 0.18f), charcoal, collider: false);
            Box(root, "Transom", new Vector3(0f, 4.2f, ShopD + 0.05f), new Vector3(ShopW * 2f - 0.6f, 0.12f, 0.18f), charcoal, collider: false);
            Box(root, "Sill", new Vector3(0f, 0.08f, ShopD + 0.05f), new Vector3(ShopW * 2f - 0.6f, 0.16f, 0.25f), charcoal, collider: false);
            // Entrance: frame, open sliding doors parked to the sides, a glowing threshold.
            Box(root, "Door Frame Head", new Vector3(0f, doorH, ShopD + 0.08f), new Vector3(doorHalf * 2f + 0.3f, 0.25f, 0.3f), charcoal, collider: false);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(root, "Door Jamb", new Vector3(s * (doorHalf + 0.05f), doorH * 0.5f, ShopD + 0.08f), new Vector3(0.15f, doorH, 0.3f), charcoal);
                Box(root, "Sliding Door (open)", new Vector3(s * (doorHalf + 1.1f), doorH * 0.5f - 0.05f, ShopD - 0.12f), new Vector3(2.1f, doorH - 0.2f, 0.05f), glass, collider: false);
            }
            Box(root, "Entrance Threshold", new Vector3(0f, 0.065f, ShopD - 0.4f), new Vector3(doorHalf * 2f, 0.01f, 0.8f), cyan, collider: false);

            // ---- canopy: a deep white slab over the storefront, LED edge, downlights
            Box(root, "Canopy", new Vector3(0f, ShopH - 0.3f, ShopD + 2.4f), new Vector3(ShopW * 2f + 1.2f, 0.45f, 4.8f), white, collider: false);
            Box(root, "Canopy LED Edge", new Vector3(0f, ShopH - 0.55f, ShopD + 4.8f), new Vector3(ShopW * 2f + 1.2f, 0.06f, 0.06f), cyan, collider: false);
            for (float x = -ShopW + 1.5f; x <= ShopW - 1.4f; x += 3f)
                Box(root, "Canopy Downlight", new Vector3(x, ShopH - 0.54f, ShopD + 2.6f), new Vector3(0.45f, 0.02f, 0.45f), downlight, collider: false);
            PointLight(root, "Canopy Light W", new Vector3(-9f, ShopH - 1.2f, ShopD + 2.5f), 14f, 10f, new Color(1f, 0.95f, 0.88f));
            PointLight(root, "Canopy Light E", new Vector3(9f, ShopH - 1.2f, ShopD + 2.5f), 14f, 10f, new Color(1f, 0.95f, 0.88f));
            // Fascia band on the canopy front with the shop name and the logo.
            Box(root, "Fascia", new Vector3(0f, ShopH + 0.35f, ShopD + 4.75f), new Vector3(ShopW * 2f + 1.2f, 1.4f, 0.2f), charcoal, collider: false);
            Box(root, "Fascia Glow", new Vector3(0f, ShopH - 0.32f, ShopD + 4.88f), new Vector3(ShopW * 2f + 1.2f, 0.05f, 0.04f), magenta, collider: false);
            Sign(root, "GENESIS  CARDS", new Vector3(2.2f, ShopH + 0.37f, ShopD + 4.87f), 180f, 0.11f, Color.Lerp(Gold, Color.white, 0.35f));
            if (logo != null) Quad(root, "Fascia Logo", new Vector3(-11.5f, ShopH + 0.37f, ShopD + 4.87f), new Vector2(1.3f * 1.075f, 1.3f), 180f, logo);

            // ---- front walk under the canopy, planters by the door
            Box(root, "Front Walk", new Vector3(0f, 0.04f, ShopD + 1.6f), new Vector3(ShopW * 2f + 2f, 0.08f, 3.2f), paving);
            Box(root, "Front Curb", new Vector3(0f, 0.09f, ShopD + 3.25f), new Vector3(ShopW * 2f + 2f, 0.18f, 0.2f), concrete);
            GameObject pot = LoadNature("pot_large"), bush = LoadNature("plant_bushDetailed");
            foreach (float x in new[] { -4.2f, 4.2f, -15f, 15f })
            {
                if (pot != null) PlaceSized(root, pot, "Entrance Planter", new Vector3(x, 0.08f, ShopD + 1.6f), 0f, 0.8f);
                if (bush != null) PlaceSized(root, bush, "Entrance Planter Bush", new Vector3(x, 0.8f, ShopD + 1.6f), 45f, 1.1f);
            }

            // =========================================================== inside
            // Ceiling: long linear lights and warm fill lights.
            foreach (float x in new[] { -12f, -6f, 0f, 6f, 12f })
                Box(root, "Linear Light", new Vector3(x, ShopH - 0.05f, -1f), new Vector3(0.25f, 0.04f, 18f), downlight, collider: false);
            foreach (float x in new[] { -11f, 0f, 11f })
                foreach (float z in new[] { -6f, 4f })
                    PointLight(root, "Showroom Light", new Vector3(x, ShopH - 1.5f, z), 14f, 9f, new Color(1f, 0.96f, 0.9f));

            // Hero plinth: a lit dais with a giant rotating card in front of the entrance.
            Vector3 hero = new Vector3(0f, 0f, 5.2f);
            Box(root, "Hero Plinth", hero + new Vector3(0f, 0.25f, 0f), new Vector3(3.6f, 0.5f, 3.6f), charcoal);
            Box(root, "Hero Plinth Glow", hero + new Vector3(0f, 0.52f, 0f), new Vector3(3.64f, 0.04f, 3.64f), cyan, collider: false);
            var spinner = new GameObject("Hero Card").transform;
            spinner.SetParent(root, false);
            spinner.localPosition = hero + new Vector3(0f, 2.3f, 0f);
            spinner.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 25f, 0f);
            Box(spinner, "Card Frame", Vector3.zero, new Vector3(2.2f, 3.2f, 0.08f), gold, collider: false);
            Quad(spinner, "Card Face A", new Vector3(0f, 0f, -0.05f), new Vector2(2f, 2.95f), 0f, screen).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 3;
            Quad(spinner, "Card Face B", new Vector3(0f, 0f, 0.05f), new Vector2(2f, 2.95f), 180f, screen).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 11;
            SpotLight(root, "Hero Spot W", new Vector3(-4f, ShopH - 0.4f, 8f), hero + Vector3.up * 2f, new Color(1f, 0.92f, 0.8f));
            SpotLight(root, "Hero Spot E", new Vector3(4f, ShopH - 0.4f, 8f), hero + Vector3.up * 2f, new Color(0.8f, 0.9f, 1f));
            // Cyan LED path from the door to the plinth.
            Box(root, "Floor Path L", new Vector3(-1.2f, 0.065f, 9.3f), new Vector3(0.06f, 0.01f, 5f), cyan, collider: false);
            Box(root, "Floor Path R", new Vector3(1.2f, 0.065f, 9.3f), new Vector3(0.06f, 0.01f, 5f), cyan, collider: false);

            // Glass showcases: two rows either side of the entrance.
            int card = 0;
            foreach (float s in new[] { -1f, 1f })
                foreach (float x in new[] { 6.5f, 10.5f, 14.5f })
                    Showcase(root, new Vector3(s * x, 0f, 6.8f), wood, charcoal, glass, cyan, Pack(card++), Pack(card++), Pack(card++));
            HangingSign(root, "RARE SINGLES", new Vector3(-10.5f, 5.2f, 6.8f), board, gold, 0f, 7f);
            HangingSign(root, "NEW RELEASES", new Vector3(10.5f, 5.2f, 6.8f), board, magenta, 0f, 7f);

            // Pack walls on both side walls.
            PackWall(root, new Vector3(-ShopW + 0.3f, 0f, 1.2f), 90f, board);
            PackWall(root, new Vector3(ShopW - 0.3f, 0f, 1.2f), -90f, board);

            // Service counter with a video wall behind it.
            Box(root, "Counter", new Vector3(0f, 0.55f, -7.2f), new Vector3(9f, 1.1f, 1.1f), white);
            Box(root, "Counter Top", new Vector3(0f, 1.12f, -7.2f), new Vector3(9.2f, 0.06f, 1.25f), charcoal, collider: false);
            Box(root, "Counter Glow", new Vector3(0f, 0.25f, -6.63f), new Vector3(9f, 0.05f, 0.02f), cyan, collider: false);
            Box(root, "Counter Case", new Vector3(0f, 1.35f, -7.2f), new Vector3(4f, 0.4f, 0.9f), glass, collider: false);
            for (int i = 0; i < 6; i++)
                Quad(root, "Counter Case Pack", new Vector3(-1.6f + i * 0.64f, 1.35f, -6.95f), new Vector2(0.24f, 0.34f), 180f, Pack(i + 4));
            Box(root, "Register", new Vector3(3.4f, 1.3f, -7.3f), new Vector3(0.55f, 0.35f, 0.45f), metal, collider: false);
            VideoWall(root, "Video Wall", new Vector3(0f, 4.6f, -ShopD + 0.32f), 180f, 2.6f, metal, screen, 4);
            Sign(root, "PACKS  ·  SINGLES  ·  SUPPLIES", new Vector3(0f, 7.6f, -ShopD + 0.32f), 180f, 0.06f, Cyan);

            // Duel zones in the back corners under pendant lights, trade lounge between.
            int seed = 700;
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float x in new[] { 9f, 14f })
                    foreach (float z in new[] { -8.5f, -4.2f })
                    {
                        ShopTable(root, $"Table {seed - 699}", new Vector3(s * x, 0.06f, z), 0f, seed++);
                        Box(root, "Pendant Cable", new Vector3(s * x, (ShopH + 3.2f) * 0.5f, z), new Vector3(0.02f, ShopH - 3.2f, 0.02f), metal, collider: false);
                        Box(root, "Pendant", new Vector3(s * x, 3.1f, z), new Vector3(1.4f, 0.12f, 0.5f), charcoal, collider: false);
                        Box(root, "Pendant Glow", new Vector3(s * x, 3.03f, z), new Vector3(1.3f, 0.02f, 0.42f), downlight, collider: false);
                    }
                Box(root, "Duel Zone Rug", new Vector3(s * 11.5f, 0.065f, -6.35f), new Vector3(9f, 0.01f, 7.5f), s < 0 ? magenta : cyan, collider: false)
                    .GetComponent<Renderer>().sharedMaterial = Mat(s < 0 ? "CS Rug Magenta" : "CS Rug Cyan", (s < 0 ? Magenta : Cyan) * 0.35f, smooth: 0.1f);
            }
            HangingSign(root, "FREE PLAY", new Vector3(-11.5f, 5.4f, -2f), board, magenta, 0f, 5f);
            HangingSign(root, "TOURNAMENT", new Vector3(11.5f, 5.4f, -2f), board, cyan, 0f, 5f);
            GameObject kTable = LoadKenney("tableRound"), kChair = LoadKenney("chairCushion");
            if (kTable != null && kChair != null)
            {
                TradeTable(root, "Trade Table W", new Vector3(-4.5f, 0.06f, -1.2f), kTable, kChair);
                TradeTable(root, "Trade Table E", new Vector3(4.5f, 0.06f, -1.2f), kTable, kChair);
            }
            GameObject plant = LoadKenney("pottedPlant");
            if (plant != null)
                foreach (Vector3 p in new[] { new Vector3(-17f, 0.06f, -11f), new Vector3(17f, 0.06f, -11f), new Vector3(-17f, 0.06f, 11f), new Vector3(17f, 0.06f, 11f) })
                    PlaceSized(root, plant, "Corner Plant", p, 0f, 1.6f);

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
            arrival.localPosition = new Vector3(0f, 0.1f, 24.4f);
            arrival.localRotation = Quaternion.LookRotation(Vector3.back);

            Debug.Log($"Duel: Genesis built the {CardShopName} showroom at {ground}: 8 duel tables, 2 trade tables, {bays} parking bays ({accessible} accessible); cleared {cleared} props off the lot.");
            return root.gameObject;
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
