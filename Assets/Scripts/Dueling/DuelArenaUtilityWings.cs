using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Keeps only the small external banished-card pockets required by the physical table.
    /// Field, Extra Deck, Graveyard and Main Deck now live directly on the classic seven-column mat.
    /// </summary>
    [DefaultExecutionOrder(5200)]
    public sealed class DuelArenaUtilityWings : MonoBehaviour
    {
        private const string HardwareRootName = "DG Arena Utility Wings";

        private static readonly Color DarkFrame = new Color(0.022f, 0.030f, 0.045f, 1f);
        private static readonly Color SlotDark = new Color(0.035f, 0.050f, 0.068f, 1f);
        private static readonly Color BanishEdge = new Color(0.34f, 0.42f, 0.58f, 1f);

        private Transform _table;
        private Transform _hardwareRoot;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            DuelArenaUtilityWings existing = Object.FindFirstObjectByType<DuelArenaUtilityWings>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.enabled = true;
                return;
            }

            GameObject host = new GameObject("Duel Arena Utility Wings");
            host.AddComponent<DuelArenaUtilityWings>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolve)
                return;

            _nextResolve = Time.unscaledTime + 0.20f;
            Resolve();
            if (_table == null)
                return;

            EnsureHardware();
        }

        private void Resolve()
        {
            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_table != null && _hardwareRoot == null)
                _hardwareRoot = _table.Find(HardwareRootName);
        }

        private void EnsureHardware()
        {
            if (_hardwareRoot != null)
                return;

            GameObject root = new GameObject(HardwareRootName);
            root.transform.SetParent(_table, false);
            _hardwareRoot = root.transform;

            BuildBanishPocket("Player Banished Pocket", DuelTabletopLayout.BanishedPosition(true));
            BuildBanishPocket("CPU Banished Pocket", DuelTabletopLayout.BanishedPosition(false));
        }

        private void BuildBanishPocket(string name, Vector3 position)
        {
            position.y = DuelTabletopLayout.BoardSurfaceY + 0.015f;
            float towardBoard = position.x > 0f ? -1f : 1f;

            CreateBlock(
                name + " Connector",
                position + new Vector3(towardBoard * 0.48f, -0.035f, 0f),
                new Vector3(0.72f, 0.10f, 1.08f),
                DarkFrame,
                true,
                false);

            CreateBlock(
                name + " Base",
                position,
                new Vector3(1.02f, 0.10f, 1.26f),
                DarkFrame,
                true,
                false);

            CreateBlock(
                name + " Inset",
                position + Vector3.up * 0.058f,
                new Vector3(0.84f, 0.030f, 1.06f),
                SlotDark,
                false,
                false);

            CreateBlock(
                name + " Edge",
                position + new Vector3(0f, 0.081f, -0.50f),
                new Vector3(0.70f, 0.018f, 0.035f),
                BanishEdge,
                false,
                true);
        }

        private void CreateBlock(string name, Vector3 position, Vector3 scale, Color color, bool collider, bool emission)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(_hardwareRoot, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;

            if (!collider)
            {
                Collider existing = obj.GetComponent<Collider>();
                if (existing != null)
                    Object.Destroy(existing);
            }

            SetMaterial(obj, color, emission);
        }

        private static void SetMaterial(GameObject obj, Color color, bool emission)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                return;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);

            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color * 0.65f);
            }

            renderer.material = material;
        }
    }
}
