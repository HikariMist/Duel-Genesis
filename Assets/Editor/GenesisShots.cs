#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Renders review screenshots of the Genesis Duel Center (outside, inside and a map of where it sits) to
    /// Logs/Shots, so the build can be checked without flying the Scene view around.
    /// </summary>
    public static class GenesisShots
    {
        [MenuItem("Duel Genesis/DEV/Capture Duel Center Screenshots")]
        public static void Capture()
        {
            GameObject hall = GameObject.Find(GenesisDuelCenter.RootName);
            if (hall == null) { Debug.LogWarning("Duel: Genesis: build the Duel Center first (World > 8)."); return; }
            Transform t = hall.transform;
            Bounds b = GenesisWorldBuilder.RendererBounds(hall);
            float W = 40f, D = 32f;
            Transform floor = t.Find("Floor");
            if (floor != null) { W = floor.localScale.x; D = floor.localScale.z; }

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Shots");
            Directory.CreateDirectory(dir);

            Shot(dir, "1_aerial", t.TransformPoint(new Vector3(-W * 0.6f, 38f, D * 0.5f + 48f)), t.TransformPoint(new Vector3(0f, 6f, 0f)), 55f);
            Shot(dir, "2_street", t.TransformPoint(new Vector3(-6f, 1.7f, D * 0.5f + 24f)), t.TransformPoint(new Vector3(0f, 7f, D * 0.5f)), 70f);
            Shot(dir, "3_lobby", t.TransformPoint(new Vector3(0f, 3.4f, D * 0.5f - 1.5f)), t.TransformPoint(new Vector3(0f, 2f, -D * 0.5f + 6f)), 80f);
            Shot(dir, "4_tournament", t.TransformPoint(new Vector3(W * 0.2f, 4.5f, D * 0.5f - 3f)), t.TransformPoint(new Vector3(-W * 0.35f, 0.5f, -D * 0.1f)), 75f);
            Shot(dir, "5_trading", t.TransformPoint(new Vector3(-W * 0.1f, 4.5f, D * 0.5f - 3f)), t.TransformPoint(new Vector3(W * 0.4f, 0.8f, -D * 0.1f)), 75f);
            Shot(dir, "6_stage", t.TransformPoint(new Vector3(0f, 3.5f, 4f)), t.TransformPoint(new Vector3(0f, 2.5f, -D * 0.25f)), 75f);

            // Map: straight down over the plaza and the hall together.
            Vector3 hub = Vector3.zero, c = b.center;
            Vector3 mid = (hub + c) * 0.5f;
            float half = Mathf.Max(Mathf.Abs(c.x - hub.x), Mathf.Abs(c.z - hub.z)) * 0.5f + 60f;
            Shot(dir, "7_map", new Vector3(mid.x, 400f, mid.z), new Vector3(mid.x, 0f, mid.z + 0.001f), 0f, half);
            Debug.Log($"Duel: Genesis saved Duel Center screenshots to {dir} (hall centre {c}, plaza at {hub}).");
        }

        private static void Shot(string dir, string name, Vector3 from, Vector3 lookAt, float fov, float orthoHalf = 0f)
        {
            var go = new GameObject("DG Shot Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(lookAt - from, orthoHalf > 0f ? Vector3.forward : Vector3.up);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            if (orthoHalf > 0f) { cam.orthographic = true; cam.orthographicSize = orthoHalf; }
            else cam.fieldOfView = fov;
            int w = orthoHalf > 0f ? 1200 : 1600, h = orthoHalf > 0f ? 1200 : 900;
            cam.aspect = w / (float)h;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }
}
#endif
