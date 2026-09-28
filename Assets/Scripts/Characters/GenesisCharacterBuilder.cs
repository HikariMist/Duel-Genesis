using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuelGenesis.Characters
{
    /// <summary>
    /// Builds a player character from a <see cref="GenesisAppearance"/>: the Genesis 9 figure with its body and face
    /// morphs, the equipped wardrobe skinned onto the same skeleton (bones matched by name), clothing-fit shapes so
    /// the body never pokes through, colours, and a humanoid locomotion animator. The player only; never NPCs.
    /// </summary>
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
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);   // DMO gameplay stubs
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rb);

            SkinnedMeshRenderer body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == BodyMeshName);
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);
            SkinnedMeshRenderer[] figure = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // Figure, body and face shaping (applied to every figure mesh that has the shape: body, brows, lashes, mouth).
            bool male = appearance.gender == GenesisGender.Male;
            Shape(figure, GenesisMorphMap.FemaleFigure, male ? 0f : 100f);
            Shape(figure, GenesisMorphMap.MaleFigure, male ? 100f : 0f);
            Shape(figure, GenesisMorphMap.Youth, appearance.age);
            if (appearance.bodyType > 0 && appearance.bodyType < GenesisMorphMap.BodyTypes.Length)
                Shape(figure, GenesisMorphMap.BodyTypes[appearance.bodyType], appearance.bodyWeight);
            if (!male) Shape(figure, GenesisMorphMap.BreastSize, appearance.breastSize);
            foreach (var group in GenesisMorphMap.FaceGroups)
            {
                float[] values = group.values(appearance.face);
                for (int i = 0; i < group.morphs.Length && i < values.Length; i++)
                    if (Mathf.Abs(values[i]) > 0.01f) Shape(figure, group.morphs[i], values[i]);
            }

            // Wardrobe, bound to the figure's skeleton; the body's fit shapes follow the tightest item.
            var fit = new Dictionary<string, float>();
            GenesisCharacterLibrary library = GenesisCharacterLibrary.Instance;
            foreach (GenesisEquip equip in appearance.equipment)
            {
                if (equip == null || equip.id < 0) continue;
                GameObject prefab = assets.Find(equip.slot, equip.id);
                if (prefab == null) continue;
                Wear(root.transform, prefab, bones, equip.colors);
                GenesisCharacterLibrary.Fit f = library?.Find(equip.slot, equip.id)?.FitFor(appearance.gender);
                if (f == null) continue;
                Max(fit, GenesisMorphMap.ScaleTorso, f.torso);
                Max(fit, GenesisMorphMap.ScaleThighs, f.thighs);
                Max(fit, GenesisMorphMap.ScaleGroin, f.groin);
                Max(fit, GenesisMorphMap.ScaleLegs, f.legs);
                Max(fit, GenesisMorphMap.ScaleArms, f.arms);
                Max(fit, GenesisMorphMap.HideFeet, f.hideFeet);
            }
            if (body != null)
                foreach (var kv in fit) Shape(new[] { body }, kv.Key, kv.Value);

            // Colours: skin on the figure, eyes on the iris.
            foreach (SkinnedMeshRenderer r in figure)
            {
                bool iris = r.name.Contains("Iris");
                foreach (Material m in r.materials)
                {
                    string n = m.name.ToLowerInvariant();
                    if (iris) Tint(m, appearance.leftEyeColor);
                    else if (r == body && (n.Contains("skin") || n.Contains("body") || n.Contains("face") || n.Contains("head") || n.Contains("arm") || n.Contains("leg") || n.Contains("torso")))
                        Tint(m, Color.Lerp(Color.white, new Color(
                            appearance.skinColor.r / DefaultSkin.r, appearance.skinColor.g / DefaultSkin.g, appearance.skinColor.b / DefaultSkin.b, 1f), 0.85f));
                }
            }

            // Size: a real-world height, feet at the parent's origin.
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

        private static readonly Color DefaultSkin = new Color(0.7725f, 0.4784f, 0.3765f, 1f);

        private static void Wear(Transform root, GameObject prefab, Dictionary<string, Transform> bones, Color[] colors)
        {
            GameObject item = Object.Instantiate(prefab);
            int colour = 0;
            foreach (SkinnedMeshRenderer smr in item.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] mapped = smr.bones.Select(b => b != null && bones.TryGetValue(b.name, out Transform t) ? t : null).ToArray();
                if (mapped.Any(b => b == null) && mapped.Count(b => b == null) > mapped.Length / 3) continue;   // not rigged to this figure
                for (int i = 0; i < mapped.Length; i++) if (mapped[i] == null) mapped[i] = bones.TryGetValue("hip", out Transform hip) ? hip : root;
                smr.bones = mapped;
                if (smr.rootBone != null && bones.TryGetValue(smr.rootBone.name, out Transform rb)) smr.rootBone = rb;
                smr.transform.SetParent(root, false);
                smr.transform.localPosition = Vector3.zero;
                smr.transform.localRotation = Quaternion.identity;
                smr.transform.localScale = Vector3.one;
                if (colors != null)
                    foreach (Material m in smr.materials)
                        if (colour < colors.Length && colors[colour].a > 0.01f) Tint(m, colors[colour++]);
            }
            // Rigid pieces (hats, glasses): follow their bone by name.
            foreach (MeshRenderer mr in item.GetComponentsInChildren<MeshRenderer>(true))
            {
                Transform p = mr.transform.parent;
                while (p != null && !bones.ContainsKey(p.name)) p = p.parent;
                if (p != null) mr.transform.SetParent(bones[p.name], true);
            }
            Object.Destroy(item);
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

        /// <summary>Sets a blendshape (by name) on every renderer that has it.</summary>
        public static void Shape(IEnumerable<SkinnedMeshRenderer> renderers, string shape, float weight)
        {
            if (string.IsNullOrEmpty(shape)) return;
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r == null || r.sharedMesh == null) continue;
                int i = ShapeIndex(r.sharedMesh, shape);
                if (i >= 0) r.SetBlendShapeWeight(i, weight);
            }
        }

        private static readonly Dictionary<Mesh, Dictionary<string, int>> ShapeNames = new Dictionary<Mesh, Dictionary<string, int>>();

        /// <summary>
        /// Blendshape index by name. Exported DAZ shapes carry a mesh prefix ("Genesis9__head_bs_FaceYoung" or
        /// "Genesis9.head_bs_FaceYoung"), so the name after the last '.' or "__" also counts.
        /// </summary>
        public static int ShapeIndex(Mesh mesh, string shape)
        {
            if (!ShapeNames.TryGetValue(mesh, out Dictionary<string, int> map))
            {
                map = new Dictionary<string, int>();
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string n = mesh.GetBlendShapeName(i);
                    if (!map.ContainsKey(n)) map[n] = i;
                    int cut = Mathf.Max(n.LastIndexOf('.'), n.LastIndexOf("__", System.StringComparison.Ordinal) + 1);
                    string tail = cut >= 0 && cut < n.Length - 1 ? n.Substring(cut + 1) : n;
                    if (!map.ContainsKey(tail)) map[tail] = i;
                }
                ShapeNames[mesh] = map;
            }
            return map.TryGetValue(shape, out int index) ? index : -1;
        }

        /// <summary>Scales the figure to <paramref name="height"/> metres with its feet on the parent's origin.</summary>
        private static void FitHeight(Transform root, SkinnedMeshRenderer body, float height)
        {
            if (body == null) return;
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            Vector3[] v = baked.vertices;
            if (v.Length == 0) return;
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
}
