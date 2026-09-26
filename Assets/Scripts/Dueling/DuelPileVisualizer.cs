using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelPileVisualizer : MonoBehaviour
    {
        private DuelGameController _duel;
        private Transform _table;
        private Transform _root;
        private string _signature = string.Empty;
        private float _nextSync;

        private static readonly Color PlayerColor = new Color(0.08f, 0.78f, 1f, 1f);
        private static readonly Color CpuColor = new Color(1f, 0.16f, 0.58f, 1f);
        private static readonly Color GraveColor = new Color(0.34f, 0.38f, 0.45f, 1f);
        private static readonly Color BanishedColor = new Color(0.72f, 0.34f, 1f, 1f);
        private static readonly Color CardBack = new Color(0.035f, 0.045f, 0.10f, 1f);

        private void Update()
        {
            Resolve();
            if (_duel == null || _table == null || Time.unscaledTime < _nextSync)
                return;

            _nextSync = Time.unscaledTime + 0.20f;
            string signature = _duel.IsActive
                ? $"{_duel.PlayerDeckCount}:{_duel.PlayerGraveyardCount}:{_duel.PlayerBanishedCount}|{_duel.CpuDeckCount}:{_duel.CpuGraveyardCount}:{_duel.CpuBanishedCount}"
                : "OFF";

            if (signature == _signature)
                return;

            _signature = signature;
            Rebuild();
        }

        private void Resolve()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            if (_table == null)
            {
                GameObject table = GameObject.Find("Duel Table Prototype");
                if (table != null)
                    _table = table.transform;
            }
        }

        private void Rebuild()
        {
            if (_table == null) return;

            if (_root != null)
                Object.Destroy(_root.gameObject);

            GameObject root = new GameObject("DG Live Card Piles");
            root.transform.SetParent(_table, false);
            _root = root.transform;

            if (_duel == null || !_duel.IsActive)
                return;

            BuildSide(true,
                _duel.PlayerDeckCount,
                _duel.PlayerGraveyardCount,
                _duel.PlayerBanishedCount);

            BuildSide(false,
                _duel.CpuDeckCount,
                _duel.CpuGraveyardCount,
                _duel.CpuBanishedCount);
        }

        private void BuildSide(bool playerSide, int deck, int grave, int banished)
        {
            float z = playerSide ? -1.26f : 1.26f;
            float direction = playerSide ? 1f : -1f;
            Color sideColor = playerSide ? PlayerColor : CpuColor;

            CreatePile("DECK", new Vector3(1.70f, 1.405f, z), deck, CardBack, sideColor, direction);
            CreatePile("GY", new Vector3(1.05f, 1.405f, z), grave, GraveColor, sideColor, direction);
            CreatePile("BANISHED", new Vector3(0.40f, 1.405f, z), banished, BanishedColor, sideColor, direction);
        }

        private void CreatePile(string label, Vector3 localPosition, int count, Color cardColor, Color accent, float direction)
        {
            GameObject holder = new GameObject(label + " Pile");
            holder.transform.SetParent(_root, false);
            holder.transform.localPosition = localPosition;

            if (count > 0)
            {
                float height = Mathf.Clamp(0.025f + count * 0.003f, 0.025f, 0.16f);
                GameObject stack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stack.name = label + " Stack";
                stack.transform.SetParent(holder.transform, false);
                stack.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                stack.transform.localScale = new Vector3(0.38f, height, 0.54f);
                RemoveCollider(stack);
                SetMaterial(stack, cardColor, true);

                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = label + " Accent";
                stripe.transform.SetParent(holder.transform, false);
                stripe.transform.localPosition = new Vector3(0f, height + 0.008f, 0f);
                stripe.transform.localScale = new Vector3(0.28f, 0.012f, 0.05f);
                RemoveCollider(stripe);
                SetMaterial(stripe, accent, true);
            }

            GameObject labelObject = new GameObject("Pile Label");
            labelObject.transform.SetParent(holder.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.24f, -0.18f * direction);
            labelObject.transform.localRotation = Quaternion.Euler(55f * direction, 0f, 0f);

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = label + "\n" + count;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.045f;
            text.fontSize = 42;
            text.color = accent;

            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null && text.font != null)
                renderer.sharedMaterial = text.font.material;
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
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.7f);
            }
            renderer.material = material;
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }
    }
}
