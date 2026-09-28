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

        [MenuItem("Duel Genesis/World/7. Place Kame Game Shop In The City")]
        public static void PlaceKameGameShop()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            if (prefab == null) { ImportKameGameShop(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath); }
            if (prefab == null) { EditorUtility.DisplayDialog("Kame Game Shop", "Import failed; see the Console.", "OK"); return; }
            GameObject city = GameObject.Find(GenesisWorldBuilder.CityRootName);
            if (city == null) { EditorUtility.DisplayDialog("Kame Game Shop", "Build Genesis City first (World > 3).", "OK"); return; }

            Transform old = city.transform.Find("Kame Game Shop");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, city.transform);
            shop.name = "Kame Game Shop";
            shop.transform.position = new Vector3(0f, -500f, 0f);   // out of the way while measuring
            Physics.SyncTransforms();
            Bounds b = GenesisWorldBuilder.RendererBounds(shop);
            Vector3 pivotOffset = shop.transform.position - b.center;
            pivotOffset.y = shop.transform.position.y - b.min.y;

            if (!FindFreeSpot(city.transform, b.size, out Vector3 ground, out float yaw))
            {
                ground = new Vector3(0f, 0f, 30f);
                yaw = 180f;
                Debug.LogWarning("Duel: Genesis found no clear flat lot for the Kame Game Shop; placed it 30 m north of the hub. Move it by hand if it clips.");
            }
            shop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            shop.transform.position = ground + Quaternion.Euler(0f, yaw, 0f) * new Vector3(pivotOffset.x, 0f, pivotOffset.z) + Vector3.up * pivotOffset.y;

            // The counter sells the Duel Genesis packs.
            Transform counter = FindDeep(shop.transform, "CashRegister") ?? FindDeep(shop.transform, "CounterTop_Prefab") ?? shop.transform;
            var terminal = counter.gameObject.AddComponent<DuelGenesis.Shops.CardShopTerminal>();
            terminal.shopName = "Kame Game Shop";
            if (counter.GetComponentInChildren<Collider>() == null) counter.gameObject.AddComponent<BoxCollider>();

            var beacon = new GameObject("Beacon - GAME SHOP");
            beacon.transform.SetParent(shop.transform, false);
            beacon.transform.position = GenesisWorldBuilder.RendererBounds(shop).center + Vector3.up * GenesisWorldBuilder.RendererBounds(shop).extents.y;
            var gb = beacon.AddComponent<DuelGenesis.Core.GenesisBeacon>();
            gb.label = "KAME GAME SHOP";
            gb.color = new Color(1f, 0.8f, 0.25f);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = shop;
            Debug.Log($"Duel: Genesis placed the Kame Game Shop at {shop.transform.position} (yaw {yaw:0}). Its counter sells booster packs.");
        }

        /// <summary>A flat patch of street near the hub big enough for <paramref name="size"/>, facing the hub.</summary>
        private static bool FindFreeSpot(Transform city, Vector3 size, out Vector3 ground, out float yaw)
        {
            Transform map = city.Find("Map");
            float radius = Mathf.Max(size.x, size.z) * 0.5f;
            for (float r = 22f; r <= 140f; r += 6f)
            for (int a = 0; a < 360; a += 12)
            {
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
                if (!Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.97f || (map != null && !hit.collider.transform.IsChildOf(map))) continue;
                bool flat = true;
                for (int i = 0; i < 8 && flat; i++)
                {
                    Vector3 edge = hit.point + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * radius;
                    flat = Physics.Raycast(edge + Vector3.up * 40f, Vector3.down, out RaycastHit e, 80f, ~0, QueryTriggerInteraction.Ignore)
                           && Mathf.Abs(e.point.y - hit.point.y) < 0.4f;
                }
                if (!flat) continue;
                Vector3 centre = hit.point + Vector3.up * (size.y * 0.5f + 0.3f);
                if (Physics.CheckBox(centre, new Vector3(radius, size.y * 0.5f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                ground = hit.point;
                yaw = Quaternion.LookRotation(-p.normalized).eulerAngles.y;   // front towards the hub
                return true;
            }
            ground = default;
            yaw = 0f;
            return false;
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
