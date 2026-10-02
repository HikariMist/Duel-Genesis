#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Non-destructive visual finishing pass for normal Genesis City buildings. Existing imported buildings,
    /// shops, collision and gameplay objects stay untouched; this pass adds removable grounding/roof/entrance
    /// details under its own root and upgrades renderer presentation settings.
    /// </summary>
    public static class GenesisBuildingPolish
    {
        private const string RootName = "Building Finish Pass";
        private const string MaterialFolder = "Assets/Art/Generated/OpenWorld/BuildingPolish";
        private const string FoundationMaterialPath = MaterialFolder + "/DG Building Foundation.mat";
        private const string MetalMaterialPath = MaterialFolder + "/DG Building Metal.mat";
        private const string AccentMaterialPath = MaterialFolder + "/DG Building Accent.mat";

        private static readonly float[] StreetLines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };

        [MenuItem("Duel Genesis/World/25. Crisp Building Facades + Rooftops")]
        public static void PolishBuildings()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null)
            {
                Debug.LogWarning("Duel: Genesis: build/open Genesis City first (World > 9).");
                return;
            }

            Transform previous = city.transform.Find(RootName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);

            Transform root = new GameObject(RootName).transform;
            root.SetParent(city.transform, false);

            Material foundation = EnsureMaterial(FoundationMaterialPath, new Color(0.14f, 0.16f, 0.19f, 1f), 0.08f, 0f);
            Material metal = EnsureMaterial(MetalMaterialPath, new Color(0.28f, 0.32f, 0.37f, 1f), 0.34f, 0.22f);
            Material accent = EnsureMaterial(AccentMaterialPath, new Color(0.53f, 0.42f, 0.24f, 1f), 0.28f, 0.12f);

            int polished = 0;
            foreach (Transform building in Candidates(city.transform))
            {
                if (building == null || building.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                Bounds b = GenesisWorldBuilder.RendererBounds(building.gameObject);
                if (!UsefulBuilding(b)) continue;

                Transform detail = new GameObject("Finish - " + building.name).transform;
                detail.SetParent(root, false);

                AddFoundation(detail, b, foundation);
                AddStreetEntrance(detail, b, metal, accent);
                if (LikelyUrbanRoof(b)) AddRoofEquipment(detail, b, metal, foundation, StableHash(building.name));
                UpgradeRenderers(building);
                polished++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"Duel: Genesis building finish pass complete: {polished} city buildings grounded and polished with street-entry/roof detail. Existing models and gameplay collision were not replaced.");
        }

        private static IEnumerable<Transform> Candidates(Transform city)
        {
            Transform blocks = city.Find(GenesisCityBlocks.RootName);
            if (blocks != null)
            {
                foreach (Transform block in blocks)
                    foreach (Transform building in block)
                        yield return building;
            }

            Transform infill = city.Find("Environment Polish/Realistic Infill Buildings");
            if (infill != null)
                foreach (Transform building in infill)
                    yield return building;
        }

        private static bool UsefulBuilding(Bounds b)
        {
            if (b.size.x < 4.5f || b.size.z < 4.5f || b.size.y < 5f) return false;
            if (b.size.x > 70f || b.size.z > 70f || b.size.y > 180f) return false;
            return true;
        }

        private static bool LikelyUrbanRoof(Bounds b)
        {
            float foot = Mathf.Max(b.size.x, b.size.z);
            return b.size.y >= 11f && foot >= 7f && b.size.y / Mathf.Max(1f, foot) < 5.5f;
        }

        private static void AddFoundation(Transform parent, Bounds b, Material mat)
        {
            Vector3 size = new Vector3(b.size.x + 0.28f, 0.12f, b.size.z + 0.28f);
            Vector3 pos = new Vector3(b.center.x, b.min.y + 0.055f, b.center.z);
            AddBox(parent, "Grounding Plinth", pos, size, Quaternion.identity, mat);
        }

        private static void AddStreetEntrance(Transform parent, Bounds b, Material metal, Material accent)
        {
            Vector3 normal = FrontDirection(b.center);
            bool xFacing = Mathf.Abs(normal.x) > 0.5f;
            float front = xFacing
                ? (normal.x > 0f ? b.max.x : b.min.x)
                : (normal.z > 0f ? b.max.z : b.min.z);

            float doorSpan = Mathf.Clamp((xFacing ? b.size.z : b.size.x) * 0.20f, 1.8f, 3.2f);
            Vector3 stepSize = xFacing ? new Vector3(0.72f, 0.10f, doorSpan) : new Vector3(doorSpan, 0.10f, 0.72f);
            Vector3 stepPos = b.center;
            stepPos.y = b.min.y + 0.055f;
            if (xFacing) stepPos.x = front + normal.x * 0.30f;
            else stepPos.z = front + normal.z * 0.30f;
            AddBox(parent, "Entrance Step", stepPos, stepSize, Quaternion.identity, accent);

            if (b.size.y < 7f) return;
            Vector3 canopySize = xFacing ? new Vector3(1.05f, 0.12f, doorSpan + 0.35f) : new Vector3(doorSpan + 0.35f, 0.12f, 1.05f);
            Vector3 canopyPos = b.center;
            canopyPos.y = Mathf.Min(b.min.y + 2.65f, b.max.y - 0.45f);
            if (xFacing) canopyPos.x = front + normal.x * 0.42f;
            else canopyPos.z = front + normal.z * 0.42f;
            AddBox(parent, "Entrance Canopy", canopyPos, canopySize, Quaternion.identity, metal);

            // Two small vertical accents make the entry read clearly at street level without replacing facade textures.
            float side = doorSpan * 0.5f + 0.18f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 trimPos = b.center;
                trimPos.y = b.min.y + 1.25f;
                if (xFacing)
                {
                    trimPos.x = front + normal.x * 0.08f;
                    trimPos.z += side * s;
                }
                else
                {
                    trimPos.z = front + normal.z * 0.08f;
                    trimPos.x += side * s;
                }
                Vector3 trimSize = xFacing ? new Vector3(0.10f, 2.30f, 0.12f) : new Vector3(0.12f, 2.30f, 0.10f);
                AddBox(parent, "Entry Trim", trimPos, trimSize, Quaternion.identity, accent);
            }
        }

        private static void AddRoofEquipment(Transform parent, Bounds b, Material metal, Material dark, int seed)
        {
            var rng = new System.Random(seed);
            float insetX = Mathf.Max(1.4f, b.extents.x * 0.30f);
            float insetZ = Mathf.Max(1.4f, b.extents.z * 0.30f);
            float roofY = b.max.y + 0.08f;

            float accessW = Mathf.Clamp(b.size.x * 0.18f, 1.6f, 3.2f);
            float accessD = Mathf.Clamp(b.size.z * 0.18f, 1.6f, 3.0f);
            Vector3 accessPos = new Vector3(
                b.center.x + Mathf.Lerp(-insetX, insetX, (float)rng.NextDouble()) * 0.35f,
                roofY + 0.75f,
                b.center.z + Mathf.Lerp(-insetZ, insetZ, (float)rng.NextDouble()) * 0.35f);
            AddBox(parent, "Roof Access", accessPos, new Vector3(accessW, 1.45f, accessD), Quaternion.identity, dark);

            int units = b.size.x * b.size.z > 180f ? 2 : 1;
            for (int i = 0; i < units; i++)
            {
                float w = 1.15f + (float)rng.NextDouble() * 0.75f;
                float d = 0.9f + (float)rng.NextDouble() * 0.65f;
                float h = 0.45f + (float)rng.NextDouble() * 0.28f;
                Vector3 p = new Vector3(
                    Mathf.Clamp(b.center.x + Mathf.Lerp(-insetX, insetX, (float)rng.NextDouble()), b.min.x + 1f, b.max.x - 1f),
                    roofY + h * 0.5f,
                    Mathf.Clamp(b.center.z + Mathf.Lerp(-insetZ, insetZ, (float)rng.NextDouble()), b.min.z + 1f, b.max.z - 1f));
                AddBox(parent, "HVAC Unit", p, new Vector3(w, h, d), Quaternion.identity, metal);

                // A thinner cap breaks the primitive silhouette and makes the unit read as manufactured equipment.
                AddBox(parent, "HVAC Cap", p + Vector3.up * (h * 0.5f + 0.035f), new Vector3(w + 0.10f, 0.07f, d + 0.10f), Quaternion.identity, dark);
            }
        }

        private static Vector3 FrontDirection(Vector3 centre)
        {
            float best = float.MaxValue;
            Vector3 direction = Vector3.forward;
            foreach (float line in StreetLines)
            {
                float dx = Mathf.Abs(centre.x - line);
                if (dx < best)
                {
                    best = dx;
                    direction = centre.x > line ? Vector3.left : Vector3.right;
                }

                float dz = Mathf.Abs(centre.z - line);
                if (dz < best)
                {
                    best = dz;
                    direction = centre.z > line ? Vector3.back : Vector3.forward;
                }
            }
            return direction;
        }

        private static void UpgradeRenderers(Transform building)
        {
            foreach (Renderer r in building.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                r.allowOcclusionWhenDynamic = true;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(r.gameObject) |
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);
                EditorUtility.SetDirty(r);
            }
        }

        private static GameObject AddBox(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = size;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private static Material EnsureMaterial(string path, Color color, float smoothness, float metallic)
        {
            EnsureFolders();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated/OpenWorld")) AssetDatabase.CreateFolder("Assets/Art/Generated", "OpenWorld");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art/Generated/OpenWorld", "BuildingPolish");
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                if (!string.IsNullOrEmpty(value))
                    foreach (char c in value) hash = hash * 31 + c;
                return hash;
            }
        }
    }
}
#endif
