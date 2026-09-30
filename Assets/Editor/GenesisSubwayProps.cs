#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// The "NY Subway - Gates and Vending" props (vending machines, turnstiles, ticket machine, charging station,
    /// info stand, lightbox, help point), converted from HDRP: textures were cut to 2K and the HDRP mask maps
    /// (R metal, G AO, A smoothness) are used directly as URP metallic/occlusion maps. Builds URP materials and
    /// prefabs (Assets/ThirdParty/NY_Subway/Prefabs) from subway_setup.json.
    /// </summary>
    public static class GenesisSubwayProps
    {
        public const string Root = "Assets/ThirdParty/NY_Subway";
        public const string PrefabFolder = Root + "/Prefabs";

        [System.Serializable] private class Renderer_ { public string @object; public string mesh; public string[] materials; }
        [System.Serializable] private class Mat_ { public string @base; public string normal; public string mask; public string emission; public float[] color; public float surface; public float smoothness; public float metallic; }

        [MenuItem("Duel Genesis/Production Assets/Set Up NY Subway Props (URP)")]
        public static void SetUp()
        {
            string json = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Root, "subway_setup.json");
            if (!File.Exists(json)) { Debug.LogWarning("Duel: Genesis: missing " + json); return; }
            var root = MiniJson(File.ReadAllText(json));
            var mats = (Dictionary<string, object>)root["materials"];
            var prefabs = (Dictionary<string, object>)root["prefabs"];

            foreach (string f in new[] { Root + "/Materials", PrefabFolder })
                if (!AssetDatabase.IsValidFolder(f)) AssetDatabase.CreateFolder(Root, Path.GetFileName(f));

            // Normal maps must be imported as normal maps.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                bool normal = p.EndsWith("_N.png");
                bool linear = p.Contains("_AOR");
                if ((ti.textureType == TextureImporterType.NormalMap) != normal || ti.sRGBTexture == linear)
                {
                    ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    ti.sRGBTexture = !linear && !normal;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var made = new Dictionary<string, Material>();
            foreach (var kv in mats)
            {
                var d = (Dictionary<string, object>)kv.Value;
                string path = $"{Root}/Materials/{kv.Key}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                Texture2D T(string key) => d.TryGetValue(key, out object v) && v is string s && s.Length > 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{s}") : null;
                Texture2D bc = T("base"), n = T("normal"), mask = T("mask"), em = T("emission");
                bool glass = d.TryGetValue("surface", out object st) && st is double sd && sd > 0.5 && bc == null;
                m.SetTexture("_BaseMap", bc);
                m.SetColor("_BaseColor", glass ? new Color(0.75f, 0.85f, 0.9f, 0.25f) : Color.white);
                m.SetTexture("_BumpMap", n);
                if (n != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", mask);
                m.SetTexture("_OcclusionMap", mask);
                if (mask != null) { m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_Smoothness", 1f); }
                else { m.DisableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", glass ? 0.95f : 0.5f); m.SetFloat("_Metallic", 0f); }
                m.SetFloat("_SmoothnessTextureChannel", 0f);   // smoothness in the metallic map's alpha
                if (em != null)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetTexture("_EmissionMap", em);
                    m.SetColor("_EmissionColor", Color.white * 1.6f);
                }
                if (glass)
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                made[kv.Key] = m;
            }

            int built = 0;
            foreach (var kv in prefabs)
            {
                var renderers = ((List<object>)kv.Value).Cast<Dictionary<string, object>>().ToList();
                string mesh = renderers.Select(r => r["mesh"] as string).FirstOrDefault(s => !string.IsNullOrEmpty(s) && s != "?");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/{mesh}");
                if (model == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = kv.Key;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var entry = renderers.FirstOrDefault(e => (e["object"] as string) == r.name) ?? renderers[0];
                    var names = ((List<object>)entry["materials"]).Cast<string>().ToList();
                    var list = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < list.Length; i++)
                        list[i] = made.TryGetValue(names[Mathf.Min(i, names.Count - 1)], out Material mm) ? mm : r.sharedMaterials[i];
                    r.sharedMaterials = list;
                }
                // A box collider round the whole prop so players bump into it.
                Bounds b = GenesisWorldBuilder.RendererBounds(go);
                var col = go.AddComponent<BoxCollider>();
                col.center = go.transform.InverseTransformPoint(b.center);
                col.size = b.size;
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{kv.Key}.prefab");
                Object.DestroyImmediate(go);
                built++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel: Genesis set up the NY Subway props: {made.Count} URP materials, {built} prefabs in {PrefabFolder}.");
        }

        public static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/RSG_Subway_{name}.prefab");

        // ---- tiny JSON reader (objects, arrays, strings, numbers, bools, null) so the setup file can stay plain JSON.
        private static Dictionary<string, object> MiniJson(string s) { int i = 0; return (Dictionary<string, object>)Val(s, ref i); }
        private static object Val(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                while (true) { Ws(s, ref i); if (s[i] == '}') { i++; return d; } string k = (string)Val(s, ref i); Ws(s, ref i); i++; d[k] = Val(s, ref i); Ws(s, ref i); if (s[i] == ',') i++; }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                while (true) { Ws(s, ref i); if (s[i] == ']') { i++; return l; } l.Add(Val(s, ref i)); Ws(s, ref i); if (s[i] == ',') i++; }
            }
            if (c == '"')
            {
                var sb = new System.Text.StringBuilder(); i++;
                while (s[i] != '"') { if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); } else sb.Append(s[i]); i++; }
                i++; return sb.ToString();
            }
            int start = i;
            while (i < s.Length && ",}] \n\r\t".IndexOf(s[i]) < 0) i++;
            string tok = s.Substring(start, i - start);
            if (tok == "null") return null;
            if (tok == "true") return true;
            if (tok == "false") return false;
            return double.Parse(tok, System.Globalization.CultureInfo.InvariantCulture);
        }
        private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    }
}
#endif
