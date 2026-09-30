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
    /// Puts the new Sketchfab landmarks into the city (run Production Assets > Set Up Cherry, Chinatown, Country Shop +
    /// Tokyo Tower Models first):
    ///   18. every cherry blossom tree (garden, parks, forest edge) becomes a realistic sakura,
    ///   20. three country shops, each with a card counter out front,
    ///   21. Tokyo Tower in the south-west park next to Genesis Plaza.
    /// (19, Chinatown, is in GenesisCityBlocks.) All are safe to re-run.
    /// </summary>
    public static partial class GenesisDuelCenter
    {
        public const string CountryShopsName = "Country Shops";
        public const string TokyoTowerName = "Tokyo Tower";

        /// <summary>Where the tower stands: the middle of the SW park, pushed out so its legs stay clear of the plaza ring.</summary>
        public static readonly Vector2 TokyoTowerSite = new Vector2(-75f, -75f);

        /// <summary>Country shops: far from the Card Vault and the four small shops, each backed onto a block with its forecourt on the street.</summary>
        public static readonly (string name, Vector2 site, float yaw)[] CountryShops =
        {
            ("Hometown Hobby Cards", new Vector2(60f, 263f), 180f),     // north, facing the z = 240 avenue
            ("Roadside Card Stop", new Vector2(-300f, 25f), 180f),     // west, facing the east-west boulevard
            ("Country Corner Cards", new Vector2(-25f, -300f), 90f),   // south, facing the north-south boulevard
        };

        /// <summary>True near any shop or the tower, so other tools (Chinatown) keep clear.</summary>
        public static bool NearLandmark(Vector2 p, float pad)
        {
            foreach (var s in SmallShops) if (Vector2.Distance(p, s.site) < s.clear + 8f + pad) return true;
            foreach (var s in CountryShops) if (Vector2.Distance(p, s.site) < 16f + pad) return true;
            return Vector2.Distance(p, TokyoTowerSite) < 45f + pad;
        }

        private static GameObject City()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return null; }
            return city;
        }

        private static void SaveScene()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        // ------------------------------------------------------------------ 18. sakura

        [MenuItem("Duel Genesis/World/18. Replace Cherry Blossoms With Realistic Sakura")]
        public static void ReplaceCherryBlossoms()
        {
            GameObject city = City();
            if (city == null) return;
            GameObject[] sakura = GenesisLandmarkModels.Cherries();
            if (sakura.Length == 0) { Debug.LogWarning("Duel: Genesis: set up the sakura first (Production Assets > Set Up Cherry, Chinatown, Country Shop + Tokyo Tower Models)."); return; }

            var old = new List<Transform>();
            foreach (string rootName in new[] { GenesisGarden.GardenName, GenesisGarden.BlossomName })
            {
                Transform root = city.transform.Find(rootName);
                if (root == null) continue;
                old.AddRange(root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Cherry Blossom Tree" || t.name == "Garden Tree"));
            }
            // Only the outermost match (never a tree inside a tree).
            old = old.Where(t => !old.Any(o => o != t && t.IsChildOf(o))).ToList();

            var rng = new System.Random(20260930);
            int swapped = 0;
            foreach (Transform t in old)
            {
                Bounds b = GenesisWorldBuilder.RendererBounds(t.gameObject);
                bool hasBounds = b.size.sqrMagnitude > 0.0001f;
                Vector3 foot = hasBounds ? new Vector3(b.center.x, b.min.y, b.center.z) : t.position;
                Transform parent = t.parent;
                int sibling = t.GetSiblingIndex();
                Object.DestroyImmediate(t.gameObject);

                int pick = rng.Next(sakura.Length);
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(sakura[pick], parent);
                tree.name = "Cherry Blossom Tree";
                tree.transform.SetSiblingIndex(sibling);
                tree.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                Bounds tb = GenesisWorldBuilder.RendererBounds(tree);
                bool big = GenesisLandmarkModels.CherryIds[pick] == "C";
                float h = big ? 8.5f + (float)rng.NextDouble() * 2f : 6f + (float)rng.NextDouble() * 2.5f;
                if (tb.size.y > 0.01f) tree.transform.localScale = Vector3.one * (h / tb.size.y);
                tree.transform.position = foot;
                GameObjectUtility.SetStaticEditorFlags(tree, (StaticEditorFlags)0);   // LOD trees stay out of static batching
                swapped++;
            }
            SaveScene();
            Debug.Log($"Duel: Genesis replaced {swapped} cherry blossom trees with realistic sakura ({sakura.Length} kinds).");
        }

        // ------------------------------------------------------------------ 20. country shops

        [MenuItem("Duel Genesis/World/20. Place 3 Country Shops")]
        public static void PlaceCountryShops()
        {
            GameObject city = City();
            if (city == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GenesisLandmarkModels.CountryShopPrefab);
            if (prefab == null) { Debug.LogWarning("Duel: Genesis: set up the country shop first (Production Assets > Set Up Cherry, Chinatown, Country Shop + Tokyo Tower Models)."); return; }
            Transform oldRoot = city.transform.Find(CountryShopsName);
            if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);
            var all = new GameObject(CountryShopsName).transform;
            all.SetParent(city.transform, false);
            EnsureFolder(VaultFolder);
            var m = new ShopMats();

            float half = GenesisLandmarkModels.CountryShopWidth * 0.5f;
            int cleared = 0;
            var log = new List<string>();
            foreach (var (name, site, yaw) in CountryShops)
            {
                cleared += ClearSite(city.transform, site, half + 4f);
                var root = new GameObject(name).transform;
                root.SetParent(all, false);
                root.SetPositionAndRotation(new Vector3(site.x, 0f, site.y), Quaternion.Euler(0f, yaw, 0f));
                var shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                shop.name = "Country Shop";

                // A card counter on the forecourt, facing the street (local +Z), with a pack board behind it.
                Counter(root, name, new Vector3(0f, 0f, half - 2.4f), 0f, 2.6f, m, m.Gold);
                PackBoard(root, new Vector3(3.6f, 0f, half - 3.4f), 0f, 2.2f, 3 + all.childCount, m, m.Gold);
                var sign = new GameObject("Shop Light").AddComponent<Light>();
                sign.transform.SetParent(root, false);
                sign.transform.localPosition = new Vector3(0f, 3.6f, half - 1.5f);
                sign.type = LightType.Point;
                sign.range = 9f;
                sign.intensity = 2.2f;
                sign.color = new Color(1f, 0.86f, 0.62f);

                var arrival = new GameObject(name + " - Arrival Spot").transform;
                arrival.SetParent(root, false);
                arrival.localPosition = new Vector3(0f, 0.1f, half + 3f);
                arrival.localRotation = Quaternion.LookRotation(Vector3.back);
                log.Add($"{name} at ({site.x:0}, {site.y:0}), {Vector2.Distance(site, new Vector2(CardShopSpot.x, CardShopSpot.z)):0} m from the main shop");
            }
            AssetDatabase.SaveAssets();
            SaveScene();
            Debug.Log($"Duel: Genesis placed {CountryShops.Length} country shops (cleared {cleared} buildings/plants):\n" + string.Join("\n", log));
        }

        // ------------------------------------------------------------------ 21. Tokyo Tower

        [MenuItem("Duel Genesis/World/21. Raise Tokyo Tower Beside The Plaza")]
        public static void PlaceTokyoTower()
        {
            GameObject city = City();
            if (city == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GenesisLandmarkModels.TokyoTowerPrefab);
            if (prefab == null) { Debug.LogWarning("Duel: Genesis: set up Tokyo Tower first (Production Assets > Set Up Cherry, Chinatown, Country Shop + Tokyo Tower Models)."); return; }
            Transform old = city.transform.Find(TokyoTowerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var tower = (GameObject)PrefabUtility.InstantiatePrefab(prefab, city.transform);
            tower.name = TokyoTowerName;
            tower.transform.SetPositionAndRotation(new Vector3(TokyoTowerSite.x, 0f, TokyoTowerSite.y), Quaternion.identity);
            Bounds b = GenesisWorldBuilder.RendererBounds(tower);
            float reach = Mathf.Max(b.extents.x, b.extents.z);

            // Clear trees, plants and the park torii from under the legs.
            int cleared = ClearSite(city.transform, TokyoTowerSite, reach * 0.8f);
            Transform torii = city.transform.Find(GenesisGarden.ParkToriiName);
            if (torii != null)
                foreach (Transform t in torii.Cast<Transform>().ToList())
                    if (Mathf.Abs(t.position.x - TokyoTowerSite.x) < reach + 3f && Mathf.Abs(t.position.z - TokyoTowerSite.y) < reach + 3f) { Object.DestroyImmediate(t.gameObject); cleared++; }

            // Warm red floodlights up the legs, like the real tower at night.
            var lights = new GameObject("Tower Floodlights").transform;
            lights.SetParent(tower.transform, false);
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var l = new GameObject("Floodlight " + (i + 1)).AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.localPosition = new Vector3(Mathf.Sin(a) * reach * 1.05f, 1f, Mathf.Cos(a) * reach * 1.05f);
                l.transform.LookAt(tower.transform.position + Vector3.up * b.size.y * 0.45f);
                l.type = LightType.Spot;
                l.spotAngle = 38f;
                l.range = b.size.y * 0.9f;
                l.intensity = 30f;
                l.color = new Color(1f, 0.55f, 0.3f);
                l.shadows = LightShadows.None;
            }
            var beacon = new GameObject("Aviation Beacon").AddComponent<Light>();
            beacon.transform.SetParent(tower.transform, false);
            beacon.transform.localPosition = new Vector3(0f, b.size.y - 1f, 0f);
            beacon.type = LightType.Point;
            beacon.range = 18f;
            beacon.intensity = 6f;
            beacon.color = new Color(1f, 0.15f, 0.1f);
            beacon.gameObject.AddComponent<DuelGenesis.Core.GenesisBlink>();

            SaveScene();
            Debug.Log($"Duel: Genesis raised Tokyo Tower ({b.size.y:0} m tall, {b.size.x:0} m across the legs) at ({TokyoTowerSite.x:0}, {TokyoTowerSite.y:0}); cleared {cleared} things from under it.");
        }
    }

    // ---------------------------------------------------------------------- 19. Chinatown

    public static partial class GenesisCityBlocks
    {
        public const string ChinatownName = "Chinatown";

        /// <summary>Chinatown Street: both sides of the z = -120 avenue between x = 120 and x = 240.</summary>
        public static readonly Rect ChinatownStreet = new Rect(126f, -140f, 108f, 40f);

        [MenuItem("Duel Genesis/World/19. Scatter Chinatown Houses")]
        public static void Chinatown()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            var houses = Enumerable.Range(1, GenesisLandmarkModels.ChinatownHouses)
                .Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(GenesisLandmarkModels.ChinatownHouse(i))).Where(g => g != null).ToList();
            if (houses.Count == 0) { Debug.LogWarning("Duel: Genesis: set up the Chinatown houses first (Production Assets > Set Up Cherry, Chinatown, Country Shop + Tokyo Tower Models)."); return; }
            var sizes = houses.Select(h => GenesisWorldBuilder.RendererBounds(h).size).ToList();

            Transform old = city.transform.Find(ChinatownName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(ChinatownName).transform;
            root.SetParent(city.transform, false);
            Transform akiba = city.transform.Find(RootName);
            var nature = NatureObjects(city.transform);
            var rng = new System.Random(20260930);
            int placed = 0, removed = 0, cleared = 0;

            // Every city block (as World > 15 lays them out), minus the parks and the garden.
            var blocks = new List<Rect>();
            for (int i = 0; i < Lines.Length - 1; i++)
            for (int j = 0; j < Lines.Length - 1; j++)
            {
                float x0 = Lines[i] + RoadHalf(Lines[i]), x1 = Lines[i + 1] - RoadHalf(Lines[i + 1]);
                float z0 = Lines[j] + RoadHalf(Lines[j]), z1 = Lines[j + 1] - RoadHalf(Lines[j + 1]);
                var rect = new Rect(x0, z0, x1 - x0, z1 - z0);
                if (Mathf.Abs(rect.center.x) < 120f && Mathf.Abs(rect.center.y) < 120f) continue;
                if (GenesisGarden.IsReserved(rect.center)) continue;
                blocks.Add(rect);
            }

            // 1. Chinatown Street: a full row of shop-houses on both sides of the avenue.
            foreach (Rect b in blocks)
            {
                if (b.xMin < 126f || b.xMax > 234f) continue;
                if (Mathf.Abs(b.yMin - (-108f)) < 1f) placed += Row(root, akiba, b, 1, b.xMin + 1f, b.xMax - 1f, houses, sizes, rng, nature, ref removed, ref cleared, "Chinatown Street");
                if (Mathf.Abs(b.yMax - (-132f)) < 1f) placed += Row(root, akiba, b, 0, b.xMin + 1f, b.xMax - 1f, houses, sizes, rng, nature, ref removed, ref cleared, "Chinatown Street");
            }

            // 2. Short rows of three to five houses tucked into other street fronts round the city.
            int rows = 0;
            for (int tries = 0; tries < 200 && rows < 7; tries++)
            {
                Rect b = blocks[rng.Next(blocks.Count)];
                if (b.Overlaps(ChinatownStreet)) continue;
                int side = rng.Next(4);
                bool alongX = side < 2;
                float lo = alongX ? b.xMin + 4f : b.yMin + 22f, hi = alongX ? b.xMax - 4f : b.yMax - 22f;
                float length = 3 + rng.Next(3);
                length *= 6f;
                if (hi - lo < length + 2f) continue;
                float start = lo + (float)rng.NextDouble() * (hi - lo - length);
                float edge = side == 0 ? b.yMax : side == 1 ? b.yMin : side == 2 ? b.xMax : b.xMin;
                float inward = side == 0 || side == 2 ? -1f : 1f;
                bool ok = true;
                for (float s = start; s <= start + length && ok; s += 6f)
                {
                    float across = edge + inward * 5f;
                    Vector2 c = alongX ? new Vector2(s, across) : new Vector2(across, s);
                    if (Blocked(c, 6f) || GenesisDuelCenter.NearLandmark(c, 6f) || root.Cast<Transform>().Any(r => Vector2.Distance(new Vector2(r.position.x, r.position.z), c) < 40f)) ok = false;
                }
                if (!ok) continue;
                int n = Row(root, akiba, b, side, start, start + length, houses, sizes, rng, nature, ref removed, ref cleared, $"Chinatown Row {rows + 1}");
                if (n > 0) { placed += n; rows++; }
            }

            SaveScene();
            Debug.Log($"Duel: Genesis built Chinatown: {placed} shop-houses (Chinatown Street plus {rows} short rows), replacing {removed} buildings and moving {cleared} plants.");
        }

        private static void SaveScene()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Lines one street front (side 0 = north/+Z, 1 = south/-Z, 2 = east/+X, 3 = west/-X) between two positions along it
        /// with shop-houses shoulder to shoulder, fronts to the street; the buildings they replace are removed first.
        /// </summary>
        private static int Row(Transform root, Transform akiba, Rect r, int side, float from, float to, List<GameObject> houses, List<Vector3> sizes,
                               System.Random rng, List<Transform> nature, ref int removed, ref int cleared, string name)
        {
            bool alongX = side < 2;
            float edge = side == 0 ? r.yMax : side == 1 ? r.yMin : side == 2 ? r.xMax : r.xMin;
            float inward = side == 0 || side == 2 ? -1f : 1f;
            Vector3 facing = side == 0 ? Vector3.forward : side == 1 ? Vector3.back : side == 2 ? Vector3.right : Vector3.left;
            float yaw = Quaternion.LookRotation(facing).eulerAngles.y;
            float deepest = sizes.Max(s => s.z) + 1.5f;

            // Clear the strip the row stands on.
            float a0 = Mathf.Min(edge, edge + inward * (deepest + 1f)), a1 = Mathf.Max(edge, edge + inward * (deepest + 1f));
            Rect strip = alongX ? Rect.MinMaxRect(from, a0, to, a1) : Rect.MinMaxRect(a0, from, a1, to);
            if (akiba != null)
                foreach (Transform t in akiba.GetComponentsInChildren<Transform>(true).Where(x => x.parent != null && x.parent.parent == akiba && x.GetComponent<BoxCollider>() != null).ToList())
                {
                    Bounds bb = GenesisWorldBuilder.RendererBounds(t.gameObject);
                    Rect footprint = Rect.MinMaxRect(bb.min.x, bb.min.z, bb.max.x, bb.max.z);
                    if (footprint.Overlaps(strip)) { Object.DestroyImmediate(t.gameObject); removed++; }
                }

            var row = new GameObject(name).transform;
            row.SetParent(root, false);
            row.position = new Vector3(alongX ? (from + to) * 0.5f : edge, 0f, alongX ? edge : (from + to) * 0.5f);
            int n = 0, last = -1;
            float t0 = from;
            while (true)
            {
                int k = rng.Next(houses.Count);
                if (k == last && houses.Count > 1) k = (k + 1) % houses.Count;
                Vector3 size = sizes[k];
                if (t0 + size.x > to) break;
                float along = t0 + size.x * 0.5f, across = edge + inward * (0.6f + size.z * 0.5f);
                Vector2 c = alongX ? new Vector2(along, across) : new Vector2(across, along);
                if (!Blocked(c, size.x * 0.5f) && !GenesisDuelCenter.NearLandmark(c, size.x * 0.5f))
                {
                    var house = (GameObject)PrefabUtility.InstantiatePrefab(houses[k], row);
                    house.name = houses[k].name;
                    house.transform.SetPositionAndRotation(new Vector3(c.x, 0f, c.y), Quaternion.Euler(0f, yaw, 0f));
                    cleared += ClearNature(nature, c, alongX ? size.x : size.z, alongX ? size.z : size.x);
                    n++;
                    last = k;
                }
                t0 += size.x + 0.05f;   // shoulder to shoulder, like the original street
            }
            if (n == 0) Object.DestroyImmediate(row.gameObject);
            return n;
        }
    }
}
#endif
