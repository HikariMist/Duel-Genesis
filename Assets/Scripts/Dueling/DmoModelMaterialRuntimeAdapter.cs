using System.Collections.Generic;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DmoModelMaterialRuntimeAdapter : MonoBehaviour
    {
        private readonly HashSet<int> _processedRenderers = new();
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<DmoModelMaterialRuntimeAdapter>() != null)
                return;

            GameObject host = new GameObject("DMO Material Runtime Adapter");
            host.AddComponent<DmoModelMaterialRuntimeAdapter>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan)
                return;

            _nextScan = Time.unscaledTime + 0.35f;
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || _processedRenderers.Contains(renderer.GetInstanceID()))
                    continue;
                if (!IsDmoMonsterRenderer(renderer.transform))
                    continue;

                AdaptRenderer(renderer);
                _processedRenderers.Add(renderer.GetInstanceID());
            }
        }

        private static bool IsDmoMonsterRenderer(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name.StartsWith("Monster Model - ", System.StringComparison.Ordinal))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static void AdaptRenderer(Renderer renderer)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            Material[] materials = renderer.materials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || source.shader == null)
                    continue;
                if (source.shader.name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
                    continue;

                Texture mainTexture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;

                Material converted = new Material(urpLit)
                {
                    name = source.name + " (DG URP Runtime)"
                };

                if (converted.HasProperty("_BaseMap") && mainTexture != null)
                    converted.SetTexture("_BaseMap", mainTexture);
                if (converted.HasProperty("_BaseColor"))
                    converted.SetColor("_BaseColor", color);
                if (converted.HasProperty("_Smoothness"))
                    converted.SetFloat("_Smoothness", 0.15f);

                materials[i] = converted;
                changed = true;
            }

            if (changed)
                renderer.materials = materials;
        }
    }
}
