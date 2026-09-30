#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// The Genesis Card Vault: the city's flagship card shop, on the park block east of the plaza.
    ///   - a round glass rotunda (26 m across) on a marble plinth, with bronze mullions and a sweeping entrance canopy,
    ///   - a floating halo roof with a glass oculus over the central atrium and a light ring round its rim,
    ///   - crowning it, a 20 m "hand" of five giant fanned cards (real card art) that can be seen from across the city,
    ///   - inside: a hero card spinning under the oculus, curved booster and duel-mat counters (the shop terminals),
    ///     showcases, pack walls, vending machines and a ring of six playable duel tables,
    ///   - outside: a paved forecourt, the parking lot with driveways onto the boulevard, and a pylon sign.
    /// All curved parts are smooth, generated meshes (saved in Assets/Art/Generated/CardShop) with photo-scanned
    /// marble/concrete/wood textures. Local +Z is the street front. Re-running replaces the old shop.
    /// </summary>
    public static partial class GenesisDuelCenter
    {
        public const string CardShopName = "Genesis Card Shop";
        public static readonly Vector3 CardShopSpot = new Vector3(78f, 0f, 54f);   // NE park block, facing the east boulevard
        public const float CardShopYaw = 180f;                                      // local +Z (front) faces world -Z

        /// <summary>The shop and its parking lot in world space, kept clear by the nature planter.</summary>
        public static bool InCardShopLot(Vector2 world) => world.x > 55f && world.x < 101f && world.y > 7f && world.y < 93f;

        private const string VaultFolder = "Assets/Art/Generated/CardShop";
        private static readonly Vector3 VaultCentre = new Vector3(0f, 0f, -12f);
        private const float VaultR = 13f;       // glass drum radius
        private const float FloorY = 0.3f;      // top of the marble plinth
        private const float WallTop = 8.2f;     // drum height
        private const float DoorHalfAngle = 17f;

        [MenuItem("Duel Genesis/World/11. Build Genesis Card Shop (open city)")]
        public static void BuildCardShopMenu()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            GameObject shop = BuildCardShop(city.transform, CardShopSpot, CardShopYaw);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = shop;
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

        /// <summary>Local point on a circle round the vault centre; angle 0 = the street front (+Z), 90 = east (+X).</summary>
        private static Vector3 OnRing(float angleDeg, float r, float y = 0f)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return VaultCentre + new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
        }

        /// <summary>Yaw that makes a thing standing on the ring face the vault centre.</summary>
        private static float FacingCentre(float angleDeg) => angleDeg + 180f;

        public static GameObject BuildCardShop(Transform city, Vector3 ground, float yaw)
        {
            Transform old = city.Find(CardShopName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            EnsureFolder(VaultFolder);
            _meshCount = 0;

            var root = new GameObject(CardShopName).transform;
            root.SetParent(city, false);
            root.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            int cleared = ClearLot(city, root, -22f, 22f, -31f, 45f);

            // ---- materials (photo-scanned textures where it matters)
            Material marble = TexMat("CS Vault Marble", "marble_01", new Color(0.95f, 0.95f, 0.97f), 0.85f, 3f);
            Material paving = TexMat("CS Paving", "concrete_panels", new Color(0.82f, 0.82f, 0.84f), 0.3f, 2f);
            Material stone = TexMat("CS Vault Plinth", "concrete_panels", new Color(0.7f, 0.7f, 0.72f), 0.35f, 2f);
            Material wood = TexMat("DC Wood", "wooden_panels", new Color(0.95f, 0.9f, 0.85f), 0.45f, 2f);
            Material white = Mat("CS Vault White", new Color(0.94f, 0.94f, 0.95f), smooth: 0.7f);
            Material bronze = Mat("CS Vault Bronze", new Color(0.42f, 0.3f, 0.18f), metallic: 0.95f, smooth: 0.72f);
            Material charcoal = Mat("CS Charcoal", new Color(0.055f, 0.06f, 0.075f), metallic: 0.55f, smooth: 0.75f);
            Material metal = Mat("DC Dark Metal", new Color(0.1f, 0.11f, 0.14f), metallic: 0.8f, smooth: 0.6f);
            Material glass = GlassMat();
            Material asphalt = Mat("CS Asphalt", new Color(0.12f, 0.125f, 0.14f), smooth: 0.2f);
            Material lineWhite = Mat("CS Line White", new Color(0.95f, 0.95f, 0.93f), smooth: 0.2f);
            Material lineBlue = Mat("CS Line Blue", new Color(0.12f, 0.35f, 0.85f), smooth: 0.2f);
            Material concrete = Mat("CS Concrete", new Color(0.62f, 0.62f, 0.6f), smooth: 0.15f);
            Material cyan = Mat("DC Glow Cyan", Cyan, emission: Cyan * 2.2f);
            Material magenta = Mat("DC Glow Magenta", Magenta, emission: Magenta * 2f);
            Material gold = Mat("DC Glow Gold", Gold, emission: Gold * 1.6f);
            Material warm = Mat("CS Vault Warm Light", new Color(1f, 0.92f, 0.8f), emission: new Color(1f, 0.9f, 0.75f) * 2.2f);
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

            var vault = new GameObject("Card Vault").transform;
            vault.SetParent(root, false);

            // =========================================================== plinth, floor and steps
            Solid(vault, "Plinth", Ring(0f, VaultR + 0.5f, 0f, FloorY - 0.02f, 0f, 360f, 128), stone, collider: true);
            Solid(vault, "Marble Floor", Ring(0f, VaultR + 0.05f, FloorY - 0.02f, FloorY, 0f, 360f, 128), marble, collider: true);
            Solid(vault, "Entrance Step", Ring(VaultR + 0.5f, VaultR + 1.4f, 0f, 0.15f, -40f, 40f, 48), stone, collider: true);
            Solid(vault, "Floor Glow Ring", Ring(VaultR - 0.35f, VaultR - 0.25f, FloorY, FloorY + 0.01f, DoorHalfAngle, 360f - DoorHalfAngle, 120), cyan);

            // =========================================================== the glass drum
            Solid(vault, "Glass Drum", Ring(VaultR - 0.03f, VaultR, FloorY, WallTop, DoorHalfAngle, 360f - DoorHalfAngle, 128), glass, collider: true, shadows: false);
            Solid(vault, "Drum Sill", Ring(VaultR - 0.12f, VaultR + 0.12f, FloorY, FloorY + 0.18f, DoorHalfAngle, 360f - DoorHalfAngle, 128), bronze);
            Solid(vault, "Drum Transom", Ring(VaultR - 0.1f, VaultR + 0.1f, 4.3f, 4.42f, DoorHalfAngle, 360f - DoorHalfAngle, 128), bronze);
            Solid(vault, "Drum Crown Beam", Ring(VaultR - 0.3f, VaultR + 0.3f, WallTop, WallTop + 0.35f, 0f, 360f, 128), bronze);
            for (float a = DoorHalfAngle; a <= 360f - DoorHalfAngle + 0.01f; a += (360f - 2f * DoorHalfAngle) / 36f)
                Solid(vault, "Mullion", Tube(OnRing(a, VaultR + 0.02f, FloorY), OnRing(a, VaultR + 0.02f, WallTop), 0.07f, 12), bronze);
            // Portal: two bronze fins and a lit lintel frame the opening.
            foreach (float s in new[] { -1f, 1f })
                Solid(vault, "Portal Fin", Ring(VaultR - 0.25f, VaultR + 0.6f, FloorY, WallTop, s * DoorHalfAngle - 0.8f, s * DoorHalfAngle + 0.8f, 4), bronze, collider: true);
            Solid(vault, "Portal Lintel", Ring(VaultR - 0.2f, VaultR + 0.4f, 4.3f, 4.8f, -DoorHalfAngle, DoorHalfAngle, 24), bronze);
            Solid(vault, "Portal Glow", Ring(VaultR + 0.4f, VaultR + 0.45f, 4.35f, 4.45f, -DoorHalfAngle, DoorHalfAngle, 24), gold);
            Solid(vault, "Portal Glass Above", Ring(VaultR - 0.03f, VaultR, 4.8f, WallTop, -DoorHalfAngle, DoorHalfAngle, 24), glass, shadows: false);

            // =========================================================== entrance canopy
            Solid(vault, "Entrance Canopy", Ring(VaultR, VaultR + 7.5f, 5.3f, 5.6f, -26f, 26f, 48), white, collider: false);
            Solid(vault, "Canopy Underglow", Ring(VaultR + 7.3f, VaultR + 7.45f, 5.22f, 5.3f, -25.5f, 25.5f, 48), warm);
            Solid(vault, "Canopy Edge", Ring(VaultR + 7.45f, VaultR + 7.55f, 5.25f, 5.65f, -26f, 26f, 48), cyan);
            foreach (float a in new[] { -21f, 21f })
                Solid(vault, "Canopy Column", Tube(OnRing(a, VaultR + 6.6f, 0.15f), OnRing(a, VaultR + 6.6f, 5.3f), 0.16f, 20), bronze, collider: true);
            foreach (float a in new[] { -12f, -4f, 4f, 12f })
                PointLight(vault, "Canopy Downlight", OnRing(a, VaultR + 4f, 5f), 9f, 3f, new Color(1f, 0.9f, 0.78f));

            // =========================================================== halo roof with oculus
            Solid(vault, "Halo Roof", Ring(4.6f, VaultR + 3.5f, WallTop + 0.35f, WallTop + 1.05f, 0f, 360f, 160), white);
            Solid(vault, "Roof Soffit Glow", Ring(VaultR + 3.2f, VaultR + 3.4f, WallTop + 0.3f, WallTop + 0.36f, 0f, 360f, 160), warm);
            Solid(vault, "Roof Rim Glow", Ring(VaultR + 3.5f, VaultR + 3.58f, WallTop + 0.45f, WallTop + 0.95f, 0f, 360f, 160), cyan);
            Solid(vault, "Oculus Rim", Ring(4.3f, 4.6f, WallTop + 0.35f, WallTop + 1.25f, 0f, 360f, 64), bronze);
            Solid(vault, "Oculus Glass", Ring(0f, 4.3f, WallTop + 1.0f, WallTop + 1.03f, 0f, 360f, 64), glass, shadows: false);
            Solid(vault, "Ceiling", Ring(4.6f, VaultR - 0.05f, WallTop + 0.3f, WallTop + 0.35f, 0f, 360f, 128), Mat("CS Vault Ceiling", new Color(0.9f, 0.88f, 0.84f), smooth: 0.2f));
            foreach (float a in new[] { 70f, 125f, 180f, 235f, 290f })
                Solid(vault, "Roof Column", Tube(OnRing(a, VaultR + 2.6f, FloorY), OnRing(a, VaultR + 2.6f, WallTop + 0.4f), 0.2f, 20), bronze, collider: true);

            // =========================================================== the crown: a fanned hand of five giant cards
            BuildCardCrown(vault, gold, bronze);

            // =========================================================== interior
            Transform inside = new GameObject("Interior").transform;
            inside.SetParent(vault, false);

            // Hero card under the oculus.
            Vector3 hero = VaultCentre + new Vector3(0f, FloorY, 0f);
            Solid(inside, "Hero Dais", Ring(0f, 2.6f, FloorY, FloorY + 0.45f, 0f, 360f, 64), charcoal, collider: true);
            Solid(inside, "Hero Dais Marble", Ring(0f, 2.3f, FloorY + 0.45f, FloorY + 0.5f, 0f, 360f, 64), marble);
            Solid(inside, "Hero Dais Glow", Ring(2.6f, 2.65f, FloorY + 0.05f, FloorY + 0.4f, 0f, 360f, 64), cyan);
            var spinner = new GameObject("Hero Card").transform;
            spinner.SetParent(inside, false);
            spinner.localPosition = hero + new Vector3(0f, 2.9f, 0f);
            spinner.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 22f, 0f);
            Box(spinner, "Card Frame", Vector3.zero, new Vector3(2.2f, 3.2f, 0.08f), gold, collider: false);
            Quad(spinner, "Card Face A", new Vector3(0f, 0f, -0.05f), new Vector2(2f, 2.95f), 0f, Pack(0)).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 3;
            Quad(spinner, "Card Face B", new Vector3(0f, 0f, 0.05f), new Vector2(2f, 2.95f), 180f, Pack(1)).AddComponent<DuelGenesis.Core.GenesisCardSlideshow>().offset = 11;
            SpotLight(inside, "Oculus Spot", hero + new Vector3(0f, WallTop, 0f), hero + Vector3.up * 2.5f, new Color(1f, 0.95f, 0.88f));

            // Booster counter (west) and duel-mat counter (east): curved wood counters with the shop terminals.
            foreach (float side in new[] { -1f, 1f })
            {
                float a = side < 0f ? 270f : 90f;
                bool boosters = side < 0f;
                Material glow = boosters ? magenta : cyan;
                Solid(inside, "Counter", Ring(8.6f, 9.5f, FloorY, FloorY + 1.05f, a - 22f, a + 22f, 32), wood, collider: true);
                Solid(inside, "Counter Top", Ring(8.5f, 9.6f, FloorY + 1.05f, FloorY + 1.12f, a - 22.5f, a + 22.5f, 32), marble);
                Solid(inside, "Counter Glow", Ring(8.48f, 8.52f, FloorY + 0.15f, FloorY + 0.25f, a - 22f, a + 22f, 32), glow);
                var terminal = new GameObject(boosters ? "Booster Counter Terminal" : "Duel Mat Counter Terminal");
                terminal.transform.SetParent(inside, false);
                terminal.transform.localPosition = OnRing(a, 9.05f, FloorY + 1.1f);
                terminal.transform.localRotation = Quaternion.Euler(0f, FacingCentre(a), 0f);
                var tcol = terminal.AddComponent<BoxCollider>();
                tcol.center = new Vector3(0f, 0.4f, 0f);
                tcol.size = new Vector3(6.5f, 0.8f, 1.4f);
                terminal.AddComponent<DuelGenesis.Shops.CardShopTerminal>().shopName = CardShopName;
                PlaceIf(inside, SubwayProp("Ticket_Vending_01"), "Register Kiosk", OnRing(a + 14f, 9.05f, FloorY + 1.12f), FacingCentre(a), 0.9f);
                // Display wall behind the counter: packs (west) or the five duel mats (east), lit from above.
                var wall = new GameObject(boosters ? "Booster Wall" : "Duel Mat Wall").transform;
                wall.SetParent(inside, false);
                wall.localPosition = OnRing(a, VaultR - 0.6f, FloorY);
                wall.localRotation = Quaternion.Euler(0f, FacingCentre(a), 0f);
                Box(wall, "Board", new Vector3(0f, 2.3f, 0f), new Vector3(7.4f, 3.6f, 0.12f), board);
                Box(wall, "Board Glow", new Vector3(0f, 4.15f, 0.08f), new Vector3(7.4f, 0.06f, 0.04f), glow, collider: false);
                if (boosters)
                    for (int i = 0; i < 12; i++)
                        Quad(wall, "Pack", new Vector3((i % 6 - 2.5f) * 1.12f, i < 6 ? 3.2f : 1.75f, 0.08f), new Vector2(0.85f, 1.25f), 180f, Pack(i));
                else
                {
                    string[] mats = { "blue_eyes", "pharaoh", "egyptian_gods", "dark_magician_girl", "emerald_seal" };
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/DuelGenesis/Mats/mat_{mats[i]}.jpg");
                        if (tex == null) continue;
                        Vector3 p = i < 3 ? new Vector3((i - 1) * 2.4f, 3.1f, 0.08f) : new Vector3((i - 3.5f) * 2.4f, 1.55f, 0.08f);
                        Quad(wall, "Mat " + mats[i], p, new Vector2(2.2f, 1.3f), 180f, Mat("CS Mat " + mats[i], Color.white, tex: tex, emission: Color.white * 0.3f));
                    }
                }
                HangingSign(inside, boosters ? "BOOSTER PACKS" : "DUEL MATS", OnRing(a, 7.4f, 5.6f), board, glow, FacingCentre(a) + 180f, 5.2f);
                PointLight(inside, "Counter Light", OnRing(a, 9f, 5.5f), 10f, 5f, new Color(1f, 0.94f, 0.86f));
            }

            // Showcases either side of the entrance.
            int card = 0;
            foreach (float a in new[] { -38f, 38f })
            {
                var sc = new GameObject("Showcase").transform;
                sc.SetParent(inside, false);
                sc.localPosition = OnRing(a, 8.3f, FloorY);
                sc.localRotation = Quaternion.Euler(0f, FacingCentre(a), 0f);
                Showcase(sc, Vector3.zero, wood, bronze, glass, a < 0f ? magenta : cyan, Pack(card++), Pack(card++), Pack(card++));
            }

            // Vending machines (NY Subway pack) against the glass by the entrance, and a charging post by the tables.
            PlaceIf(inside, SubwayProp("Vending_Machine_Water"), "Drinks Machine", OnRing(-55f, VaultR - 0.9f, FloorY), FacingCentre(-55f), 1.95f);
            PlaceIf(inside, SubwayProp("Vending_Machine_Snacks"), "Snack Machine", OnRing(-63f, VaultR - 0.9f, FloorY), FacingCentre(-63f), 1.95f);
            PlaceIf(inside, SubwayProp("Charging_01"), "Charging Post", OnRing(125f, VaultR - 1f, FloorY), FacingCentre(125f), 1.5f);
            PlaceIf(inside, SubwayProp("Information_Stand_01"), "Info Stand", OnRing(55f, VaultR - 1.2f, FloorY), FacingCentre(55f), 2.1f);

            // The duel ring: six playable tables round the back of the atrium under pendant lights.
            int seed = 700, tables = 0;
            foreach (float a in new[] { 130f, 155f, 180f, 205f, 230f })
            {
                Vector3 p = OnRing(a, 7.4f, FloorY);
                ShopTable(inside, $"Table {++tables}", p, FacingCentre(a) + 90f, seed++);
                Solid(inside, "Pendant", Ring(0f, 0.45f, 3.4f, 3.5f, 0f, 360f, 32), bronze, offset: p - VaultCentre - new Vector3(0f, FloorY, 0f));
                Solid(inside, "Pendant Glow", Ring(0f, 0.4f, 3.38f, 3.4f, 0f, 360f, 32), warm, offset: p - VaultCentre - new Vector3(0f, FloorY, 0f));
                Solid(inside, "Pendant Cable", Tube(p + Vector3.up * 3.5f, new Vector3(p.x, WallTop + 0.3f, p.z), 0.012f, 6), metal);
            }
            HangingSign(inside, "DUEL RING", OnRing(180f, 5.2f, 6.6f), board, gold, 0f, 5f);
            foreach (float a in new[] { 0f, 72f, 144f, 216f, 288f })
                PointLight(inside, "Atrium Light", OnRing(a, 6f, 6.8f), 12f, 5f, new Color(1f, 0.95f, 0.9f));

            // =========================================================== forecourt
            Box(root, "Forecourt", new Vector3(0f, 0.035f, 7.9f), new Vector3(44f, 0.07f, 14.8f), paving);
            Box(root, "Forecourt Curb", new Vector3(0f, 0.09f, 15.25f), new Vector3(44f, 0.18f, 0.2f), concrete);
            foreach (float s in new[] { -1f, 1f })
            {
                // Planters with real shrubs either side of the walk.
                Vector3 pl = new Vector3(s * 11f, 0f, 10f);
                Solid(root, "Planter", Ring(0f, 1.6f, 0f, 0.55f, 0f, 360f, 48), stone, collider: true, offset: pl - VaultCentre);
                Solid(root, "Planter Soil", Ring(0f, 1.45f, 0.55f, 0.57f, 0f, 360f, 48), Mat("CS Soil", new Color(0.18f, 0.13f, 0.1f), smooth: 0.05f), offset: pl - VaultCentre);
                GameObject shrub = GenesisNatureProps.Load(s < 0 ? "shrub_02" : "shrub_04");
                if (shrub != null) PlaceSized(root, shrub, "Planter Shrub", pl + Vector3.up * 0.55f, s * 40f, 1.3f);
                LampPost(root, new Vector3(s * 17f, 0.07f, 12.5f), bronze, warm);
            }

            // =========================================================== parking lot and driveways
            BuildParkingLot(root, asphalt, lineWhite, lineBlue, concrete, bronze, warm, charcoal, cyan, magenta, logo);

            // Where the teleport lands: in the lot, looking at the entrance.
            var arrival = new GameObject(CardShopName + " - Arrival Spot").transform;
            arrival.SetParent(root, false);
            arrival.localPosition = new Vector3(-6f, 0.1f, 22f);
            arrival.localRotation = Quaternion.LookRotation(Vector3.back);

            // Reflection probe so the glass, marble and bronze pick up the room and the sky.
            var probeGo = new GameObject("Vault Reflection Probe");
            probeGo.transform.SetParent(root, false);
            probeGo.transform.localPosition = VaultCentre + new Vector3(0f, 3f, 0f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(VaultR * 2f + 8f, 12f, VaultR * 2f + 8f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.resolution = 128;

            AssetDatabase.SaveAssets();
            Debug.Log($"Duel: Genesis built the {CardShopName} (the Card Vault rotunda) at {ground}: {tables} duel tables; cleared {cleared} props off the lot.");
            return root.gameObject;
        }

        // ------------------------------------------------------------------ the card crown

        private static void BuildCardCrown(Transform vault, Material gold, Material bronze)
        {
            var crown = new GameObject("Card Crown").transform;
            crown.SetParent(vault, false);
            // The hand fans from a point inside the roof slab, behind the oculus, leaning back a little.
            Vector3 origin = VaultCentre + new Vector3(0f, WallTop + 1.6f, -8.5f);
            crown.localPosition = origin;
            crown.localRotation = Quaternion.Euler(-6f, 0f, 0f);

            const float w = 9f, h = 13.1f, depth = 0.4f;
            Mesh slab = SaveMesh(CardSlab(w, h, depth, 0.55f), "CardSlab");
            Material back = CardMaterial("Card Back Anime 1", null);
            (string art, string frame)[] cards =
            {
                ("Black Luster Soldier", "Ritual Card Background Texture"),
                ("Red-Eyes Black Dragon", "Normal Monster Card Background"),
                ("Blue-Eyes White Dragon", "Normal Monster Card Background"),
                ("Dark Magician", "Normal Monster Card Background"),
                ("Exodia the Forbidden One", "Effect Monster Card Background"),
            };
            float[] fan = { 34f, 17f, 0f, -17f, -34f };
            for (int i = 0; i < cards.Length; i++)
            {
                var pivot = new GameObject("Crown Card - " + cards[i].art).transform;
                pivot.SetParent(crown, false);
                pivot.localRotation = Quaternion.Euler(0f, 0f, fan[i]);
                pivot.localPosition = new Vector3(0f, 0f, (i - 2) * 0.45f);   // held like a hand: each card overlaps the one before
                var go = new GameObject("Card");
                go.transform.SetParent(pivot, false);
                go.transform.localPosition = new Vector3(0f, 1.6f + h * 0.5f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = slab;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = new[] { CardMaterial(cards[i].art, cards[i].frame) ?? gold, back ?? bronze, gold };
                PointLight(pivot, "Card Wash", new Vector3(0f, 1.6f + h * 0.5f, 4f), 12f, 3f, new Color(1f, 0.92f, 0.8f));
            }
            // The fan's hub: a bronze boss where the cards meet the roof.
            Solid(crown, "Crown Hub", Ring(0f, 1.7f, -0.9f, 1.8f, 0f, 360f, 48), bronze, offset: -VaultCentre);
        }

        /// <summary>A lit card face: the card frame with the art composited into its window (cached as a PNG).</summary>
        private static Material CardMaterial(string art, string frame)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string key = frame == null ? art : art + " face";
            string outPath = $"{VaultFolder}/{key}.png";
            if (!File.Exists(Path.Combine(project, outPath)))
            {
                string artFile = Path.Combine(project, "Cards", "DMO_card_art", frame == null ? "card_backs_frames" : "cards", art + ".png");
                if (!File.Exists(artFile)) return null;
                var artTex = new Texture2D(2, 2);
                artTex.LoadImage(File.ReadAllBytes(artFile));
                Texture2D outTex;
                if (frame == null) outTex = artTex;
                else
                {
                    string frameFile = Path.Combine(project, "Cards", "DMO_card_art", "card_backs_frames", frame + ".png");
                    if (!File.Exists(frameFile)) return null;
                    var frameTex = new Texture2D(2, 2);
                    frameTex.LoadImage(File.ReadAllBytes(frameFile));
                    int W = frameTex.width * 2, H = frameTex.height * 2;
                    outTex = new Texture2D(W, H, TextureFormat.RGB24, true);
                    var px = new Color[W * H];
                    // Art window of the frame (fractions of the card, from the bottom-left).
                    const float ax0 = 0.1f, ax1 = 0.9f, ay0 = 1f - 0.717f, ay1 = 1f - 0.172f;
                    for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                        Color c = frameTex.GetPixelBilinear(u, v);
                        if (u > ax0 && u < ax1 && v > ay0 && v < ay1)
                            c = artTex.GetPixelBilinear((u - ax0) / (ax1 - ax0), (v - ay0) / (ay1 - ay0));
                        px[y * W + x] = new Color(c.r, c.g, c.b, 1f);
                    }
                    outTex.SetPixels(px);
                    outTex.Apply();
                    Object.DestroyImmediate(frameTex);
                }
                File.WriteAllBytes(Path.Combine(project, outPath), outTex.EncodeToPNG());
                if (outTex != artTex) Object.DestroyImmediate(outTex);
                Object.DestroyImmediate(artTex);
                AssetDatabase.ImportAsset(outPath);
            }
            var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            return Mat("CS Crown " + key, Color.white, smooth: 0.55f, tex: texAsset, emission: Color.white * 0.45f);
        }

        // ------------------------------------------------------------------ parking lot

        private static void BuildParkingLot(Transform root, Material asphalt, Material lineWhite, Material lineBlue, Material concrete,
                                            Material bronze, Material warm, Material charcoal, Material cyan, Material magenta, Material logo)
        {
            // Local z: forecourt ends at 15.25; bays 15.3-20.8, aisle 20.8-28, bays 28-33.5; hedge strip 34.2-35.9; city pavement 36-40; road edge 44.
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
            foreach ((float z0, float facing) in new[] { (lotFront, 1f), (28f, -1f) })
            {
                for (float x = -lotHalf + 0.6f; x <= lotHalf - 0.5f; x += bay)
                {
                    float cx = x + bay * 0.5f;
                    if (cx > lotHalf - 0.6f) break;
                    if (facing < 0f && InDrive(cx)) continue;
                    Box(root, "Bay Line", new Vector3(x, 0.05f, z0 + bayDepth * 0.5f), new Vector3(0.12f, 0.01f, bayDepth), lineWhite, collider: false);
                    if (facing > 0f && Mathf.Abs(cx) < 3f)
                        Box(root, "Accessible Bay", new Vector3(cx, 0.045f, z0 + bayDepth * 0.5f), new Vector3(bay - 0.2f, 0.01f, bayDepth - 0.2f), lineBlue, collider: false);
                    float stopZ = facing > 0f ? z0 + 0.6f : z0 + bayDepth - 0.6f;
                    Box(root, "Wheel Stop", new Vector3(cx, 0.08f, stopZ), new Vector3(1.8f, 0.15f, 0.2f), concrete);
                }
                if (!(facing < 0f && InDrive(lotHalf - 0.5f)))
                    Box(root, "Bay Line", new Vector3(lotHalf - 0.5f, 0.05f, z0 + bayDepth * 0.5f), new Vector3(0.12f, 0.01f, bayDepth), lineWhite, collider: false);
            }
            Sign(root, "<  <  <", new Vector3(0f, 0.08f, 24.4f), 180f, 0.12f, new Color(0.95f, 0.95f, 0.93f), flatOnFloor: true);
            foreach (float s in new[] { -1f, 1f })
                Box(root, "Lot Side Curb", new Vector3(s * (lotHalf + 0.1f), 0.09f, (lotFront + lotBack) * 0.5f), new Vector3(0.2f, 0.18f, lotBack - lotFront), concrete);
            // Hedge strip of real shrubs between the lot and the street, broken by the driveways.
            Box(root, "Hedge Strip", new Vector3(0f, 0.03f, 35.02f), new Vector3(lotHalf * 2f, 0.06f, 1.65f), Mat("CS Lawn", new Color(0.24f, 0.42f, 0.18f), smooth: 0.1f));
            string[] hedges = { "shrub_02", "shrub_03", "shrub_04" };
            int k = 0;
            for (float x = -lotHalf + 1f; x <= lotHalf - 0.9f; x += 1.9f)
            {
                if (InDrive(x)) continue;
                GameObject shrub = GenesisNatureProps.Load(hedges[k++ % hedges.Length]);
                if (shrub != null) PlaceSized(root, shrub, "Hedge Shrub", new Vector3(x, 0.06f, 35.02f), (x * 37f) % 360f, 1.1f);
            }
            foreach (float d in driveX)
                foreach (float s in new[] { -1f, 1f })
                    Box(root, "Driveway Curb", new Vector3(d + s * (driveHalf + 0.1f), 0.09f, 35.02f), new Vector3(0.2f, 0.18f, 1.65f), concrete);
            foreach (float x in new[] { -18f, 0f, 18f })
                LampPost(root, new Vector3(x, 0.04f, 24.4f), bronze, warm);

            // Pylon sign by the entrance driveway: a tall rounded bronze blade with the logo.
            Vector3 pylon = new Vector3(18f, 0f, 35.02f);
            Solid(root, "Pylon", Stadium(1.3f, 0.5f, 0f, 8f, 24), bronze, collider: true, offset: pylon - VaultCentre);
            Solid(root, "Pylon Glow", Stadium(1.34f, 0.08f, 0.4f, 7.6f, 24), cyan, offset: pylon - VaultCentre);
            if (logo != null)
            {
                Quad(root, "Pylon Logo Front", pylon + new Vector3(0f, 6.6f, 0.27f), new Vector2(1.1f, 1.03f), 180f, logo);
                Quad(root, "Pylon Logo Back", pylon + new Vector3(0f, 6.6f, -0.27f), new Vector2(1.1f, 1.03f), 0f, logo);
            }
            Sign(root, "G\nE\nN\nE\nS\nI\nS", new Vector3(pylon.x, 3.4f, pylon.z + 0.27f), 180f, 0.055f, Color.Lerp(Gold, Color.white, 0.3f));
            Sign(root, "C\nA\nR\nD\nS", new Vector3(pylon.x, 3.6f, pylon.z - 0.27f), 0f, 0.07f, Color.Lerp(Gold, Color.white, 0.3f));
        }

        /// <summary>A slender bronze lamp post with a disc head.</summary>
        private static void LampPost(Transform root, Vector3 foot, Material bronze, Material warm)
        {
            Solid(root, "Lamp Pole", Tube(foot, foot + Vector3.up * 5.6f, 0.08f, 16), bronze, collider: true);
            Solid(root, "Lamp Base", Ring(0f, 0.22f, 0f, 0.5f, 0f, 360f, 24), bronze, offset: foot - VaultCentre);
            Solid(root, "Lamp Head", Ring(0f, 0.45f, 5.6f, 5.75f, 0f, 360f, 32), bronze, offset: foot - VaultCentre);
            Solid(root, "Lamp Lens", Ring(0f, 0.4f, 5.58f, 5.6f, 0f, 360f, 32), warm, offset: foot - VaultCentre);
            PointLight(root, "Lamp Light", foot + Vector3.up * 5.3f, 16f, 5f, new Color(1f, 0.9f, 0.75f));
        }

        private static GameObject PlaceIf(Transform parent, GameObject prefab, string name, Vector3 foot, float yaw, float height) =>
            prefab == null ? null : PlaceSized(parent, prefab, name, foot, yaw, height);

        private static GameObject SubwayProp(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ThirdParty/NY_Subway/Prefabs/RSG_Subway_{name}.prefab");

        // ------------------------------------------------------------------ smooth mesh building

        private static int _meshCount;

        /// <summary>Adds a mesh object (saved as an asset) under <paramref name="parent"/>; its vertices are relative to the vault centre.</summary>
        private static GameObject Solid(Transform parent, string name, Mesh mesh, Material m, bool collider = false, bool shadows = true, Vector3? offset = null)
        {
            mesh = SaveMesh(mesh, $"{name.Replace(' ', '_')}_{_meshCount++}");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = VaultCentre + (offset ?? Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{VaultFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private sealed class SmoothMesh
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int>[] Sub;
            public SmoothMesh(int subs = 1) { Sub = new List<int>[subs]; for (int i = 0; i < subs; i++) Sub[i] = new List<int>(); }

            public int Add(Vector3 p, Vector3 n, Vector2 uv) { V.Add(p); N.Add(n.normalized); UV.Add(uv); return V.Count - 1; }

            /// <summary>Triangle wound so its face points along the vertices' normals.</summary>
            public void Tri(int a, int b, int c, int s = 0)
            {
                Vector3 f = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (Vector3.Dot(f, N[a] + N[b] + N[c]) < 0f) { int t = b; b = c; c = t; }
                Sub[s].Add(a); Sub[s].Add(b); Sub[s].Add(c);
            }

            /// <summary>A (nu+1) x (nv+1) grid of vertices from <paramref name="f"/>, triangulated.</summary>
            public void Grid(int nu, int nv, Func<float, float, (Vector3 p, Vector3 n, Vector2 uv)> f, int s = 0)
            {
                int start = V.Count;
                for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                {
                    var (p, n, uv) = f(i / (float)nu, j / (float)nv);
                    Add(p, n, uv);
                }
                for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int a = start + j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                    Tri(a, b, d, s);
                    Tri(a, d, c, s);
                }
            }

            public Mesh ToMesh()
            {
                var m = new Mesh();
                if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetUVs(0, UV);
                m.subMeshCount = Sub.Length;
                for (int i = 0; i < Sub.Length; i++) m.SetTriangles(Sub[i], i);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }

        private static Vector3 Radial(float deg) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad));

        /// <summary>A smooth solid ring sector (a full disc when rIn is 0 and the sweep is 360°), relative to the vault centre.
        /// UVs are in metres / 3 so tiled textures keep their scale.</summary>
        private static Mesh Ring(float rIn, float rOut, float y0, float y1, float a0, float a1, int segs)
        {
            var sm = new SmoothMesh();
            const float uvm = 1f / 3f;
            bool full = Mathf.Abs(a1 - a0) >= 359.9f;
            float A(float u) => Mathf.Lerp(a0, a1, u);
            float arcOut = (a1 - a0) * Mathf.Deg2Rad * rOut;
            sm.Grid(segs, 1, (u, v) => (Radial(A(u)) * rOut + Vector3.up * Mathf.Lerp(y0, y1, v), Radial(A(u)), new Vector2(u * arcOut * uvm, Mathf.Lerp(y0, y1, v) * uvm)));
            if (rIn > 0.001f)
            {
                float arcIn = (a1 - a0) * Mathf.Deg2Rad * rIn;
                sm.Grid(segs, 1, (u, v) => (Radial(A(u)) * rIn + Vector3.up * Mathf.Lerp(y0, y1, v), -Radial(A(u)), new Vector2(u * arcIn * uvm, Mathf.Lerp(y0, y1, v) * uvm)));
            }
            int rings = Mathf.Max(1, Mathf.CeilToInt((rOut - rIn) / 1.5f));
            foreach ((float y, Vector3 n) in new[] { (y1, Vector3.up), (y0, Vector3.down) })
                sm.Grid(segs, rings, (u, v) =>
                {
                    Vector3 p = Radial(A(u)) * Mathf.Lerp(rIn, rOut, v) + Vector3.up * y;
                    return (p, n, new Vector2(p.x * uvm, p.z * uvm));
                });
            if (!full)
                foreach (float a in new[] { a0, a1 })
                {
                    Vector3 t = Vector3.Cross(Vector3.up, Radial(a)) * (a == a0 ? 1f : -1f);   // points out of the sector
                    sm.Grid(1, 1, (u, v) =>
                    {
                        Vector3 p = Radial(a) * Mathf.Lerp(rIn, rOut, u) + Vector3.up * Mathf.Lerp(y0, y1, v);
                        return (p, t, new Vector2(Mathf.Lerp(rIn, rOut, u) * uvm, p.y * uvm));
                    });
                }
            return sm.ToMesh();
        }

        /// <summary>A smooth round tube (with caps) from a to b, both relative to the vault centre... given in shop space.</summary>
        private static Mesh Tube(Vector3 a, Vector3 b, float r, int sides)
        {
            a -= VaultCentre; b -= VaultCentre;
            var sm = new SmoothMesh();
            Vector3 axis = (b - a).normalized;
            Vector3 u0 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 w0 = Vector3.Cross(axis, u0);
            float len = Vector3.Distance(a, b);
            Vector3 Dir(float t) => u0 * Mathf.Cos(t * Mathf.PI * 2f) + w0 * Mathf.Sin(t * Mathf.PI * 2f);
            sm.Grid(sides, 1, (u, v) => (Vector3.Lerp(a, b, v) + Dir(u) * r, Dir(u), new Vector2(u, v * len)));
            foreach ((Vector3 c, Vector3 n) in new[] { (a, -axis), (b, axis) })
            {
                int centre = sm.Add(c, n, new Vector2(0.5f, 0.5f));
                int first = sm.V.Count;
                for (int i = 0; i <= sides; i++) sm.Add(c + Dir(i / (float)sides) * r, n, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < sides; i++) sm.Tri(centre, first + i, first + i + 1);
            }
            return sm.ToMesh();
        }

        /// <summary>A rounded blade (stadium cross-section: width x thickness) standing from y0 to y1.</summary>
        private static Mesh Stadium(float width, float thick, float y0, float y1, int segs)
        {
            var sm = new SmoothMesh();
            float r = thick * 0.5f, half = width * 0.5f - r;
            var outline = new List<(Vector3 p, Vector3 n)>();
            for (int i = 0; i <= segs; i++)   // right cap
            {
                float t = -90f + 180f * i / segs;
                Vector3 n = new Vector3(Mathf.Cos(t * Mathf.Deg2Rad), 0f, Mathf.Sin(t * Mathf.Deg2Rad));
                outline.Add((new Vector3(half, 0f, 0f) + n * r, n));
            }
            for (int i = 0; i <= segs; i++)   // left cap
            {
                float t = 90f + 180f * i / segs;
                Vector3 n = new Vector3(Mathf.Cos(t * Mathf.Deg2Rad), 0f, Mathf.Sin(t * Mathf.Deg2Rad));
                outline.Add((new Vector3(-half, 0f, 0f) + n * r, n));
            }
            outline.Add(outline[0]);
            int count = outline.Count;
            sm.Grid(count - 1, 1, (u, v) =>
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(u * (count - 1)), 0, count - 1);
                return (outline[i].p + Vector3.up * Mathf.Lerp(y0, y1, v), outline[i].n, new Vector2(u * 4f, v * (y1 - y0)));
            });
            foreach ((float y, Vector3 n) in new[] { (y1, Vector3.up), (y0, Vector3.down) })
            {
                int c = sm.Add(new Vector3(0f, y, 0f), n, Vector2.zero);
                int first = sm.V.Count;
                foreach (var o in outline) sm.Add(o.p + Vector3.up * y, n, Vector2.zero);
                for (int i = 0; i < count - 1; i++) sm.Tri(c, first + i, first + i + 1);
            }
            return sm.ToMesh();
        }

        /// <summary>A giant card: a rounded-corner slab. Submesh 0 = face (+Z, UV 0-1), 1 = back (-Z, mirrored), 2 = edge.</summary>
        private static Mesh CardSlab(float w, float h, float depth, float corner)
        {
            var sm = new SmoothMesh(3);
            var outline = new List<Vector2>();
            Vector2[] centres = { new Vector2(w * 0.5f - corner, h * 0.5f - corner), new Vector2(-w * 0.5f + corner, h * 0.5f - corner), new Vector2(-w * 0.5f + corner, -h * 0.5f + corner), new Vector2(w * 0.5f - corner, -h * 0.5f + corner) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= 8; i++)
                {
                    float t = (c * 90f + i * 90f / 8f) * Mathf.Deg2Rad;
                    outline.Add(centres[c] + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * corner);
                }
            float z = depth * 0.5f;
            foreach ((float side, int sub) in new[] { (1f, 0), (-1f, 1) })
            {
                Vector3 n = new Vector3(0f, 0f, side);
                Vector2 UV(Vector2 p) => new Vector2(side > 0f ? 0.5f - p.x / w : 0.5f + p.x / w, 0.5f + p.y / h);   // face reads correctly from +Z
                int centre = sm.Add(new Vector3(0f, 0f, side * z), n, UV(Vector2.zero));
                int first = sm.V.Count;
                foreach (Vector2 p in outline) sm.Add(new Vector3(p.x, p.y, side * z), n, UV(p));
                for (int i = 0; i < outline.Count; i++) sm.Tri(centre, first + i, first + (i + 1) % outline.Count, sub);
            }
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % outline.Count];
                Vector2 e = (q - p).normalized;
                Vector3 n = new Vector3(e.y, -e.x, 0f);
                if (Vector2.Dot(new Vector2(n.x, n.y), (p + q) * 0.5f) < 0f) n = -n;
                int a = sm.Add(new Vector3(p.x, p.y, z), n, new Vector2(0f, 0f)), b = sm.Add(new Vector3(q.x, q.y, z), n, new Vector2(1f, 0f));
                int c = sm.Add(new Vector3(q.x, q.y, -z), n, new Vector2(1f, 1f)), d = sm.Add(new Vector3(p.x, p.y, -z), n, new Vector2(0f, 1f));
                sm.Tri(a, b, c, 2); sm.Tri(a, c, d, 2);
            }
            return sm.ToMesh();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        // ------------------------------------------------------------------ shared with the other shop/world tools

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

        /// <summary>A glass display case on a base with three lit cards inside (faces local +Z).</summary>
        private static void Showcase(Transform root, Vector3 foot, Material wood, Material trim, Material glass, Material glow, Material a, Material b, Material c)
        {
            Box(root, "Showcase Base", foot + new Vector3(0f, 0.45f, 0f), new Vector3(3f, 0.9f, 1.1f), wood);
            Box(root, "Showcase Trim", foot + new Vector3(0f, 0.92f, 0f), new Vector3(3.04f, 0.05f, 1.14f), trim, collider: false);
            Box(root, "Showcase Glass", foot + new Vector3(0f, 1.3f, 0f), new Vector3(3f, 0.7f, 1.1f), glass, collider: false);
            Box(root, "Showcase Glow", foot + new Vector3(0f, 0.6f, 0.56f), new Vector3(2.9f, 0.04f, 0.02f), glow, collider: false);
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
