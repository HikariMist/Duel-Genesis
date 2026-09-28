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
    /// <summary>
    /// Builds "Genesis City" into GenesisPrototype.unity from the imported asset packs:
    ///  - Japanese Otaku City (ZENRIN Akihabara) is the map, scaled to real metres and positioned so the
    ///    widest flat open space in it becomes the Genesis hub (card shop, duel table, spawn).
    ///  - POLYGON City props (lamps, benches, bins, vending machine, shop front), Low Poly ATMs,
    ///    ICONIC sports cars, Otaku City traffic, Polytope trees/shrubs dress the hub.
    ///  - UMA 3 avatars replace the capsule player and populate the hub with NPCs.
    /// Everything lives under "DG City" and is rebuilt from scratch each run, so it can be re-run safely.
    /// </summary>
    public static class GenesisWorldBuilder
    {
        private const string ScenePath = "Assets/Scenes/GenesisPrototype.unity";
        public const string CityRootName = "DG City";

        // ---- asset paths
        private const string AkibaModel = "Assets/ZRNAssets/005339_08932_25_14/Models/PQ_Remake_AKIHABARA.fbx";
        private const string OtakuCars = "Assets/ZRNAssets/Cars/";
        private const string OtakuSkybox = "Assets/ZRNAssets/Skyboxes/DawnDusk Skybox.mat";
        private const string Polygon = "Assets/POLYGON city pack/Prefabs/";
        private const string Atm = "Assets/Low Poly ATM/Prefabs/";
        private const string Iconic = "Assets/ICONIC - Sports Car FREE Vol.02/Prefabs/Colliders/";
        private const string Nature = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";
        private const string UmaAvatarVariant = "Assets/UMA/UMA3/Getting Started/UMADynamicCharacterAvatar Variant.prefab";
        private const string UmaAvatarBase = "Assets/UMA/Core/Defaults/UMADynamicCharacterAvatar.prefab";
        private const string UmaLocomotion = "Assets/UMA/UMA3/Animation/Locomotion.controller";
        private const string UmaIdle = "Assets/UMA/UMA3/Animation/IdleController.controller";

        // The Akihabara FBX is authored at 1:10 (its import scale is 0.1); x10 makes it real metres.
        private const float CityScale = 10f;
        // Preferred hub location in the model's own (1:10) coordinates: the station square the ZENRIN demo
        // calls "QuerySquare". The builder searches for the flat open area closest to it.
        private static readonly Vector2 PreferredHubModel = new Vector2(-14.85f, -21.43f);
        // Footprint the hub needs (x = shop..duel table, z = spawn..kiosks), metres.
        private static readonly Vector2 HubSize = new Vector2(26f, 20f);

        // Car stops from the ZENRIN demo's traffic route (model coordinates) - on the road network.
        private static readonly Vector3[] CarRoute =
        {
            new Vector3(-10.829627f, .3f, -38.5402069f), new Vector3(-11.8414364f, .3f, -39.386776f),
            new Vector3(-10.376565f, .3f, -17.876009f), new Vector3(-9.22620964f, .3f, -20.8351536f),
            new Vector3(-32.1390152f, .3f, -18.2936974f), new Vector3(-30.8911343f, .3f, -17.5626221f),
            new Vector3(-35.5616493f, .3f, -37.3712845f), new Vector3(-35.8414268f, .3f, -36.9852295f),
            new Vector3(-23.3000031f, .3f, -38.6994057f), new Vector3(-24.5962124f, .3f, -38.4869537f),
        };

        private static readonly StringBuilder Log = new StringBuilder();

        // ================================================================== menu

        [MenuItem("Duel Genesis/World/1. Install URP Add-ons (UMA + Nature)")]
        public static void InstallUrpAddons()
        {
            bool any = false;
            string umaUrp = "Assets/UMA/SRP/UMAURP.unitypackage";
            if (File.Exists(umaUrp) && !Directory.Exists("Assets/UMA/SRP/ShaderGraphs/URP") && !File.Exists("Assets/UMA/SRP/.dg-urp-installed"))
            {
                AssetDatabase.ImportPackage(umaUrp, false);
                File.WriteAllText("Assets/UMA/SRP/.dg-urp-installed", "UMAURP.unitypackage imported by Duel Genesis");
                any = true;
            }
            string natureUrp = "Assets/Polytope Studio/Lowpoly_Environments/URP/PT_Nature_Free_URP_17.unitypackage";
            if (File.Exists(natureUrp))
            {
                AssetDatabase.ImportPackage(natureUrp, false);
                any = true;
            }
            Debug.Log(any ? "Duel: Genesis queued URP add-on packages (UMA URP + Polytope Nature URP 17). Wait for the import/compile to finish, then run step 2."
                          : "Duel: Genesis found no URP add-on packages to install.");
        }

        [MenuItem("Duel Genesis/World/3. Build Genesis City")]
        public static void BuildCityMenu()
        {
            if (!OpenPlayableScene()) return;
            BuildCity();
        }

        [MenuItem("Duel Genesis/World/Build Everything (2 + 3)")]
        public static void BuildEverything()
        {
            GenesisMaterialConverter.ConvertAll(out int scanned, out List<string> report);
            Debug.Log($"Duel: Genesis converted {report.Count}/{scanned} pack materials to URP.");
            BuildCityMenu();
        }

        private static bool OpenPlayableScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return true;
        }

        [MenuItem("Duel Genesis/World/6. Spread Card Shops & Duel Tables Across The City")]
        public static void DistrictsMenu()
        {
            if (!OpenPlayableScene()) return;
            GameObject root = GameObject.Find(CityRootName);
            if (root == null) { EditorUtility.DisplayDialog("Genesis City", "Build Genesis City first (World > 3).", "OK"); return; }
            Log.Clear();
            PlaceDistricts(root.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Duel: Genesis spread shops and duel tables across the city.\n" + Log);
        }

        // ================================================================== build

        public static void BuildCity()
        {
            Log.Clear();
            GameObject old = GameObject.Find(CityRootName);
            if (old != null) Object.DestroyImmediate(old);
            foreach (GameObject stray in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stray != null && stray.name == "Genesis Avatar") Object.DestroyImmediate(stray);

            GameObject root = new GameObject(CityRootName);
            GameObject staticRoot = new GameObject("Map");
            staticRoot.transform.SetParent(root.transform, false);

            // ---------------- map
            GameObject city = Spawn(AkibaModel, staticRoot.transform, Vector3.zero, 0f);
            if (city == null)
            {
                EditorUtility.DisplayDialog("Genesis City", "Could not find the Japanese Otaku City model:\n" + AkibaModel, "OK");
                Object.DestroyImmediate(root);
                return;
            }
            city.name = "Akihabara (Japanese Otaku City)";
            city.transform.localScale = Vector3.one * CityScale;
            int colliders = AddMeshColliders(city);
            SetStatic(city);
            Log.AppendLine($"Map: {colliders} mesh colliders.");

            PlaceCityAroundHub(city.transform);

            // ---------------- hub dressing
            Transform hub = new GameObject("Hub Dressing").transform;
            hub.SetParent(root.transform, false);
            HideBlockouts();
            DressCardShop(hub);
            DressHub(hub);
            PlaceTraffic(root.transform, city.transform);
            int boards = GenesisBillboards.Place(root.transform, Log);
            Log.AppendLine($"Billboards: {boards} placed.");
            GenesisAdReplacer.Replace(root.transform, Log);

            // ---------------- people
            Transform people = new GameObject("People").transform;
            people.SetParent(root.transform, false);
            SetupPlayerAvatar();
            SpawnNpcs(people);
            PlaceDistricts(root.transform);

            // ---------------- atmosphere
            ApplyAtmosphere();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Duel: Genesis City built.\n" + Log);
        }

        // ------------------------------------------------------------------ placement of the map

        private static void PlaceCityAroundHub(Transform city)
        {
            Physics.SyncTransforms();
            Bounds b = RendererBounds(city.gameObject);
            const float step = 2f;
            int nx = Mathf.CeilToInt(b.size.x / step), nz = Mathf.CeilToInt(b.size.z / step);
            float[,] ground = new float[nx, nz];
            bool[,] free = new bool[nx, nz];
            float top = b.max.y + 10f;
            RaycastHit[] hits = new RaycastHit[32];

            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                Vector3 o = new Vector3(b.min.x + (ix + 0.5f) * step, top, b.min.z + (iz + 0.5f) * step);
                int n = Physics.RaycastNonAlloc(o, Vector3.down, hits, b.size.y + 20f, ~0, QueryTriggerInteraction.Ignore);
                float lo = float.MaxValue, hi = float.MinValue;
                int count = 0;
                for (int h = 0; h < n; h++)
                {
                    if (!hits[h].collider.transform.IsChildOf(city)) continue;
                    lo = Mathf.Min(lo, hits[h].point.y);
                    hi = Mathf.Max(hi, hits[h].point.y);
                    count++;
                }
                ground[ix, iz] = lo;
                free[ix, iz] = count > 0 && hi - lo < 0.45f;
            }

            // Search both orientations for an axis-aligned rectangle that is entirely flat and open.
            Vector3 preferred = city.TransformPoint(new Vector3(PreferredHubModel.x, 0f, PreferredHubModel.y));
            float bestScore = float.MaxValue;
            Vector3 bestCentre = preferred;
            float bestGround = 0f;
            int bestYaw = -1;
            for (int yaw = 0; yaw < 2; yaw++)
            {
                Vector2 size = yaw == 0 ? HubSize : new Vector2(HubSize.y, HubSize.x);
                int wx = Mathf.CeilToInt(size.x / step), wz = Mathf.CeilToInt(size.y / step);
                for (int ix = 0; ix + wx <= nx; ix++)
                for (int iz = 0; iz + wz <= nz; iz++)
                {
                    bool ok = true;
                    float gmin = float.MaxValue, gmax = float.MinValue;
                    for (int x = ix; x < ix + wx && ok; x++)
                    for (int z = iz; z < iz + wz; z++)
                    {
                        if (!free[x, z]) { ok = false; break; }
                        gmin = Mathf.Min(gmin, ground[x, z]);
                        gmax = Mathf.Max(gmax, ground[x, z]);
                    }
                    if (!ok || gmax - gmin > 0.35f) continue;
                    Vector3 c = new Vector3(b.min.x + (ix + wx * 0.5f) * step, gmin, b.min.z + (iz + wz * 0.5f) * step);
                    float score = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(preferred.x, preferred.z)) + yaw * 5f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCentre = c;
                        bestGround = gmin;
                        bestYaw = yaw;
                    }
                }
            }

            if (bestYaw < 0)
            {
                // No perfectly open rectangle: fall back to the preferred square at street level.
                bestCentre = preferred;
                bestGround = SampleGround(city, preferred, b);
                bestYaw = 0;
                Log.AppendLine("Map: no fully open hub rectangle found; using the station square.");
            }

            float yawDeg = bestYaw == 0 ? 0f : 90f;
            // Move the city so the chosen centre sits at the world origin with its ground at y = 0.
            city.position -= new Vector3(bestCentre.x, bestGround, bestCentre.z);
            if (yawDeg != 0f) city.RotateAround(Vector3.zero, Vector3.up, yawDeg);
            city.position += new Vector3(0f, -0.02f, 0f); // tuck the street just under the old test ground plane
            Physics.SyncTransforms();
            Log.AppendLine($"Map: hub centre at model-space {bestCentre}, yaw {yawDeg}, score {bestScore:0.0}m from the station square.");

            // Report anything still intruding into the hub footprint.
            foreach (Vector3 p in new[] { new Vector3(0, 0, -7), new Vector3(-6, 0, 2), new Vector3(6, 0, 2), new Vector3(0, 0, 5.8f), new Vector3(-10, 0, -8), new Vector3(10, 0, 8) })
            {
                int n = Physics.RaycastNonAlloc(p + Vector3.up * 60f, Vector3.down, hits, 80f, ~0, QueryTriggerInteraction.Ignore);
                float hi = float.MinValue;
                string what = "";
                for (int h = 0; h < n; h++)
                    if (hits[h].collider.transform.IsChildOf(city) && hits[h].point.y > hi) { hi = hits[h].point.y; what = hits[h].collider.name; }
                Log.AppendLine($"  probe {p}: top surface y={hi:0.00} ({what})");
            }
        }

        private static float SampleGround(Transform city, Vector3 p, Bounds b)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, b.max.y + 10f, p.z), Vector3.down, b.size.y + 20f);
            float lo = float.MaxValue;
            foreach (RaycastHit h in hits) if (h.collider.transform.IsChildOf(city)) lo = Mathf.Min(lo, h.point.y);
            return lo == float.MaxValue ? p.y : lo;
        }

        // ------------------------------------------------------------------ hub

        private static void HideBlockouts()
        {
            // The prototype's grey ground and shop box give way to the real city (colliders stay as safety nets).
            foreach (string name in new[] { "Genesis_City_Test_Ground", "Shop Building", "Pack Terminal - 1000 GC", "Seat Interaction" })
            {
                GameObject go = FindIncludingInactive(name);
                if (go != null && go.TryGetComponent(out Renderer r)) r.enabled = false;
            }
            // The 4.8 m prototype slab and its rails: the real table is built on this root at runtime.
            foreach (string name in new[] { "Tabletop Arena Blockout", "Blue Rail", "Red Rail", "Back Rail", "Front Rail" })
            {
                GameObject go = FindIncludingInactive(name);
                if (go != null) go.SetActive(false);
            }
        }

        private static GameObject FindIncludingInactive(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(t => t.gameObject).FirstOrDefault(g => g.name == name && g.scene.IsValid());

        private static void DressCardShop(Transform hub)
        {
            GameObject shop = GameObject.Find("Genesis Card Shop Prototype");
            Vector3 shopPos = shop != null ? shop.transform.position : new Vector3(-6f, 0f, 2f);
            // A real storefront behind the pack terminal, facing the plaza (-z).
            GameObject front = SpawnSized(Polygon + "Buildings/Shop_A_prefab.prefab", hub, shopPos + new Vector3(0f, 0f, 3.2f), 180f, width: 8.5f);
            if (front != null) front.name = "Genesis Card Shop (storefront)";

            // The pack terminal becomes a real kiosk machine (the invisible box keeps the interaction collider).
            GameObject terminal = FindIncludingInactive("Pack Terminal - 1000 GC");
            if (terminal != null)
            {
                Vector3 tp = terminal.transform.position;
                GameObject kiosk = SpawnSized(Atm + "ATM3.prefab", hub, new Vector3(tp.x, 0f, tp.z), 180f, height: 1.95f);
                if (kiosk != null) kiosk.name = "Card Pack Kiosk (Pack Terminal)";
            }
            SpawnSized(Atm + "ATM1.prefab", hub, shopPos + new Vector3(-3.6f, 0f, 0.6f), 180f, height: 1.9f);
            SpawnSized(Atm + "ATM2.prefab", hub, shopPos + new Vector3(-2.9f, 0f, 0.6f), 180f, height: 1.9f);
            SpawnSized(Polygon + "Props/ColaMachine prefab.prefab", hub, shopPos + new Vector3(3.4f, 0f, 0.7f), 180f, height: 1.85f);
            SpawnSized(Polygon + "Props/Bin prefab.prefab", hub, shopPos + new Vector3(4.3f, 0f, 0.4f), 180f, height: 0.9f);
        }

        private static void DressHub(Transform hub)
        {
            // Street lamps on the four corners of the plaza, each with a warm light.
            foreach (Vector3 p in new[] { new Vector3(-12f, 0, -9f), new Vector3(12f, 0, -9f), new Vector3(-12f, 0, 9f), new Vector3(12f, 0, 9f) })
            {
                GameObject lamp = SpawnSized(Polygon + "Lamps/street_lamp 1 prefab.prefab", hub, p, p.x < 0 ? 90f : -90f, height: 5.2f);
                if (lamp == null) continue;
                Bounds lb = RendererBounds(lamp);
                GameObject light = new GameObject("Lamp Light");
                light.transform.SetParent(lamp.transform, true);
                light.transform.position = new Vector3(lb.center.x, lb.max.y - 0.35f, lb.center.z);
                Light l = light.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 11f;
                l.intensity = 2.2f;
                l.color = new Color(1f, 0.82f, 0.6f);
                l.shadows = LightShadows.None;
            }

            // Trees and planters frame the plaza without blocking the routes.
            SpawnSized(Nature + "Trees/PT_Fruit_Tree_01_green.prefab", hub, new Vector3(-12.5f, 0, 3f), 20f, height: 5.5f);
            SpawnSized(Nature + "Trees/PT_Fruit_Tree_01_green.prefab", hub, new Vector3(12.5f, 0, -3f), 140f, height: 5.2f);
            SpawnSized(Nature + "Trees/PT_Pine_Tree_03_green.prefab", hub, new Vector3(12.8f, 0, 6f), 60f, height: 6.5f);
            SpawnSized(Nature + "Trees/PT_Fruit_Tree_01_apples.prefab", hub, new Vector3(-12.8f, 0, -4f), 250f, height: 5.0f);
            foreach (float x in new[] { -4.2f, -1.4f, 1.4f, 4.2f })
                SpawnSized(Nature + "Shrubs/PT_Generic_Shrub_01_green.prefab", hub, new Vector3(x, 0, 7.2f), x * 40f, height: 0.9f);
            SpawnSized(Nature + "Rocks/PT_Generic_Rock_01.prefab", hub, new Vector3(11f, 0, 8.5f), 30f, height: 0.7f);

            // Seating and street furniture.
            SpawnSized(Polygon + "Props/bench prefab.prefab", hub, new Vector3(-8.5f, 0, 7.6f), 180f, length: 1.8f);
            SpawnSized(Polygon + "Props/bench prefab.prefab", hub, new Vector3(8.5f, 0, 7.6f), 180f, length: 1.8f);
            SpawnSized(Polygon + "Props/trashcan prefab.prefab", hub, new Vector3(-10.2f, 0, 7.6f), 0f, height: 0.95f);
            SpawnSized(Polygon + "Props/Bus stop prefab.prefab", hub, new Vector3(0f, 0, -10.6f), 0f, length: 4.2f);
            SpawnSized(Polygon + "Props/Hydrant prefab.prefab", hub, new Vector3(-11.3f, 0, -6.5f), 0f, height: 0.8f);
            SpawnSized(Polygon + "Props/Mail_box prefab.prefab", hub, new Vector3(11.3f, 0, -6.5f), -90f, height: 1.3f);
            foreach (float x in new[] { -10f, -7f, 7f, 10f })
                SpawnSized(Polygon + "Props/Flower mass prefab.prefab", hub, new Vector3(x, 0, 8.6f), 0f, length: 1.6f);

            // Show cars parked either side of the spawn.
            SpawnSized(Iconic + "Samurai_Red_A.prefab", hub, new Vector3(-9.5f, 0, -6.2f), 25f, length: 4.4f);
            SpawnSized(Iconic + "Samurai_BlueDark_A.prefab", hub, new Vector3(9.5f, 0, -6.2f), -25f, length: 4.4f);
        }

        private static void PlaceTraffic(Transform root, Transform city)
        {
            Transform traffic = new GameObject("Traffic (parked)").transform;
            traffic.SetParent(root, false);
            string[] cars = { "Car_TAXI_01", "Car_Sedan_02", "Car_TAXI_03", "Car_Sedan_04", "Car_Sedan_01" };
            for (int i = 0; i + 1 < CarRoute.Length && i / 2 < cars.Length; i += 2)
            {
                Vector3 a = city.TransformPoint(CarRoute[i]);
                Vector3 b = city.TransformPoint(CarRoute[i + 1]);
                Vector3 dir = b - a;
                dir.y = 0f;
                float yaw = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir).eulerAngles.y : 0f;
                Vector3 p = new Vector3(a.x, 0f, a.z);
                if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 80f)) p.y = hit.point.y;
                string path = OtakuCars + cars[i / 2] + ".fbx";
                SpawnSized(path, traffic, p, yaw, length: 4.5f);
            }
        }

        // ------------------------------------------------------------------ people (UMA)

        private static void SetupPlayerAvatar()
        {
            GameObject player = GameObject.Find("Player_Hikari_Blockout");
            if (player == null)
            {
                Log.AppendLine("Player: Player_Hikari_Blockout not found.");
                return;
            }
            GameObject avatar = SpawnUma(player.transform, Vector3.zero, 0f, "Human Male 3.0",
                new[] { "male_hoodie_blue_Recipe", "male_sportpants_grey_Recipe", "male_shoe_low_white.001_Recipe", "HairAnimeMessy_Recipe" },
                UmaLocomotion);
            if (avatar == null) return;
            avatar.name = "Genesis Avatar";
            // The CharacterController is the player's only collider; avatar physics would fight it.
            foreach (Collider c in avatar.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Rigidbody rb in avatar.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localRotation = Quaternion.identity;
            Log.AppendLine("Player: UMA avatar attached (Human Male 3.0).");
        }

        private static void SpawnNpcs(Transform parent)
        {
            GameObject table = GameObject.Find("Duel Table Prototype");
            GameObject shop = GameObject.Find("Genesis Card Shop Prototype");
            Vector3 tablePos = table != null ? table.transform.position : new Vector3(6f, 0f, 2f);
            Vector3 shopPos = shop != null ? shop.transform.position : new Vector3(-6f, 0f, 2f);

            // The CPU duelist waits on the far side of the duel table, facing the player's seat.
            GameObject rival = SpawnUma(parent, tablePos + new Vector3(0f, 0f, 1.05f), 180f, "Human Female 3.0",
                new[] { "Hoodie_turquoise_Recipe", "tights_gray_Recipe", "shoes_tall_white_Recipe", "HairPonytail_Recipe" }, UmaIdle);
            if (rival != null) rival.name = "NPC - Rival Duelist";

            GameObject clerk = SpawnUma(parent, shopPos + new Vector3(1.6f, 0f, 0.1f), 200f, "Human Male 3.0",
                new[] { "male_jacket_hive_Recipe", "male_sweatpants_black_Recipe", "male_shoes_tall_Recipe", "Hair_PulledBack_Recipe" }, UmaIdle);
            if (clerk != null) clerk.name = "NPC - Card Shop Clerk";

            // Two ambient tables where NPCs are mid-duel (same table, mat, arena and holograms as the real one).
            SpawnAmbientTable(parent, "Ambient Duel Table A", new Vector3(10.6f, 0f, 2.0f), 90f, 1,
                new[] { "male_hoodie_grey_Recipe", "male_sportpants_alt_black_Recipe", "male_shoes_tall_Recipe", "Hair_MessyPomp_Recipe" },
                new[] { "sportswear_top_Recipe", "shorts_turquoise_Recipe", "shoes_tall_turquoise.001_Recipe", "Hair_Bun_Recipe" });
            SpawnAmbientTable(parent, "Ambient Duel Table B", new Vector3(-10.2f, 0f, -1.8f), 90f, 2,
                new[] { "male_tanktop_yellow_Recipe", "male_shorts_hive_Recipe", "male_shoe_low_white.001_Recipe", "Hair_StraigntPulledBack_Recipe" },
                new[] { "jacket_hive.001_Recipe", "skirt_turquoise_Recipe", "shoes_tall_white_Recipe", "Hair_Bob_Recipe" });

            // Spectators watch the playable table from its sides.
            GameObject fanA = SpawnUma(parent, tablePos + new Vector3(-1.35f, 0f, 1.2f), 125f, "Human Female 3.0",
                new[] { "colors_top_Recipe", "colors_top_bottom_Recipe", "shoe_low_white_Recipe", "Hair_CurveUnder_Recipe" }, UmaIdle);
            if (fanA != null) fanA.name = "NPC - Spectator";

            GameObject fanB = SpawnUma(parent, tablePos + new Vector3(1.4f, 0f, 1.25f), 235f, "Human Male 3.0",
                new[] { "male_tshirt_white_Recipe", "male_sportpants_blueWhite_Recipe", "male_shoes_tall_turquoise_Recipe", "HairMessyUp_Recipe" }, UmaIdle);
            if (fanB != null) fanB.name = "NPC - Spectator";
        }

        private static void SpawnAmbientTable(Transform parent, string name, Vector3 position, float yaw, int seed, string[] maleOutfit, string[] femaleOutfit)
        {
            GameObject table = new GameObject(name);
            table.transform.SetParent(parent, false);
            table.transform.position = position;
            table.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            System.Type type = System.Type.GetType("DuelGenesis.Dueling.AmbientDuelTable, Assembly-CSharp");
            if (type != null)
            {
                Component c = table.AddComponent(type);
                SerializedObject so = new SerializedObject(c);
                SerializedProperty seedProp = so.FindProperty("seed");
                if (seedProp != null) { seedProp.intValue = seed; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            // Invisible collider so the player walks around the table rather than through it.
            BoxCollider col = table.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.38f, 0f);
            col.size = new Vector3(1.24f, 0.76f, 0.94f);

            Vector3 near = table.transform.TransformPoint(new Vector3(0f, 0f, -0.85f));
            Vector3 far = table.transform.TransformPoint(new Vector3(0f, 0f, 0.85f));
            GameObject a = SpawnUma(parent, near, yaw, "Human Male 3.0", maleOutfit, UmaIdle);
            if (a != null) a.name = "NPC - Duelist (" + name + ")";
            GameObject b = SpawnUma(parent, far, yaw + 180f, "Human Female 3.0", femaleOutfit, UmaIdle);
            if (b != null) b.name = "NPC - Duelist (" + name + ")";
        }

        private static GameObject SpawnUma(Transform parent, Vector3 position, float yaw, string race, string[] recipes, string controllerPath)
        {
            string prefabPath = File.Exists(UmaAvatarVariant) ? UmaAvatarVariant : UmaAvatarBase;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Log.AppendLine("UMA: avatar prefab not found; skipping characters.");
                return null;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = parent != null && parent.GetComponent<CharacterController>() != null ? parent.position : position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Component dca = go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "DynamicCharacterAvatar");
            if (dca == null)
            {
                Log.AppendLine("UMA: DynamicCharacterAvatar component missing on " + prefabPath);
                return go;
            }

            SerializedObject so = new SerializedObject(dca);
            so.FindProperty("activeRace.name").stringValue = race;
            SerializedProperty list = so.FindProperty("preloadWardrobeRecipes.recipes");
            if (list != null)
            {
                list.arraySize = recipes.Length;
                for (int i = 0; i < recipes.Length; i++)
                {
                    SerializedProperty e = list.GetArrayElementAtIndex(i);
                    SetString(e, "_recipeName", recipes[i]);
                    SetBool(e, "_enabledInDefaultWardrobe", true);
                    SerializedProperty races = e.FindPropertyRelative("_compatibleRaces");
                    if (races != null)
                    {
                        races.arraySize = 1;
                        races.GetArrayElementAtIndex(0).stringValue = race;
                    }
                }
            }
            SetBool(so.FindProperty("preloadWardrobeRecipes"), "loadDefaultRecipes", true);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            SerializedProperty anim = so.FindProperty("raceAnimationControllers.defaultAnimationController");
            if (anim != null && controller != null) anim.objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (go.TryGetComponent(out Animator animator)) animator.applyRootMotion = false;
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            return go;
        }

        private static void SetString(SerializedProperty parent, string rel, string value)
        {
            SerializedProperty p = parent?.FindPropertyRelative(rel);
            if (p != null && p.propertyType == SerializedPropertyType.String) p.stringValue = value;
        }

        private static void SetBool(SerializedProperty parent, string rel, bool value)
        {
            SerializedProperty p = parent?.FindPropertyRelative(rel);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value;
        }

        // ------------------------------------------------------------------ atmosphere

        private static void ApplyAtmosphere()
        {
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(OtakuSkybox);
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
            }
            GameObject sun = GameObject.Find("Sun");
            if (sun != null && sun.TryGetComponent(out Light l))
            {
                l.color = new Color(1f, 0.78f, 0.58f);
                l.intensity = 1.35f;
                l.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(28f, -40f, 0f);
                RenderSettings.sun = l;
            }
            DynamicGI.UpdateEnvironment();
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Spawn(string path, Transform parent, Vector3 position, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Log.AppendLine("Missing asset: " + path);
                return null;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>Spawns a prefab and scales it uniformly so its height, horizontal length or width matches real-world size,
        /// then sits it on the ground under <paramref name="position"/>.</summary>
        private static GameObject SpawnSized(string path, Transform parent, Vector3 position, float yaw,
            float height = 0f, float length = 0f, float width = 0f)
        {
            GameObject go = Spawn(path, parent, Vector3.zero, 0f);
            if (go == null) return null;
            Bounds b = RendererBounds(go);
            float scale = 1f;
            if (height > 0f && b.size.y > 0.001f) scale = height / b.size.y;
            else if (length > 0f) scale = length / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
            else if (width > 0f && b.size.x > 0.001f) scale = width / b.size.x;
            go.transform.localScale *= scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Put the bottom-centre of the bounds exactly on the target point, then drop to the ground surface.
            b = RendererBounds(go);
            Vector3 bottomCentre = new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += position - bottomCentre;
            float groundY = position.y;
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(position.x, 30f, position.z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(go.transform)) continue;
                if (h.point.y > position.y + 1.2f) continue; // ignore awnings/signs overhead
                best = Mathf.Max(best, h.point.y);
            }
            if (best > float.MinValue) groundY = best;
            go.transform.position += Vector3.up * (groundY - position.y);
            return go;
        }

        // ------------------------------------------------------------------ open world: shops + duel tables

        public const string DistrictsRootName = "Genesis Districts";

        private static readonly string[][] MaleOutfits =
        {
            new[] { "male_hoodie_grey_Recipe", "male_sportpants_alt_black_Recipe", "male_shoes_tall_Recipe", "Hair_MessyPomp_Recipe" },
            new[] { "male_tanktop_yellow_Recipe", "male_shorts_hive_Recipe", "male_shoe_low_white.001_Recipe", "Hair_StraigntPulledBack_Recipe" },
            new[] { "male_tshirt_white_Recipe", "male_sportpants_blueWhite_Recipe", "male_shoes_tall_turquoise_Recipe", "HairMessyUp_Recipe" },
            new[] { "male_jacket_hive_Recipe", "male_sweatpants_black_Recipe", "male_shoes_tall_Recipe", "Hair_PulledBack_Recipe" },
        };
        private static readonly string[][] FemaleOutfits =
        {
            new[] { "sportswear_top_Recipe", "shorts_turquoise_Recipe", "shoes_tall_turquoise.001_Recipe", "Hair_Bun_Recipe" },
            new[] { "jacket_hive.001_Recipe", "skirt_turquoise_Recipe", "shoes_tall_white_Recipe", "Hair_Bob_Recipe" },
            new[] { "colors_top_Recipe", "colors_top_bottom_Recipe", "shoe_low_white_Recipe", "Hair_CurveUnder_Recipe" },
            new[] { "Hoodie_turquoise_Recipe", "tights_gray_Recipe", "shoes_tall_white_Recipe", "HairPonytail_Recipe" },
        };

        /// <summary>
        /// Turns the whole map into play space: finds flat, open street spots spread across the city
        /// (farthest-point sampling from the hub) and puts a working card shop or a playable duel table,
        /// each with a resident duelist and a light beacon, on each of them.
        /// </summary>
        private static void PlaceDistricts(Transform root)
        {
            Transform old = root.Find(DistrictsRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform map = root.Find("Map");
            if (map == null) { Log.AppendLine("Districts: no Map."); return; }
            Transform districts = new GameObject(DistrictsRootName).transform;
            districts.SetParent(root, false);
            Physics.SyncTransforms();

            Bounds b = RendererBounds(map.gameObject);
            var open = new List<Vector3>();
            const float step = 4f;
            for (float x = b.min.x + 6f; x < b.max.x - 6f; x += step)
            for (float z = b.min.z + 6f; z < b.max.z - 6f; z += step)
            {
                if (new Vector2(x, z).magnitude < 28f) continue;                 // the hub has its own shop and table
                if (OpenSite(map, new Vector3(x, 0f, z), out float y)) open.Add(new Vector3(x, y, z));
            }

            // Spread sites as far from each other (and the hub) as possible.
            var chosen = new List<Vector3>();
            var anchors = new List<Vector3> { Vector3.zero };
            while (chosen.Count < 12 && open.Count > 0)
            {
                Vector3 best = default;
                float bestDist = -1f;
                foreach (Vector3 p in open)
                {
                    float d = anchors.Min(a => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(p.x, p.z)));
                    if (d > bestDist) { bestDist = d; best = p; }
                }
                if (bestDist < 30f) break;
                chosen.Add(best);
                anchors.Add(best);
            }

            int shops = 0, tables = 0;
            for (int i = 0; i < chosen.Count; i++)
            {
                Vector3 site = chosen[i];
                Vector3 away = new Vector3(site.x, 0f, site.z).normalized;
                float yaw = Quaternion.LookRotation(away, Vector3.up).eulerAngles.y;   // backs away from the hub
                if (i % 3 == 1) BuildCityShop(districts, site, yaw, ++shops);
                else BuildCityDuelTable(districts, site, yaw, ++tables);
            }
            Log.AppendLine($"Districts: {open.Count} open street spots, {tables} duel tables and {shops} card shops placed across the city.");
        }

        private static bool OpenSite(Transform map, Vector3 p, out float groundY)
        {
            groundY = 0f;
            float? first = null;
            for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
            {
                Vector3 o = new Vector3(p.x + ix * 3.5f, 250f, p.z + iz * 3.5f);
                RaycastHit[] hits = Physics.RaycastAll(o, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
                if (hits.Length == 0) return false;
                System.Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
                RaycastHit top = hits[0];
                if (top.collider.name.Contains("Test_Ground") && hits.Length > 1) top = hits[1];
                if (!top.collider.transform.IsChildOf(map)) return false;      // something built here already
                if (top.point.y > 3f || top.point.y < -8f) return false;       // a roof, not the street
                if (first == null) first = top.point.y;
                else if (Mathf.Abs(top.point.y - first.Value) > 0.35f) return false;
            }
            groundY = first.Value;
            // Nothing standing in a 7 x 7 m box from knee to head height.
            Collider[] blockers = Physics.OverlapBox(new Vector3(p.x, groundY + 1.7f, p.z), new Vector3(3.6f, 1.3f, 3.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            return blockers.All(c => c.name.Contains("Test_Ground"));
        }

        private static void BuildCityDuelTable(Transform parent, Vector3 site, float yaw, int number)
        {
            GameObject table = new GameObject($"City Duel Table {number}");
            table.transform.SetParent(parent, false);
            table.transform.SetPositionAndRotation(site, Quaternion.Euler(0f, yaw, 0f));
            var decor = table.AddComponent<DuelGenesis.Dueling.AmbientDuelTable>();
            decor.seed = 50 + number;
            decor.dealCards = false;
            BoxCollider col = table.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.38f, 0f);
            col.size = new Vector3(1.24f, 0.76f, 0.94f);
            var duel = table.AddComponent<DuelGenesis.Dueling.CityDuelTable>();
            duel.opponentSeed = 7919 * number + 101;
            duel.tableName = $"Street Duel Table {number}";

            // The resident duelist waits on the far side (one avatar per table keeps UMA cost down).
            Vector3 far = table.transform.TransformPoint(new Vector3(0f, 0f, 0.85f));
            bool female = number % 2 == 0;
            GameObject rival = SpawnUma(parent, far, yaw + 180f, female ? "Human Female 3.0" : "Human Male 3.0",
                female ? FemaleOutfits[number % FemaleOutfits.Length] : MaleOutfits[number % MaleOutfits.Length], UmaIdle);
            if (rival != null) rival.name = $"NPC - Resident Duelist (Table {number})";
            Beacon(table.transform, new Vector3(-1.9f, 0f, 0.4f), "DUEL TABLE", new Color(1f, 0.3f, 0.75f));
        }

        private static void BuildCityShop(Transform parent, Vector3 site, float yaw, int number)
        {
            GameObject shop = new GameObject($"City Card Shop {number}");
            shop.transform.SetParent(parent, false);
            shop.transform.SetPositionAndRotation(site, Quaternion.Euler(0f, yaw, 0f));
            Transform t = shop.transform;
            float back = yaw + 180f;   // kiosks face the hub side

            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 pos = t.TransformPoint(new Vector3(k * 1.1f, 0f, 1.2f));
                GameObject kiosk = SpawnSized(Atm + "ATM3.prefab", shop.transform, pos, back, height: 1.95f);
                if (kiosk != null) kiosk.name = "Card Pack Kiosk";
                var terminal = new GameObject("Pack Terminal - City Shop " + number);
                terminal.transform.SetParent(shop.transform, false);
                terminal.transform.SetPositionAndRotation(pos + Vector3.up * 1f, Quaternion.Euler(0f, back, 0f));
                BoxCollider c = terminal.AddComponent<BoxCollider>();
                c.size = new Vector3(1.1f, 2f, 1f);
                var shopTerminal = terminal.AddComponent<DuelGenesis.Shops.CardShopTerminal>();
                shopTerminal.shopName = $"Genesis Card Shop #{number + 1}";
            }
            SpawnSized(Polygon + "Props/ColaMachine prefab.prefab", shop.transform, t.TransformPoint(new Vector3(2.9f, 0f, 1.3f)), back, height: 1.85f);
            SpawnSized(Polygon + "Props/bench prefab.prefab", shop.transform, t.TransformPoint(new Vector3(-2.8f, 0f, -1.2f)), yaw + 90f, length: 1.8f);
            SpawnSized(Polygon + "Props/Bin prefab.prefab", shop.transform, t.TransformPoint(new Vector3(-2.9f, 0f, 1.3f)), back, height: 0.9f);

            GameObject clerk = SpawnUma(parent, t.TransformPoint(new Vector3(0f, 0f, 2.2f)), back, number % 2 == 0 ? "Human Female 3.0" : "Human Male 3.0",
                number % 2 == 0 ? FemaleOutfits[(number + 2) % FemaleOutfits.Length] : MaleOutfits[(number + 2) % MaleOutfits.Length], UmaIdle);
            if (clerk != null) clerk.name = $"NPC - Card Shop Clerk (Shop {number})";

            Beacon(shop.transform, new Vector3(0f, 0f, 2.6f), "CARD SHOP", new Color(0.2f, 0.85f, 1f));
        }

        private static void Beacon(Transform parent, Vector3 local, string label, Color color)
        {
            var go = new GameObject("Beacon - " + label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var beacon = go.AddComponent<DuelGenesis.Core.GenesisBeacon>();
            beacon.label = label;
            beacon.color = color;
        }

        public static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static int AddMeshColliders(GameObject go)
        {
            int n = 0;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                n++;
            }
            return n;
        }

        private static void SetStatic(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }
    }
}
#endif
