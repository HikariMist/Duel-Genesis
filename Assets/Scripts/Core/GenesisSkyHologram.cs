using UnityEngine;
using UnityEngine.Rendering;

namespace DuelGenesis.Core
{
    /// <summary>
    /// The Duel Genesis logo as a giant hologram spinning above the city: a double-sided self-lit logo with
    /// scanlines and a flicker, two counter-rotating light rings and a slow rain of sparks. Built at runtime
    /// from Resources/DuelGenesis/DuelGenesisLogo, so it needs no scene assets.
    /// </summary>
    public sealed class GenesisSkyHologram : MonoBehaviour
    {
        public float width = 30f;
        public float spinDegreesPerSecond = 14f;
        public Color tint = new Color(0.55f, 0.95f, 1f, 1f);

        private Transform _spinner;
        private Transform _ringA, _ringB;
        private Material _logoMaterial, _scanMaterial, _ringMaterial;
        private Vector3 _home;
        private float _nextGlitch, _glitchUntil;

        public static void EnsureInCity()
        {
            if (GameObject.Find("DG City") == null || FindFirstObjectByType<GenesisSkyHologram>() != null) return;
            var go = new GameObject("Genesis Sky Hologram");
            go.transform.position = new Vector3(0f, 42f, 26f);
            go.AddComponent<GenesisSkyHologram>();
        }

        private void Start()
        {
            Texture2D logo = Resources.Load<Texture2D>("DuelGenesis/DuelGenesisLogo") ?? Resources.Load<Texture2D>("UI/DuelGenesisLogo");
            if (logo == null) { enabled = false; return; }
            _home = transform.position;
            float height = width * logo.height / logo.width;

            _spinner = new GameObject("Spinner").transform;
            _spinner.SetParent(transform, false);

            _logoMaterial = Additive("DG Sky Logo", logo, tint);
            _scanMaterial = Additive("DG Sky Scanlines", Scanlines(), new Color(0.2f, 0.8f, 1f, 0.35f));
            // Two faces back to back so the logo reads correctly from every side as it turns.
            for (int side = 0; side < 2; side++)
            {
                Quaternion turn = Quaternion.Euler(0f, side * 180f, 0f);
                Quad(_spinner, "Logo " + side, new Vector3(width, height, 1f), turn, _logoMaterial, 0f);
                Quad(_spinner, "Scanlines " + side, new Vector3(width * 1.04f, height * 1.1f, 1f), turn, _scanMaterial, -0.05f);   // just in front of each face
            }

            _ringMaterial = Additive("DG Sky Ring", Texture2D.whiteTexture, Color.white);
            _ringA = Ring("Ring A", width * 0.62f, 0.35f, new Color(0.2f, 0.9f, 1f, 0.8f));
            _ringB = Ring("Ring B", width * 0.7f, 0.22f, new Color(0.85f, 0.35f, 1f, 0.7f));
            _ringA.localRotation = Quaternion.Euler(78f, 0f, 0f);
            _ringB.localRotation = Quaternion.Euler(-72f, 30f, 0f);
            Sparks(height);
        }

        private void Update()
        {
            if (_spinner == null) return;
            float t = Time.time;
            _spinner.localRotation = Quaternion.Euler(0f, t * spinDegreesPerSecond, 0f);
            transform.position = _home + Vector3.up * Mathf.Sin(t * 0.6f) * 0.8f;
            _ringA.Rotate(0f, 0f, 22f * Time.deltaTime, Space.Self);
            _ringB.Rotate(0f, 0f, -16f * Time.deltaTime, Space.Self);

            // Hologram flicker, plus a short sideways glitch every few seconds.
            float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(t * 7f, 0.3f);
            if (t > _nextGlitch)
            {
                _glitchUntil = t + 0.12f;
                _nextGlitch = t + Random.Range(3.5f, 7f);
            }
            bool glitch = t < _glitchUntil;
            _spinner.localPosition = glitch ? new Vector3(Random.Range(-0.6f, 0.6f), 0f, 0f) : Vector3.zero;
            Color c = tint * (glitch ? 1.5f : flicker);
            c.a = 1f;
            _logoMaterial.SetColor("_BaseColor", c);
            _scanMaterial.SetTextureOffset("_BaseMap", new Vector2(0f, -t * 0.35f));
        }

        // ------------------------------------------------------------------ building blocks

        private static Material Additive(string name, Texture texture, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            var m = new Material(shader) { name = name };
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);                               // transparent
                m.SetFloat("_Blend", 2f);                                 // additive
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", (float)CullMode.Back);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent + 10;
            }
            return m;
        }

        private static void Quad(Transform parent, string name, Vector3 scale, Quaternion rotation, Material material, float z)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localRotation = rotation;
            q.transform.localPosition = rotation * new Vector3(0f, 0f, z);
            q.transform.localScale = scale;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private Transform Ring(string name, float radius, float lineWidth, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            const int segments = 96;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                // Dashed look: every fourth segment dips inward a touch, like a HUD dial.
                float r = radius * (i % 8 == 0 ? 0.97f : 1f);
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            line.widthMultiplier = lineWidth;
            line.sharedMaterial = _ringMaterial;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        private void Sparks(float height)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 6f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.95f, 1f, 0.9f), new Color(0.9f, 0.5f, 1f, 0.9f));
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.02f;
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width * 0.9f, height * 0.8f, 2f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Additive("DG Sky Sparks", Glow(), Color.white);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        private static Texture2D Scanlines()
        {
            var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { name = "DG Scanlines", wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++)
            {
                float a = (y % 4 == 0 ? 0.55f : 0.08f) + (y > 28 && y < 34 ? 0.25f : 0f);
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return t;
        }

        private static Texture2D Glow()
        {
            const int s = 32;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "DG Spark", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            t.Apply();
            return t;
        }
    }
}
