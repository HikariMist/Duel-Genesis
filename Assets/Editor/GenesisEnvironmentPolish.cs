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

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Finishing pass for the open Genesis City. It keeps Claude's existing city, Colosseum, shops,
    /// roads and nature intact, then adds a tiled photo-scanned grass surface and carefully placed
    /// realistic infill buildings only where an empty lot is actually available.
    /// </summary>
    public static class GenesisEnvironmentPolish
    {
        private const string RootName = "Environment Polish";
        private const string InfillName = "Realistic Infill Buildings";
        private const string GrassId = "leafy_grass";
        private const string GrassFolder = GenesisAssetDownloads.PolyHavenFolder + "/" + GrassId;
        private const string GrassDiffuse = GrassFolder + "/leafy_grass_diff_4k.jpg";
        private const string GrassNormal = GrassFolder + "/leafy_grass_nor_gl_4k.jpg";
        private const string GrassMaterial = "Assets/Art/Generated/OpenWorld/OW Grass.mat";
        private const string ChinatownFolder = "Assets/ThirdParty/Sketchfab/Chinatown/Prefabs";
        private const float Half = 420f;

        private static readonly float[] StreetLines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };
        private static readonly float[] CellLines = { -420f, -360f, -240f, -120f, 0f, 120f, 240f, 360f, 420f };

        [MenuItem("Duel Genesis/World/24. Polish Genesis City (4K grass + realistic infill)")]
        public static void Polish()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null)
            {
                Debug.LogWarning("Duel: Genesis: build/open the Genesis City first (World > 9).");
                return;
            }

            if (!EnsureGrassMaps())
            {
                Debug.LogWarning("Duel: Genesis: the grass download did not complete. Buildings will still be polished, but the grass was left unchanged.");
            }
            else
            {
                ApplyGrass(city.transform);
            }

            Transform old = city.transform.Find(RootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var polishRoot = new GameObject(RootName).transform;
            polishRoot.SetParent(city.transform, false);

            int buildings = AddRealisticInfill(city.transform, polishRoot);
            int renderers = SmoothPresentation(city.transform);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = polishRoot.gameObject;
            Debug.Log($"Duel: Genesis environment polish complete: 4K Poly Haven grass applied, {buildings} realistic infill buildings placed, {renderers} renderers normalized. Colosseum, card shops, roads and protected landmarks were kept clear.");
        }

        [MenuItem("Duel Genesis/Downloads/Poly Haven Leafy Grass (CC0, 4K)")]
        public static void DownloadGrassOnly()
        {
            if (EnsureGrassMaps()) Debug.Log("Duel: Genesis downloaded the Poly Haven Leafy Grass 4K surface (CC0).");
        }

        private static bool EnsureGrassMaps()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string diffDisk = Path.Combine(project, GrassDiffuse);
            string normalDisk = Path.Combine(project, GrassNormal);
            Directory.CreateDirectory(Path.GetDirectoryName(diffDisk));

            bool ok = true;
            try
            {
                if (!File.Exists(diffDisk))
                {
                    EditorUtility.DisplayProgressBar("Duel: Genesis", "Downloading Poly Haven 4K grass colour...", 0.2f);
                    ok &= GenesisAssetDownloads.Save(
                        "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/leafy_grass/leafy_grass_diff_4k.jpg", diffDisk);
                }
                if (!File.Exists(normalDisk))
                {
                    EditorUtility.DisplayProgressBar("Duel: Genesis", "Downloading Poly Haven 4K grass normal map...", 0.65f);
                    ok &= GenesisAssetDownloads.Save(
                        "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/leafy_grass/leafy_grass_nor_gl_4k.jpg", normalDisk);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (!ok || !File.Exists(diffDisk) || !File.Exists(normalDisk)) return false;

            string license = Path.Combine(project, GenesisAssetDownloads.PolyHavenFolder, "License.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(license));
            string credit = "Leafy Grass by Charlotte Baglioni, Poly Haven (https://polyhaven.com/a/leafy_grass), CC0 1.0 public domain.\n";
            if (!File.Exists(license) || !File.ReadAllText(license).Contains("Leafy Grass"))
                File.AppendAllText(license, credit);

            AssetDatabase.Refresh();
            ConfigureTexture(GrassDiffuse, false);
            ConfigureTexture(GrassNormal, true);
            return true;
        }

        private static void ConfigureTexture(string path, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ApplyGrass(Transform city)
        {
            Transform ground = city.Find("Map/Ground");
            if (ground == null) { Debug.LogWarning("Duel: Genesis: Map/Ground was not found; grass material not changed."); return; }
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer == null) return;

            EnsureGeneratedFolders();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterial);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = "OW Grass" };
                AssetDatabase.CreateAsset(material, GrassMaterial);
            }

            Texture2D diff = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassDiffuse);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassNormal);
            material.SetColor("_BaseColor", new Color(0.86f, 0.92f, 0.82f, 1f));
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", diff);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", diff);
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.72f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;

            // Leafy Grass represents a 2 m scan. Slightly larger repeats reduce obvious patterning across the city.
            Vector2 tiling = new Vector2(260f, 260f);
            if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", tiling);
            if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", tiling);
            EditorUtility.SetDirty(material);
            groundRenderer.sharedMaterial = material;
            EditorUtility.SetDirty(groundRenderer);
        }

        private static void EnsureGeneratedFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated/OpenWorld")) AssetDatabase.CreateFolder("Assets/Art/Generated", "OpenWorld");
        }

        private static int AddRealisticInfill(Transform city, Transform polishRoot)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ChinatownFolder });
            List<GameObject> prefabs = prefabGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).IndexOf("House", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null)
                .ToList();

            if (prefabs.Count == 0)
            {
                Debug.LogWarning("Duel: Genesis: Chinatown house prefabs were not found; realistic infill was skipped.");
                return 0;
            }

            var root = new GameObject(InfillName).transform;
            root.SetParent(polishRoot, false);
            var rng = new System.Random(20260930);
            List<Bounds> shopBounds = FindProtectedShopBounds(city);
            int placed = 0;

            Physics.SyncTransforms();
            for (int ix = 0; ix < CellLines.Length - 1 && placed < 18; ix++)
            for (int iz = 0; iz < CellLines.Length - 1 && placed < 18; iz++)
            {
                float x0 = CellLines[ix], x1 = CellLines[ix + 1];
                float z0 = CellLines[iz], z1 = CellLines[iz + 1];
                float inset = 22f;
                if (x1 - x0 < inset * 2f + 12f || z1 - z0 < inset * 2f + 12f) continue;

                Vector2 cellCentre = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
                // Preserve the four landscaped inner blocks around the plaza.
                if (Mathf.Abs(cellCentre.x) < 120f && Mathf.Abs(cellCentre.y) < 120f) continue;

                int attempts = (x1 - x0 > 90f && z1 - z0 > 90f) ? 2 : 1;
                for (int a = 0; a < attempts && placed < 18; a++)
                {
                    float px = Mathf.Lerp(x0 + inset, x1 - inset, 0.28f + 0.44f * (float)rng.NextDouble());
                    float pz = Mathf.Lerp(z0 + inset, z1 - inset, 0.28f + 0.44f * (float)rng.NextDouble());
                    Vector2 p = new Vector2(px, pz);
                    if (Protected(p, 18f, shopBounds)) continue;

                    GameObject prefab = prefabs[rng.Next(prefabs.Count)];
                    GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                    if (go == null) continue;
                    go.name = "Realistic Infill - " + prefab.name;
                    go.transform.position = new Vector3(px, 0f, pz);
                    go.transform.rotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);

                    Bounds initial = GenesisWorldBuilder.RendererBounds(go);
                    float footprint = Mathf.Max(initial.size.x, initial.size.z);
                    if (footprint < 0.1f) { UnityEngine.Object.DestroyImmediate(go); continue; }
                    float target = Mathf.Lerp(18f, 28f, (float)rng.NextDouble());
                    float s = Mathf.Clamp(target / footprint, 0.45f, 2.0f);
                    go.transform.localScale *= s;
                    Bounds b = GenesisWorldBuilder.RendererBounds(go);
                    go.transform.position += new Vector3(px - b.center.x, -b.min.y + 0.02f, pz - b.center.z);
                    Physics.SyncTransforms();
                    b = GenesisWorldBuilder.RendererBounds(go);

                    float radius = Mathf.Max(b.extents.x, b.extents.z) + 4f;
                    Vector2 final = new Vector2(b.center.x, b.center.z);
                    if (Protected(final, radius, shopBounds) || CrossesRoad(b, 4f) || OverlapsExisting(go, b))
                    {
                        UnityEngine.Object.DestroyImmediate(go);
                        continue;
                    }

                    EnsureBuildingCollider(go, b);
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        r.shadowCastingMode = ShadowCastingMode.On;
                        r.receiveShadows = true;
                        r.allowOcclusionWhenDynamic = true;
                        GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                    }
                    placed++;
                }
            }
            return placed;
        }

        private static List<Bounds> FindProtectedShopBounds(Transform city)
        {
            var result = new List<Bounds>();
            foreach (Transform t in city.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!(n.Contains("card shop") || n.Contains("kame game shop") || n.Contains("card vault"))) continue;
                Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs.Skip(1)) b.Encapsulate(r.bounds);
                b.Expand(new Vector3(16f, 6f, 16f));
                if (!result.Any(existing => Vector3.Distance(existing.center, b.center) < 1f)) result.Add(b);
            }
            return result;
        }

        private static bool Protected(Vector2 p, float radius, List<Bounds> shopBounds)
        {
            if (p.magnitude < 72f + radius) return true;
            if (GenesisDuelCenter.InColosseum(p, radius + 12f)) return true;
            if (GenesisDuelCenter.InCardShopLot(p)) return true;
            if (GenesisGarden.IsReserved(p)) return true;
            if (Mathf.Abs(p.x) > Half - radius - 22f || Mathf.Abs(p.y) > Half - radius - 22f) return true;
            Vector3 wp = new Vector3(p.x, 2f, p.y);
            return shopBounds.Any(b => Mathf.Abs(wp.x - b.center.x) <= b.extents.x + radius && Mathf.Abs(wp.z - b.center.z) <= b.extents.z + radius);
        }

        private static bool CrossesRoad(Bounds b, float clearance)
        {
            foreach (float line in StreetLines)
            {
                float half = Mathf.Abs(line) < 0.01f ? 14f : 12f;
                if (b.min.x < line + half + clearance && b.max.x > line - half - clearance) return true;
                if (b.min.z < line + half + clearance && b.max.z > line - half - clearance) return true;
            }
            return false;
        }

        private static bool OverlapsExisting(GameObject candidate, Bounds b)
        {
            Vector3 half = b.extents * 0.92f;
            half.y = Mathf.Max(0.2f, half.y - 0.15f);
            Collider[] hits = Physics.OverlapBox(b.center + Vector3.up * 0.12f, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in hits)
            {
                if (c == null || c.transform.IsChildOf(candidate.transform)) continue;
                string n = c.name.ToLowerInvariant();
                // The city ground, paved roads and curb surfaces are expected underneath a building footprint.
                if (c.bounds.max.y <= 0.22f || n.Contains("ground") || n.Contains("road") || n.Contains("pavement") || n.Contains("paving") || n.Contains("forecourt")) continue;
                return true;
            }
            return false;
        }

        private static void EnsureBuildingCollider(GameObject go, Bounds world)
        {
            if (go.GetComponentsInChildren<Collider>(true).Any()) return;
            BoxCollider c = go.AddComponent<BoxCollider>();
            Vector3 localCentre = go.transform.InverseTransformPoint(world.center);
            Vector3 localA = go.transform.InverseTransformVector(new Vector3(world.size.x, 0f, 0f));
            Vector3 localB = go.transform.InverseTransformVector(new Vector3(0f, world.size.y, 0f));
            Vector3 localC = go.transform.InverseTransformVector(new Vector3(0f, 0f, world.size.z));
            c.center = localCentre;
            c.size = new Vector3(Mathf.Max(Mathf.Abs(localA.x), Mathf.Abs(localC.x)), Mathf.Abs(localB.y), Mathf.Max(Mathf.Abs(localA.z), Mathf.Abs(localC.z)));
        }

        private static int SmoothPresentation(Transform city)
        {
            int count = 0;
            foreach (MeshRenderer r in city.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.allowOcclusionWhenDynamic = true;
                foreach (Material m in r.sharedMaterials)
                    if (m != null) m.enableInstancing = true;
                count++;
            }
            return count;
        }
    }
}
#endif
