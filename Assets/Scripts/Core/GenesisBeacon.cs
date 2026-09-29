using UnityEngine;
using UnityEngine.Rendering;

namespace DuelGenesis.Core
{
    /// <summary>
    /// A tall hologram light beam with a floating, camera-facing label, so card shops and duel tables
    /// can be found from anywhere in the city. Built at runtime; placed by the world builder.
    /// </summary>
    public sealed class GenesisBeacon : MonoBehaviour
    {
        public string label = "DUEL TABLE";
        public Color color = new Color(1f, 0.3f, 0.75f);
        public float beamHeight = 55f;
        public float labelHeight = 5.5f;

        private Transform _label;
        private Material _beam;
        private float _phase;

        private void Start()
        {
            _phase = Random.value * 10f;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");

            // Beam: an open cylinder with a vertical fade, additive.
            _beam = new Material(shader) { name = "DG Beacon Beam" };
            _beam.SetTexture("_BaseMap", Fade());
            _beam.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.55f));
            if (_beam.HasProperty("_Surface"))
            {
                _beam.SetFloat("_Surface", 1f);
                _beam.SetFloat("_Blend", 2f);
                _beam.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _beam.SetFloat("_DstBlend", (float)BlendMode.One);
                _beam.SetFloat("_ZWrite", 0f);
                _beam.SetFloat("_Cull", (float)CullMode.Off);
                _beam.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _beam.renderQueue = (int)RenderQueue.Transparent;
            }
            var beam = new GameObject("Beam");
            beam.transform.SetParent(transform, false);
            beam.AddComponent<MeshFilter>().sharedMesh = Tube(0.35f, beamHeight, 20);
            var r = beam.AddComponent<MeshRenderer>();
            r.sharedMaterial = _beam;
            r.shadowCastingMode = ShadowCastingMode.Off;

            // Floating label.
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, labelHeight, 0f);
            var text = labelGo.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.05f;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.Lerp(color, Color.white, 0.35f);
            _label = labelGo.transform;
        }

        private void LateUpdate()
        {
            if (_label != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 away = _label.position - cam.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.01f) _label.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                    // Stay readable from afar without getting huge up close.
                    float d = Vector3.Distance(cam.transform.position, _label.position);
                    _label.localScale = Vector3.one * Mathf.Clamp(d / 18f, 1f, 5f);
                }
                _label.localPosition = new Vector3(0f, labelHeight + Mathf.Sin(Time.time * 1.4f + _phase) * 0.15f, 0f);
            }
            if (_beam != null)
            {
                float pulse = 0.45f + 0.15f * Mathf.Sin(Time.time * 2f + _phase);
                _beam.SetColor("_BaseColor", new Color(color.r, color.g, color.b, pulse));
            }
        }

        private static Mesh Tube(float radius, float height, int sides)
        {
            var mesh = new Mesh { name = "DG Beacon Tube" };
            var v = new Vector3[(sides + 1) * 2];
            var uv = new Vector2[v.Length];
            var tris = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                v[i * 2] = p;
                v[i * 2 + 1] = p + Vector3.up * height;
                uv[i * 2] = new Vector2(i / (float)sides, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)sides, 1f);
            }
            for (int i = 0; i < sides; i++)
            {
                int b = i * 2, t = i * 6;
                tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                tris[t + 3] = b + 1; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
            }
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D Fade()
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DG Beacon Fade" };
            for (int y = 0; y < 64; y++)
            {
                float k = y / 63f;
                t.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - k, 1.6f)));
            }
            t.Apply();
            return t;
        }
    }
}
