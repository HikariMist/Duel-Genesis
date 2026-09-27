#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Converts the Built-in-pipeline materials that ship with the third-party world packs
    /// (Otaku City, POLYGON City, Low Poly ATM, ICONIC cars, Polytope nature, Simple Face Makeup)
    /// to URP. Handles Unity's Standard/Legacy shaders AND the packs' custom surface shaders by
    /// reading each material's saved properties directly, so textures, tints, cutouts, double-sided
    /// faces and vertex colours survive the switch.
    /// </summary>
    public static class GenesisMaterialConverter
    {
        public static readonly string[] VendorRoots =
        {
            "Assets/ZRNAssets",
            "Assets/POLYGON city pack",
            "Assets/Low Poly ATM",
            "Assets/ICONIC - Sports Car FREE Vol.02",
            "Assets/Polytope Studio",
            "Assets/BadDog",
            "Packages/com.hikarimist.dmo-characters",   // DMO monster models (local package, editable)
        };

        private static readonly string[] MainTexNames = { "_MainTex", "_BaseMap", "_MainTexture", "_Texture", "_Albedo", "_AlbedoMap", "_Diffuse", "_BaseColorMap", "_MainTexture0" };
        private static readonly string[] ColorNames = { "_Color", "_BaseColor", "_TexColor", "_MainColor", "_TintColor", "_Tint" };
        private static readonly string[] CutoffNames = { "_Cutoff", "_Clip_Val", "_ClipValue", "_AlphaCutoff", "_AlphaClipThreshold" };

        [MenuItem("Duel Genesis/World/2. Convert Imported Pack Materials to URP")]
        public static void ConvertAllMenu()
        {
            int converted = ConvertAll(out int scanned, out List<string> report);
            string msg = $"Scanned {scanned} materials, converted {converted} to URP.";
            Debug.Log("Duel: Genesis material converter — " + msg + "\n" + string.Join("\n", report.Take(400)));
            EditorUtility.DisplayDialog("URP Material Conversion", msg + "\n\nDetails are in the Console.", "OK");
        }

        public static int ConvertAll(out int scanned, out List<string> report)
        {
            report = new List<string>();
            scanned = 0;
            int converted = 0;
            string[] roots = VendorRoots.Where(AssetDatabase.IsValidFolder).ToArray();
            if (roots.Length == 0) return 0;

            string[] guids = AssetDatabase.FindAssets("t:Material", roots);
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (i % 25 == 0)
                        EditorUtility.DisplayProgressBar("Converting materials to URP", path, i / (float)guids.Length);
                    if (!path.EndsWith(".mat")) continue;
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) continue;
                    scanned++;
                    string before = mat.shader != null ? mat.shader.name : "<missing>";
                    if (TryConvert(mat, out string mode))
                    {
                        converted++;
                        report.Add($"{path}: {before} -> {mode}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            return converted;
        }

        // ------------------------------------------------------------------ detection

        public static bool NeedsConversion(Shader shader)
        {
            if (shader == null) return true;
            string n = shader.name;
            if (n == "Hidden/InternalErrorShader") return true;
            if (n.StartsWith("Universal Render Pipeline/") || n.StartsWith("Shader Graphs/") || n.StartsWith("Skybox/") ||
                n.StartsWith("UI/") || n.StartsWith("Sprites/") || n.StartsWith("TextMeshPro") || n.StartsWith("UMA/") ||
                n.StartsWith("Hidden/") || n.StartsWith("Unlit/"))
                return false;

            if (n == "Standard" || n == "Standard (Specular setup)" || n == "Autodesk Interactive" || n == "VertexLit" ||
                n == "Diffuse" || n == "Bumped Diffuse" || n == "Specular" || n == "Bumped Specular" ||
                n.StartsWith("Legacy Shaders/") || n.StartsWith("Mobile/") || n.StartsWith("Nature/") ||
                n.StartsWith("Transparent/") || n.StartsWith("Self-Illumin/") || n.StartsWith("Reflective/") ||
                (n.StartsWith("Particles/") && !n.Contains("Universal")))
                return true;

            string source = ShaderSource(shader);
            if (source == null) return !shader.isSupported;
            // Surface shaders and ForwardBase-only passes never render in URP.
            if (source.Contains("#pragma surface")) return true;
            bool urpPass = source.Contains("UniversalForward") || source.Contains("UniversalPipeline") || source.Contains("SRPDefaultUnlit");
            bool builtinLit = source.Contains("\"LightMode\"=\"ForwardBase\"") || source.Contains("\"LightMode\" = \"ForwardBase\"") || source.Contains("UnityStandard");
            return builtinLit && !urpPass;
        }

        private static string ShaderSource(Shader shader)
        {
            string path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/") || !File.Exists(path)) return null;
            try { return File.ReadAllText(path); } catch { return null; }
        }

        // ------------------------------------------------------------------ conversion

        private struct Saved
        {
            public Dictionary<string, (Texture tex, Vector2 scale, Vector2 offset)> textures;
            public Dictionary<string, float> floats;
            public Dictionary<string, Color> colors;
        }

        private static Saved ReadSaved(Material mat)
        {
            Saved s = new Saved
            {
                textures = new Dictionary<string, (Texture, Vector2, Vector2)>(),
                floats = new Dictionary<string, float>(),
                colors = new Dictionary<string, Color>(),
            };
            SerializedObject so = new SerializedObject(mat);
            SerializedProperty saved = so.FindProperty("m_SavedProperties");
            if (saved == null) return s;

            SerializedProperty texEnvs = saved.FindPropertyRelative("m_TexEnvs");
            for (int i = 0; texEnvs != null && i < texEnvs.arraySize; i++)
            {
                SerializedProperty e = texEnvs.GetArrayElementAtIndex(i);
                string name = e.FindPropertyRelative("first")?.stringValue;
                SerializedProperty second = e.FindPropertyRelative("second");
                if (string.IsNullOrEmpty(name) || second == null) continue;
                Texture tex = second.FindPropertyRelative("m_Texture")?.objectReferenceValue as Texture;
                Vector2 scale = second.FindPropertyRelative("m_Scale")?.vector2Value ?? Vector2.one;
                Vector2 offset = second.FindPropertyRelative("m_Offset")?.vector2Value ?? Vector2.zero;
                s.textures[name] = (tex, scale, offset);
            }

            SerializedProperty floats = saved.FindPropertyRelative("m_Floats");
            for (int i = 0; floats != null && i < floats.arraySize; i++)
            {
                SerializedProperty e = floats.GetArrayElementAtIndex(i);
                string name = e.FindPropertyRelative("first")?.stringValue;
                SerializedProperty second = e.FindPropertyRelative("second");
                if (!string.IsNullOrEmpty(name) && second != null) s.floats[name] = second.floatValue;
            }

            SerializedProperty colors = saved.FindPropertyRelative("m_Colors");
            for (int i = 0; colors != null && i < colors.arraySize; i++)
            {
                SerializedProperty e = colors.GetArrayElementAtIndex(i);
                string name = e.FindPropertyRelative("first")?.stringValue;
                SerializedProperty second = e.FindPropertyRelative("second");
                if (!string.IsNullOrEmpty(name) && second != null) s.colors[name] = second.colorValue;
            }
            return s;
        }

        public static bool TryConvert(Material mat, out string mode)
        {
            mode = null;
            Shader old = mat.shader;
            if (!NeedsConversion(old)) return false;

            string oldName = old != null ? old.name : "";
            string source = old != null ? ShaderSource(old) : null;
            Saved s = ReadSaved(mat);

            // --- what the old material was doing
            (Texture tex, Vector2 scale, Vector2 offset) main = default;
            foreach (string n in MainTexNames)
                if (s.textures.TryGetValue(n, out var t) && t.tex != null) { main = t; break; }
            if (main.tex == null)
            {
                foreach (var kv in s.textures)
                {
                    string k = kv.Key.ToLowerInvariant();
                    if (kv.Value.tex == null || k.Contains("bump") || k.Contains("normal") || k.Contains("mask") ||
                        k.Contains("detail") || k.Contains("occlusion") || k.Contains("emission") || k.Contains("gloss") || k.Contains("lut"))
                        continue;
                    main = kv.Value;
                    break;
                }
            }

            Color color = Color.white;
            foreach (string n in ColorNames)
                if (s.colors.TryGetValue(n, out Color c)) { color = c; break; }

            float cutoff = 0.5f;
            bool hasCutoffProp = false;
            foreach (string n in CutoffNames)
                if (s.floats.TryGetValue(n, out float f)) { cutoff = f; hasCutoffProp = true; break; }

            float standardMode = s.floats.TryGetValue("_Mode", out float m) ? m : -1f;
            string lowerName = oldName.ToLowerInvariant();
            bool usesClip = source != null && Regex.IsMatch(source, @"\bclip\s*\(");
            bool alphaTestKeyword = mat.IsKeywordEnabled("_ALPHATEST_ON");
            bool cutout = standardMode == 1f || alphaTestKeyword || lowerName.Contains("cutout") || lowerName.Contains("clip") ||
                          usesClip || (hasCutoffProp && source != null && source.Contains("alphatest"));
            bool transparent = !cutout && (standardMode == 2f || standardMode == 3f ||
                               (lowerName.Contains("transparent") && !lowerName.Contains("cutout")) ||
                               (source != null && Regex.IsMatch(source, @"alpha\s*:\s*(blend|fade)|Blend\s+SrcAlpha", RegexOptions.IgnoreCase)));
            bool doubleSided = lowerName.Contains("double") || lowerName.Contains("twosided") ||
                               (source != null && Regex.IsMatch(source, @"Cull\s+Off", RegexOptions.IgnoreCase)) ||
                               (s.floats.TryGetValue("_Cull", out float cull) && cull == 0f) ||
                               (s.floats.TryGetValue("_CullMode", out float cullMode) && cullMode == 0f);
            bool vertexColor = source != null && Regex.IsMatch(source, @"IN\.color|i\.color|v\.color|IN\.vertexColor|\bCOLOR\b");
            // Foliage with alpha cards is almost always cut out and double sided.
            if (lowerName.Contains("foliage") || lowerName.Contains("leaves") || lowerName.Contains("leaf") || lowerName.Contains("grass") || lowerName.Contains("flower"))
            {
                cutout = true;
                doubleSided = true;
                transparent = false;
            }

            // --- pick the URP shader
            Shader target = vertexColor ? Shader.Find("Universal Render Pipeline/Particles/Simple Lit") : null;
            if (target == null) target = Shader.Find("Universal Render Pipeline/Lit");
            if (target == null) return false;
            bool particles = target.name.Contains("Particles");

            Undo.RecordObject(mat, "Convert to URP");
            mat.shader = target;
            mat.shaderKeywords = new string[0];

            if (main.tex != null)
            {
                mat.SetTexture("_BaseMap", main.tex);
                mat.SetTextureScale("_BaseMap", main.scale);
                mat.SetTextureOffset("_BaseMap", main.offset);
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", main.tex);
                    mat.SetTextureScale("_MainTex", main.scale);
                    mat.SetTextureOffset("_MainTex", main.offset);
                }
            }
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            if (!particles)
            {
                if (s.textures.TryGetValue("_BumpMap", out var bump) && bump.tex != null)
                {
                    mat.SetTexture("_BumpMap", bump.tex);
                    mat.SetFloat("_BumpScale", s.floats.TryGetValue("_BumpScale", out float bs) ? bs : 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (s.textures.TryGetValue("_MetallicGlossMap", out var mg) && mg.tex != null)
                {
                    mat.SetTexture("_MetallicGlossMap", mg.tex);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                if (s.textures.TryGetValue("_OcclusionMap", out var occ) && occ.tex != null)
                {
                    mat.SetTexture("_OcclusionMap", occ.tex);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }
                mat.SetFloat("_Metallic", s.floats.TryGetValue("_Metallic", out float metal) ? metal : 0f);
                float smooth = s.floats.TryGetValue("_Glossiness", out float gl) ? gl :
                               s.floats.TryGetValue("_Smoothness", out float sm) ? sm :
                               s.floats.TryGetValue("_Shininess", out float sh) ? Mathf.Clamp01(sh) * 0.6f : 0.15f;
                mat.SetFloat("_Smoothness", smooth);
                if (s.floats.TryGetValue("_GlossyReflections", out float gr)) mat.SetFloat("_EnvironmentReflections", gr);
                if (s.floats.TryGetValue("_SpecularHighlights", out float sph)) mat.SetFloat("_SpecularHighlights", sph);
                if (s.floats.TryGetValue("_SmoothnessTextureChannel", out float stc)) mat.SetFloat("_SmoothnessTextureChannel", stc);
                mat.SetFloat("_WorkflowMode", 1f);
            }

            // Emission
            bool emissive = s.colors.TryGetValue("_EmissionColor", out Color emission) && emission.maxColorComponent > 0.01f;
            s.textures.TryGetValue("_EmissionMap", out var emissionMap);
            if (emissive || (emissionMap.tex != null && emission.maxColorComponent > 0.01f))
            {
                if (emissionMap.tex != null) mat.SetTexture("_EmissionMap", emissionMap.tex);
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            // Surface / blending
            SetFloatIf(mat, "_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            if (transparent)
            {
                SetFloatIf(mat, "_Surface", 1f);
                SetFloatIf(mat, "_Blend", 0f);
                SetFloatIf(mat, "_AlphaClip", 0f);
                SetFloatIf(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
                SetFloatIf(mat, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                SetFloatIf(mat, "_SrcBlendAlpha", (float)BlendMode.One);
                SetFloatIf(mat, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                SetFloatIf(mat, "_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetShaderPassEnabled("DepthOnly", false);
                mat.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                SetFloatIf(mat, "_Surface", 0f);
                SetFloatIf(mat, "_SrcBlend", (float)BlendMode.One);
                SetFloatIf(mat, "_DstBlend", (float)BlendMode.Zero);
                SetFloatIf(mat, "_SrcBlendAlpha", (float)BlendMode.One);
                SetFloatIf(mat, "_DstBlendAlpha", (float)BlendMode.Zero);
                SetFloatIf(mat, "_ZWrite", 1f);
                mat.SetShaderPassEnabled("DepthOnly", true);
                mat.SetShaderPassEnabled("ShadowCaster", true);
                if (cutout)
                {
                    SetFloatIf(mat, "_AlphaClip", 1f);
                    SetFloatIf(mat, "_Cutoff", Mathf.Clamp(cutoff, 0.05f, 0.95f));
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else
                {
                    SetFloatIf(mat, "_AlphaClip", 0f);
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.renderQueue = -1;
                }
            }

            if (particles)
            {
                SetFloatIf(mat, "_ColorMode", 0f); // multiply by vertex colour
                SetFloatIf(mat, "_ReceiveShadows", 1f);
            }

            EditorUtility.SetDirty(mat);
            mode = target.name + (cutout ? " [cutout]" : transparent ? " [transparent]" : "") + (doubleSided ? " [2-sided]" : "") + (vertexColor ? " [vcol]" : "");
            return true;
        }

        private static void SetFloatIf(Material mat, string name, float value)
        {
            if (mat.HasProperty(name)) mat.SetFloat(name, value);
        }
    }
}
#endif
