using UnityEngine;

namespace DuelGenesis.Core
{
    public class PrototypeVisualPolish : MonoBehaviour
    {
        private bool _applied;

        private static readonly Color Graphite = new Color(0.055f, 0.065f, 0.09f, 1f);
        private static readonly Color DeepBlue = new Color(0.06f, 0.10f, 0.18f, 1f);
        private static readonly Color Cyan = new Color(0.08f, 0.78f, 1.00f, 1f);
        private static readonly Color Purple = new Color(0.62f, 0.20f, 1.00f, 1f);
        private static readonly Color Magenta = new Color(1.00f, 0.16f, 0.58f, 1f);
        private static readonly Color Mint = new Color(0.34f, 1.00f, 0.72f, 1f);
        private static readonly Color SoftWhite = new Color(0.88f, 0.95f, 1.00f, 1f);

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            if (_applied) return;
            _applied = true;

            RenderSettings.ambientLight = new Color(0.12f, 0.15f, 0.23f);

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = new Color(0.018f, 0.028f, 0.055f, 1f);
                camera.clearFlags = CameraClearFlags.SolidColor;
            }

            Colorize("Genesis_City_Test_Ground", Graphite);
            Colorize("Shop Building", DeepBlue);
            Colorize("Pack Terminal - 1000 GC", Purple, true);
            Colorize("Tabletop Arena Blockout", new Color(0.035f, 0.045f, 0.07f, 1f));
            Colorize("Blue Rail", Cyan, true);
            Colorize("Red Rail", Magenta, true);
            Colorize("Back Rail", Purple, true);
            Colorize("Front Rail", Purple, true);
            Colorize("Player_Hikari_Blockout", SoftWhite);
            Colorize("Seat Interaction", new Color(0.10f, 0.16f, 0.22f, 1f));

            BuildTabletopZoneGlow();
            AddAccentLights();
        }

        private static void Colorize(string objectName, Color color, bool emission = false)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj == null) return;

            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            Material material = CreateMaterial(color, emission);
            if (material != null)
                renderer.material = material;
        }

        private static Material CreateMaterial(Color color, bool emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return null;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                Color emissionColor = color * 1.8f;
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", emissionColor);
            }

            material.name = "DG Runtime Material";
            return material;
        }

        private static void BuildTabletopZoneGlow()
        {
            GameObject root = GameObject.Find("Duel Table Prototype");
            if (root == null || root.transform.Find("DG Visual Zone Grid") != null)
                return;

            GameObject grid = new GameObject("DG Visual Zone Grid");
            grid.transform.SetParent(root.transform, false);

            const float startX = -1.68f;
            const float spacing = 0.84f;

            for (int i = 0; i < 5; i++)
            {
                float x = startX + spacing * i;
                CreateTile(grid.transform, $"Player Monster Zone {i + 1}", new Vector3(x, 1.345f, -0.34f), new Vector3(0.67f, 0.018f, 0.46f), Cyan);
                CreateTile(grid.transform, $"CPU Monster Zone {i + 1}", new Vector3(x, 1.345f, 0.34f), new Vector3(0.67f, 0.018f, 0.46f), Magenta);
                CreateTile(grid.transform, $"Player Spell Trap Zone {i + 1}", new Vector3(x, 1.345f, -0.91f), new Vector3(0.67f, 0.014f, 0.32f), new Color(0.10f, 0.45f, 0.52f, 1f));
                CreateTile(grid.transform, $"CPU Spell Trap Zone {i + 1}", new Vector3(x, 1.345f, 0.91f), new Vector3(0.67f, 0.014f, 0.32f), new Color(0.42f, 0.13f, 0.50f, 1f));
            }

            CreateTile(grid.transform, "Genesis Center Line", new Vector3(0f, 1.36f, 0f), new Vector3(4.08f, 0.022f, 0.045f), Mint);
        }

        private static void CreateTile(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = name;
            tile.transform.SetParent(parent, false);
            tile.transform.localPosition = localPosition;
            tile.transform.localScale = localScale;

            Collider collider = tile.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = tile.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateMaterial(color, true);
        }

        private static void AddAccentLights()
        {
            GameObject table = GameObject.Find("Duel Table Prototype");
            if (table != null && table.transform.Find("DG Cyan Light") == null)
            {
                AddPointLight(table.transform, "DG Cyan Light", new Vector3(-1.7f, 3.2f, -0.2f), Cyan, 3.2f, 6.5f);
                AddPointLight(table.transform, "DG Magenta Light", new Vector3(1.7f, 3.2f, 0.2f), Magenta, 3.2f, 6.5f);
            }

            GameObject shop = GameObject.Find("Genesis Card Shop Prototype");
            if (shop != null && shop.transform.Find("DG Shop Glow") == null)
                AddPointLight(shop.transform, "DG Shop Glow", new Vector3(0f, 2.4f, -1.3f), Purple, 2.8f, 5f);
        }

        private static void AddPointLight(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }
    }
}
