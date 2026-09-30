#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DuelGenesis.EditorTools
{
    /// <summary>Renders the whole city from straight above (north up) into Resources/DuelGenesis/Map/city_map.png for the minimap.</summary>
    public static class GenesisMinimapBaker
    {
        private const int Size = 2048;

        [MenuItem("Duel Genesis/World/17. Bake Minimap (top-down city map)")]
        public static void Bake()
        {
            string dir = "Assets/Resources/DuelGenesis/Map";
            Directory.CreateDirectory(dir);
            float half = DuelGenesis.UI.GenesisMinimap.WorldHalf;

            // Hide things that float above the city (the sky hologram, particles) while we shoot.
            var hidden = new List<Renderer>();
            foreach (var holo in Object.FindObjectsByType<DuelGenesis.Core.GenesisSkyHologram>(FindObjectsSortMode.None))
                foreach (var r in holo.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            foreach (var ps in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
                if (ps.enabled) { ps.enabled = false; hidden.Add(ps); }

            var go = new GameObject("Minimap Bake Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            go.transform.SetPositionAndRotation(new Vector3(0f, 500f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            cam.orthographic = true;
            cam.orthographicSize = half;
            cam.aspect = 1f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.14f, 0.1f);
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;

            // Brighten a dusk render so the map reads clearly (aim for a mid-grey average).
            Color[] px = tex.GetPixels();
            float lum = 0f;
            foreach (var c in px) lum += c.grayscale;
            lum /= px.Length;
            float gain = Mathf.Clamp(0.42f / Mathf.Max(0.02f, lum), 1f, 3f);
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i] * gain;
                float g = c.grayscale;
                c = Color.Lerp(new Color(g, g, g), c, 0.85f);   // a touch less saturated, map-like
                px[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
            }
            tex.SetPixels(px);
            tex.Apply();
            string path = dir + "/city_map.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            foreach (var r in hidden) if (r != null) r.enabled = true;

            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.maxTextureSize = 2048;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            Debug.Log($"Duel: Genesis baked the minimap ({Size}px, {half * 2f:0} m square, brightness x{gain:0.00}) into {path}.");
        }
    }
}
#endif
