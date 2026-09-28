#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Mounts the artwork in Resources/DuelGenesis/Artwork as lit LED billboards on the building
    /// facades around the Genesis City hub. Walls are found by ray-casting out from the hub, and each
    /// board is only placed where the whole rectangle sits flat on one facade.
    /// </summary>
    public static class GenesisBillboards
    {
        public const string RootName = "Genesis Billboards";
        private const string ArtworkFolder = "Assets/Resources/DuelGenesis/Artwork";

        private struct Spot
        {
            public Vector3 centre;
            public Vector3 normal;
            public float width;
            public float angle;
            public float distance;
        }

        [MenuItem("Duel Genesis/World/4. Place Billboards (Artwork folder)")]
        public static void PlaceMenu()
        {
            GameObject cityRoot = GameObject.Find(GenesisWorldBuilder.CityRootName);
            if (cityRoot == null)
            {
                EditorUtility.DisplayDialog("Billboards", "Build Genesis City first (Duel Genesis > World > 3).", "OK");
                return;
            }
            var log = new StringBuilder();
            int placed = Place(cityRoot.transform, log);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis placed {placed} billboards.\n{log}");
        }

        /// <summary>Replaces any previous billboards under <paramref name="cityRoot"/>. Returns how many were placed.</summary>
        public static int Place(Transform cityRoot, StringBuilder log)
        {
            Transform old = cityRoot.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            List<Texture2D> art = LoadArtwork();
            if (art.Count == 0)
            {
                log.AppendLine("Billboards: no images in " + ArtworkFolder);
                return 0;
            }

            Transform map = cityRoot.Find("Map");
            if (map == null) { log.AppendLine("Billboards: no Map under the city root."); return 0; }
            Physics.SyncTransforms();

            List<Spot> spots = FindSpots(map, art.Min(t => t.width / (float)t.height));   // fit the tallest artwork
            var root = new GameObject(RootName).transform;
            root.SetParent(cityRoot, false);

            // Two boards per image at most; biggest walls first, spread around the square.
            int count = Mathf.Min(spots.Count, art.Count * 2);
            for (int i = 0; i < count; i++)
            {
                Texture2D texture = art[i % art.Count];
                Build(root, spots[i], texture, i);
                log.AppendLine($"Billboards: {texture.name} {spots[i].width:0.0} m wide, {spots[i].distance:0} m from the hub at {spots[i].angle:0} deg.");
            }
            if (count == 0) log.AppendLine("Billboards: no flat facade faced the hub.");
            return count;
        }

        private static List<Texture2D> LoadArtwork()
        {
            var textures = new List<Texture2D>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtworkFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    bool dirty = importer.maxTextureSize != 2048 || importer.wrapMode != TextureWrapMode.Clamp || !importer.mipmapEnabled;
                    importer.maxTextureSize = 2048;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = true;
                    importer.anisoLevel = 8;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    if (dirty) importer.SaveAndReimport();
                }
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t != null) textures.Add(t);
            }
            return textures.OrderBy(t => t.name).ToList();
        }

        // ------------------------------------------------------------------ finding walls

        private static List<Spot> FindSpots(Transform map, float aspect)
        {
            var found = new List<Spot>();
            float[] heights = { 8f, 12f, 16f };
            float[] widths = { 14f, 12f, 10f, 8f, 6.5f };

            for (int a = 0; a < 360; a += 5)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                foreach (float h in heights)
                {
                    if (!Physics.Raycast(new Vector3(0f, h, 0f), dir, out RaycastHit hit, 110f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!hit.collider.transform.IsChildOf(map) || hit.distance < 13f) continue;
                    Vector3 n = hit.normal;
                    if (Mathf.Abs(n.y) > 0.2f) continue;
                    n.y = 0f;
                    n.Normalize();
                    if (Vector3.Dot(n, -dir) < 0.55f) continue;   // facade must face the square

                    foreach (float w in widths)
                    {
                        float height = w / aspect;
                        Vector3 centre = hit.point;
                        centre.y = Mathf.Max(h, 4f + height * 0.5f);
                        if (Fits(map, centre, n, w, height))
                        {
                            found.Add(new Spot { centre = centre, normal = n, width = w, angle = a, distance = hit.distance });
                            break;
                        }
                    }
                }
            }

            // Largest and nearest first, then keep them apart so every wall gets one board.
            var chosen = new List<Spot>();
            foreach (Spot s in found.OrderByDescending(s => s.width * 3f - s.distance * 0.15f))
            {
                bool clash = chosen.Any(c => Vector3.Distance(c.centre, s.centre) < Mathf.Max(c.width, s.width) + 4f ||
                                             Mathf.Abs(Mathf.DeltaAngle(c.angle, s.angle)) < 28f);
                if (!clash) chosen.Add(s);
                if (chosen.Count >= 8) break;
            }
            return chosen;
        }

        /// <summary>True when a width x height rectangle at <paramref name="centre"/> lies flat on one facade.</summary>
        private static bool Fits(Transform map, Vector3 centre, Vector3 normal, float width, float height)
        {
            Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;
            float plane = Vector3.Dot(centre, normal);
            for (int ix = -2; ix <= 2; ix++)
            for (int iy = -1; iy <= 1; iy++)
            {
                Vector3 p = centre + right * (ix * width * 0.24f) + Vector3.up * (iy * height * 0.46f);
                Vector3 origin = p + normal * 3f;
                if (!Physics.Raycast(origin, -normal, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (!hit.collider.transform.IsChildOf(map)) return false;
                if (Vector3.Dot(hit.normal, normal) < 0.9f) return false;
                if (Mathf.Abs(Vector3.Dot(hit.point, normal) - plane) > 0.7f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ building a board

        private static void Build(Transform root, Spot spot, Texture2D texture, int index)
        {
            float aspect = texture.width / (float)texture.height;
            float width = spot.width;
            float height = width / aspect;

            var board = new GameObject($"Billboard {index + 1} - {texture.name}").transform;
            board.SetParent(root, false);
            board.position = spot.centre + spot.normal * 0.35f;
            board.rotation = Quaternion.LookRotation(-spot.normal, Vector3.up);   // quad front faces +normal

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            // Screen: the artwork, self-lit like an LED panel so it reads at dusk.
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(board, false);
            screen.transform.localScale = new Vector3(width, height, 1f);   // a Quad's front faces -Z: out of the wall
            var screenMat = new Material(lit) { name = "DG Billboard " + texture.name };
            screenMat.SetTexture("_BaseMap", texture);
            screenMat.SetColor("_BaseColor", Color.white);
            screenMat.SetFloat("_Smoothness", 0.55f);
            screenMat.EnableKeyword("_EMISSION");
            screenMat.SetTexture("_EmissionMap", texture);
            screenMat.SetColor("_EmissionColor", Color.white * 0.85f);
            screenMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            screen.GetComponent<Renderer>().sharedMaterial = SaveMaterial(screenMat);

            // Housing: a dark metal frame a little bigger than the screen, with neon strips top and bottom.
            var frameMat = SaveMaterial(NewLit(lit, new Color(0.06f, 0.07f, 0.09f), 0.45f, 0.6f, Color.black, "DG Billboard Frame"));
            float border = Mathf.Max(0.25f, width * 0.025f);
            Box(board, "Frame Back", new Vector3(0f, 0f, 0.18f), new Vector3(width + border * 2f, height + border * 2f, 0.3f), frameMat);
            var cyan = SaveMaterial(NewLit(lit, new Color(0.1f, 0.5f, 0.7f), 0.8f, 0f, new Color(0.2f, 0.9f, 1.4f) * 2.2f, "DG Billboard Neon Cyan"));
            var magenta = SaveMaterial(NewLit(lit, new Color(0.6f, 0.1f, 0.5f), 0.8f, 0f, new Color(1.4f, 0.3f, 1.1f) * 2.2f, "DG Billboard Neon Magenta"));
            Box(board, "Neon Top", new Vector3(0f, height * 0.5f + border * 0.5f, -0.02f), new Vector3(width + border * 2f, border * 0.35f, 0.12f), index % 2 == 0 ? cyan : magenta);
            Box(board, "Neon Bottom", new Vector3(0f, -height * 0.5f - border * 0.5f, -0.02f), new Vector3(width + border * 2f, border * 0.35f, 0.12f), index % 2 == 0 ? magenta : cyan);

            // Two brackets back to the wall so it reads as mounted, not floating.
            for (int side = -1; side <= 1; side += 2)
                Box(board, "Bracket", new Vector3(side * width * 0.35f, 0f, 0.4f), new Vector3(0.25f, height * 0.8f, 0.5f), frameMat);

            GameObjectUtility.SetStaticEditorFlags(board.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static void Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material NewLit(Shader lit, Color baseColor, float smoothness, float metallic, Color emission, string name)
        {
            var m = new Material(lit) { name = name };
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>Scene objects must reference saved materials or they turn pink after a reload.</summary>
        private static Material SaveMaterial(Material m)
        {
            const string folder = "Assets/Art/Generated/Billboards";
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "Billboards");
            string path = $"{folder}/{m.name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.globalIlluminationFlags = m.globalIlluminationFlags;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
#endif
