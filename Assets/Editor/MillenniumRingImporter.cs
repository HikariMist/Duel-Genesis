using System.Linq;
using DuelGenesis.Characters;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Converts the custom Millennium Ring OBJ into a deterministic one-bone neck wearable.
    /// The generated prefab is deliberately skinned to a fake neckLower bone so the normal
    /// Genesis wardrobe builder remaps it to the player's real neck bone at runtime.
    /// </summary>
    [InitializeOnLoad]
    internal static class MillenniumRingImporter
    {
        private const string ModelPath = "Assets/Resources/DuelGenesis/Characters/MillenniumRing.obj";
        private const string PrefabPath = "Assets/Resources/DuelGenesis/Characters/MillenniumRingGenerated.prefab";
        private const string MeshFolder = "Assets/Resources/DuelGenesis/Characters/MillenniumRingMeshes";
        private const string GoldMaterialPath = "Assets/Resources/DuelGenesis/Characters/MillenniumRing_Gold.mat";
        private const string BlackMaterialPath = "Assets/Resources/DuelGenesis/Characters/MillenniumRing_Black.mat";
        private const string CharacterAssetsPath = "Assets/Resources/DuelGenesis/Characters/GenesisCharacterAssets.asset";

        static MillenniumRingImporter()
        {
            EditorApplication.delayCall += EnsureBuiltAndAssigned;
        }

        [MenuItem("Duel Genesis/Characters/Rebuild Millennium Ring")]
        private static void RebuildFromMenu()
        {
            Build(force: true);
        }

        private static void EnsureBuiltAndAssigned()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) return;

            GameObject generated = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (generated == null) Build(force: true);
            else AssignToNeckSlot(generated);
        }

        internal static void Build(bool force)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogWarning("Duel: Genesis: MillenniumRing.obj is missing from " + ModelPath);
                return;
            }

            if (force)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
                AssetDatabase.DeleteAsset(MeshFolder);
                AssetDatabase.DeleteAsset(GoldMaterialPath);
                AssetDatabase.DeleteAsset(BlackMaterialPath);
            }

            EnsureMeshFolder();

            Material gold = CreateArtifactMaterial(
                GoldMaterialPath,
                "Millennium Ring Gold",
                new Color(0.95f, 0.62f, 0.055f, 1f),
                0.82f,
                0.72f);

            Material black = CreateArtifactMaterial(
                BlackMaterialPath,
                "Millennium Ring Black",
                new Color(0.018f, 0.018f, 0.022f, 1f),
                0.30f,
                0.38f);

            GameObject source = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (source == null) source = Object.Instantiate(model);
            source.transform.position = Vector3.zero;
            source.transform.rotation = Quaternion.identity;
            source.transform.localScale = Vector3.one;

            GameObject root = new GameObject("Millennium Ring");
            GameObject boneObject = new GameObject("neckLower");
            Transform bone = boneObject.transform;
            bone.SetParent(root.transform, false);
            bone.localPosition = Vector3.zero;
            bone.localRotation = Quaternion.identity;
            bone.localScale = Vector3.one;

            // Bakura-style presentation: the cord starts at the neck, the Ring sits across the
            // upper chest, and the bottom points lean slightly away from the body so the face reads.
            Quaternion animePitch = Quaternion.Euler(-11f, 0f, 0f);
            Vector3 animeOffset = new Vector3(0f, -0.018f, 0.052f);

            int created = 0;
            foreach (MeshFilter sourceFilter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (sourceFilter.sharedMesh == null) continue;

                Mesh mesh = Object.Instantiate(sourceFilter.sharedMesh);
                mesh.name = sourceFilter.sharedMesh.name + " (Millennium Ring Neck Mesh)";

                Matrix4x4 relative = source.transform.worldToLocalMatrix * sourceFilter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = relative.MultiplyPoint3x4(vertices[i]);
                    vertices[i] = animePitch * p + animeOffset;
                }
                mesh.vertices = vertices;

                Vector3[] normals = mesh.normals;
                if (normals != null && normals.Length == mesh.vertexCount)
                {
                    for (int i = 0; i < normals.Length; i++)
                        normals[i] = animePitch * relative.MultiplyVector(normals[i]).normalized;
                    mesh.normals = normals;
                }
                else mesh.RecalculateNormals();

                BoneWeight[] weights = new BoneWeight[mesh.vertexCount];
                for (int i = 0; i < weights.Length; i++)
                    weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                mesh.boneWeights = weights;
                mesh.bindposes = new[] { Matrix4x4.identity };

                // GenesisCharacterBuilder sizes skinned wardrobe around mesh.bounds.center.
                // Force that center onto the neck origin so 70%-130% scales DOWN from the
                // necklace anchor instead of sliding the Ring up/down the torso.
                Bounds b = mesh.bounds;
                float ex = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
                float ey = Mathf.Max(Mathf.Abs(b.min.y), Mathf.Abs(b.max.y));
                float ez = Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(ex * 2f, ey * 2f, ez * 2f));

                string meshPath = MeshFolder + "/RingPart_" + created.ToString("D2") + ".asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

                string partName = sourceFilter.gameObject.name;
                GameObject part = new GameObject(partName);
                part.transform.SetParent(root.transform, false);
                SkinnedMeshRenderer smr = part.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = savedMesh;
                smr.bones = new[] { bone };
                smr.rootBone = bone;
                smr.updateWhenOffscreen = true;
                smr.sharedMaterial = IsBlackPart(partName) ? black : gold;
                created++;
            }

            if (created == 0)
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(source);
                Debug.LogError("Duel: Genesis: Millennium Ring model imported but contained no usable meshes.");
                return;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(source);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            AssignToNeckSlot(prefab);
            Debug.Log("Duel: Genesis: Millennium Ring installed in Neck slot 7 with anime-style neck anchoring.");
        }

        private static void EnsureMeshFolder()
        {
            if (AssetDatabase.IsValidFolder(MeshFolder)) return;
            AssetDatabase.CreateFolder("Assets/Resources/DuelGenesis/Characters", "MillenniumRingMeshes");
        }

        private static Material CreateArtifactMaterial(string path, string displayName, Color color, float metallic, float smoothness)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("DuelGenesis/MillenniumArtifact");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = new Material(shader) { name = displayName };
            if (material.HasProperty("_ArtifactColor")) material.SetColor("_ArtifactColor", color);
            else if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_ArtifactMetallic")) material.SetFloat("_ArtifactMetallic", metallic);
            if (material.HasProperty("_ArtifactSmoothness")) material.SetFloat("_ArtifactSmoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static bool IsBlackPart(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n.Contains("preto") || n.Contains("black");
        }

        private static void AssignToNeckSlot(GameObject prefab)
        {
            if (prefab == null) return;
            GenesisCharacterAssets assets = AssetDatabase.LoadAssetAtPath<GenesisCharacterAssets>(CharacterAssetsPath);
            if (assets == null) return;

            GenesisCharacterAssets.Item ring = assets.items.FirstOrDefault(i => i != null && i.slot == GenesisSlot.Neck && i.id == 7);
            if (ring == null)
            {
                ring = new GenesisCharacterAssets.Item
                {
                    slot = GenesisSlot.Neck,
                    id = 7,
                    name = "Millennium Ring",
                    prefab = prefab
                };
                assets.items.Add(ring);
            }
            else
            {
                ring.name = "Millennium Ring";
                ring.prefab = prefab;
            }

            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }
    }

    internal sealed class MillenniumRingAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(p => p == "Assets/Resources/DuelGenesis/Characters/MillenniumRing.obj"))
                EditorApplication.delayCall += () => MillenniumRingImporter.Build(force: true);
        }
    }
}
