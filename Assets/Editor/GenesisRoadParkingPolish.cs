#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Non-destructive finishing pass for Genesis City's existing roads and Card Shop parking lot.
    /// Road positions/widths are intentionally unchanged so all existing world and gameplay systems still line up.
    /// </summary>
    public static class GenesisRoadParkingPolish
    {
        private const string RootName = "Road & Parking Polish";
        private const string ParkingRootName = "Parking Lot Polish";
        private const string MatFolder = "Assets/Art/Generated/RoadParkingPolish";
        private const float Half = 420f, PlazaR = 50f, RingW = 12f, RingOuter = 62f;
        private const float Boulevard = 20f, Avenue = 16f, PaintY = 0.034f;
        private static readonly float[] Lines = { -360f, -240f, -120f, 0f, 120f, 240f, 360f };
        private static Material _intersection, _white, _yellow, _blue, _dark;

        [MenuItem("Duel Genesis/World/23. Polish Roads + Parking Lots")]
        public static void Build()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null)
            {
                Debug.LogWarning("Duel: Genesis: build the open city first (World > 9).");
                return;
            }
            Transform map = city.transform.Find("Map");
            Transform roads = map != null ? map.Find("Roads") : null;
            if (map == null || roads == null) { Debug.LogWarning("Duel: Genesis: Map/Roads is missing."); return; }

            Transform old = map.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Materials();

            int removed = 0;
            foreach (Renderer r in roads.GetComponentsInChildren<Renderer>(true).ToArray())
            {
                if (r == null || r.sharedMaterial == null || r.sharedMaterial.name != "OW Road Paint Yellow") continue;
                if (!r.gameObject.name.EndsWith(" Line", StringComparison.Ordinal)) continue;
                Object.DestroyImmediate(r.gameObject);
                removed++;
            }

            Transform root = Child(map, RootName);
            int intersections = Intersections(Child(root, "Finished Intersections"));
            int roadMarks = RoadMarkings(Child(root, "Road Markings"));
            int ringMarks = RingRoad(Child(root, "Ring Road Markings"));
            int lotDetails = CardShopParking(city.transform);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"Duel: Genesis road/parking polish: {intersections} intersections, {roadMarks + ringMarks} road markings, " +
                      $"{lotDetails} parking-lot details; replaced {removed} legacy centre stripes. Gameplay/world coordinates unchanged.");
        }

        private static int Intersections(Transform root)
        {
            int count = 0;
            foreach (float x in Lines)
            foreach (float z in Lines)
            {
                if (Mathf.Abs(x) < .01f && Mathf.Abs(z) < .01f) continue;
                float wx = RoadWidth(x), wz = RoadWidth(z);
                Box(root, "Intersection", new Vector3(x, .024f, z), new Vector3(wx + .8f, .008f, wz + .8f), _intersection);
                count++;

                if (Mathf.Abs(x) < .01f)
                {
                    float e = wz * .5f + 2f;
                    ZebraX(root, new Vector3(x, PaintY, z - e), Boulevard - 1.5f, 3.2f);
                    ZebraX(root, new Vector3(x, PaintY, z + e), Boulevard - 1.5f, 3.2f);
                    BarX(root, new Vector3(x, PaintY, z - e - 2.2f), Boulevard - 1.5f);
                    BarX(root, new Vector3(x, PaintY, z + e + 2.2f), Boulevard - 1.5f);
                }
                else if (Mathf.Abs(z) < .01f)
                {
                    float e = wx * .5f + 2f;
                    ZebraZ(root, new Vector3(x - e, PaintY, z), Boulevard - 1.5f, 3.2f);
                    ZebraZ(root, new Vector3(x + e, PaintY, z), Boulevard - 1.5f, 3.2f);
                    BarZ(root, new Vector3(x - e - 2.2f, PaintY, z), Boulevard - 1.5f);
                    BarZ(root, new Vector3(x + e + 2.2f, PaintY, z), Boulevard - 1.5f);
                }
                else
                {
                    float hz = wz * .5f;
                    Box(root, "Stop Bar", new Vector3(x, PaintY, z - hz - 1.1f), new Vector3(wx - 2f, .008f, .22f), _white);
                    Box(root, "Stop Bar", new Vector3(x, PaintY, z + hz + 1.1f), new Vector3(wx - 2f, .008f, .22f), _white);
                }
            }
            return count;
        }

        private static int RoadMarkings(Transform root)
        {
            int n = 0;
            foreach (float line in Lines)
            {
                n += CentreDashes(root, line, true);
                n += CentreDashes(root, line, false);
                if (Mathf.Abs(line) < .01f)
                {
                    n += LaneDashes(root, 4.75f, true); n += LaneDashes(root, -4.75f, true);
                    n += LaneDashes(root, 4.75f, false); n += LaneDashes(root, -4.75f, false);
                }
            }
            return n;
        }

        private static int CentreDashes(Transform root, float fixedLine, bool alongZ)
        {
            const float dash = 4.5f, gap = 3f;
            int n = 0;
            for (float p = -Half + dash * .5f; p < Half; p += dash + gap)
            {
                if (NearCrossStreet(p, 6f) || (Mathf.Abs(fixedLine) < .01f && Mathf.Abs(p) < RingOuter + 3f)) continue;
                foreach (float side in new[] { -.18f, .18f })
                {
                    Vector3 c = alongZ ? new Vector3(fixedLine + side, PaintY, p) : new Vector3(p, PaintY, fixedLine + side);
                    Vector3 s = alongZ ? new Vector3(.13f, .008f, dash) : new Vector3(dash, .008f, .13f);
                    Box(root, "Double Yellow Dash", c, s, _yellow); n++;
                }
            }
            return n;
        }

        private static int LaneDashes(Transform root, float lateral, bool alongZ)
        {
            const float dash = 3.2f, gap = 4.8f;
            int n = 0;
            for (float p = -Half + dash * .5f; p < Half; p += dash + gap)
            {
                if (NearCrossStreet(p, 6f) || Mathf.Abs(p) < RingOuter + 3f) continue;
                Vector3 c = alongZ ? new Vector3(lateral, PaintY, p) : new Vector3(p, PaintY, lateral);
                Vector3 s = alongZ ? new Vector3(.12f, .008f, dash) : new Vector3(dash, .008f, .12f);
                Box(root, "Boulevard Lane Dash", c, s, _white); n++;
            }
            return n;
        }

        private static int RingRoad(Transform root)
        {
            int n = RingLine(root, PlazaR + .65f, .18f, _white, false);
            n += RingLine(root, PlazaR + RingW * .5f - .18f, .12f, _yellow, true);
            n += RingLine(root, PlazaR + RingW * .5f + .18f, .12f, _yellow, true);
            float g = RingOuter + 4.2f;
            ZebraX(root, new Vector3(0f, PaintY, g), Boulevard - 1.6f, 3f);
            ZebraX(root, new Vector3(0f, PaintY, -g), Boulevard - 1.6f, 3f);
            ZebraZ(root, new Vector3(g, PaintY, 0f), Boulevard - 1.6f, 3f);
            ZebraZ(root, new Vector3(-g, PaintY, 0f), Boulevard - 1.6f, 3f);
            BarX(root, new Vector3(0f, PaintY, g + 2.1f), Boulevard - 1.6f);
            BarX(root, new Vector3(0f, PaintY, -g - 2.1f), Boulevard - 1.6f);
            BarZ(root, new Vector3(g + 2.1f, PaintY, 0f), Boulevard - 1.6f);
            BarZ(root, new Vector3(-g - 2.1f, PaintY, 0f), Boulevard - 1.6f);
            return n + 8;
        }

        private static int RingLine(Transform root, float radius, float width, Material mat, bool dashed)
        {
            const int segments = 96;
            int n = 0;
            float len = 2f * Mathf.PI * radius / segments * 1.04f;
            for (int i = 0; i < segments; i++)
            {
                if (dashed && i % 3 != 0) continue;
                float a = i * 360f / segments;
                if (NearCardinal(a, 10f)) continue;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * radius + Vector3.up * PaintY;
                GameObject b = Box(root, "Ring Marking", p, new Vector3(len, .008f, width), mat);
                b.transform.localRotation = Quaternion.Euler(0f, a, 0f); n++;
            }
            return n;
        }

        private static int CardShopParking(Transform city)
        {
            Transform shop = city.Find(GenesisDuelCenter.CardShopName);
            if (shop == null) return 0;
            Transform old = shop.Find(ParkingRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform root = Child(shop, ParkingRootName);
            int n = 0;

            Box(root, "Front Row Header", new Vector3(0f, .061f, 20.8f), new Vector3(41f, .008f, .12f), _white); n++;
            Box(root, "Back Row Header", new Vector3(0f, .061f, 28f), new Vector3(41f, .008f, .12f), _white); n++;

            for (float z = 21.25f; z <= 27.55f; z += .9f)
            { Box(root, "Pedestrian Crossing", new Vector3(0f, .067f, z), new Vector3(2.8f, .008f, .42f), _white); n++; }

            Vector3 access = new Vector3(0f, .068f, 18.05f);
            Box(root, "Accessible Access Aisle", access, new Vector3(1.25f, .006f, 5f), _blue); n++;
            for (float z = -2f; z <= 2f; z += .65f)
            {
                GameObject h = Box(root, "Accessible Hatch", access + new Vector3(0f, .006f, z), new Vector3(1.45f, .006f, .10f), _white);
                h.transform.localRotation = Quaternion.Euler(0f, 28f, 0f); n++;
            }

            foreach ((float x, float yaw) in new[] { (-12f, 180f), (12f, 0f) })
            {
                Box(root, "Driveway Stop Bar", new Vector3(x, .071f, 42.6f), new Vector3(6f, .008f, .26f), _white); n++;
                Arrow(root, new Vector3(x, .072f, 38.7f), yaw); n += 3;
            }

            foreach (float x in new[] { -18f, 0f, 18f })
            {
                Box(root, "Lamp Safety Island", new Vector3(x, .063f, 24.4f), new Vector3(2.6f, .006f, 1.25f), _yellow); n++;
                Box(root, "Island Inner", new Vector3(x, .065f, 24.4f), new Vector3(2.2f, .006f, .85f), _dark); n++;
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return n;
        }

        private static void ZebraX(Transform root, Vector3 c, float span, float depth)
        {
            const int k = 7; float step = span / k;
            for (int i = 0; i < k; i++) Box(root, "Crosswalk Stripe", c + Vector3.right * (-span * .5f + step * (i + .5f)), new Vector3(step * .56f, .008f, depth), _white);
        }
        private static void ZebraZ(Transform root, Vector3 c, float span, float depth)
        {
            const int k = 7; float step = span / k;
            for (int i = 0; i < k; i++) Box(root, "Crosswalk Stripe", c + Vector3.forward * (-span * .5f + step * (i + .5f)), new Vector3(depth, .008f, step * .56f), _white);
        }
        private static void BarX(Transform root, Vector3 c, float span) => Box(root, "Stop Bar", c, new Vector3(span, .008f, .32f), _white);
        private static void BarZ(Transform root, Vector3 c, float span) => Box(root, "Stop Bar", c, new Vector3(.32f, .008f, span), _white);

        private static void Arrow(Transform parent, Vector3 c, float yaw)
        {
            Transform r = Child(parent, "Directional Arrow"); r.localPosition = c; r.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(r, "Stem", new Vector3(0f, 0f, -.25f), new Vector3(.22f, .008f, 2.2f), _white);
            GameObject l = Box(r, "Head", new Vector3(-.42f, 0f, .75f), new Vector3(.18f, .008f, 1.15f), _white); l.transform.localRotation = Quaternion.Euler(0f, -42f, 0f);
            GameObject rr = Box(r, "Head", new Vector3(.42f, 0f, .75f), new Vector3(.18f, .008f, 1.15f), _white); rr.transform.localRotation = Quaternion.Euler(0f, 42f, 0f);
        }

        private static bool NearCrossStreet(float p, float pad) => Lines.Any(x => Mathf.Abs(p - x) < RoadWidth(x) * .5f + pad);
        private static float RoadWidth(float line) => Mathf.Abs(line) < .01f ? Boulevard : Avenue;
        private static bool NearCardinal(float a, float d) => new[] { 0f, 90f, 180f, 270f }.Any(x => Mathf.Abs(Mathf.DeltaAngle(a, x)) < d);

        private static Transform Child(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }
        private static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }

        private static void Materials()
        {
            _intersection = Mat("RP Intersection Asphalt", new Color(.125f, .13f, .145f), .18f);
            _white = Mat("RP Traffic White", new Color(.94f, .94f, .91f), .22f);
            _yellow = Mat("RP Traffic Yellow", new Color(.96f, .75f, .16f), .22f);
            _blue = Mat("RP Accessible Blue", new Color(.08f, .28f, .78f), .25f);
            _dark = Mat("RP Island Dark", new Color(.08f, .085f, .095f), .12f);
        }
        private static Material Mat(string name, Color colour, float smooth)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "RoadParkingPolish");
            string path = $"{MatFolder}/{name}.mat"; Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = lit; m.SetColor("_BaseColor", colour); m.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(m); return m;
        }
    }
}
#endif
