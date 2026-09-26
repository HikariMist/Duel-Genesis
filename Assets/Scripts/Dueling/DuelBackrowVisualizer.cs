using System.Collections.Generic;
using System.Text;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelBackrowVisualizer : MonoBehaviour
    {
        private DuelGameController _duel;
        private Transform _table;
        private Transform _playerRoot;
        private Transform _cpuRoot;
        private string _playerSignature = string.Empty;
        private string _cpuSignature = string.Empty;
        private float _nextSync;

        private static readonly Color SpellColor = new Color(0.10f, 0.80f, 0.52f, 1f);
        private static readonly Color TrapColor = new Color(0.78f, 0.20f, 0.88f, 1f);
        private static readonly Color CardBack = new Color(0.045f, 0.055f, 0.12f, 1f);
        private static readonly Color NegatedColor = new Color(0.52f, 0.52f, 0.58f, 1f);

        private void Update()
        {
            Resolve();
            EnsureRoots();
            if (_duel == null || _table == null || Time.unscaledTime < _nextSync) return;

            _nextSync = Time.unscaledTime + 0.12f;
            if (!_duel.IsActive)
            {
                Clear(_playerRoot);
                Clear(_cpuRoot);
                _playerSignature = string.Empty;
                _cpuSignature = string.Empty;
                return;
            }

            Sync(true);
            Sync(false);
        }

        private void Resolve()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null) _table = tableObject.transform;
            }
        }

        private void EnsureRoots()
        {
            if (_table == null) return;
            if (_playerRoot == null) _playerRoot = FindOrCreate("DG Live Player Backrow");
            if (_cpuRoot == null) _cpuRoot = FindOrCreate("DG Live CPU Backrow");
        }

        private Transform FindOrCreate(string name)
        {
            Transform existing = _table.Find(name);
            if (existing != null) return existing;
            GameObject root = new GameObject(name);
            root.transform.SetParent(_table, false);
            return root.transform;
        }

        private void Sync(bool playerSide)
        {
            IReadOnlyList<DuelBackrowState> states = playerSide ? _duel.PlayerBackrow : _duel.CpuBackrow;
            Transform root = playerSide ? _playerRoot : _cpuRoot;
            if (root == null) return;

            string signature = Signature(states, _duel.TurnNumber);
            string old = playerSide ? _playerSignature : _cpuSignature;
            if (signature == old) return;

            Clear(root);
            Build(root, states, playerSide);
            if (playerSide) _playerSignature = signature;
            else _cpuSignature = signature;
        }

        private static string Signature(IReadOnlyList<DuelBackrowState> states, int turn)
        {
            if (states == null || states.Count == 0) return "EMPTY";
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < states.Count; i++)
            {
                DuelBackrowState state = states[i];
                builder.Append(state.Card.id)
                    .Append(state.FaceDown ? 'D' : 'U')
                    .Append(state.IsNegated(turn) ? 'N' : 'A')
                    .Append('|');
            }
            return builder.ToString();
        }

        private void Build(Transform root, IReadOnlyList<DuelBackrowState> states, bool playerSide)
        {
            const float startX = -1.68f;
            const float spacing = 0.84f;
            float z = playerSide ? -0.91f : 0.91f;

            for (int i = 0; i < states.Count && i < 5; i++)
            {
                DuelBackrowState state = states[i];
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
                card.name = "Backrow " + state.Card.id;
                card.transform.SetParent(root, false);
                card.transform.localPosition = new Vector3(startX + spacing * i, 1.405f, z);
                card.transform.localScale = new Vector3(0.44f, 0.025f, 0.31f);
                RemoveCollider(card);

                Color color = state.FaceDown
                    ? CardBack
                    : state.IsNegated(_duel.TurnNumber)
                        ? NegatedColor
                        : state.Card.kind == CardKind.Spell ? SpellColor : TrapColor;
                SetMaterial(card, color);

                bool hideName = !playerSide && state.FaceDown;
                if (!hideName)
                    CreateLabel(card.transform, state.Card.cardName, color, state.IsNegated(_duel.TurnNumber));
            }
        }

        private static void CreateLabel(Transform parent, string label, Color color, bool negated)
        {
            GameObject labelObject = new GameObject("Backrow Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = label + (negated ? "\nNEGATED" : string.Empty);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.12f;
            text.fontSize = 40;
            text.color = color;
        }

        private static void SetMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.5f);
            renderer.material = material;
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        private static void Clear(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.Destroy(root.GetChild(i).gameObject);
        }
    }
}
