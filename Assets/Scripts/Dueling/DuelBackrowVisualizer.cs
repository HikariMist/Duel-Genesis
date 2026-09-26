using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DuelGenesis.Cards;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DuelGenesis.Dueling
{
    public class DuelBackrowVisualizer : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private DuelPrototype _duel;
        private Transform _table;
        private Transform _playerRoot;
        private Transform _cpuRoot;
        private FieldInfo _playerZonesInfo;
        private FieldInfo _cpuZonesInfo;
        private string _playerSignature = string.Empty;
        private string _cpuSignature = string.Empty;
        private float _nextSync;

        private static readonly Color SpellColor = new Color(0.10f, 0.80f, 0.52f, 1f);
        private static readonly Color TrapColor = new Color(0.78f, 0.20f, 0.88f, 1f);
        private static readonly Color CardBack = new Color(0.045f, 0.055f, 0.12f, 1f);

        private void Update()
        {
            Resolve();
            EnsureRoots();
            if (_duel == null || _table == null || Time.unscaledTime < _nextSync) return;

            _nextSync = Time.unscaledTime + 0.15f;
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
            {
                _duel = Object.FindFirstObjectByType<DuelPrototype>();
                if (_duel != null)
                {
                    Type type = typeof(DuelPrototype);
                    _playerZonesInfo = type.GetField("_playerSpellTrap", PrivateInstance);
                    _cpuZonesInfo = type.GetField("_opponentSpellTrap", PrivateInstance);
                }
            }

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
            FieldInfo info = playerSide ? _playerZonesInfo : _cpuZonesInfo;
            Transform root = playerSide ? _playerRoot : _cpuRoot;
            if (info == null || root == null) return;

            IEnumerable zones = info.GetValue(_duel) as IEnumerable;
            if (zones == null) return;

            List<BackrowState> states = Read(zones);
            string signature = Signature(states);
            string old = playerSide ? _playerSignature : _cpuSignature;
            if (signature == old) return;

            Clear(root);
            Build(root, states, playerSide);
            if (playerSide) _playerSignature = signature;
            else _cpuSignature = signature;
        }

        private static List<BackrowState> Read(IEnumerable zones)
        {
            List<BackrowState> result = new();
            foreach (object entry in zones)
            {
                if (entry == null) continue;
                Type type = entry.GetType();
                CardData card = type.GetField("Card")?.GetValue(entry) as CardData;
                object faceDownValue = type.GetField("FaceDown")?.GetValue(entry);
                if (card == null) continue;

                result.Add(new BackrowState
                {
                    Card = card,
                    FaceDown = faceDownValue is bool value && value
                });
            }
            return result;
        }

        private static string Signature(List<BackrowState> states)
        {
            if (states.Count == 0) return "EMPTY";
            System.Text.StringBuilder builder = new();
            foreach (BackrowState state in states)
                builder.Append(state.Card.id).Append(state.FaceDown ? 'D' : 'U').Append('|');
            return builder.ToString();
        }

        private static void Build(Transform root, List<BackrowState> states, bool playerSide)
        {
            const float startX = -1.68f;
            const float spacing = 0.84f;
            float z = playerSide ? -0.91f : 0.91f;

            for (int i = 0; i < states.Count && i < 5; i++)
            {
                BackrowState state = states[i];
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
                card.name = $"Backrow {state.Card.id}";
                card.transform.SetParent(root, false);
                card.transform.localPosition = new Vector3(startX + spacing * i, 1.405f, z);
                card.transform.localScale = new Vector3(0.44f, 0.025f, 0.31f);
                RemoveCollider(card);

                Color color = state.FaceDown
                    ? CardBack
                    : state.Card.kind == CardKind.Spell ? SpellColor : TrapColor;
                SetMaterial(card, color);

                bool hideName = !playerSide && state.FaceDown;
                if (!hideName)
                    CreateLabel(card.transform, state.Card.cardName, color);
            }
        }

        private static void CreateLabel(Transform parent, string label, Color color)
        {
            GameObject labelObject = new GameObject("Backrow Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = label;
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

        private sealed class BackrowState
        {
            public CardData Card;
            public bool FaceDown;
        }
    }
}
