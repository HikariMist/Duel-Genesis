using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Final layout/presentation cleanup for the physical duel table.
    /// Keeps the mat visually simple and scales the table frame with the active board layout.
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
                Object.Destroy(existing.gameObject);

            float boardWidth = DuelTabletopLayout.BoardScale.x;
            float boardDepth = DuelTabletopLayout.BoardScale.z;
            float railOffsetX = boardWidth * 0.5f + 0.08f;
            float railOffsetZ = boardDepth * 0.5f + 0.08f;

            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "DG Table Frame";
            frame.transform.SetParent(_tabletop, false);
            frame.transform.localPosition = new Vector3(0f, DuelTabletopLayout.BoardSurfaceY - 0.10f, 0f);
            frame.transform.localScale = new Vector3(boardWidth + 0.36f, 0.16f, boardDepth + 0.36f);
            RemoveCollider(frame);
            SetMaterial(frame, new Color(0.055f, 0.048f, 0.060f, 1f));

            CreateRail("Near Rail",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.028f, -railOffsetZ),
                new Vector3(boardWidth + 0.34f, 0.11f, 0.12f));
            CreateRail("Far Rail",
                new Vector3(0f, DuelTabletopLayout.BoardSurfaceY + 0.028f, railOffsetZ),
                new Vector3(boardWidth + 0.34f, 0.11f, 0.12f));
            CreateRail("Left Rail",
                new Vector3(-railOffsetX, DuelTabletopLayout.BoardSurfaceY + 0.028f, 0f),
                new Vector3(0.12f, 0.11f, boardDepth + 0.34f));
            CreateRail("Right Rail",
                new Vector3(railOffsetX, DuelTabletopLayout.BoardSurfaceY + 0.028f, 0f),
                new Vector3(0.12f, 0.11f, boardDepth + 0.34f));

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

            // Player hand is rendered in screen-space now; keep the old 3D hand hidden.
            Transform playerHand = _table.Find("DG Physical Player Hand");
            if (playerHand != null)
                playerHand.gameObject.SetActive(false);

            Transform cpuHand = _table.Find("DG Physical CPU Hand");
            if (cpuHand != null)
            {
                cpuHand.localPosition = new Vector3(0f, 0.18f, 1.00f);
                cpuHand.localScale = Vector3.one * 1.08f;
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
