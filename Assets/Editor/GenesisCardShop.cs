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
    /// The Genesis Card Shop: a two-storey corner card shop on the park block east of the plaza, fronting the
    /// east boulevard. Built from our own geometry (nothing to license), reusing the Duel Center's materials.
    ///   - cut-off corner entrance under the first-floor canopy, facing the plaza,
    ///   - striped awning over a 21 m display-window storefront with packs in the windows,
    ///   - ground floor: shop counter with a pack wall, glass display cases, deck-box shelves, card screens,
    ///     six playable duel tables and a trade table,
    ///   - stairs up to a first-floor tournament room (six more tables, big screen) with a glass front that
    ///     opens onto a roof terrace (two trade tables),
    ///   - blade sign on the corner and a spinning double-sided card screen on the roof.
    /// Local +Z is the street front. Re-running it replaces the old shop and clears plants and props off its lot.
    /// </summary>
    public static partial class GenesisDuelCenter
    {
        public const string CardShopName = "Genesis Card Shop";
        public static readonly Vector3 CardShopSpot = new Vector3(80f, 0f, 38f);   // NE park block, front on the east boulevard
        public const float CardShopYaw = 180f;                                      // local +Z (front) faces world -Z

        /// <summary>The shop's lot in world space (footprint, apron and a margin), kept clear by the nature planter.</summary>
        public static bool InCardShopLot(Vector2 world) => world.x > 62f && world.x < 97f && world.y > 19f && world.y < 51f;

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

        public static GameObject BuildCardShop(Transform city, Vector3 ground, float yaw)
        {
            Transform old = city.Find(CardShopName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(CardShopName).transform;
            root.SetParent(city, false);
            root.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            int cleared = ClearLot(city, root, -14.5f, 14.5f, -11.5f, 17.5f);

            // ---- materials (shared with the Duel Center where the look matches)
            Material facade = TexMat("CS Facade", "concrete_panels", new Color(0.95f, 0.92f, 0.88f), 0.25f, 3f);
            Material wood = TexMat("DC Wood", "wooden_panels", new Color(0.95f, 0.9f, 0.85f), 0.35f, 2f);
            Material floorWood = TexMat("CS Floor", "wooden_panels", new Color(0.78f, 0.66f, 0.54f), 0.45f, 1.5f);
            Material navy = Mat("CS Navy", new Color(0.07f, 0.09f, 0.2f), smooth: 0.5f);
            Material metal = Mat("DC Dark Metal", new Color(0.1f, 0.11f, 0.14f), metallic: 0.8f, smooth: 0.6f);
            Material paving = Mat("DC Plaza Paving", new Color(0.55f, 0.56f, 0.6f), smooth: 0.35f);
            Material glass = GlassMat();
            Material cyan = Mat("DC Glow Cyan", Cyan, emission: Cyan * 2.2f);
            Material magenta = Mat("DC Glow Magenta", Magenta, emission: Magenta * 2f);
            Material gold = Mat("DC Glow Gold", Gold, emission: Gold * 1.6f);
            Material ceilingLight = Mat("DC Ceiling Light", Color.white, emission: new Color(1f, 0.96f, 0.9f) * 1.8f);
            Material screen = Mat("DC Screen", Color.white, smooth: 0.8f, emission: Color.white * 1.4f);
            Material board = Mat("DC Booth Board", new Color(0.05f, 0.06f, 0.1f), smooth: 0.5f);
            Material awningRed = Mat("CS Awning Red", new Color(0.72f, 0.08f, 0.14f), smooth: 0.3f);
            Material awningCream = Mat("CS Awning Cream", new Color(0.95f, 0.91f, 0.82f), smooth: 0.3f);
            Material logo = LogoMat();
            string[] packIds = { "monster", "spell", "trap", "dark", "light", "earth", "fire", "water", "wind" };
            var packs = new List<Material>();
            foreach (string id in packIds)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/DuelGenesis/Packs/pack_{id}.jpg");
                if (tex != null) packs.Add(Mat("DC Pack " + id, Color.white, tex: tex, emission: Color.white * 0.35f));
            }
            Material Pack(int i) => packs.Count > 0 ? packs[i % packs.Count] : gold;

            // ---- ground, floors and the apron out to the boulevard
            Box(root, "Shop Floor", new Vector3(0f, 0.025f, 0f), new Vector3(26f, 0.05f, 20f), floorWood);
            Box(root, "Shop Apron", new Vector3(0.5f, 0.015f, 13.5f), new Vector3(27f, 0.03f, 7f), paving);
            Box(root, "Entrance Inlay", new Vector3(10.2f, 0.055f, 7.2f), new Vector3(0.12f, 0.02f, 5f), cyan, collider: false).transform.localRotation = Quaternion.Euler(0f, -45f, 0f);

            // ---- outer walls: full height at the back half, ground floor only under the terrace
            Box(root, "Back Wall", new Vector3(0f, 4.75f, -9.85f), new Vector3(26f, 9.5f, 0.3f), facade);
            Box(root, "West Wall", new Vector3(-12.85f, 2.35f, 0f), new Vector3(0.3f, 4.7f, 20f), facade);
            Box(root, "West Wall Upper", new Vector3(-12.85f, 7.25f, -3f), new Vector3(0.3f, 4.5f, 14f), facade);
            Box(root, "East Wall", new Vector3(12.85f, 2.35f, -2.5f), new Vector3(0.3f, 4.7f, 15f), facade);
            Box(root, "East Wall Upper", new Vector3(12.85f, 7.25f, -3f), new Vector3(0.3f, 4.5f, 14f), facade);

            // ---- the cut-off corner entrance: two pillars and a sign header, open below
            Box(root, "Entrance Pillar E", new Vector3(12.75f, 2.35f, 5f), new Vector3(0.5f, 4.7f, 0.5f), metal);
            Box(root, "Entrance Pillar N", new Vector3(8f, 2.35f, 9.75f), new Vector3(0.5f, 4.7f, 0.5f), metal);
            Box(root, "Entrance Header", new Vector3(10.4f, 4.15f, 7.4f), new Vector3(7.1f, 1.1f, 0.3f), navy, collider: false).transform.localRotation = Quaternion.Euler(0f, -135f, 0f);
            Box(root, "Entrance Header Glow", new Vector3(10.5f, 3.58f, 7.5f), new Vector3(7.1f, 0.06f, 0.34f), cyan, collider: false).transform.localRotation = Quaternion.Euler(0f, -135f, 0f);
            Sign(root, "WELCOME, DUELISTS", new Vector3(10.58f, 4.15f, 7.58f), -135f, 0.045f, Color.Lerp(Gold, Color.white, 0.3f));

            // ---- storefront: kick plate, display glass with mullions, header band with the shop name
            Box(root, "Storefront Kick", new Vector3(-2.5f, 0.3f, 9.85f), new Vector3(21f, 0.6f, 0.3f), navy);
            Box(root, "Storefront Glass", new Vector3(-2.5f, 1.9f, 9.9f), new Vector3(21f, 2.6f, 0.08f), glass);
            for (float x = -13f; x <= 8.01f; x += 3f)
                Box(root, "Mullion", new Vector3(x, 1.9f, 9.93f), new Vector3(0.12f, 2.6f, 0.14f), metal, collider: false);
            Box(root, "Storefront Header", new Vector3(-2.5f, 3.95f, 9.85f), new Vector3(21f, 1.5f, 0.35f), navy);
            Box(root, "Header Glow Low", new Vector3(-2.5f, 3.23f, 10.05f), new Vector3(21f, 0.06f, 0.05f), cyan, collider: false);
            Box(root, "Header Glow High", new Vector3(-2.5f, 4.67f, 10.05f), new Vector3(21f, 0.06f, 0.05f), magenta, collider: false);
            Sign(root, "GENESIS CARD SHOP", new Vector3(-2.5f, 3.98f, 10.06f), 180f, 0.1f, Color.Lerp(Gold, Color.white, 0.35f));

            // Striped awning over the windows.
            for (int i = 0; i < 21; i++)
            {
                Material m = i % 2 == 0 ? awningRed : awningCream;
                float x = -12.5f + i;
                Box(root, "Awning Stripe", new Vector3(x, 3.0f, 10.75f), new Vector3(1f, 0.05f, 1.8f), m, collider: false).transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                Box(root, "Awning Valance", new Vector3(x, 2.55f, 11.62f), new Vector3(1f, 0.28f, 0.04f), m, collider: false);
            }

            // Window display: a riser of booster packs facing the street.
            Box(root, "Window Riser", new Vector3(-2f, 0.35f, 9.2f), new Vector3(18f, 0.7f, 0.9f), navy);
            for (int i = 0; i < 9; i++)
                Quad(root, "Window Pack", new Vector3(-10f + i * 2f, 1.2f, 9.35f), new Vector2(0.6f, 0.9f), 180f, Pack(i));
            PointLight(root, "Window Light", new Vector3(-2f, 2.9f, 8.6f), 10f, 4f, new Color(1f, 0.93f, 0.82f));

            // ---- first floor slab (ceiling below, terrace and tournament room above), with the stair well
            Box(root, "First Floor Slab", new Vector3(0.8f, 4.85f, 0f), new Vector3(24.4f, 0.3f, 20f), facade);
            Box(root, "First Floor Slab W1", new Vector3(-12.2f, 4.85f, -6f), new Vector3(1.6f, 0.3f, 8f), facade);
            Box(root, "First Floor Slab W2", new Vector3(-12.2f, 4.85f, 7f), new Vector3(1.6f, 0.3f, 6f), facade);
            Box(root, "Slab Fascia Glow", new Vector3(0f, 4.72f, 10.03f), new Vector3(26f, 0.06f, 0.06f), magenta, collider: false);
            Box(root, "Slab Fascia Glow E", new Vector3(13.03f, 4.72f, 0f), new Vector3(0.06f, 0.06f, 20f), cyan, collider: false);
            foreach (float lx in new[] { -6.5f, -0.5f, 5.5f })
                foreach (float lz in new[] { -5.5f, 0f, 5.5f })
                    Box(root, "Ceiling Light", new Vector3(lx, 4.68f, lz), new Vector3(2.4f, 0.04f, 0.6f), ceilingLight, collider: false);
            foreach (Vector3 p in new[] { new Vector3(-6f, 4f, -4f), new Vector3(-6f, 4f, 3.5f), new Vector3(5f, 4f, -3.5f), new Vector3(5f, 4f, 3.5f) })
                PointLight(root, "Shop Light", p, 11f, 12f, new Color(1f, 0.95f, 0.88f));

            // ---- stairs against the west wall, rising from the front (z 4) to the first floor (z -2), boxed in
            for (int i = 0; i < 20; i++)
            {
                float top = (i + 1) * 0.25f;
                Box(root, "Stair Step", new Vector3(-12.2f, top * 0.5f, 4f - 0.3f * i - 0.15f), new Vector3(1.4f, top, 0.3f), wood);
            }
            Box(root, "Stair Wall", new Vector3(-11.42f, 2.35f, 1f), new Vector3(0.12f, 4.7f, 6f), wood);
            Sign(root, "TOURNAMENT ROOM  ^", new Vector3(-11.34f, 2.6f, 2.2f), -90f, 0.022f, Cyan);
            Box(root, "Stair Rail Upper", new Vector3(-11.4f, 5.55f, 1f), new Vector3(0.06f, 1.1f, 6f), glass);

            // ---- shop floor: counter and pack wall at the back
            Box(root, "Shop Counter", new Vector3(7f, 0.55f, -5.8f), new Vector3(10f, 1.05f, 0.9f), wood);
            Box(root, "Counter Case", new Vector3(7f, 1.25f, -5.8f), new Vector3(10f, 0.35f, 0.9f), glass, collider: false);
            Box(root, "Counter Glow", new Vector3(7f, 0.9f, -5.33f), new Vector3(10f, 0.05f, 0.02f), cyan, collider: false);
            for (int i = 0; i < 8; i++)
                Quad(root, "Counter Case Pack", new Vector3(3f + i * 1.15f, 1.25f, -5.55f), new Vector2(0.24f, 0.34f), 180f, Pack(i + 2));
            Box(root, "Register", new Vector3(10.5f, 1.25f, -6f), new Vector3(0.5f, 0.35f, 0.4f), metal, collider: false);
            PackWall(root, new Vector3(7f, 0f, -9.7f), 0f, board);
            HangingSign(root, "PACKS & SINGLES", new Vector3(7f, 3.9f, -5.8f), board, cyan, 0f, 6f);

            // Glass display cases along the east wall.
            foreach (float z in new[] { -3f, 0f, 3f })
            {
                Box(root, "Display Case Base", new Vector3(11.9f, 0.45f, z), new Vector3(1.4f, 0.9f, 2.2f), wood);
                Box(root, "Display Case Glass", new Vector3(11.9f, 1.2f, z), new Vector3(1.4f, 0.6f, 2.2f), glass, collider: false);
                for (int k = 0; k < 3; k++)
                    Quad(root, "Display Card", new Vector3(11.7f, 1.2f, z - 0.65f + k * 0.65f), new Vector2(0.34f, 0.5f), 90f, Pack((int)(z + 3) + k));
            }
            HangingSign(root, "RARE SINGLES", new Vector3(11.2f, 3.9f, 0f), board, gold, 90f, 5f);

            // Deck-box shelves and card screens on the back wall.
            Box(root, "Deck Shelf", new Vector3(-6f, 1.4f, -9.4f), new Vector3(8f, 2.8f, 0.6f), wood);
            var deckColours = new[] { Magenta, Cyan, Gold, new Color(0.9f, 0.15f, 0.15f), new Color(0.2f, 0.8f, 0.3f), new Color(0.95f, 0.95f, 0.95f) };
            var rng = new System.Random(515);
            foreach (float y in new[] { 0.7f, 1.4f, 2.1f })
                for (int i = 0; i < 18; i++)
                {
                    int c = rng.Next(deckColours.Length);
                    Box(root, "Deck Box", new Vector3(-9.6f + i * 0.42f, y + 0.2f, -8.98f), new Vector3(0.3f, 0.4f, 0.25f),
                        Mat("CS Deck Box " + c, deckColours[c], smooth: 0.7f), collider: false);
                }
            VideoWall(root, "Card Screen W", new Vector3(-9f, 3.75f, -9.62f), 180f, 1.3f, metal, screen, 1);
            VideoWall(root, "Card Screen E", new Vector3(-3f, 3.75f, -9.62f), 180f, 1.3f, metal, screen, 2);

            // Duel tables and a trade table.
            int seed = 700;
            foreach (float x in new[] { -8.5f, -4f })
                foreach (float z in new[] { -5.5f, -1.5f, 2.5f })
                    ShopTable(root, $"Table {seed - 699}", new Vector3(x, 0.05f, z), 0f, seed++);
            GameObject kTable = LoadKenney("tableRound"), kChair = LoadKenney("chairCushion");
            if (kTable != null && kChair != null) TradeTable(root, "Trade Table", new Vector3(3.5f, 0.05f, 2f), kTable, kChair);
            HangingSign(root, "FREE PLAY TABLES", new Vector3(-6.2f, 3.9f, -1.5f), board, magenta, 90f, 6f);

            // ---- first floor: tournament room behind a glass front, terrace outside
            Box(root, "Upper Glass W", new Vector3(-5f, 7.25f, 4f), new Vector3(16f, 4.5f, 0.08f), glass);
            Box(root, "Upper Glass E", new Vector3(9f, 7.25f, 4f), new Vector3(8f, 4.5f, 0.08f), glass);
            Box(root, "Upper Door Transom", new Vector3(4f, 8.6f, 4f), new Vector3(2f, 1.8f, 0.08f), glass);
            for (float x = -13f; x <= 13.01f; x += 2.6f)
                Box(root, "Upper Mullion", new Vector3(x, 7.25f, 4.03f), new Vector3(0.12f, 4.5f, 0.14f), metal, collider: false);
            Box(root, "Upper Roof", new Vector3(0f, 9.65f, -2.7f), new Vector3(26.6f, 0.3f, 14.6f), facade);
            Box(root, "Upper Roof Glow", new Vector3(0f, 9.48f, 4.58f), new Vector3(26.6f, 0.06f, 0.06f), cyan, collider: false);
            foreach (float lx in new[] { -6.5f, 0f, 6.5f })
                foreach (float lz in new[] { -7f, -2f })
                    Box(root, "Upper Ceiling Light", new Vector3(lx, 9.48f, lz), new Vector3(2.4f, 0.04f, 0.6f), ceilingLight, collider: false);
            foreach (Vector3 p in new[] { new Vector3(-6f, 8.8f, -5f), new Vector3(0f, 8.8f, -2f), new Vector3(6.5f, 8.8f, -5f) })
                PointLight(root, "Tournament Light", p, 11f, 12f, new Color(1f, 0.95f, 0.88f));
            VideoWall(root, "Tournament Screen", new Vector3(0f, 7.6f, -9.62f), 180f, 2.4f, metal, screen, 5);
            HangingSign(root, "TOURNAMENT ROOM", new Vector3(0f, 8.9f, 0.5f), board, magenta, 0f, 6f);
            foreach (float x in new[] { -6f, 0f, 6f })
                foreach (float z in new[] { -7f, -3.2f })
                    ShopTable(root, $"Tournament Table {seed - 699}", new Vector3(x, 5f, z), 0f, seed++);

            // Terrace: deck, glass balustrade, trade tables and planters.
            Box(root, "Terrace Deck", new Vector3(0f, 5.02f, 7f), new Vector3(26f, 0.04f, 6f), wood, collider: false);
            Box(root, "Terrace Rail N", new Vector3(0f, 5.55f, 9.9f), new Vector3(26f, 1.1f, 0.06f), glass);
            Box(root, "Terrace Rail E", new Vector3(12.9f, 5.55f, 7f), new Vector3(0.06f, 1.1f, 6f), glass);
            Box(root, "Terrace Rail W", new Vector3(-12.9f, 5.55f, 7f), new Vector3(0.06f, 1.1f, 6f), glass);
            Box(root, "Terrace Handrail", new Vector3(0f, 6.12f, 9.9f), new Vector3(26f, 0.06f, 0.1f), metal, collider: false);
            if (kTable != null && kChair != null)
            {
                TradeTable(root, "Terrace Table W", new Vector3(-6f, 5.05f, 7f), kTable, kChair);
                TradeTable(root, "Terrace Table E", new Vector3(0.5f, 5.05f, 7f), kTable, kChair);
            }
            GameObject plant = LoadKenney("pottedPlant");
            if (plant != null)
                foreach (float x in new[] { -11.5f, 7.5f, 11.5f })
                    PlaceSized(root, plant, "Terrace Planter", new Vector3(x, 5.05f, 8.8f), 0f, 1.1f);

            // ---- roof: sign band and a spinning double-sided card screen
            Box(root, "Roof Sign Board", new Vector3(0f, 10.6f, 3.9f), new Vector3(15f, 1.4f, 0.25f), navy, collider: false);
            Box(root, "Roof Sign Glow", new Vector3(0f, 9.93f, 4.05f), new Vector3(15f, 0.06f, 0.06f), gold, collider: false);
            Sign(root, "DUEL  ·  TRADE  ·  COLLECT", new Vector3(0f, 10.6f, 4.04f), 180f, 0.075f, Cyan);
            Box(root, "Card Pole", new Vector3(0f, 11.3f, -3f), new Vector3(0.3f, 3f, 0.3f), metal, collider: false);
            var spinner = new GameObject("Roof Card Screen").transform;
            spinner.SetParent(root, false);
            spinner.localPosition = new Vector3(0f, 14.4f, -3f);
            spinner.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 22f, 0f);
            Box(spinner, "Card Frame", Vector3.zero, new Vector3(3.3f, 3.3f, 0.14f), gold, collider: false);
            Quad(spinner, "Card Face A", new Vector3(0f, 0f, -0.08f), new Vector2(3f, 3f), 0f, screen).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 7;
            if (logo != null) Quad(spinner, "Card Face B", new Vector3(0f, 0f, 0.08f), new Vector2(3f * 1.075f, 3f), 180f, logo);
            else Quad(spinner, "Card Face B", new Vector3(0f, 0f, 0.08f), new Vector2(3f, 3f), 180f, screen).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 9;

            // Blade sign on the west corner, Akihabara style.
            Box(root, "Blade Sign", new Vector3(-12.6f, 7.8f, 10.8f), new Vector3(0.35f, 5f, 1.6f), navy, collider: false);
            Box(root, "Blade Sign Edge", new Vector3(-12.6f, 7.8f, 11.62f), new Vector3(0.4f, 5f, 0.05f), magenta, collider: false);
            Box(root, "Blade Sign Arm", new Vector3(-12.6f, 10.35f, 10.5f), new Vector3(0.1f, 0.1f, 1.4f), metal, collider: false);
            Sign(root, "C\nA\nR\nD\nS", new Vector3(-12.8f, 7.8f, 10.8f), 90f, 0.09f, Color.Lerp(Gold, Color.white, 0.3f));
            Sign(root, "C\nA\nR\nD\nS", new Vector3(-12.4f, 7.8f, 10.8f), -90f, 0.09f, Color.Lerp(Gold, Color.white, 0.3f));

            // Where the teleport lands: on the apron, looking at the corner entrance.
            var arrival = new GameObject(CardShopName + " - Arrival Spot").transform;
            arrival.SetParent(root, false);
            arrival.localPosition = new Vector3(12f, 0.1f, 14f);
            arrival.localRotation = Quaternion.LookRotation(new Vector3(-0.3f, 0f, -1f));

            Debug.Log($"Duel: Genesis built the {CardShopName} at {ground} (yaw {yaw:0}): 12 duel tables, 3 trade tables; cleared {cleared} props off the lot.");
            return root.gameObject;
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
