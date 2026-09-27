using System.Collections.Generic;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Cleans the legacy tabletop presentation for a Master-Duel-style board:
    /// no floating world labels, no prototype pile text, and real visible deck stacks.
    /// </summary>
    public sealed class DuelMasterBoardPresentation : MonoBehaviour
    {
        private readonly Dictionary<MeshRenderer, bool> _hiddenTextRenderers = new();

        private DuelGameController _duel;
        private Transform _table;
        private Transform _deckRoot;
        private int _lastPlayerDeck = -1;
        private int _lastCpuDeck = -1;
        private bool _wasActive;
        private float _nextSync;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelMasterBoardPresentation>() != null)
                return;

            GameObject host = new GameObject("Duel Master Board Presentation");
            host.AddComponent<DuelMasterBoardPresentation>();
        }

        private void Update()
        {
            Resolve();
            bool active = _duel != null && _duel.IsActive && _table != null;

            if (active && !_wasActive)
                EnterBoardMode();
            else if (!active && _wasActive)
                ExitBoardMode();

            _wasActive = active;
            if (!active || Time.unscaledTime < _nextSync)
                return;

            _nextSync = Time.unscaledTime + 0.15f;
            HideWorldText();
            HideLegacyPilePresentation();
            SyncDeckStacks();
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

        private void EnterBoardMode()
        {
            HideWorldText();
            HideLegacyPilePresentation();
            EnsureDeckRoot();
            _lastPlayerDeck = -1;
            _lastCpuDeck = -1;
            SyncDeckStacks();
        }

        private void ExitBoardMode()
        {
            RestoreWorldText();
            if (_deckRoot != null)
                Object.Destroy(_deckRoot.gameObject);
            _deckRoot = null;
            _lastPlayerDeck = -1;
            _lastCpuDeck = -1;
        }

        private void HideWorldText()
        {
            if (_table == null)
                return;

            TextMesh[] textMeshes = _table.GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh text in textMeshes)
            {
                if (text == null)
                    continue;

                MeshRenderer renderer = text.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;

                if (!_hiddenTextRenderers.ContainsKey(renderer))
                    _hiddenTextRenderers.Add(renderer, renderer.enabled);

                renderer.enabled = false;
            }
        }

        private void RestoreWorldText()
        {
            foreach (KeyValuePair<MeshRenderer, bool> pair in _hiddenTextRenderers)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }
            _hiddenTextRenderers.Clear();
        }

        private void HideLegacyPilePresentation()
        {
            if (_table == null)
                return;

            Transform piles = _table.Find("DG Live Card Piles");
            if (piles != null && piles.gameObject.activeSelf)
                piles.gameObject.SetActive(false);

            // The old arena core was useful for the prototype but makes the new clean
            // overhead board visually busy. Monsters still retain their own hologram look.
            Transform fx = _table.Find("DG Duel Arena FX");
            if (fx != null && fx.gameObject.activeSelf)
                fx.gameObject.SetActive(false);
        }

        private void EnsureDeckRoot()
        {
            if (_table == null || _deckRoot != null)
                return;

            Transform old = _table.Find("DG Master Duel Deck Stacks");
            if (old != null)
                Object.Destroy(old.gameObject);

            GameObject root = new GameObject("DG Master Duel Deck Stacks");
            root.transform.SetParent(_table, false);
            _deckRoot = root.transform;
        }

        private void SyncDeckStacks()
        {
            if (_duel == null || _table == null)
                return;

            EnsureDeckRoot();
            if (_deckRoot == null)
                return;

            if (_lastPlayerDeck == _duel.PlayerDeckCount && _lastCpuDeck == _duel.CpuDeckCount)
                return;

            _lastPlayerDeck = _duel.PlayerDeckCount;
            _lastCpuDeck = _duel.CpuDeckCount;

            ClearChildren(_deckRoot);
            BuildDeckStack(true, _duel.PlayerDeckCount);
            BuildDeckStack(false, _duel.CpuDeckCount);
        }

        private void BuildDeckStack(bool playerSide, int count)
        {
            if (count <= 0)
                return;

            Texture2D cardBack = ProductionCardArtRegistry.LoadCardBack();
            Vector3 basePosition = DuelTabletopLayout.DeckPosition(playerSide);
            int visibleLayers = Mathf.Clamp(Mathf.CeilToInt(count / 6f), 3, 8);

            for (int i = 0; i < visibleLayers; i++)
            {
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = (playerSide ? "Player" : "CPU") + " Deck Card Back " + i;
                card.transform.SetParent(_deckRoot, false);
                card.transform.localPosition = basePosition + new Vector3(0f, 0.012f + i * 0.012f, 0f);
                card.transform.localRotation = Quaternion.Euler(-90f, playerSide ? 0f : 180f, 0f);
                card.transform.localScale = DuelTabletopLayout.CardScale * 0.86f;

                Collider collider = card.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);

                ApplyCardBack(card, cardBack);
            }
        }

        private static void ApplyCardBack(GameObject obj, Texture2D texture)
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

        private static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.Destroy(root.GetChild(i).gameObject);
        }
    }
}
