using System;
using System.Reflection;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Removes obsolete prototype presentation that conflicts with the classic physical field:
    /// giant floating shop/table labels, old arena FX, old pile visualizers, legacy raised Deck/GY
    /// housings, and modern Extra Monster pads that are not part of the requested reference mat.
    /// </summary>
    [DefaultExecutionOrder(6500)]
    public sealed class DuelTableLegacyCleanup : MonoBehaviour
    {
        private Transform _table;
        private float _nextCleanup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            DuelTableLegacyCleanup existing = Object.FindFirstObjectByType<DuelTableLegacyCleanup>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.enabled = true;
                return;
            }

            GameObject host = new GameObject("Duel Table Legacy Cleanup");
            host.AddComponent<DuelTableLegacyCleanup>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCleanup)
                return;

            _nextCleanup = Time.unscaledTime + 0.15f;
            ResolveTable();
            HideNamedFloatingTextAcrossScene();

            if (_table == null)
                return;

            HideAllTableText();
            DisableLegacyArenaFx();
            DisableLegacyPileScripts();
            HideObsoleteHardware();
        }

        private void ResolveTable()
        {
            if (_table != null)
                return;

            GameObject tableObject = GameObject.Find("Duel Table Prototype");
            if (tableObject != null)
                _table = tableObject.transform;
        }

        private static bool IsAnnoyingLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string text = value.Trim().ToLowerInvariant();
            return text.Contains("genesis card shop") ||
                   text.Contains("neon duel table") ||
                   text.Contains("prototype duel") ||
                   text == "duel table prototype";
        }

        private void HideNamedFloatingTextAcrossScene()
        {
            TextMesh[] meshes = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < meshes.Length; i++)
            {
                TextMesh mesh = meshes[i];
                if (mesh != null && IsAnnoyingLabel(mesh.text))
                    mesh.gameObject.SetActive(false);
            }

            // Handle TextMeshPro without taking a direct package dependency.
            Behaviour[] behaviours = Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                Type type = behaviour.GetType();
                string typeName = type.Name;
                if (!typeName.Contains("TextMeshPro") && typeName != "TMP_Text")
                    continue;

                PropertyInfo textProperty = type.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (textProperty == null || textProperty.PropertyType != typeof(string))
                    continue;

                string value = textProperty.GetValue(behaviour) as string;
                if (IsAnnoyingLabel(value))
                    behaviour.gameObject.SetActive(false);
            }
        }

        private void HideAllTableText()
        {
            TextMesh[] textMeshes = _table.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                TextMesh text = textMeshes[i];
                if (text != null && text.gameObject.activeSelf)
                    text.gameObject.SetActive(false);
            }

            Behaviour[] behaviours = _table.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

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
            if (oldFx != null)
                oldFx.gameObject.SetActive(false);
        }

        private static void DisableLegacyPileScripts()
        {
            DuelPileVisualizer[] piles = Object.FindObjectsByType<DuelPileVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < piles.Length; i++)
                if (piles[i] != null) piles[i].enabled = false;

            DuelBackrowVisualizer[] oldBackrow = Object.FindObjectsByType<DuelBackrowVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < oldBackrow.Length; i++)
                if (oldBackrow[i] != null) oldBackrow[i].enabled = false;
        }

        private void HideObsoleteHardware()
        {
            Transform solid = _table.Find("DG Solid Duel Table Model");
            if (solid != null)
            {
                HideDescendantByContains(solid, "Deck Housing");
                HideDescendantByContains(solid, "Grave Housing");
            }

            Transform physical = _table.Find("DG Physical Tabletop");
            if (physical != null)
            {
                HideDescendantByContains(physical, "ExtraMonster");
                HideDescendantByContains(physical, "Extra Monster");
            }
        }

        private static void HideDescendantByContains(Transform root, string fragment)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform child = descendants[i];
                if (child == null || child == root)
                    continue;
                if (child.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    child.gameObject.SetActive(false);
            }
        }
    }
}
