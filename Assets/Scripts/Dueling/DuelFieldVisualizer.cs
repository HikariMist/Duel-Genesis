using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelFieldVisualizer : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Color PlayerColor = new Color(0.08f, 0.78f, 1f, 1f);
        private static readonly Color CpuColor = new Color(1.00f, 0.16f, 0.58f, 1f);

        private DuelPrototype _duel;
        private Transform _table;
        private Transform _playerRoot;
        private Transform _cpuRoot;
        private FieldInfo _playerFieldInfo;
        private FieldInfo _opponentFieldInfo;
        private string _playerSignature = string.Empty;
        private string _cpuSignature = string.Empty;
        private float _nextSync;

        private void Start()
        {
            Resolve();
            EnsureRoots();
        }

        private void Update()
        {
            Resolve();
            EnsureRoots();

            if (_duel == null || _table == null || Time.unscaledTime < _nextSync)
                return;

            _nextSync = Time.unscaledTime + 0.15f;

            if (!_duel.IsActive)
            {
                ClearRoot(_playerRoot);
                ClearRoot(_cpuRoot);
                _playerSignature = string.Empty;
                _cpuSignature = string.Empty;
                return;
            }

            SyncSide(true);
            SyncSide(false);
        }

        private void Resolve()
        {
            if (_duel == null)
            {
                _duel = Object.FindFirstObjectByType<DuelPrototype>();
                if (_duel != null)
                {
                    Type type = typeof(DuelPrototype);
                    _playerFieldInfo = type.GetField("_playerField", PrivateInstance);
                    _opponentFieldInfo = type.GetField("_opponentField", PrivateInstance);
                }
            }

            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }
        }

        private void EnsureRoots()
        {
            if (_table == null) return;

            if (_playerRoot == null)
                _playerRoot = FindOrCreateRoot("DG Live Player Monsters");
            if (_cpuRoot == null)
                _cpuRoot = FindOrCreateRoot("DG Live CPU Monsters");
        }

        private Transform FindOrCreateRoot(string name)
        {
            Transform existing = _table.Find(name);
            if (existing != null) return existing;

            GameObject root = new GameObject(name);
            root.transform.SetParent(_table, false);
            return root.transform;
        }

        private void SyncSide(bool playerSide)
        {
            FieldInfo info = playerSide ? _playerFieldInfo : _opponentFieldInfo;
            Transform root = playerSide ? _playerRoot : _cpuRoot;
            if (info == null || root == null) return;

            IEnumerable field = info.GetValue(_duel) as IEnumerable;
            if (field == null) return;

            List<VisualCardState> states = ReadStates(field);
            string signature = BuildSignature(states);
            string current = playerSide ? _playerSignature : _cpuSignature;
            if (signature == current) return;

            ClearRoot(root);
            BuildSide(root, states, playerSide);

            if (playerSide) _playerSignature = signature;
            else _cpuSignature = signature;
        }

        private static List<VisualCardState> ReadStates(IEnumerable field)
        {
            List<VisualCardState> states = new();

            foreach (object entry in field)
            {
                if (entry == null) continue;
                Type entryType = entry.GetType();
                CardData card = entryType.GetField("Card")?.GetValue(entry) as CardData;
                object position = entryType.GetField("Position")?.GetValue(entry);
                if (card == null) continue;

                states.Add(new VisualCardState
                {
                    Card = card,
                    Position = position?.ToString() ?? "FaceUpAttack"
                });
            }

            return states;
        }

        private static string BuildSignature(List<VisualCardState> states)
        {
            if (states == null || states.Count == 0) return "EMPTY";
            System.Text.StringBuilder builder = new();
            foreach (VisualCardState state in states)
                builder.Append(state.Card.id).Append(':').Append(state.Position).Append('|');
            return builder.ToString();
        }

        private void BuildSide(Transform root, List<VisualCardState> states, bool playerSide)
        {
            const float startX = -1.68f;
            const float spacing = 0.84f;
            float z = playerSide ? -0.34f : 0.34f;
            Color color = playerSide ? PlayerColor : CpuColor;

            for (int i = 0; i < states.Count && i < 5; i++)
            {
                VisualCardState state = states[i];
                Vector3 slotPosition = new Vector3(startX + spacing * i, 1.50f, z);
                BuildSlotVisual(root, state, slotPosition, color, playerSide);
            }
        }

        private void BuildSlotVisual(Transform root, VisualCardState state, Vector3 localPosition, Color color, bool playerSide)
        {
            GameObject actor = new GameObject($"Hologram {state.Card.id}");
            actor.transform.SetParent(root, false);
            actor.transform.localPosition = localPosition;

            bool faceDown = state.Position.Contains("FaceDown", StringComparison.OrdinalIgnoreCase);
            bool defense = state.Position.Contains("Defense", StringComparison.OrdinalIgnoreCase);

            CreatePedestal(actor.transform, color);

            if (faceDown)
            {
                GameObject cardBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cardBack.name = "Face Down Card";
                cardBack.transform.SetParent(actor.transform, false);
                cardBack.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                cardBack.transform.localScale = new Vector3(0.38f, 0.035f, 0.54f);
                RemoveCollider(cardBack);
                SetMaterial(cardBack, new Color(0.05f, 0.06f, 0.12f, 1f), true);
                actor.AddComponent<HologramBob>().Configure(false, defense, playerSide);
                return;
            }

            GameObject prefab = CardModelRegistry.LoadPrefab(state.Card.id);
            if (prefab != null)
            {
                GameObject model = Object.Instantiate(prefab, actor.transform);
                model.name = "Monster Model - " + state.Card.cardName;
                NormalizeModel(model.transform, 0.52f);
                model.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            }
            else
            {
                BuildFallbackMonster(actor.transform, state.Card, color);
            }

            CreateLabel(actor.transform, state.Card, color, defense);
            actor.AddComponent<HologramBob>().Configure(true, defense, playerSide);
        }

        private static void CreatePedestal(Transform parent, Color color)
        {
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Hologram Pedestal";
            pedestal.transform.SetParent(parent, false);
            pedestal.transform.localScale = new Vector3(0.30f, 0.018f, 0.30f);
            RemoveCollider(pedestal);
            SetMaterial(pedestal, color, true);
        }

        private static void BuildFallbackMonster(Transform parent, CardData card, Color color)
        {
            PrimitiveType shape = card.typeLine.Contains("Dragon") ? PrimitiveType.Capsule :
                                  card.typeLine.Contains("Machine") ? PrimitiveType.Cube :
                                  card.typeLine.Contains("Warrior") ? PrimitiveType.Capsule :
                                  PrimitiveType.Sphere;

            GameObject proxy = GameObject.CreatePrimitive(shape);
            proxy.name = "Prototype Monster Proxy";
            proxy.transform.SetParent(parent, false);
            proxy.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            proxy.transform.localScale = shape == PrimitiveType.Cube
                ? new Vector3(0.28f, 0.40f, 0.28f)
                : new Vector3(0.28f, 0.40f, 0.28f);
            RemoveCollider(proxy);
            SetMaterial(proxy, color, true);
        }

        private static void CreateLabel(Transform parent, CardData card, Color color, bool defense)
        {
            GameObject labelObject = new GameObject("Monster Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.70f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = defense
                ? $"{card.cardName}\nDEF {card.defense}"
                : $"{card.cardName}\nATK {card.attack}";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.06f;
            text.fontSize = 48;
            text.color = color;
        }

        private static void NormalizeModel(Transform model, float targetSize)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                model.localScale = Vector3.one * 0.25f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest <= 0.001f) return;

            float scale = targetSize / largest;
            model.localScale = model.localScale * scale;
        }

        private static void SetMaterial(GameObject obj, Color color, bool emission)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2f);
            }
            renderer.material = material;
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        private static void ClearRoot(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.Destroy(root.GetChild(i).gameObject);
        }

        private sealed class VisualCardState
        {
            public CardData Card;
            public string Position;
        }
    }

    public class HologramBob : MonoBehaviour
    {
        private Vector3 _basePosition;
        private bool _rotate;
        private bool _defense;
        private float _direction = 1f;

        public void Configure(bool rotate, bool defense, bool playerSide)
        {
            _rotate = rotate;
            _defense = defense;
            _direction = playerSide ? 1f : -1f;
            _basePosition = transform.localPosition;
            if (_defense)
                transform.localRotation = Quaternion.Euler(0f, 0f, 12f * _direction);
        }

        private void Start()
        {
            _basePosition = transform.localPosition;
        }

        private void Update()
        {
            Vector3 position = _basePosition;
            position.y += Mathf.Sin(Time.time * 2.4f + transform.GetSiblingIndex()) * 0.025f;
            transform.localPosition = position;

            if (_rotate && !_defense)
                transform.Rotate(Vector3.up, 18f * _direction * Time.deltaTime, Space.Self);
        }
    }
}
