using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Keeps the futuristic duel hardware as an arena DEVICE sitting on the existing
    /// card-shop table. The previous pass accidentally made the arena larger than the
    /// furniture and gave it its own legs. This scales the duel hardware footprint down
    /// while preserving its tabletop height and hides the generated furniture supports.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    public sealed class DuelArenaScaleController : MonoBehaviour
    {
        public const float ArenaFootprintScale = 0.50f;

        private Transform _table;
        private float _nextApply;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelArenaScaleController>() != null)
                return;

            GameObject host = new GameObject("Duel Arena Scale Controller");
            host.AddComponent<DuelArenaScaleController>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextApply)
                return;

            _nextApply = Time.unscaledTime + 0.10f;
            ResolveTable();
            if (_table == null)
                return;

            ScaleArenaRoots();
            HideGeneratedFurnitureSupports();
        }

        private void ResolveTable()
        {
            if (_table != null)
                return;

            GameObject tableObject = GameObject.Find("Duel Table Prototype");
            if (tableObject != null)
                _table = tableObject.transform;
        }

        private void ScaleArenaRoots()
        {
            for (int i = 0; i < _table.childCount; i++)
            {
                Transform child = _table.GetChild(i);
                if (child == null || !child.name.StartsWith("DG "))
                    continue;

                // Player hand is screen-space and the CPU hand has its own presentation
                // sizing, so neither should be changed by the arena footprint scale.
                if (child.name.Contains("Hand"))
                    continue;

                Vector3 scale = child.localScale;
                child.localScale = new Vector3(ArenaFootprintScale, scale.y, ArenaFootprintScale);
            }
        }

        private void HideGeneratedFurnitureSupports()
        {
            Transform solidArena = _table.Find("DG Solid Duel Table Model");
            if (solidArena == null)
                return;

            Transform[] descendants = solidArena.GetComponentsInChildren<Transform>(true);
            foreach (Transform part in descendants)
            {
                if (part == null || part == solidArena)
                    continue;

                string partName = part.name;
                bool furnitureSupport =
                    partName.Contains(" Leg") ||
                    partName.Contains(" Foot") ||
                    partName.Contains("Underframe") ||
                    partName.Contains("Brace") ||
                    partName.Contains("Vertical Light");

                if (furnitureSupport && part.gameObject.activeSelf)
                    part.gameObject.SetActive(false);
            }
        }
    }
}
