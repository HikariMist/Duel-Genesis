using System.Collections.Generic;
using System.Reflection;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Renders the CPU's hand as face-down physical cards on the far edge of the table.
    /// The card identities remain hidden; only the real hand count is represented.
    /// </summary>
    public sealed class DuelOpponentHandVisualizer : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private DuelGameController _duel;
        private Transform _table;
        private Transform _root;
        private FieldInfo _cpuHandField;
        private int _lastCount = -1;
        private float _nextSync;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelOpponentHandVisualizer>() != null)
                return;

            GameObject host = new GameObject("Duel Opponent Hand Visualizer");
            host.AddComponent<DuelOpponentHandVisualizer>();
        }

        private void Awake()
        {
            _cpuHandField = typeof(DuelGameController).GetField("_cpuHand", PrivateInstance);
        }

        private void Update()
        {
            Resolve();
            EnsureRoot();
            if (_duel == null || _root == null || Time.unscaledTime < _nextSync)
                return;

            _nextSync = Time.unscaledTime + 0.12f;
            if (!_duel.IsActive)
            {
                if (_lastCount != -1)
                {
                    ClearChildren();
                    _lastCount = -1;
                }
                _root.gameObject.SetActive(false);
                return;
            }

            _root.gameObject.SetActive(true);
            List<CardData> hand = _cpuHandField?.GetValue(_duel) as List<CardData>;
            int count = hand?.Count ?? 0;
            if (count == _lastCount)
                return;

            _lastCount = count;
            Rebuild(count);
        }

        private void Resolve()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }
        }

        private void EnsureRoot()
        {
            if (_table == null || _root != null)
                return;

            Transform existing = _table.Find("DG Physical CPU Hand");
            if (existing != null)
                Object.Destroy(existing.gameObject);

            GameObject rootObject = new GameObject("DG Physical CPU Hand");
            rootObject.transform.SetParent(_table, false);
            _root = rootObject.transform;
            _root.gameObject.SetActive(false);
        }

        private void Rebuild(int count)
        {
            ClearChildren();
            if (count <= 0)
                return;

            Texture2D cardBack = ProductionCardArtRegistry.LoadCardBack();
            float spacing = Mathf.Min(0.42f, 2.9f / Mathf.Max(1, count - 1));
            float startX = -spacing * (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = "CPU Hand Card " + (i + 1);
                card.transform.SetParent(_root, false);
                card.transform.localPosition = new Vector3(
                    startX + spacing * i,
                    DuelTabletopLayout.BoardSurfaceY + 0.10f + i * 0.001f,
                    1.63f);
                card.transform.localRotation = Quaternion.Euler(-102f, 180f, 0f);
                card.transform.localScale = DuelTabletopLayout.CardScale * 0.72f;

                Collider collider = card.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);

                ApplyCardBackMaterial(card, cardBack);
            }
        }

        private static void ApplyCardBackMaterial(GameObject obj, Texture2D texture)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            }
            renderer.material = material;
        }

        private void ClearChildren()
        {
            if (_root == null) return;
            for (int i = _root.childCount - 1; i >= 0; i--)
                Object.Destroy(_root.GetChild(i).gameObject);
        }
    }
}
