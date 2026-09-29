using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DuelGenesis.Characters
{
    public static class GenesisCharacterBuilder
    {
        public const string BodyMeshName = "Genesis9.Shape";

        public static GameObject Build(Transform parent, GenesisAppearance appearance, GenesisCharacterAssets assets = null)
        {
            assets = assets != null ? assets : GenesisCharacterAssets.Instance;
            if (assets == null || assets.basePrefab == null) return null;
            appearance = appearance ?? GenesisAppearance.CreateDefault(GenesisGender.Female);

            GameObject root = Object.Instantiate(assets.basePrefab, parent, false);
            root.name = "Genesis Character";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rb);

            SkinnedMeshRenderer[] figure = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer body = FindBodyRenderer(figure);

            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);

            bool male = appearance.gender == GenesisGender.Male;
            var fit = new Dictionary<string, float>();
            var authoredOffsets = new Dictionary<string, float>();
            GenesisCharacterLibrary library = GenesisCharacterLibrary.Instance;

            foreach (GenesisEquip equip in appearance.equipment)
            {
                if (equip == null || equip.id < 0) continue;
                GameObject prefab = assets.Find(equip.slot, equip.id);
                if (prefab == null) continue;

                Wear(root.transform, prefab, bones, equip.slot, equip.id, equip.colors, equip.proportion);

                GenesisCharacterLibrary.Item entry = library?.Find(equip.slot, equip.id);
                GenesisCharacterLibrary.Fit f = entry?.FitFor(appearance.gender);
                if (f != null)
                {
                    Max(fit, GenesisMorphMap.ScaleTorso, f.torso);
                    Max(fit, GenesisMorphMap.ScaleThighs, f.thighs);
                    Max(fit, GenesisMorphMap.ScaleGroin, f.groin);
                    Max(fit, GenesisMorphMap.ScaleLegs, f.legs);
                    Max(fit, GenesisMorphMap.ScaleArms, f.arms);
                    Max(fit, GenesisMorphMap.HideFeet, f.hideFeet);
                }

                GenesisCharacterLibrary.MorphOffset[] offsets = entry?.OffsetsFor(appearance.gender);
                if (offsets != null)
                {
                    foreach (GenesisCharacterLibrary.MorphOffset offset in offsets)
                    {
                        if (offset == null || string.IsNullOrEmpty(offset.morph)) continue;
                        if (!authoredOffsets.TryGetValue(offset.morph, out float current)) current = 0f;
                        authoredOffsets[offset.morph] = current + offset.value;
                    }
                }
            }

            figure = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            body = FindBodyRenderer(figure);
            ApplyShapes(root, appearance);

            if (body != null)
                foreach (var kv in fit) Shape(new[] { body }, kv.Key, kv.Value);

            foreach (var kv in authoredOffsets) AddShape(figure, kv.Key, kv.Value);

            foreach (SkinnedMeshRenderer r in figure)
            {
                bool iris = r.name.IndexOf("Iris", System.StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (Material m in r.materials)
                {
                    string n = m.name.ToLowerInvariant();
                    if (iris) Tint(m, appearance.leftEyeColor);
                    else if (r == body && (n.Contains("skin") || n.Contains("body") || n.Contains("face") || n.Contains("head") || n.Contains("arm") || n.Contains("leg") || n.Contains("torso")))
                    {
                        Tint(m, Color.Lerp(Color.white, new Color(
                            appearance.skinColor.r / DefaultSkin.r,
                            appearance.skinColor.g / DefaultSkin.g,
                            appearance.skinColor.b / DefaultSkin.b, 1f), 0.85f));
                    }
                }
            }

            FixFaceOverlayMaterials(figure, appearance);

            float target = (male ? 1.74f : 1.64f) * (1f + appearance.height * 0.0012f);
            FitHeight(root.transform, body, target);

            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = animator.avatar != null && animator.avatar.isHuman ? assets.locomotion : null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
            return root;
        }

        private static SkinnedMeshRenderer FindBodyRenderer(IEnumerable<SkinnedMeshRenderer> renderers)
        {
            SkinnedMeshRenderer[] all = renderers != null ? renderers.Where(r => r != null && r.sharedMesh != null).ToArray() : new SkinnedMeshRenderer[0];
            SkinnedMeshRenderer exact = all.FirstOrDefault(r => r.name == BodyMeshName);
            if (exact != null) return exact;

            exact = all.FirstOrDefault(r => r.name.IndexOf("Genesis9", System.StringComparison.OrdinalIgnoreCase) >= 0 && r.sharedMesh.blendShapeCount > 0);
            if (exact != null) return exact;

            return all.OrderByDescending(r => r.sharedMesh.blendShapeCount).FirstOrDefault();
        }

        public static void ApplyShapes(GameObject root, GenesisAppearance appearance)
        {
            if (root == null || appearance == null) return;
            SkinnedMeshRenderer[] all = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool male = appearance.gender == GenesisGender.Male;
            Shape(all, GenesisMorphMap.FemaleFigure, male ? 0f : 100f);
            Shape(all, GenesisMorphMap.MaleFigure, male ? 100f : 0f);
            Shape(all, GenesisMorphMap.Youth, appearance.age);
            for (int t = 1; t < GenesisMorphMap.BodyTypes.Length; t++)
                Shape(all, GenesisMorphMap.BodyTypes[t], t == appearance.bodyType ? appearance.bodyWeight : 0f);
            Shape(all, GenesisMorphMap.BreastSize, male ? 0f : appearance.breastSize);
            foreach (var group in GenesisMorphMap.FaceGroups)
            {
                float[] values = group.values(appearance.face);
                for (int i = 0; i < group.morphs.Length && i < values.Length; i++) Shape(all, group.morphs[i], values[i]);
            }
        }

        private static readonly Color DefaultSkin = new Color(0.7725f, 0.4784f, 0.3765f, 1f);

        private static void Wear(Transform root, GameObject prefab, Dictionary<string, Transform> bones, GenesisSlot slot, int id, Color[] colors, float proportion)
        {
            GameObject item = Object.Instantiate(prefab);
            float itemScale = ItemScale(proportion);
            // Items can carry extra bones the figure doesn't have (the Millennium Puzzle's chain and pendant, the
            // Ring's swing bones). Graft those onto the figure's matching parent bone so they keep their place.
            var bonesHere = new Dictionary<string, Transform>(bones);   // this item's grafts stay its own
            GraftExtraBones(item, bonesHere);

            int colour = 0;
            foreach (SkinnedMeshRenderer smr in item.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] mapped = smr.bones.Select(b => b != null && bonesHere.TryGetValue(b.name, out Transform t) ? t : null).ToArray();
                if (mapped.Any(b => b == null) && mapped.Count(b => b == null) > mapped.Length / 3) continue;
                for (int i = 0; i < mapped.Length; i++)
                    if (mapped[i] == null) mapped[i] = bonesHere.TryGetValue("hip", out Transform hip) ? hip : root;

                smr.bones = mapped;
                if (smr.rootBone != null && bonesHere.TryGetValue(smr.rootBone.name, out Transform rb)) smr.rootBone = rb;
                smr.transform.SetParent(root, false);
                smr.transform.localPosition = Vector3.zero;
                smr.transform.localRotation = Quaternion.identity;
                smr.transform.localScale = Vector3.one;
                TransformSkinnedGeometry(smr, itemScale);

                if (colors != null)
                    foreach (Material m in smr.materials)
                        if (colour < colors.Length && colors[colour].a > 0.01f) Tint(m, colors[colour++]);
            }

            foreach (MeshRenderer mr in item.GetComponentsInChildren<MeshRenderer>(true))
            {
                Transform p = mr.transform.parent;
                while (p != null && !bonesHere.ContainsKey(p.name)) p = p.parent;
                if (p == null) continue;
                mr.transform.SetParent(bonesHere[p.name], true);
                mr.transform.localScale *= itemScale;
            }

            Object.Destroy(item);
        }

        /// <summary>
        /// For every bone an item's skin uses that the figure lacks, moves that bone (with its children) under the
        /// figure bone its item-side parent corresponds to, keeping its local pose, and registers it by name.
        /// </summary>
        private static void GraftExtraBones(GameObject item, Dictionary<string, Transform> bones)
        {
            var extras = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer smr in item.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Transform b in smr.bones)
                    if (b != null && !bones.ContainsKey(b.name)) extras.Add(b);
            foreach (Transform b in extras.ToList())
            {
                // Graft at the topmost unknown ancestor so chains of extra bones move together.
                Transform top = b;
                while (top.parent != null && !bones.ContainsKey(top.parent.name) && top.parent != item.transform) top = top.parent;
                if (top.parent == null || !bones.TryGetValue(top.parent.name, out Transform anchor)) continue;
                Vector3 lp = top.localPosition;
                Quaternion lr = top.localRotation;
                Vector3 ls = top.localScale;
                top.SetParent(anchor, false);
                top.localPosition = lp;
                top.localRotation = lr;
                top.localScale = ls;
                foreach (Transform t in top.GetComponentsInChildren<Transform>(true))
                    if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            }
        }

        private static bool WearMillenniumPuzzle(Transform root, GameObject sourceItem, Dictionary<string, Transform> bones, Color[] colors, float scale)
        {
            Transform neck = FindCharacterNeck(bones);
            if (root == null || sourceItem == null || neck == null) return false;

            Renderer[] sourceRenderers = sourceItem.GetComponentsInChildren<Renderer>(true);
            if (sourceRenderers.Length == 0) return false;

            bool haveBounds = false;
            Bounds itemBounds = default;
            foreach (Renderer r in sourceRenderers)
            {
                Bounds b = r.bounds;
                Vector3 min = b.min;
                Vector3 max = b.max;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 world = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                    Vector3 local = sourceItem.transform.InverseTransformPoint(world);
                    if (!haveBounds)
                    {
                        itemBounds = new Bounds(local, Vector3.zero);
                        haveBounds = true;
                    }
                    else itemBounds.Encapsulate(local);
                }
            }

            if (!haveBounds) return false;
            Vector3 sourceAnchor = new Vector3(itemBounds.center.x, itemBounds.max.y, itemBounds.center.z);

            GameObject holderObject = new GameObject("Millennium Puzzle Wearable");
            Transform holder = holderObject.transform;
            holder.SetParent(neck, true);
            holder.position = neck.position + root.forward * 0.055f - root.up * 0.025f;
            holder.rotation = root.rotation * Quaternion.Euler(-14f, 0f, 0f);
            holder.localScale = Vector3.one;

            GameObject geometryObject = new GameObject("Puzzle Geometry");
            Transform geometry = geometryObject.transform;
            geometry.SetParent(holder, false);
            geometry.localRotation = Quaternion.identity;
            geometry.localScale = Vector3.one * scale;
            geometry.localPosition = -sourceAnchor * scale;

            int colour = 0;

            foreach (SkinnedMeshRenderer smr in sourceItem.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh baked = new Mesh();
                smr.BakeMesh(baked, true);
                baked.name = smr.sharedMesh != null ? smr.sharedMesh.name + " (Millennium Puzzle Baked)" : "Millennium Puzzle Baked";

                Matrix4x4 relative = sourceItem.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                GameObject part = new GameObject(smr.name);
                part.transform.SetParent(geometry, false);
                part.transform.localPosition = relative.GetColumn(3);
                part.transform.localRotation = relative.rotation;
                part.transform.localScale = relative.lossyScale;

                MeshFilter filter = part.AddComponent<MeshFilter>();
                filter.sharedMesh = baked;
                MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                Material[] mats = smr.materials;
                if (colors != null)
                    foreach (Material m in mats)
                        if (colour < colors.Length && colors[colour].a > 0.01f) Tint(m, colors[colour++]);
                renderer.materials = mats;

                GenesisRuntimeMeshCleanup cleanup = part.AddComponent<GenesisRuntimeMeshCleanup>();
                cleanup.mesh = baked;
            }

            foreach (MeshRenderer mr in sourceItem.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter sourceFilter = mr.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

                Matrix4x4 relative = sourceItem.transform.worldToLocalMatrix * mr.transform.localToWorldMatrix;
                GameObject part = new GameObject(mr.name);
                part.transform.SetParent(geometry, false);
                part.transform.localPosition = relative.GetColumn(3);
                part.transform.localRotation = relative.rotation;
                part.transform.localScale = relative.lossyScale;

                MeshFilter filter = part.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                Material[] mats = mr.materials;
                if (colors != null)
                    foreach (Material m in mats)
                        if (colour < colors.Length && colors[colour].a > 0.01f) Tint(m, colors[colour++]);
                renderer.materials = mats;
            }

            return true;
        }

        private static Transform FindCharacterNeck(Dictionary<string, Transform> bones)
        {
            if (bones == null || bones.Count == 0) return null;
            Transform lower = bones.Values.FirstOrDefault(t => t != null && NormalizeBoneName(t.name).EndsWith("necklower"));
            if (lower != null) return lower;
            Transform exact = bones.Values.FirstOrDefault(t => t != null && NormalizeBoneName(t.name) == "neck");
            if (exact != null) return exact;
            Transform upper = bones.Values.FirstOrDefault(t => t != null && NormalizeBoneName(t.name).EndsWith("neckupper"));
            if (upper != null) return upper;
            return bones.Values.FirstOrDefault(t => t != null && NormalizeBoneName(t.name).Contains("neck"));
        }

        private static string NormalizeBoneName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static void TransformSkinnedGeometry(SkinnedMeshRenderer smr, float scale)
        {
            if (smr == null || smr.sharedMesh == null || Mathf.Abs(scale - 1f) < 0.001f) return;
            Mesh source = smr.sharedMesh;
            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + " (Duel Genesis Sized)";
            Vector3[] vertices = copy.vertices;
            Vector3 pivot = copy.bounds.center;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = pivot + (vertices[i] - pivot) * scale;
            copy.vertices = vertices;
            copy.RecalculateBounds();
            smr.sharedMesh = copy;
            GenesisRuntimeMeshCleanup cleanup = smr.gameObject.AddComponent<GenesisRuntimeMeshCleanup>();
            cleanup.mesh = copy;
        }

        private static float ItemScale(float proportion) => Mathf.Lerp(0.70f, 1.30f, Mathf.InverseLerp(-100f, 100f, Mathf.Clamp(proportion, -100f, 100f)));

        private static void FixFaceOverlayMaterials(IEnumerable<SkinnedMeshRenderer> renderers, GenesisAppearance appearance)
        {
            if (renderers == null || appearance == null) return;
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r == null) continue;
                foreach (Material m in r.materials)
                {
                    if (m == null) continue;
                    string key = (r.name + " " + m.name).ToLowerInvariant();
                    bool brow = key.Contains("eyebrow") || key.Contains("brow");
                    bool lash = key.Contains("eyelash") || key.Contains("lash");
                    bool liner = key.Contains("eyeliner") || key.Contains("eye liner");
                    bool paint = key.Contains("facepaint") || key.Contains("face paint") || key.Contains("makeup") || key.Contains("make up");
                    if (!brow && !lash && !liner && !paint) continue;
                    Color tint = brow ? appearance.eyebrowColor : lash ? appearance.eyelashColor : liner ? appearance.eyelinerColor : appearance.paintColor;
                    if (tint.a < 0.01f && (brow || lash)) tint.a = 1f;
                    Tint(m, tint);
                    MakeTransparentOverlay(m);
                }
            }
        }

        private static void MakeTransparentOverlay(Material m)
        {
            if (m == null) return;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        private static void Tint(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void Max(Dictionary<string, float> fit, string key, float value)
        {
            if (!fit.TryGetValue(key, out float v) || value > v) fit[key] = value;
        }

        public static void Shape(IEnumerable<SkinnedMeshRenderer> renderers, string shape, float weight)
        {
            if (string.IsNullOrEmpty(shape) || renderers == null) return;
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r == null || r.sharedMesh == null) continue;
                if (SetShapeOnRenderer(r, shape, weight)) continue;
                foreach (string converted in ConvertedMorphs(shape)) SetShapeOnRenderer(r, converted, weight);
            }
        }

        private static void AddShape(IEnumerable<SkinnedMeshRenderer> renderers, string shape, float delta)
        {
            if (string.IsNullOrEmpty(shape) || Mathf.Approximately(delta, 0f) || renderers == null) return;
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r == null || r.sharedMesh == null) continue;
                int direct = ShapeIndex(r.sharedMesh, shape);
                if (direct >= 0)
                {
                    r.SetBlendShapeWeight(direct, r.GetBlendShapeWeight(direct) + delta);
                    continue;
                }
                foreach (string converted in ConvertedMorphs(shape))
                {
                    int i = ShapeIndex(r.sharedMesh, converted);
                    if (i >= 0) r.SetBlendShapeWeight(i, r.GetBlendShapeWeight(i) + delta);
                }
            }
        }

        private static bool SetShapeOnRenderer(SkinnedMeshRenderer renderer, string shape, float weight)
        {
            int i = ShapeIndex(renderer.sharedMesh, shape);
            if (i < 0) return false;
            renderer.SetBlendShapeWeight(i, weight);
            return true;
        }

        private static IEnumerable<string> ConvertedMorphs(string shape)
        {
            if (shape == GenesisMorphMap.Youth)
            {
                yield return "GU Head";
                yield return "GU Body";
            }
            else if (shape == GenesisMorphMap.FemaleFigure) yield return "BaseAnimeF_body_bs_BodyFeminine";
            else if (shape == GenesisMorphMap.MaleFigure) yield return "BaseAnimeM_body_bs_BodyMasculine";
        }

        private static readonly Dictionary<Mesh, Dictionary<string, int>> ShapeNames = new Dictionary<Mesh, Dictionary<string, int>>();

        public static int ShapeIndex(Mesh mesh, string shape)
        {
            if (mesh == null || string.IsNullOrEmpty(shape)) return -1;
            if (!ShapeNames.TryGetValue(mesh, out Dictionary<string, int> map))
            {
                map = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string n = mesh.GetBlendShapeName(i) ?? string.Empty;
                    AddShapeKey(map, n, i);
                    AddShapeKey(map, NormalizeShapeName(n), i);
                    int cut = LastShapeSeparator(n);
                    if (cut >= 0 && cut < n.Length - 1)
                    {
                        string tail = n.Substring(cut + 1);
                        AddShapeKey(map, tail, i);
                        AddShapeKey(map, NormalizeShapeName(tail), i);
                    }
                }
                ShapeNames[mesh] = map;
            }
            if (map.TryGetValue(shape, out int index)) return index;
            string normalized = NormalizeShapeName(shape);
            if (map.TryGetValue(normalized, out index)) return index;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string candidate = NormalizeShapeName(mesh.GetBlendShapeName(i));
                if (candidate.EndsWith(normalized, System.StringComparison.OrdinalIgnoreCase))
                {
                    AddShapeKey(map, shape, i);
                    AddShapeKey(map, normalized, i);
                    return i;
                }
            }
            return -1;
        }

        private static void AddShapeKey(Dictionary<string, int> map, string key, int index)
        {
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key)) map[key] = index;
        }

        private static int LastShapeSeparator(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int cut = -1;
            cut = Mathf.Max(cut, name.LastIndexOf('.'));
            cut = Mathf.Max(cut, name.LastIndexOf('/'));
            cut = Mathf.Max(cut, name.LastIndexOf(':'));
            cut = Mathf.Max(cut, name.LastIndexOf('|'));
            int doubleUnderscore = name.LastIndexOf("__", System.StringComparison.Ordinal);
            if (doubleUnderscore >= 0) cut = Mathf.Max(cut, doubleUnderscore + 1);
            return cut;
        }

        private static string NormalizeShapeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static void FitHeight(Transform root, SkinnedMeshRenderer body, float height)
        {
            if (body == null) return;
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            Vector3[] v = baked.vertices;
            if (v.Length == 0)
            {
                Object.Destroy(baked);
                return;
            }
            float min = float.MaxValue, max = float.MinValue;
            Matrix4x4 toRoot = root.worldToLocalMatrix * Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
            for (int i = 0; i < v.Length; i += 7)
            {
                float y = toRoot.MultiplyPoint3x4(v[i]).y;
                if (y < min) min = y;
                if (y > max) max = y;
            }
            Object.Destroy(baked);
            float current = (max - min) * root.lossyScale.y;
            if (current < 0.01f) return;
            float s = height / current;
            root.localScale *= s;
            root.localPosition = new Vector3(0f, -min * root.localScale.y, 0f);
        }
    }

    internal sealed class GenesisRuntimeMeshCleanup : MonoBehaviour
    {
        public Mesh mesh;
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
