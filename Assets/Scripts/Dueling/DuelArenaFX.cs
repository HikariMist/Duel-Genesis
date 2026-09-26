using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelArenaFX : MonoBehaviour
    {
        private Transform _fxRoot;
        private Transform _innerCore;
        private Transform _outerCore;
        private Light _centerLight;
        private DuelGameController _duel;

        private static readonly Color Cyan = new Color(0.08f, 0.78f, 1f, 1f);
        private static readonly Color Purple = new Color(0.62f, 0.20f, 1f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.16f, 0.58f, 1f);

        private void Start()
        {
            BuildIfNeeded();
        }

        private void Update()
        {
            if (_fxRoot == null)
                BuildIfNeeded();
            if (_fxRoot == null)
                return;

            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            bool active = _duel != null && _duel.IsActive;
            bool cpuTurn = active && _duel.Phase == DuelTurnPhase.Opponent;
            float speed = active ? 65f : 14f;
            float pulse = active ? 1f + Mathf.Sin(Time.time * 4f) * 0.10f : 0.82f;

            if (_innerCore != null)
            {
                _innerCore.Rotate(Vector3.up, speed * Time.deltaTime, Space.Self);
                _innerCore.localScale = new Vector3(0.85f * pulse, 0.025f, 0.85f * pulse);
            }

            if (_outerCore != null)
            {
                _outerCore.Rotate(Vector3.up, -speed * 0.55f * Time.deltaTime, Space.Self);
                float outerPulse = active ? 1f + Mathf.Sin(Time.time * 3f + 1f) * 0.08f : 0.90f;
                _outerCore.localScale = new Vector3(1.35f * outerPulse, 0.018f, 1.35f * outerPulse);
            }

            if (_centerLight != null)
            {
                _centerLight.color = cpuTurn ? Magenta : Cyan;
                _centerLight.intensity = active ? 3.2f + Mathf.Sin(Time.time * 5f) * 0.8f : 0.8f;
            }
        }

        private void BuildIfNeeded()
        {
            GameObject table = GameObject.Find("Duel Table Prototype");
            if (table == null) return;

            Transform existing = table.transform.Find("DG Duel Arena FX");
            if (existing != null)
            {
                _fxRoot = existing;
                _innerCore = existing.Find("Inner Hologram Core");
                _outerCore = existing.Find("Outer Hologram Core");
                _centerLight = existing.GetComponentInChildren<Light>();
                return;
            }

            GameObject root = new GameObject("DG Duel Arena FX");
            root.transform.SetParent(table.transform, false);
            _fxRoot = root.transform;

            _innerCore = CreateDisc(_fxRoot, "Inner Hologram Core", new Vector3(0f, 1.43f, 0f), new Vector3(0.85f, 0.025f, 0.85f), Cyan);
            _outerCore = CreateDisc(_fxRoot, "Outer Hologram Core", new Vector3(0f, 1.40f, 0f), new Vector3(1.35f, 0.018f, 1.35f), Purple);

            CreateBeacon(_fxRoot, "Player Holo Beacon", new Vector3(0f, 1.62f, -1.20f), Cyan);
            CreateBeacon(_fxRoot, "CPU Holo Beacon", new Vector3(0f, 1.62f, 1.20f), Purple);

            GameObject lightObject = new GameObject("Arena Hologram Light");
            lightObject.transform.SetParent(_fxRoot, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            _centerLight = lightObject.AddComponent<Light>();
            _centerLight.type = LightType.Point;
            _centerLight.color = Cyan;
            _centerLight.range = 4.5f;
            _centerLight.intensity = 0.8f;
        }

        private static Transform CreateDisc(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            RemoveCollider(obj);

            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateEmissiveMaterial(color);

            return obj.transform;
        }

        private static void CreateBeacon(Transform parent, string name, Vector3 localPosition, Color color)
        {
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = name;
            baseObj.transform.SetParent(parent, false);
            baseObj.transform.localPosition = localPosition;
            baseObj.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);
            RemoveCollider(baseObj);

            Renderer renderer = baseObj.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateEmissiveMaterial(color);
        }

        private static Material CreateEmissiveMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2.2f);
            return material;
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }
    }
}
