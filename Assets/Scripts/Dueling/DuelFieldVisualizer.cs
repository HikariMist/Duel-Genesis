using System.Collections.Generic;
using System.Text;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelFieldVisualizer : MonoBehaviour
    {
        private static readonly Color PlayerColor = new Color(0.08f, 0.78f, 1f, 1f);
        private static readonly Color CpuColor = new Color(1.00f, 0.16f, 0.58f, 1f);
        private static readonly Color NegatedColor = new Color(0.52f, 0.52f, 0.58f, 1f);

        private DuelGameController _duel;
        private Transform _table;
        private Transform _playerRoot;
        private Transform _cpuRoot;
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

            _nextSync = Time.unscaledTime + 0.12f;
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
                _duel = Object.FindFirstObjectByType<DuelGameController>();

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
            if (_playerRoot == null) _playerRoot = FindOrCreateRoot("DG Live Player Monsters");
            if (_cpuRoot == null) _cpuRoot = FindOrCreateRoot("DG Live CPU Monsters");
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
            IReadOnlyList<DuelMonsterState> states = playerSide ? _duel.PlayerMonsters : _duel.CpuMonsters;
            Transform root = playerSide ? _playerRoot : _cpuRoot;
            if (root == null) return;

            string signature = BuildSignature(states, _duel.TurnNumber);
            string old = playerSide ? _playerSignature : _cpuSignature;
            if (signature == old) return;

            ClearRoot(root);
            BuildSide(root, states, playerSide);
            if (playerSide) _playerSignature = signature;
            else _cpuSignature = signature;
        }

        private static string BuildSignature(IReadOnlyList<DuelMonsterState> states, int turn)
        {
            if (states == null || states.Count == 0) return "EMPTY";
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < states.Count; i++)
            {
                DuelMonsterState state = states[i];
                builder.Append(state.Card.id).Append(':')
                    .Append((int)state.Position).Append(':')
                    .Append(state.AttackBonus).Append(':')
                    .Append(state.IsNegated(turn) ? 'N' : 'A').Append('|');
            }
            return builder.ToString();
        }

        private void BuildSide(Transform root, IReadOnlyList<DuelMonsterState> states, bool playerSide)
        {
            const float startX = -1.68f;
            const float spacing = 0.84f;
            float z = playerSide ? -0.34f : 0.34f;
            Color sideColor = playerSide ? PlayerColor : CpuColor;

            for (int i = 0; i < states.Count && i < 5; i++)
            {
                DuelMonsterState state = states[i];
                GameObject actor = new GameObject("Hologram " + state.Card.id);
                actor.transform.SetParent(root, false);
                actor.transform.localPosition = new Vector3(startX + spacing * i, 1.50f, z);

                bool negated = state.IsNegated(_duel.TurnNumber);
                Color color = negated ? NegatedColor : sideColor;
                CreatePedestal(actor.transform, color);

                if (state.IsFaceDown)
                {
                    BuildFaceDownCard(actor.transform, playerSide);
                    actor.AddComponent<HologramBob>().Configure(false, true, playerSide);
                    continue;
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

                CreateLabel(actor.transform, state, color, negated);
                actor.AddComponent<HologramBob>().Configure(true, state.Position == DuelMonsterPosition.FaceUpDefense, playerSide);
            }
        }

        private static void BuildFaceDownCard(Transform parent, bool playerSide)
        {
            GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
            card.name = "Face Down Monster Card";
            card.transform.SetParent(parent, false);
            card.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            card.transform.localScale = new Vector3(0.38f, 0.035f, 0.54f);
            RemoveCollider(card);
            SetMaterial(card, new Color(0.035f, 0.045f, 0.10f, 1f), true);

            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Card Back Accent";
            stripe.transform.SetParent(parent, false);
            stripe.transform.localPosition = new Vector3(0f, 0.056f, 0f);
            stripe.transform.localScale = new Vector3(0.28f, 0.012f, 0.05f);
            RemoveCollider(stripe);
            SetMaterial(stripe, playerSide ? PlayerColor : CpuColor, true);
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
                                  card.typeLine.Contains("Spellcaster") ? PrimitiveType.Capsule :
                                  PrimitiveType.Sphere;

            GameObject proxy = GameObject.CreatePrimitive(shape);
            proxy.name = "Prototype Monster Proxy";
            proxy.transform.SetParent(parent, false);
            proxy.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            proxy.transform.localScale = new Vector3(0.28f, 0.40f, 0.28f);
            RemoveCollider(proxy);
            SetMaterial(proxy, color, true);
        }

        private static void CreateLabel(Transform parent, DuelMonsterState state, Color color, bool negated)
        {
            GameObject labelObject = new GameObject("Monster Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

            TextMesh text = labelObject.AddComponent<TextMesh>();
            int attack = Mathf.Max(0, state.Card.attack + state.AttackBonus);
            string stat = state.Position == DuelMonsterPosition.FaceUpDefense
                ? "DEF " + state.Card.defense
                : "ATK " + attack;
            text.text = state.Card.cardName + "\n" + stat + (negated ? "\nNEGATED" : string.Empty);
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
            model.localScale *= targetSize / largest;
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
