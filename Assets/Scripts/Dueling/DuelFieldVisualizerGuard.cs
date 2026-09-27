using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Ensures exactly one DuelFieldVisualizer stays active. Older scene copies can be
    /// serialized disabled, which caused both player and CPU monsters to disappear.
    /// </summary>
    [DefaultExecutionOrder(6300)]
    public sealed class DuelFieldVisualizerGuard : MonoBehaviour
    {
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            DuelFieldVisualizerGuard existing = Object.FindFirstObjectByType<DuelFieldVisualizerGuard>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.enabled = true;
                return;
            }

            GameObject host = new GameObject("Duel Field Visualizer Guard");
            host.AddComponent<DuelFieldVisualizerGuard>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck)
                return;

            _nextCheck = Time.unscaledTime + 0.25f;
            EnsureVisualizer();
        }

        private static void EnsureVisualizer()
        {
            DuelFieldVisualizer[] visualizers = Object.FindObjectsByType<DuelFieldVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (visualizers.Length == 0)
            {
                GameObject host = new GameObject("Duel Field Visualizer");
                host.AddComponent<DuelFieldVisualizer>();
                return;
            }

            DuelFieldVisualizer primary = visualizers[0];
            if (!primary.gameObject.activeSelf)
                primary.gameObject.SetActive(true);
            if (!primary.enabled)
                primary.enabled = true;

            // Multiple visualizers would duplicate holograms. Keep one authoritative copy.
            for (int i = 1; i < visualizers.Length; i++)
            {
                DuelFieldVisualizer duplicate = visualizers[i];
                if (duplicate != null && duplicate.enabled)
                    duplicate.enabled = false;
            }
        }
    }
}
