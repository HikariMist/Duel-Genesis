using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Removes the old prototype presentation that conflicts with the physical duel arena:
    /// giant floating TextMesh labels, TextMeshPro labels, and the legacy hologram FX root.
    /// </summary>
    [DefaultExecutionOrder(6000)]
    public sealed class DuelTableLegacyCleanup : MonoBehaviour
    {
        private Transform _table;
        private float _nextCleanup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelTableLegacyCleanup>() != null)
                return;

            GameObject host = new GameObject("Duel Table Legacy Cleanup");
            host.AddComponent<DuelTableLegacyCleanup>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCleanup)
                return;

            _nextCleanup = Time.unscaledTime + 0.12f;
            ResolveTable();
            if (_table == null)
                return;

            HideLegacyText();
            DisableLegacyArenaFx();
        }

        private void ResolveTable()
        {
            if (_table != null)
                return;

            GameObject tableObject = GameObject.Find("Duel Table Prototype");
            if (tableObject != null)
                _table = tableObject.transform;
        }

        private void HideLegacyText()
        {
            TextMesh[] textMeshes = _table.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                TextMesh text = textMeshes[i];
                if (text != null && text.gameObject.activeSelf)
                    text.gameObject.SetActive(false);
            }

            // Avoid a direct TMPro package dependency while still shutting off any old
            // TextMeshPro labels that may be serialized on the prototype table.
            Behaviour[] behaviours = _table.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null) continue;
                string typeName = behaviour.GetType().Name;
                if ((typeName.Contains("TextMeshPro") || typeName == "TMP_Text") && behaviour.enabled)
                    behaviour.enabled = false;
            }
        }

        private void DisableLegacyArenaFx()
        {
            DuelArenaFX[] fxComponents = Object.FindObjectsByType<DuelArenaFX>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < fxComponents.Length; i++)
                if (fxComponents[i] != null) fxComponents[i].enabled = false;

            Transform oldFx = _table.Find("DG Duel Arena FX");
            if (oldFx != null && oldFx.gameObject.activeSelf)
                oldFx.gameObject.SetActive(false);
        }
    }
}
