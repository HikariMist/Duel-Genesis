using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Final layout/presentation cleanup for the physical duel table.
    /// Keeps the mat visually simple, adds a real tabletop frame, and positions
    /// both hands where the duel camera can actually see them.
    /// </summary>
    public sealed class DuelTabletopPolish : MonoBehaviour
    {
        private Transform _table;
        private Transform _tabletop;
        private bool _surfaceBuilt;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelTabletopPolish>() != null)
                return;

            GameObject host = new GameObject("Duel Tabletop Polish");
            host.AddComponent<DuelTabletopPolish>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolve)
                return;

            _nextResolve = Time.unscaledTime + 0.15f;
            Resolve();
            if (_tabletop == null)
                return;

            HideZoneWords();
            EnsureTableModel();
            PositionHands();
        }

        private void Resolve()
        {
            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_table != null && _tabletop == null)
                _tabletop = _table.Find("DG Physical Tabletop");
        }

        private void HideZoneWords()
        {
            TextMesh[] textMeshes = _tabletop.GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh text in textMeshes)
            {
                if (text != null && text.gameObject.name == "Zone Label")
                    text.gameObject.SetActive(false);
            }
        }

        private void EnsureTableModel()
        {
            if (_surfaceBuilt)
                return;

            Transform existing = _tabletop.Find("DG Table Frame");
            if (existing != null)
            {
                _surfaceBuilt = true;
                return;
            }

            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "DG Table Frame";
            frame.transform.SetParent(_tabletop, false);
            frame.transform.localPosition = new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.09f, 0f);
            frame.transform.localScale = new Vector3(
                DuelTabletopLayout.BoardScale.x + 0.30f,
                0.14f,
                DuelTabletopLayout.BoardScale.z + 0.30f);
            RemoveCollider(frame);
            SetMaterial(frame, new Color(0.055f, 0.048f, 0.060f, 1f));

            CreateRail("Near Rail", new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.025f, -2.58f), new Vector3(6.65f, 0.10f, 0.10f));
            CreateRail("Far Rail", new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.025f, 2.58f), new Vector3(6.65f, 0.10f, 0.10f));
            CreateRail("Left Rail", new Vector3(-3.28f, DuelTabletopLayout.BoardSurfaceY + 0.025f, 0f), new Vector3(0.10f, 0.10f, 5.08f));
            CreateRail("Right Rail", new Vector3(3.28f, DuelTabletopLayout.BoardSurfaceY + 0.025f, 0f), new Vector3(0.10f, 0.10f, 5.08f));

            _surfaceBuilt = true;
        }

        private void CreateRail(string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = name;
            rail.transform.SetParent(_tabletop, false);
            rail.transform.localPosition = localPosition;
            rail.transform.localScale = localScale;
            RemoveCollider(rail);
            SetMaterial(rail, new Color(0.12f, 0.12f, 0.15f, 1f));
        }

        private void PositionHands()
        {
            if (_table == null)
                return;

            Transform playerHand = _table.Find("DG Physical Player Hand");
            if (playerHand != null)
            {
                playerHand.localPosition = new Vector3(0f, 0.30f, -0.48f);
                playerHand.localScale = Vector3.one * 1.10f;
            }

            Transform cpuHand = _table.Find("DG Physical CPU Hand");
            if (cpuHand != null)
            {
                cpuHand.localPosition = new Vector3(0f, 0.18f, 0.42f);
                cpuHand.localScale = Vector3.one;
            }
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }

        private static void SetMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            renderer.material = material;
        }
    }
}
