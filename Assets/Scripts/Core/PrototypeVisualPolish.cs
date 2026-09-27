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
        private static readonly Color Gold = new Color(1.00f, 0.72f, 0.20f, 1f);

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            if (_applied) return;
            _applied = true;

            if (GameObject.Find("DG City") != null)
            {
                ApplyCityAtmosphere();
                return;
            }

            RenderSettings.ambientLight = new Color(0.12f, 0.15f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.018f, 0.025f, 0.05f, 1f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 55f;

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

            BuildGenesisPlazaDecor();
            AddAccentLights();   // the duel table builds its own mat, zones and lighting (DuelBoardView)
        }

        /// <summary>Genesis City (Akihabara map) is in the scene: dusk sky, long-range haze, neon accents kept.</summary>
        private static void ApplyCityAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.47f, 0.52f, 1f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 480f;

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 900f);
            }

            Colorize("Pack Terminal - 1000 GC", Purple, true);
            Colorize("Seat Interaction", new Color(0.10f, 0.16f, 0.22f, 1f));
            BuildGenesisPlazaDecor(true);
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

        private static void BuildGenesisPlazaDecor(bool city = false)
        {
            if (GameObject.Find("DG Genesis Plaza Decor") != null)
                return;

            GameObject root = new GameObject("DG Genesis Plaza Decor");

            // Main illuminated route from spawn toward the shop and duel table.
            for (int i = 0; i < 8; i++)
            {
                float z = -5.5f + i * 1.45f;
                CreateWorldTile(root.transform, $"Path Cyan {i}", new Vector3(-0.55f, 0.025f, z), new Vector3(0.055f, 0.025f, 1.0f), Cyan);
                CreateWorldTile(root.transform, $"Path Purple {i}", new Vector3(0.55f, 0.025f, z), new Vector3(0.055f, 0.025f, 1.0f), Purple);
            }

            // Neon plaza pylons give the otherwise graybox world depth and landmarks.
            CreatePylon(root.transform, "Shop Pylon A", new Vector3(-9.2f, 0f, -1.2f), Purple);
            CreatePylon(root.transform, "Shop Pylon B", new Vector3(-2.8f, 0f, -1.2f), Cyan);
            CreatePylon(root.transform, "Arena Pylon A", new Vector3(3.4f, 0f, -1.1f), Cyan);
            CreatePylon(root.transform, "Arena Pylon B", new Vector3(8.8f, 0f, -1.1f), Magenta);

            if (city)
            {
                // Real-world sized signage for the Genesis City hub (letters ~25-40 cm tall).
                CreateSign(root.transform, "GENESIS CARD SHOP", new Vector3(-6f, 2.75f, 0.85f), Purple, 0.06f);
                CreateSign(root.transform, "DUEL TABLE", new Vector3(6f, 2.55f, 3.6f), Cyan, 0.05f);
                CreateSign(root.transform, "GENESIS CITY", new Vector3(0f, 0.03f, 7.8f), Mint, 0.07f, new Vector3(90f, 0f, 0f));
            }
            else
            {
                CreateSign(root.transform, "GENESIS CARD SHOP", new Vector3(-6f, 4.15f, -0.62f), Purple, 0.42f);
                CreateSign(root.transform, "NEON DUEL TABLE", new Vector3(6f, 3.15f, 0f), Cyan, 0.36f);
                CreateSign(root.transform, "GENESIS CITY // PROTOTYPE DISTRICT", new Vector3(0f, 0.08f, 7.8f), Mint, 0.31f, new Vector3(90f, 0f, 0f));
            }

            // Small glowing kiosks imply a larger future city without needing external art assets yet.
            CreateKiosk(root.transform, "Collection Kiosk", new Vector3(-2.8f, 0.6f, 5.8f), Cyan);
            CreateKiosk(root.transform, "Deck Workshop Kiosk", new Vector3(0f, 0.6f, 5.8f), Purple);
            CreateKiosk(root.transform, "Ranked Arena Kiosk", new Vector3(2.8f, 0.6f, 5.8f), Magenta);
        }

        private static void CreateWorldTile(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = name;
            tile.transform.SetParent(parent, false);
            tile.transform.position = position;
            tile.transform.localScale = scale;
            RemoveCollider(tile);
            Renderer renderer = tile.GetComponent<Renderer>();
            if (renderer != null) renderer.material = CreateMaterial(color, true);
        }

        private static void CreatePylon(Transform parent, string name, Vector3 position, Color color)
        {
            GameObject pylon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pylon.name = name;
            pylon.transform.SetParent(parent, false);
            pylon.transform.position = position + new Vector3(0f, 1.6f, 0f);
            pylon.transform.localScale = new Vector3(0.22f, 3.2f, 0.22f);
            RemoveCollider(pylon);
            Renderer renderer = pylon.GetComponent<Renderer>();
            if (renderer != null) renderer.material = CreateMaterial(color, true);

            AddPointLight(pylon.transform, name + " Light", new Vector3(0f, 0.8f, 0f), color, 1.8f, 4.5f);
        }

        private static void CreateKiosk(Transform parent, string name, Vector3 position, Color color)
        {
            GameObject kiosk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kiosk.name = name;
            kiosk.transform.SetParent(parent, false);
            kiosk.transform.position = position;
            kiosk.transform.localScale = new Vector3(1.5f, 1.2f, 0.65f);
            RemoveCollider(kiosk);
            Renderer renderer = kiosk.GetComponent<Renderer>();
            if (renderer != null) renderer.material = CreateMaterial(new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f), false);

            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name + " Glow";
            strip.transform.SetParent(kiosk.transform, false);
            strip.transform.localPosition = new Vector3(0f, 0.25f, -0.53f);
            strip.transform.localScale = new Vector3(0.84f, 0.08f, 0.04f);
            RemoveCollider(strip);
            Renderer stripRenderer = strip.GetComponent<Renderer>();
            if (stripRenderer != null) stripRenderer.material = CreateMaterial(color, true);
        }

        private static void CreateSign(Transform parent, string text, Vector3 position, Color color, float characterSize, Vector3? rotation = null)
        {
            GameObject sign = new GameObject("DG Sign - " + text);
            sign.transform.SetParent(parent, false);
            sign.transform.position = position;
            sign.transform.rotation = Quaternion.Euler(rotation ?? Vector3.zero);

            TextMesh mesh = sign.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = characterSize;
            mesh.fontSize = 64;
            mesh.color = color;

            MeshRenderer renderer = sign.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.material = CreateMaterial(color, true);
        }

        private static void CreateTile(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = name;
            tile.transform.SetParent(parent, false);
            tile.transform.localPosition = localPosition;
            tile.transform.localScale = localScale;
            RemoveCollider(tile);

            Renderer renderer = tile.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateMaterial(color, true);
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }

        private static void AddAccentLights()
        {
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
