using System.Collections.Generic;
using System.Text;
using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Physical presentation layer for the duel field. The center field and all utility
    /// zones are real clickable world objects while gameplay rules remain in DuelGameController.
    /// </summary>
    public sealed class DuelTabletopPresentation : MonoBehaviour
    {
        private static readonly Color BoardColor = new Color(0.022f, 0.032f, 0.050f, 1f);
        private static readonly Color PlayerZoneColor = new Color(0.88f, 0.075f, 0.095f, 1f);
        private static readonly Color CpuZoneColor = new Color(0.045f, 0.38f, 0.95f, 1f);
        private static readonly Color UtilityZoneColor = new Color(0.20f, 0.46f, 0.64f, 1f);
        private static readonly Color FieldZoneColor = new Color(0.14f, 0.76f, 0.46f, 1f);
        private static readonly Color BanishZoneColor = new Color(0.68f, 0.28f, 0.92f, 1f);
        private static readonly Color ExtraMonsterColor = new Color(0.88f, 0.64f, 0.18f, 1f);

        private readonly List<DuelTabletopZone> _playerMonsterZones = new();
        private readonly List<DuelTabletopZone> _cpuMonsterZones = new();
        private readonly List<DuelTabletopZone> _playerBackrowZones = new();
        private readonly List<DuelTabletopZone> _cpuBackrowZones = new();

        private DuelGameController _duel;
        private DuelPhysicalInputController _physicalInput;
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

            _nextSync = Time.unscaledTime + 0.08f;
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
            if (_physicalInput == null)
                _physicalInput = Object.FindFirstObjectByType<DuelPhysicalInputController>();

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
                Object.Destroy(existing.gameObject);

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
                _playerMonsterZones.Add(CreateZone(DuelTabletopLayout.MonsterZonePosition(i, true), true, DuelTabletopZoneKind.Monster, i, PlayerZoneColor));
                _cpuMonsterZones.Add(CreateZone(DuelTabletopLayout.MonsterZonePosition(i, false), false, DuelTabletopZoneKind.Monster, i, CpuZoneColor));
                _playerBackrowZones.Add(CreateZone(DuelTabletopLayout.BackrowZonePosition(i, true), true, DuelTabletopZoneKind.SpellTrap, i, PlayerZoneColor));
                _cpuBackrowZones.Add(CreateZone(DuelTabletopLayout.BackrowZonePosition(i, false), false, DuelTabletopZoneKind.SpellTrap, i, CpuZoneColor));
            }

            // Player left wing: Field Spell above Extra Deck.
            CreateZone(DuelTabletopLayout.FieldZonePosition(true), true, DuelTabletopZoneKind.FieldSpell, 0, FieldZoneColor);
            CreateZone(DuelTabletopLayout.ExtraDeckPosition(true), true, DuelTabletopZoneKind.ExtraDeck, 0, UtilityZoneColor);

            // Player right wing: Graveyard above Main Deck, Banished beside GY.
            CreateZone(DuelTabletopLayout.GraveyardPosition(true), true, DuelTabletopZoneKind.Graveyard, 0, UtilityZoneColor);
            CreateZone(DuelTabletopLayout.DeckPosition(true), true, DuelTabletopZoneKind.Deck, 0, UtilityZoneColor);
            CreateZone(DuelTabletopLayout.BanishedPosition(true), true, DuelTabletopZoneKind.Banished, 0, BanishZoneColor);

            // CPU side is mirrored from its viewpoint.
            CreateZone(DuelTabletopLayout.FieldZonePosition(false), false, DuelTabletopZoneKind.FieldSpell, 0, FieldZoneColor);
            CreateZone(DuelTabletopLayout.ExtraDeckPosition(false), false, DuelTabletopZoneKind.ExtraDeck, 0, UtilityZoneColor);
            CreateZone(DuelTabletopLayout.GraveyardPosition(false), false, DuelTabletopZoneKind.Graveyard, 0, UtilityZoneColor);
            CreateZone(DuelTabletopLayout.DeckPosition(false), false, DuelTabletopZoneKind.Deck, 0, UtilityZoneColor);
            CreateZone(DuelTabletopLayout.BanishedPosition(false), false, DuelTabletopZoneKind.Banished, 0, BanishZoneColor);

            // Shared Extra Monster Zone pads.
            CreateZone(DuelTabletopLayout.ExtraMonsterZonePosition(0), true, DuelTabletopZoneKind.ExtraMonster, 0, ExtraMonsterColor);
            CreateZone(DuelTabletopLayout.ExtraMonsterZonePosition(1), true, DuelTabletopZoneKind.ExtraMonster, 1, ExtraMonsterColor);
        }

        private DuelTabletopZone CreateZone(Vector3 localPosition, bool playerSide, DuelTabletopZoneKind kind, int index, Color color)
        {
            Vector3 scale = ZoneScaleFor(kind);
            Color baseColor = Color.Lerp(BoardColor, color, 0.22f);

            GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneObject.name = $"{(playerSide ? "Player" : "CPU")} {kind} Zone {index + 1}";
            zoneObject.transform.SetParent(_root, false);
            zoneObject.transform.localPosition = localPosition;
            zoneObject.transform.localScale = scale;
            SetMaterial(zoneObject, baseColor, false);

            DuelTabletopZone zone = zoneObject.AddComponent<DuelTabletopZone>();
            zone.Configure(this, playerSide, kind, index, zoneObject.GetComponent<Renderer>(), baseColor);

            CreateZoneOutline(localPosition, scale, color, kind + " Outline " + index);
            return zone;
        }

        private static Vector3 ZoneScaleFor(DuelTabletopZoneKind kind)
        {
            if (kind == DuelTabletopZoneKind.Monster || kind == DuelTabletopZoneKind.SpellTrap)
                return DuelTabletopLayout.ZoneScale;
            if (kind == DuelTabletopZoneKind.ExtraMonster)
                return new Vector3(1.18f, 0.026f, 0.82f);
            return DuelTabletopLayout.UtilityZoneScale;
        }

        private void CreateZoneOutline(Vector3 position, Vector3 scale, Color color, string name)
        {
            float y = position.y + 0.026f;
            float edge = 0.045f;
            float halfX = scale.x * 0.5f;
            float halfZ = scale.z * 0.5f;

            CreateEdge(name + " Near", new Vector3(position.x, y, position.z - halfZ), new Vector3(scale.x, 0.018f, edge), color);
            CreateEdge(name + " Far", new Vector3(position.x, y, position.z + halfZ), new Vector3(scale.x, 0.018f, edge), color);
            CreateEdge(name + " Left", new Vector3(position.x - halfX, y, position.z), new Vector3(edge, 0.018f, scale.z), color);
            CreateEdge(name + " Right", new Vector3(position.x + halfX, y, position.z), new Vector3(edge, 0.018f, scale.z), color);
        }

        private void CreateEdge(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = name;
            edge.transform.SetParent(_root, false);
            edge.transform.localPosition = position;
            edge.transform.localScale = scale;
            Collider collider = edge.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            SetMaterial(edge, color, true);
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
                _playerMonsterZones[i].SetOccupied(DuelFieldSlotRegistry.IsMonsterSlotOccupied(_duel.PlayerMonsters, true, i));
                _cpuMonsterZones[i].SetOccupied(DuelFieldSlotRegistry.IsMonsterSlotOccupied(_duel.CpuMonsters, false, i));
                _playerBackrowZones[i].SetOccupied(DuelFieldSlotRegistry.IsBackrowSlotOccupied(_duel.PlayerBackrow, true, i));
                _cpuBackrowZones[i].SetOccupied(DuelFieldSlotRegistry.IsBackrowSlotOccupied(_duel.CpuBackrow, false, i));
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
            AppendBackrowSignature(builder, _duel.PlayerBackrow, true, 'P');
            AppendBackrowSignature(builder, _duel.CpuBackrow, false, 'C');
            return builder.ToString();
        }

        private static void AppendBackrowSignature(StringBuilder builder, IReadOnlyList<DuelBackrowState> cards, bool playerSide, char side)
        {
            builder.Append(side).Append(':');
            for (int i = 0; i < cards.Count; i++)
            {
                DuelBackrowState state = cards[i];
                int slot = DuelFieldSlotRegistry.GetBackrowSlot(state, playerSide, cards);
                builder.Append(state.Card.id).Append('@').Append(slot).Append(state.FaceDown ? 'D' : 'U').Append('|');
            }
        }

        private void BuildBackrowSide(IReadOnlyList<DuelBackrowState> cards, bool playerSide)
        {
            for (int i = 0; i < cards.Count && i < 5; i++)
            {
                DuelBackrowState state = cards[i];
                int slot = DuelFieldSlotRegistry.GetBackrowSlot(state, playerSide, cards);
                bool hidden = state.FaceDown;
                Texture2D texture = hidden
                    ? ProductionCardArtRegistry.LoadCardBack()
                    : ProductionCardArtRegistry.LoadDisplayTexture(state.Card);

                GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cardObject.name = hidden ? "Face Down Backrow" : "Card - " + state.Card.cardName;
                cardObject.transform.SetParent(_backrowCardRoot, false);

                Vector3 position = DuelTabletopLayout.BackrowZonePosition(slot, playerSide);
                position.y += 0.040f;
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

            if (_physicalInput == null)
                _physicalInput = Object.FindFirstObjectByType<DuelPhysicalInputController>();

            if (_physicalInput != null && _physicalInput.ClickZone(zone))
            {
                if (_selectedZone != null)
                    _selectedZone.SetSelected(false);
                _selectedZone = null;
                return;
            }

            if (_selectedZone != null && _selectedZone != zone)
                _selectedZone.SetSelected(false);

            bool selecting = _selectedZone != zone;
            _selectedZone = selecting ? zone : null;
            zone.SetSelected(selecting);
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
                    material.SetColor("_EmissionColor", color * 1.10f);
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
