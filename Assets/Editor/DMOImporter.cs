#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Brings pieces of the user's own game DMO into Duel Genesis from the AssetRipper export
    /// (C:\Games\DMO\DMO_Recovered\ExportedProject). Only the assets a piece actually uses are copied,
    /// with their .meta files so every reference survives. Ripped scripts and dummy shaders are never
    /// copied: materials are pointed at the real URP shaders instead. The copies live in
    /// Assets/ThirdParty/DMO, which is git-ignored (some meshes are over GitHub's 100 MB limit), so run
    /// the import again on any new machine.
    /// </summary>
    public static class DMOImporter
    {
        public const string DestRoot = "Assets/ThirdParty/DMO";
        private const string ExportPrefKey = "DG_DMO_EXPORT_ROOT";
        private const string DefaultExport = @"C:\Games\DMO\DMO_Recovered\ExportedProject";
        private static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        public static string ExportRoot
        {
            get => EditorPrefs.GetString(ExportPrefKey, DefaultExport);
            set => EditorPrefs.SetString(ExportPrefKey, value);
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Duel Genesis/DMO/Set DMO Export Folder...")]
        public static void SetExportFolder()
        {
            string picked = EditorUtility.OpenFolderPanel("AssetRipper export of DMO (the folder with Assets/ProjectSettings)", ExportRoot, "");
            if (!string.IsNullOrEmpty(picked)) ExportRoot = picked;
            Debug.Log("Duel: Genesis DMO export folder: " + ExportRoot);
        }

        // ------------------------------------------------------------------ Kame Game Shop

        public const string ShopPrefabPath = DestRoot + "/Prefabs/Kame Game Shop.prefab";

        [MenuItem("Duel Genesis/DMO/1. Import Kame Game Shop")]
        public static void ImportKameGameShop()
        {
            string source = Path.Combine(ProjectRoot, "DMOImport", "KameGameShop.prefab");
            if (!File.Exists(source)) { Debug.LogError("Duel: Genesis is missing " + source); return; }
            string yaml = File.ReadAllText(source);
            var log = new StringBuilder();
            int copied = ImportDependencies(GuidRx.Matches(yaml).Cast<Match>().Select(m => m.Groups[1].Value), log);
            if (copied < 0) return;

            Directory.CreateDirectory(Path.Combine(ProjectRoot, DestRoot, "Prefabs"));
            File.WriteAllText(Path.Combine(ProjectRoot, ShopPrefabPath), yaml);
            AssetDatabase.Refresh();
            FixMaterials(log);
            CleanPrefab(ShopPrefabPath, log);
            Debug.Log($"Duel: Genesis imported the Kame Game Shop ({copied} assets copied).\n{log}");
        }

        public const int CityShopCount = 3;          // plus the one on the plaza
        public const float MaxShopDistance = 200f;   // metres from the plaza, so shops stay within walking range

        [MenuItem("Duel Genesis/World/7. Place Kame Game Shops (Plaza + Across The City)")]
        public static void PlaceKameGameShop()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            if (prefab == null) { ImportKameGameShop(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath); }
            if (prefab == null) { EditorUtility.DisplayDialog("Kame Game Shop", "Import failed; see the Console.", "OK"); return; }
            GameObject city = GameObject.Find(GenesisWorldBuilder.CityRootName);
            if (city == null) { EditorUtility.DisplayDialog("Kame Game Shop", "Build Genesis City first (World > 3).", "OK"); return; }

            // Start clean: earlier Kame shops go, and the plaza's old placeholder shop is replaced.
            foreach (Transform t in city.transform.Cast<Transform>().Where(t => t.name.StartsWith("Kame Game Shop")).ToList())
                Object.DestroyImmediate(t.gameObject);
            bool hadOld = RemoveOldHubShop(city.transform, out Vector3 oldSpot);
            Physics.SyncTransforms();

            var log = new StringBuilder();
            var placed = new List<Vector3>();

            // The model's shop front (Millennium Eye sign + glass windows) is its local +X side, so a yaw of
            // "direction - 90" turns the front towards that direction.
            // 1. The plaza shop, where the old storefront stood, front to the plaza (-Z).
            Vector3 hubSpot = hadOld ? oldSpot : new Vector3(-6f, 0f, 5.2f);
            float hubGround = Physics.Raycast(hubSpot + Vector3.up * 50f, Vector3.down, out RaycastHit g, 120f, ~0, QueryTriggerInteraction.Ignore) ? g.point.y : 0f;
            GameObject first = SpawnShop(prefab, city.transform, "Kame Game Shop", new Vector3(hubSpot.x, hubGround, hubSpot.z), StreetFacingYaw(city.transform, hubSpot, 9f, Vector3.back));
            placed.Add(hubSpot);
            log.AppendLine($"Plaza shop at {first.transform.position}.");

            // 2. More shops on open lots spread across the map, each facing the plaza.
            Bounds size = SolidBounds(first);
            float radius = Mathf.Max(size.size.x, size.size.z) * 0.5f + 1.5f;
            List<Vector3> lots = FindLots(city.transform, radius, size.size.y);
            log.AppendLine($"{lots.Count} open lots fit a {radius * 2f:0} m shop.");
            for (int n = 0; n < CityShopCount; n++)
            {
                Vector3 best = default;
                float bestDist = -1f;
                foreach (Vector3 p in lots)
                {
                    float d = placed.Min(a => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(p.x, p.z)));
                    if (d > bestDist) { bestDist = d; best = p; }
                }
                if (bestDist < radius * 2f + 20f) { log.AppendLine("No more lots far enough apart."); break; }
                float yaw = StreetFacingYaw(city.transform, best, radius, new Vector3(-best.x, 0f, -best.z).normalized);
                GameObject shop = SpawnShop(prefab, city.transform, $"Kame Game Shop {n + 2}", best, yaw);
                placed.Add(best);
                Physics.SyncTransforms();
                lots.RemoveAll(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(best.x, best.z)) < radius * 2f + 20f);
                log.AppendLine($"{shop.name} at {best} ({bestDist:0} m from the nearest other shop).");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = first;
            Debug.Log($"Duel: Genesis placed {placed.Count} Kame Game Shops (their counters sell booster packs).\n{log}");
        }

        /// <summary>One shop, its centre at <paramref name="ground"/>, door towards <paramref name="yaw"/>.</summary>
        private static GameObject SpawnShop(GameObject prefab, Transform city, string name, Vector3 ground, float yaw)
        {
            var shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, city);
            shop.name = name;
            shop.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.Euler(0f, yaw, 0f));
            Physics.SyncTransforms();
            Bounds b = SolidBounds(shop);
            Vector3 offset = shop.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            shop.transform.position = ground + offset;

            // Let players walk in: the door keeps its look but loses its collider.
            Transform door = FindDeep(shop.transform, "Door 1");
            if (door != null) foreach (Collider c in door.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // The counter sells the Duel Genesis packs.
            Transform counter = FindDeep(shop.transform, "CashRegister") ?? FindDeep(shop.transform, "CounterTop_Prefab") ?? shop.transform;
            var terminal = counter.gameObject.AddComponent<DuelGenesis.Shops.CardShopTerminal>();
            terminal.shopName = name.Replace(" 2", "").Replace(" 3", "").Replace(" 4", "");
            if (counter.GetComponentInChildren<Collider>() == null) counter.gameObject.AddComponent<BoxCollider>();

            Physics.SyncTransforms();
            Bounds placed = SolidBounds(shop);
            AddDoorway(shop, city, name, ground.y);
            FurnishInterior(shop, city, name, ground.y);

            var beacon = new GameObject("Beacon - GAME SHOP");
            beacon.transform.SetParent(shop.transform, false);
            beacon.transform.position = new Vector3(placed.center.x, placed.max.y, placed.center.z);
            var gb = beacon.AddComponent<DuelGenesis.Core.GenesisBeacon>();
            gb.label = "KAME GAME SHOP";
            gb.color = new Color(1f, 0.8f, 0.25f);
            return shop;
        }

        [MenuItem("Duel Genesis/DEV/Teleport Into Nearest Kame Game Shop (Play Mode)")]
        public static void TeleportIntoShop()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Duel: Genesis: enter Play mode first."); return; }
            var player = Object.FindFirstObjectByType<DuelGenesis.Player.ThirdPersonPlayerController>();
            if (player == null) return;
            Transform spot = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.EndsWith(" - Inside Spot"))
                .OrderBy(t => Vector3.Distance(t.position, player.transform.position)).FirstOrDefault();
            if (spot != null) player.Teleport(spot.position, spot.rotation);
        }

        /// <summary>
        /// Yaw that turns the shop front (local +X) towards the street: of the eight compass directions, the one
        /// with the longest clear run at head height past the shop's own footprint (streets are long open
        /// corridors, walls and alleys are short). Ties prefer <paramref name="preferred"/>.
        /// </summary>
        private static float StreetFacingYaw(Transform city, Vector3 spot, float radius, Vector3 preferred)
        {
            Vector3 best = preferred;
            float bestScore = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Vector3 origin = new Vector3(spot.x, spot.y + 1.6f, spot.z) + dir * (radius + 0.5f);
                float free = Physics.Raycast(origin, dir, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 120f;
                float score = free + Vector3.Dot(dir, preferred) * 4f;   // small bias towards the plaza
                if (score > bestScore) { bestScore = score; best = dir; }
            }
            return Quaternion.LookRotation(best, Vector3.up).eulerAngles.y - 90f;   // local +X -> best
        }

        /// <summary>
        /// Fills the shop floor with our own pieces: two playable duel tables and a wall of the nine Genesis
        /// packs. Everything goes only where the floor is measured clear, so nothing clips into DMO's shelves.
        /// </summary>
        private static void FurnishInterior(GameObject shop, Transform city, string name, float groundY)
        {
            Transform fit = new GameObject(name + " - Interior").transform;
            fit.SetParent(city, false);
            Vector3 front = shop.transform.right, side = shop.transform.forward;
            Bounds b = SolidBounds(shop);
            Vector3 c = new Vector3(b.center.x, groundY, b.center.z);
            float half = Mathf.Min(b.extents.x, b.extents.z) - 0.8f;

            // Clear floor cells on a 0.5 m grid, in the shop's own axes (u along the front, v across).
            const float cell = 0.5f;
            int n = Mathf.Max(2, Mathf.FloorToInt(half * 2f / cell));
            var free = new bool[n, n];
            var floorY = new float[n, n];
            Vector3 Cell(int iu, int iv) => c + front * (-half + (iu + 0.5f) * cell) + side * (-half + (iv + 0.5f) * cell);
            for (int iu = 0; iu < n; iu++)
            for (int iv = 0; iv < n; iv++)
            {
                Vector3 p = Cell(iu, iv);
                if (!Physics.Raycast(new Vector3(p.x, groundY + 2.4f, p.z), Vector3.down, out RaycastHit h, 3.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!h.collider.transform.IsChildOf(shop.transform) || h.normal.y < 0.9f || Mathf.Abs(h.point.y - groundY) > 0.8f) continue;
                if (Physics.CheckBox(new Vector3(p.x, h.point.y + 1.0f, p.z), new Vector3(cell * 0.5f, 0.85f, cell * 0.5f), shop.transform.rotation, ~0, QueryTriggerInteraction.Collide)) continue;
                free[iu, iv] = true;
                floorY[iu, iv] = h.point.y;
            }

            bool Clear(int u0, int v0, int w, int d)
            {
                if (u0 < 0 || v0 < 0 || u0 + w > n || v0 + d > n) return false;
                for (int iu = u0; iu < u0 + w; iu++)
                for (int iv = v0; iv < v0 + d; iv++)
                    if (!free[iu, iv]) return false;
                return true;
            }
            void Take(int u0, int v0, int w, int d)
            {
                for (int iu = Mathf.Max(0, u0 - 1); iu < Mathf.Min(n, u0 + w + 1); iu++)
                for (int iv = Mathf.Max(0, v0 - 1); iv < Mathf.Min(n, v0 + d + 1); iv++)
                    free[iu, iv] = false;
            }
            Vector3 Centre(int u0, int v0, int w, int d)
            {
                Vector3 a = Cell(u0, v0), z = Cell(u0 + w - 1, v0 + d - 1);
                return new Vector3((a.x + z.x) * 0.5f, floorY[u0 + w / 2, v0 + d / 2], (a.z + z.z) * 0.5f);
            }

            // Keep the doorway clear: the strip just inside the front wall stays empty.
            for (int iu = n - 4; iu < n; iu++) for (int iv = 0; iv < n; iv++) if (iu >= 0) free[iu, iv] = false;

            // 1. Two duel tables (table + both seats need about 2.5 x 3 m), deepest in the shop first.
            int tables = 0;
            int tw = Mathf.CeilToInt(2.5f / cell), td = Mathf.CeilToInt(3.0f / cell);
            for (int iu = 0; iu < n && tables < 2; iu++)
            for (int iv = 0; iv < n && tables < 2; iv++)
            {
                bool across = Clear(iu, iv, td, tw);   // try both orientations
                bool along = !across && Clear(iu, iv, tw, td);
                if (!across && !along) continue;
                int w = across ? td : tw, d = across ? tw : td;
                Vector3 at = Centre(iu, iv, w, d);
                float yaw = Quaternion.LookRotation(across ? front : side, Vector3.up).eulerAngles.y;
                var table = new GameObject($"{name} - Duel Table {tables + 1}");
                table.transform.SetParent(fit, false);
                table.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                var decor = table.AddComponent<DuelGenesis.Dueling.AmbientDuelTable>();
                decor.seed = 90 + tables;
                decor.dealCards = false;
                var col = table.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.38f, 0f);
                col.size = new Vector3(1.24f, 0.76f, 0.94f);
                var duel = table.AddComponent<DuelGenesis.Dueling.CityDuelTable>();
                duel.opponentSeed = Mathf.Abs((name + tables).GetHashCode()) % 100000 + 7;
                duel.tableName = $"{name} Table {tables + 1}";
                Take(iu, iv, w, d);
                tables++;
            }

            // 2. The pack wall: all nine Genesis packs on a lit display, back to the deepest free wall.
            bool packs = false;
            int pw = Mathf.CeilToInt(3.2f / cell);
            for (int iu = 0; iu < n && !packs; iu++)
            for (int iv = 0; iv < n && !packs; iv++)
            {
                if (!Clear(iu, iv, 2, pw)) continue;
                Vector3 at = Centre(iu, iv, 2, pw);
                BuildPackWall(fit, name, at, Quaternion.LookRotation(front, Vector3.up));
                Take(iu, iv, 2, pw);
                packs = true;
            }
            Debug.Log($"Duel: Genesis furnished {name}: {tables} duel tables, pack wall {(packs ? "placed" : "had no room")}.");
        }

        /// <summary>A dark display board with the nine pack wrappers in a 5 + 4 layout, lit from the front.</summary>
        private static void BuildPackWall(Transform parent, string name, Vector3 foot, Quaternion facing)
        {
            var wall = new GameObject(name + " - Pack Wall").transform;
            wall.SetParent(parent, false);
            wall.SetPositionAndRotation(foot, facing);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.transform.SetParent(wall, false);
            board.transform.localPosition = new Vector3(0f, 1.25f, -0.05f);
            board.transform.localScale = new Vector3(3.1f, 1.9f, 0.08f);
            board.GetComponent<Renderer>().sharedMaterial = SavedMaterial("DG Pack Wall Board", lit, new Color(0.05f, 0.06f, 0.1f), null, Color.black);
            var col = wall.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.25f, -0.05f);
            col.size = new Vector3(3.1f, 1.9f, 0.3f);

            string[] ids = { "monster", "spell", "trap", "dark", "light", "earth", "fire", "water", "wind" };
            for (int i = 0; i < ids.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/DuelGenesis/Packs/pack_{ids[i]}.jpg");
                if (tex == null) continue;
                int row = i < 5 ? 0 : 1, col2 = i < 5 ? i : i - 5;
                float count = row == 0 ? 5 : 4;
                float x = (col2 - (count - 1) * 0.5f) * 0.58f;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Pack " + ids[i];
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(wall, false);
                quad.transform.localPosition = new Vector3(x, row == 0 ? 1.68f : 0.86f, 0.0f);
                quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // quad front faces -Z; turn it to face out (+Z)
                quad.transform.localScale = new Vector3(0.5f, 0.75f, 1f);
                quad.GetComponent<Renderer>().sharedMaterial = SavedMaterial("DG Pack " + ids[i], lit, Color.white, tex, Color.white * 0.35f);
            }

            var lightGo = new GameObject("Pack Wall Light");
            lightGo.transform.SetParent(wall, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.4f, 1.2f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.92f, 0.8f);
        }

        private static Material SavedMaterial(string matName, Shader shader, Color colour, Texture tex, Color emission)
        {
            const string folder = "Assets/Art/Generated/Shops";
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "Shops");
            string path = $"{folder}/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader) { name = matName }; AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", colour);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                if (tex != null) m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// The model's front door is solid, so players go in the way DMO did it: press E at the shop front to
        /// step inside, and E at the inner side of the front wall to step back out. The model's shop front is
        /// its local +X side. Door objects sit under the city root (not the scaled shop) with the shop's name
        /// as a prefix, so re-running the placement cleans them up.
        /// </summary>
        private static void AddDoorway(GameObject shop, Transform city, string name, float groundY)
        {
            Vector3 front = shop.transform.right;
            Bounds b = SolidBounds(shop);
            Vector3 centre = new Vector3(b.center.x, groundY + 1.1f, b.center.z);
            float reach = Mathf.Max(b.extents.x, b.extents.z) + 4f;

            Vector3 outerWall = centre + front * Mathf.Max(b.extents.x, b.extents.z) * 0.7f;
            if (Physics.Raycast(centre + front * reach, -front, out RaycastHit outer, reach * 2f, ~0, QueryTriggerInteraction.Ignore) &&
                outer.collider.transform.IsChildOf(shop.transform))
                outerWall = outer.point;
            Vector3 innerWall = outerWall - front * 0.4f;
            if (Physics.Raycast(centre, front, out RaycastHit inner, reach, ~0, QueryTriggerInteraction.Ignore) &&
                inner.collider.transform.IsChildOf(shop.transform))
                innerWall = inner.point;

            Vector3 insideSpot = innerWall - front * 2.2f;
            if (Physics.Raycast(new Vector3(insideSpot.x, groundY + 2.2f, insideSpot.z), Vector3.down, out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore))
                insideSpot.y = floor.point.y + 0.05f;
            else insideSpot.y = groundY + 0.05f;
            Vector3 outsideSpot = new Vector3(outerWall.x, groundY + 0.05f, outerWall.z) + front * 2.2f;

            Transform inside = new GameObject(name + " - Inside Spot").transform;
            inside.SetParent(city, false);
            inside.SetPositionAndRotation(insideSpot, Quaternion.LookRotation(-front, Vector3.up));
            Transform outside = new GameObject(name + " - Outside Spot").transform;
            outside.SetParent(city, false);
            outside.SetPositionAndRotation(outsideSpot, Quaternion.LookRotation(front, Vector3.up));

            Door(city, name + " - Door (enter)", new Vector3(outerWall.x, groundY, outerWall.z) + front * 0.7f, front, inside, "Enter the Kame Game Shop");
            Door(city, name + " - Door (leave)", new Vector3(innerWall.x, insideSpot.y, innerWall.z) - front * 0.6f, front, outside, "Leave the shop");
        }

        private static void Door(Transform city, string name, Vector3 foot, Vector3 front, Transform destination, string prompt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(city, false);
            go.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(front, Vector3.up));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.2f, 0f);
            box.size = new Vector3(3f, 2.4f, 1.2f);
            var door = go.AddComponent<DuelGenesis.Shops.ShopDoor>();
            door.destination = destination;
            door.prompt = prompt;
        }

        /// <summary>
        /// Deletes the plaza's old placeholder card shop: the prototype box and pack terminal, the red
        /// storefront, the pack kiosk and the ATMs, cola machine and bin beside it. Returns the storefront spot.
        /// </summary>
        public static bool RemoveOldHubShop(Transform city, out Vector3 spot)
        {
            spot = default;
            GameObject proto = GameObject.Find("Genesis Card Shop Prototype");
            GameObject front = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "Genesis Card Shop (storefront)" && go.scene.IsValid());
            if (proto == null && front == null) return false;

            Vector3 protoPos = proto != null ? proto.transform.position : front.transform.position - new Vector3(0f, 0f, 3.2f);
            spot = front != null ? front.transform.position : protoPos + new Vector3(0f, 0f, 3.2f);

            string[] propNames = { "Card Pack Kiosk", "ATM", "ColaMachine", "Bin" };
            List<GameObject> doomed = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene.IsValid() && t.parent != null && t.parent.name == "Hub Dressing" &&
                            propNames.Any(n => t.name.StartsWith(n)) &&
                            Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(protoPos.x, protoPos.z)) < 6.5f)
                .Select(t => t.gameObject).ToList();
            if (proto != null) doomed.Add(proto);
            if (front != null) doomed.Add(front);
            foreach (GameObject go in doomed.Distinct()) if (go != null) Object.DestroyImmediate(go);
            Debug.Log($"Duel: Genesis removed the old plaza card shop ({doomed.Count} objects).");
            return true;
        }

        /// <summary>Flat, empty street lots across the whole map where a shop of <paramref name="radius"/> fits.</summary>
        private static List<Vector3> FindLots(Transform city, float radius, float height)
        {
            var lots = new List<Vector3>();
            Transform map = city.Find("Map");
            if (map == null) return lots;
            Bounds b = GenesisWorldBuilder.RendererBounds(map.gameObject);
            for (float x = b.min.x + radius; x < b.max.x - radius; x += 6f)
            for (float z = b.min.z + radius; z < b.max.z - radius; z += 6f)
            {
                float fromHub = new Vector2(x, z).magnitude;
                if (fromHub < 45f || fromHub > MaxShopDistance) continue;   // not on the plaza, not at the map's edge
                float? y0 = null;
                bool ok = true;
                for (int ix = -1; ix <= 1 && ok; ix++)
                for (int iz = -1; iz <= 1 && ok; iz++)
                {
                    var o = new Vector3(x + ix * radius * 0.9f, 250f, z + iz * radius * 0.9f);
                    RaycastHit[] hits = Physics.RaycastAll(o, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
                    if (hits.Length == 0) { ok = false; break; }
                    System.Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
                    RaycastHit top = hits[0];
                    if (top.collider.name.Contains("Test_Ground") && hits.Length > 1) top = hits[1];
                    if (!top.collider.transform.IsChildOf(map) || top.point.y > 3f || top.point.y < -8f) { ok = false; break; }
                    if (y0 == null) y0 = top.point.y;
                    else if (Mathf.Abs(top.point.y - y0.Value) > 0.5f) ok = false;
                }
                if (!ok || y0 == null) continue;
                Vector3 centre = new Vector3(x, y0.Value + height * 0.5f + 0.3f, z);
                Collider[] blockers = Physics.OverlapBox(centre, new Vector3(radius, height * 0.5f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (blockers.All(c => c.name.Contains("Test_Ground"))) lots.Add(new Vector3(x, y0.Value, z));
            }
            return lots;
        }


        // ------------------------------------------------------------------ generic dependency copy

        /// <summary>Copies every export asset reachable from <paramref name="seedGuids"/>. Returns the count, or -1 on error.</summary>
        public static int ImportDependencies(IEnumerable<string> seedGuids, StringBuilder log)
        {
            string exportAssets = Path.Combine(ExportRoot, "Assets");
            if (!Directory.Exists(exportAssets))
            {
                EditorUtility.DisplayDialog("DMO import", "Cannot find the DMO export at\n" + ExportRoot + "\n\nUse Duel Genesis > DMO > Set DMO Export Folder.", "OK");
                return -1;
            }

            Dictionary<string, string> index = GuidIndex(exportAssets);
            var queue = new Queue<string>(seedGuids.Distinct());
            var seen = new HashSet<string>();
            int copied = 0, skipped = 0;
            while (queue.Count > 0)
            {
                string guid = queue.Dequeue();
                if (!seen.Add(guid) || !index.TryGetValue(guid, out string file)) continue;
                string rel = file.Substring(exportAssets.Length).TrimStart('\\', '/');
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".cs" || ext == ".shader" || ext == ".compute" || ext == ".dll" || ext == ".unity" ||
                    rel.StartsWith("Scripts") || rel.StartsWith("Plugins")) { skipped++; continue; }

                string dest = Path.Combine(ProjectRoot, DestRoot, rel);
                if (!File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(file, dest);
                    File.Copy(file + ".meta", dest + ".meta", true);
                    copied++;
                }
                // Follow references inside text assets (materials, controllers, small .asset files).
                if (ext == ".mat" || ext == ".controller" || ext == ".overridecontroller" || ext == ".prefab" || ext == ".mask" ||
                    (ext == ".asset" && new FileInfo(file).Length < 4_000_000))
                    foreach (Match m in GuidRx.Matches(File.ReadAllText(file))) queue.Enqueue(m.Groups[1].Value);
            }
            log.AppendLine($"DMO import: {copied} new assets copied, {skipped} scripts/shaders skipped, {seen.Count} references followed.");
            return copied;
        }

        private static Dictionary<string, string> _guidIndex;
        private static Dictionary<string, string> _shaderNames;

        private static Dictionary<string, string> GuidIndex(string exportAssets)
        {
            if (_guidIndex != null) return _guidIndex;
            _guidIndex = new Dictionary<string, string>();
            _shaderNames = new Dictionary<string, string>();
            foreach (string meta in Directory.EnumerateFiles(exportAssets, "*.meta", SearchOption.AllDirectories))
            {
                string guid = null;
                using (var reader = new StreamReader(meta))
                    for (int i = 0; i < 4 && guid == null; i++)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        if (line.StartsWith("guid: ")) guid = line.Substring(6).Trim();
                    }
                if (guid == null) continue;
                string asset = meta.Substring(0, meta.Length - 5);
                _guidIndex[guid] = asset;
                if (asset.EndsWith(".shader") && File.Exists(asset))
                {
                    Match m = Regex.Match(File.ReadLines(asset).Take(5).FirstOrDefault(l => l.TrimStart().StartsWith("Shader")) ?? "", "Shader \"([^\"]+)\"");
                    if (m.Success) _shaderNames[guid] = m.Groups[1].Value;
                }
            }
            return _guidIndex;
        }

        /// <summary>
        /// Materials arrive pointing at AssetRipper's dummy shaders. Point each one at the real shader of the
        /// same name when this project has it, otherwise URP Lit, and carry the main texture across.
        /// </summary>
        public static void FixMaterials(StringBuilder log)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            int fixedCount = 0, kept = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { DestRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                if (mat.shader != null && mat.shader.name != "Hidden/InternalErrorShader" && mat.shader.isSupported) { kept++; continue; }

                string text = File.ReadAllText(Path.Combine(ProjectRoot, path));
                Match sm = Regex.Match(text, @"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})");
                string wanted = sm.Success && _shaderNames != null && _shaderNames.TryGetValue(sm.Groups[1].Value, out string n) ? n : null;
                Shader target = wanted != null ? Shader.Find(wanted) : null;
                if (target == null || !target.isSupported) target = lit;

                Texture main = FirstTexture(mat, "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_Diffuse", "_DiffuseMap", "_MainTexture");
                Color colour = FirstColour(mat, "_BaseColor", "_Color", "_MainColor", "_Tint");
                mat.shader = target;
                if (main != null && mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null) mat.SetTexture("_BaseMap", main);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
                EditorUtility.SetDirty(mat);
                fixedCount++;
            }
            AssetDatabase.SaveAssets();
            log.AppendLine($"DMO import: {fixedCount} materials moved to project shaders, {kept} already fine.");
        }

        private static Texture FirstTexture(Material m, params string[] names)
        {
            foreach (string n in names)
                if (m.HasProperty(n) && m.GetTexture(n) != null) return m.GetTexture(n);
            // Shader is missing, so HasProperty can fail: read the saved properties instead.
            var so = new SerializedObject(m);
            SerializedProperty envs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; envs != null && i < envs.arraySize; i++)
            {
                SerializedProperty e = envs.GetArrayElementAtIndex(i);
                if (!names.Contains(e.FindPropertyRelative("first").stringValue)) continue;
                Object t = e.FindPropertyRelative("second.m_Texture").objectReferenceValue;
                if (t is Texture tex) return tex;
            }
            return null;
        }

        private static Color FirstColour(Material m, params string[] names)
        {
            var so = new SerializedObject(m);
            SerializedProperty cols = so.FindProperty("m_SavedProperties.m_Colors");
            for (int i = 0; cols != null && i < cols.arraySize; i++)
            {
                SerializedProperty e = cols.GetArrayElementAtIndex(i);
                if (names.Contains(e.FindPropertyRelative("first").stringValue)) return e.FindPropertyRelative("second").colorValue;
            }
            return Color.white;
        }

        /// <summary>Default layer, static flags for scenery, and no leftover missing-script components.</summary>
        private static void CleanPrefab(string path, StringBuilder log)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int removed = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 0;
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            log.AppendLine($"DMO import: prefab cleaned ({removed} missing scripts removed).");
        }

        /// <summary>
        /// Bounds of the static meshes only. Skinned figurines report huge, stale bounds from DMO, and a
        /// stray oversized mesh would do the same, so both are ignored.
        /// </summary>
        private static Bounds SolidBounds(GameObject go)
        {
            bool any = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>())
            {
                Bounds rb = r.bounds;
                if (rb.size.magnitude > 80f || rb.size.magnitude < 0.001f) continue;
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
            }
            return b;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
#endif
