using System.Collections.Generic;
using System.Text;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Non-invasive physical presentation layer for the existing duel engine.
    /// It owns the duel camera, mat, zones and back-row card visuals while leaving
    /// all gameplay rules/actions inside DuelGameController.
    /// </summary>
    public sealed class DuelTabletopPresentation : MonoBehaviour
    {
        private static readonly Color BoardColor = new Color(0.025f, 0.04f, 0.07f, 1f);
        private static readonly Color PlayerZoneColor = new Color(0.05f, 0.34f, 0.42f, 1f);
        private static readonly Color CpuZoneColor = new Color(0.38f, 0.06f, 0.23f, 1f);
        private static readonly Color UtilityZoneColor = new Color(0.18f, 0.16f, 0.26f, 1f);

        private readonly List<DuelTabletopZone> _playerMonsterZones = new();
        private readonly List<DuelTabletopZone> _cpuMonsterZones = new();
        private readonly List<DuelTabletopZone> _playerBackrowZones = new();
        private readonly List<DuelTabletopZone> _cpuBackrowZones = new();

        private DuelGameController _duel;
        private Transform _table;
        private Transform _root;
        private Transform _backrowCardRoot;
        private Camera _camera;
        private Behaviour _thirdPersonCameraDriver;
        private DuelTabletopZone _selectedZone;

        private bool _tabletopActive;
        private bool _cameraCached;
        private bool _cameraDriverWasEnabled;
        private Vector3 _cachedCameraPosition;
        private Quaternion _cachedCameraRotation;
        private float _cachedFieldOfView;
        private float _cameraTransitionStart;
        private Vector3 _duelCameraPosition;
        private Quaternion _duelCameraRotation;
        private string _backrowSignature = string.Empty;
        private float _nextSync;

        public bool IsTabletopActive => _tabletopActive && _duel != null && _duel.IsActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelTabletopPresentation>() != null)
                return;

            GameObject host = new GameObject("Duel Tabletop Presentation");
            host.AddComponent<DuelTabletopPresentation>();
        }

        private void Start()
        {
            ResolveReferences();
            EnsureTabletop();
        }

        private void Update()
        {
            ResolveReferences();
            EnsureTabletop();

            bool shouldBeActive = _duel != null && _duel.IsActive && _table != null;
            if (shouldBeActive && !_tabletopActive)
                EnterTabletop();
            else if (!shouldBeActive && _tabletopActive)
                ExitTabletop();

            if (!IsTabletopActive || Time.unscaledTime < _nextSync)
                return;

            _nextSync = Time.unscaledTime + 0.10f;
            SyncZones();
            SyncBackrowCards();
        }

        private void LateUpdate()
        {
            if (!IsTabletopActive || _camera == null)
                return;

            float elapsed = Time.unscaledTime - _cameraTransitionStart;
            float t = Mathf.Clamp01(elapsed / 0.48f);
            t = t * t * (3f - 2f * t);

            _camera.transform.position = Vector3.Lerp(_cachedCameraPosition, _duelCameraPosition, t);
            _camera.transform.rotation = Quaternion.Slerp(_cachedCameraRotation, _duelCameraRotation, t);
            _camera.fieldOfView = Mathf.Lerp(_cachedFieldOfView, 46f, t);
        }

        private void OnDestroy()
        {
            if (_tabletopActive)
                RestoreCamera();
        }

        private void ResolveReferences()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_camera == null)
                _camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        }

        private void EnsureTabletop()
        {
            if (_table == null || _root != null)
                return;

            Transform existing = _table.Find("DG Physical Tabletop");
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
            }

            GameObject rootObject = new GameObject("DG Physical Tabletop");
            rootObject.transform.SetParent(_table, false);
            _root = rootObject.transform;

            CreateBoard();
            CreateFieldZones();

            GameObject backrowObject = new GameObject("Backrow Cards");
            backrowObject.transform.SetParent(_root, false);
            _backrowCardRoot = backrowObject.transform;

            _root.gameObject.SetActive(false);
        }

        private void CreateBoard()
        {
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Duel Mat";
            board.transform.SetParent(_root, false);
            board.transform.localPosition = new Vector3(0f, DuelTabletopLayout.BoardSurfaceY, 0f);
            board.transform.localScale = DuelTabletopLayout.BoardScale;
            SetMaterial(board, BoardColor, false);

            Collider collider = board.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }

        private void CreateFieldZones()
        {
            for (int i = 0; i < 5; i++)
            {
                _playerMonsterZones.Add(CreateZone(
                    DuelTabletopLayout.MonsterZonePosition(i, true), true, DuelTabletopZoneKind.Monster, i, PlayerZoneColor, "P MONSTER"));
                _cpuMonsterZones.Add(CreateZone(
                    DuelTabletopLayout.MonsterZonePosition(i, false), false, DuelTabletopZoneKind.Monster, i, CpuZoneColor, "CPU MONSTER"));
                _playerBackrowZones.Add(CreateZone(
                    DuelTabletopLayout.BackrowZonePosition(i, true), true, DuelTabletopZoneKind.SpellTrap, i, PlayerZoneColor, "P S/T"));
                _cpuBackrowZones.Add(CreateZone(
                    DuelTabletopLayout.BackrowZonePosition(i, false), false, DuelTabletopZoneKind.SpellTrap, i, CpuZoneColor, "CPU S/T"));
            }

            CreateZone(DuelTabletopLayout.DeckPosition(true), true, DuelTabletopZoneKind.Deck, 0, UtilityZoneColor, "DECK");
            CreateZone(DuelTabletopLayout.GraveyardPosition(true), true, DuelTabletopZoneKind.Graveyard, 0, UtilityZoneColor, "GY");
            CreateZone(DuelTabletopLayout.DeckPosition(false), false, DuelTabletopZoneKind.Deck, 0, UtilityZoneColor, "DECK");
            CreateZone(DuelTabletopLayout.GraveyardPosition(false), false, DuelTabletopZoneKind.Graveyard, 0, UtilityZoneColor, "GY");
        }

        private DuelTabletopZone CreateZone(
            Vector3 localPosition,
            bool playerSide,
            DuelTabletopZoneKind kind,
            int index,
            Color color,
            string label)
        {
            GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneObject.name = $"{(playerSide ? "Player" : "CPU")} {kind} Zone {index + 1}";
            zoneObject.transform.SetParent(_root, false);
            zoneObject.transform.localPosition = localPosition;
            zoneObject.transform.localScale = DuelTabletopLayout.ZoneScale;
            SetMaterial(zoneObject, color, true);

            DuelTabletopZone zone = zoneObject.AddComponent<DuelTabletopZone>();
            zone.Configure(this, playerSide, kind, index, zoneObject.GetComponent<Renderer>(), color);

            CreateZoneLabel(zoneObject.transform, label);
            return zone;
        }

        private static void CreateZoneLabel(Transform parent, string label)
        {
            GameObject labelObject = new GameObject("Zone Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1.15f, 1.15f, 1.15f);

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.12f;
            text.fontSize = 36;
            text.color = new Color(0.82f, 0.90f, 1f, 0.72f);
        }

        private void EnterTabletop()
        {
            if (_root == null || _camera == null)
                return;

            _tabletopActive = true;
            _root.gameObject.SetActive(true);
            _backrowSignature = string.Empty;
            _nextSync = 0f;

            CacheCamera();
            DisableThirdPersonCameraDriver();
            CalculateDuelCameraPose();
            _cameraTransitionStart = Time.unscaledTime;

            SyncZones();
            SyncBackrowCards();
        }

        private void ExitTabletop()
        {
            _tabletopActive = false;
            _selectedZone = null;
            ClearChildren(_backrowCardRoot);
            _backrowSignature = string.Empty;

            if (_root != null)
                _root.gameObject.SetActive(false);

            RestoreCamera();
        }

        private void CacheCamera()
        {
            if (_camera == null) return;

            _cachedCameraPosition = _camera.transform.position;
            _cachedCameraRotation = _camera.transform.rotation;
            _cachedFieldOfView = _camera.fieldOfView;
            _cameraCached = true;
        }

        private void CalculateDuelCameraPose()
        {
            if (_table == null) return;

            _duelCameraPosition = _table.TransformPoint(DuelTabletopLayout.CameraLocalPosition);
            Vector3 target = _table.TransformPoint(DuelTabletopLayout.CameraTargetLocalPosition);
            Vector3 up = _table.up.sqrMagnitude > 0.1f ? _table.up : Vector3.up;
            _duelCameraRotation = Quaternion.LookRotation(target - _duelCameraPosition, up);
        }

        private void DisableThirdPersonCameraDriver()
        {
            if (_thirdPersonCameraDriver != null)
                return;

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour.GetType().Name != "ThirdPersonCamera")
                    continue;

                _thirdPersonCameraDriver = behaviour;
                _cameraDriverWasEnabled = behaviour.enabled;
                behaviour.enabled = false;
                break;
            }
        }

        private void RestoreCamera()
        {
            if (_camera != null && _cameraCached)
            {
                _camera.transform.position = _cachedCameraPosition;
                _camera.transform.rotation = _cachedCameraRotation;
                _camera.fieldOfView = _cachedFieldOfView;
            }

            if (_thirdPersonCameraDriver != null)
            {
                _thirdPersonCameraDriver.enabled = _cameraDriverWasEnabled;
                _thirdPersonCameraDriver = null;
            }

            _cameraCached = false;
        }

        private void SyncZones()
        {
            if (_duel == null) return;

            for (int i = 0; i < 5; i++)
            {
                _playerMonsterZones[i].SetOccupied(i < _duel.PlayerMonsters.Count);
                _cpuMonsterZones[i].SetOccupied(i < _duel.CpuMonsters.Count);
                _playerBackrowZones[i].SetOccupied(i < _duel.PlayerBackrow.Count);
                _cpuBackrowZones[i].SetOccupied(i < _duel.CpuBackrow.Count);
            }
        }

        private void SyncBackrowCards()
        {
            if (_duel == null || _backrowCardRoot == null)
                return;

            string signature = BuildBackrowSignature();
            if (signature == _backrowSignature)
                return;

            _backrowSignature = signature;
            ClearChildren(_backrowCardRoot);

            BuildBackrowSide(_duel.PlayerBackrow, true);
            BuildBackrowSide(_duel.CpuBackrow, false);
        }

        private string BuildBackrowSignature()
        {
            StringBuilder builder = new StringBuilder();
            AppendBackrowSignature(builder, _duel.PlayerBackrow, 'P');
            AppendBackrowSignature(builder, _duel.CpuBackrow, 'C');
            return builder.ToString();
        }

        private static void AppendBackrowSignature(StringBuilder builder, IReadOnlyList<DuelBackrowState> cards, char side)
        {
            builder.Append(side).Append(':');
            for (int i = 0; i < cards.Count; i++)
            {
                DuelBackrowState state = cards[i];
                builder.Append(state.Card.id).Append(state.FaceDown ? 'D' : 'U').Append('|');
            }
        }

        private void BuildBackrowSide(IReadOnlyList<DuelBackrowState> cards, bool playerSide)
        {
            for (int i = 0; i < cards.Count && i < 5; i++)
            {
                DuelBackrowState state = cards[i];
                bool hidden = state.FaceDown;
                Texture2D texture = hidden
                    ? ProductionCardArtRegistry.LoadCardBack()
                    : ProductionCardArtRegistry.LoadDisplayTexture(state.Card);

                GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cardObject.name = hidden ? "Face Down Backrow" : "Card - " + state.Card.cardName;
                cardObject.transform.SetParent(_backrowCardRoot, false);

                Vector3 position = DuelTabletopLayout.BackrowZonePosition(i, playerSide);
                position.y += 0.035f;
                cardObject.transform.localPosition = position;
                cardObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                cardObject.transform.localScale = DuelTabletopLayout.CardScale;

                Collider collider = cardObject.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);

                Color fallback = hidden
                    ? new Color(0.025f, 0.03f, 0.08f, 1f)
                    : (playerSide ? PlayerZoneColor : CpuZoneColor);
                SetTexturedMaterial(cardObject, texture, texture == null ? fallback : Color.white);
            }
        }

        public void OnZoneClicked(DuelTabletopZone zone)
        {
            if (!IsTabletopActive || zone == null)
                return;

            if (_selectedZone != null && _selectedZone != zone)
                _selectedZone.SetSelected(false);

            bool selecting = _selectedZone != zone;
            _selectedZone = selecting ? zone : null;
            zone.SetSelected(selecting);

            if (selecting)
            {
                string side = zone.PlayerSide ? "Player" : "CPU";
                Debug.Log($"Duel tabletop selected: {side} {zone.Kind} zone {zone.Index + 1}.");
            }
        }

        private static void SetTexturedMaterial(GameObject obj, Texture2D texture, Color tint)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            }
            renderer.material = material;
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
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color * 0.55f);
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
