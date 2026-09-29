using System;
using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using DuelGenesis.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// The one and only duel-table presentation. It owns the life-size table in the world and,
    /// while a duel runs, mirrors the engine state onto physical 3D cards every frame:
    /// each card is given a pose (deck stack, hand fan, zone, Graveyard pile...) and glides there.
    /// It also drives the duel camera, mouse picking, highlights, holograms and table effects.
    /// </summary>
    public sealed class DuelBoardView : MonoBehaviour
    {
        public const string TableObjectName = "Duel Table Prototype";

        public DuelCard HoveredCard { get; private set; }
        public DuelZoneView HoveredZone { get; private set; }
        public bool HologramsEnabled { get; set; } = true;
        public bool IsSettled => _cards.Values.All(v => v.IsSettled);
        public bool IsPresenting => _engine != null;
        public Camera DuelCamera => _camera;

        public event Action<DuelCard> CardClicked;
        public event Action<DuelZoneView> ZoneClicked;
        public event Action RightClicked;

        /// <summary>Supplied by the HUD: glow colour/strength for a card (strength 0 = none).</summary>
        public Func<DuelCard, (Color color, float strength)> CardHighlight;
        public Func<DuelZoneView, (Color color, float strength)> ZoneHighlight;

        private Transform _table;
        private Transform _root;
        private Transform _cardRoot;
        private readonly List<DuelZoneView> _zones = new();
        private readonly Dictionary<int, DuelCardView> _cards = new();
        private readonly Dictionary<int, MonsterHologram> _holograms = new();

        private DuelEngine _engine;
        private int _human;

        // Camera
        private Camera _camera;
        private Transform _cameraOriginalParent;
        private Vector3 _cameraOriginalPosition;
        private Quaternion _cameraOriginalRotation;
        private float _cameraOriginalFov;
        private float _cameraOriginalNear;
        private readonly List<Behaviour> _disabledCameraDrivers = new();
        private readonly List<Renderer> _hiddenPlayerRenderers = new();
        private float _zoom;          // 0 = seated view, 1 = overhead
        private float _zoomTarget;
        private float _orbit;
        private float _shake;
        private float _enterTime;

        // ============================================================== world table

        private void Start() => EnsureTable();

        private void EnsureTable()
        {
            if (_root != null) return;
            GameObject tableObject = GameObject.Find(TableObjectName);
            if (tableObject == null) return;
            _table = tableObject.transform;

            // Retire the prototype blockout (a 4.8 m slab with rails) in favour of a life-size table.
            foreach (string legacy in new[] { "Tabletop Arena Blockout", "Blue Rail", "Red Rail", "Back Rail", "Front Rail" })
            {
                Transform t = _table.Find(legacy);
                if (t != null) t.gameObject.SetActive(false);
            }

            _root = DuelTableBuilder.Build(_table, _zones);
            _cardRoot = new GameObject("DG Duel Cards").transform;
            _cardRoot.SetParent(_root, false);

            // Seat the player at the near edge of the real table and make the seat trigger the table's collider.
            Transform seat = _table.Find("Seat Point");
            if (seat != null) seat.localPosition = new Vector3(0f, 0f, -DuelMatLayout.TableDepth * 0.5f - 0.45f);
            Transform stand = _table.Find("Stand Point");
            if (stand != null) stand.localPosition = new Vector3(0f, 0f, -DuelMatLayout.TableDepth * 0.5f - 1.1f);
            Transform trigger = _table.Find("Seat Interaction");
            if (trigger != null)
            {
                trigger.localPosition = new Vector3(0f, DuelMatLayout.TableHeight * 0.5f, 0f);
                trigger.localScale = new Vector3(DuelMatLayout.TableWidth + 0.04f, DuelMatLayout.TableHeight, DuelMatLayout.TableDepth + 0.04f);
                Renderer r = trigger.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                Transform top = _root.Find("Table Top");
                if (top != null && top.GetComponent<Collider>() != null) Destroy(top.GetComponent<Collider>());
            }
        }

        // ============================================================== duel lifecycle

        public void BeginPresentation(DuelEngine engine, int humanIndex)
        {
            EnsureTable();
            if (_root == null) return;
            EndPresentation();

            _engine = engine;
            _human = humanIndex;
            _zoom = _zoomTarget = 0f;
            _orbit = 0f;
            _enterTime = Time.unscaledTime;

            foreach (DuelCard card in engine.AllCards())
            {
                DuelCardView view = DuelCardView.Create(_cardRoot, card);
                _cards[card.Uid] = view;
                Pose pose = ComputePose(card);
                view.SetPose(pose.position, pose.rotation, pose.scale, instant: true);
            }

            // Queue faces for everything the human might see soon.
            CardFaceCompositor.Prewarm(engine.Me(humanIndex).Hand.Select(c => c.Data));
            CardFaceCompositor.Prewarm(engine.Me(humanIndex).Deck.Select(c => c.Data).Distinct());

            engine.EventRaised += OnEngineEvent;
            TakeCamera();
            HidePlayer(true);
        }

        public void EndPresentation()
        {
            if (_engine != null) _engine.EventRaised -= OnEngineEvent;
            _engine = null;
            foreach (DuelCardView view in _cards.Values) if (view != null) Destroy(view.gameObject);
            _cards.Clear();
            foreach (MonsterHologram h in _holograms.Values) if (h != null) Destroy(h.gameObject);
            _holograms.Clear();
            foreach (DuelZoneView zone in _zones) zone.SetHighlight(Color.clear, 0f);
            HoveredCard = null;
            HoveredZone = null;
            ReleaseCamera();
            HidePlayer(false);
        }

        private bool _hasHome;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        /// <summary>Moves the playable table (and its seat) onto another table spot in the city for a duel there.</summary>
        public bool MoveTableTo(Transform anchor)
        {
            EnsureTable();
            if (_table == null || anchor == null) return false;
            if (!_hasHome)
            {
                _homePosition = _table.position;
                _homeRotation = _table.rotation;
                _hasHome = true;
            }
            _table.SetPositionAndRotation(anchor.position, anchor.rotation);
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>Puts the playable table back at its home spot in the hub.</summary>
        public void ReturnTableHome()
        {
            if (_table == null || !_hasHome) return;
            _table.SetPositionAndRotation(_homePosition, _homeRotation);
            Physics.SyncTransforms();
        }

        public void ToggleCameraView() => _zoomTarget = _zoomTarget < 0.5f ? 1f : 0f;

        // ============================================================== per-frame sync

        private void LateUpdate()
        {
            DuelTableBuilder.TickMatRender();
            if (_engine == null) return;

            HandleCameraInput();
            UpdateCamera();

            foreach (DuelCard card in _engine.AllCards())
            {
                if (!_cards.TryGetValue(card.Uid, out DuelCardView view))
                {
                    view = DuelCardView.Create(_cardRoot, card);
                    _cards[card.Uid] = view;
                }

                Pose pose = ComputePose(card);
                view.SetPose(pose.position, pose.rotation, pose.scale);

                if (!view.HasFace && ShouldShowFace(card) && CardFaceCompositor.TryGetFace(card.Data, out Texture2D face))
                    view.SetFace(face);

                (Color color, float strength) glow = CardHighlight != null ? CardHighlight(card) : (Color.clear, 0f);
                if (card == HoveredCard && glow.strength < 0.35f) glow = (Color.white, 0.35f);
                view.SetGlow(glow.color, glow.strength);
            }

            foreach (DuelZoneView zone in _zones)
            {
                (Color color, float strength) glow = ZoneHighlight != null ? ZoneHighlight(zone) : (Color.clear, 0f);
                zone.SetHighlight(glow.color, glow.strength);
            }

            SyncHolograms();
            HandlePointer();
        }

        private bool ShouldShowFace(DuelCard card)
        {
            if (card.Zone == DuelZone.Hand) return card.Owner == _human;
            if (card.Zone == DuelZone.Deck || card.Zone == DuelZone.ExtraDeck) return false;
            return card.FaceUp || card.OnField;
        }

        // ============================================================== poses

        private struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float scale;
        }

        private static Quaternion Flat(float yaw, bool faceDown) =>
            Quaternion.Euler(0f, yaw, 0f) * (faceDown ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity);

        private Pose ComputePose(DuelCard card)
        {
            float thickness = DuelMatLayout.CardThicknessMm * 0.001f;
            DuelistState owner = _engine.Me(card.Owner);
            int controller = card.Controller;
            float yaw = DuelMatLayout.Yaw(card.Owner);

            switch (card.Zone)
            {
                case DuelZone.Deck:
                {
                    int i = owner.Deck.IndexOf(card);
                    return new Pose { position = DuelMatLayout.DeckZone(card.Owner) + Vector3.up * (thickness * (i + 0.5f)), rotation = Flat(yaw, true), scale = 1f };
                }
                case DuelZone.ExtraDeck:
                {
                    int i = owner.ExtraDeck.IndexOf(card);
                    return new Pose { position = DuelMatLayout.ExtraDeckZone(card.Owner) + Vector3.up * (thickness * (i + 0.5f)), rotation = Flat(yaw, true), scale = 1f };
                }
                case DuelZone.Graveyard:
                {
                    int i = owner.Graveyard.IndexOf(card);
                    // A real Graveyard is a slightly messy pile.
                    float jitter = ((card.Uid * 37) % 7 - 3) * 0.6f;
                    return new Pose { position = DuelMatLayout.GraveyardZone(card.Owner) + Vector3.up * (thickness * (i + 0.5f)), rotation = Flat(yaw + jitter, false), scale = 1f };
                }
                case DuelZone.Banished:
                {
                    int i = owner.Banished.IndexOf(card);
                    return new Pose { position = DuelMatLayout.BanishedZone(card.Owner) + Vector3.up * (thickness * (i + 0.5f)), rotation = Flat(yaw, false), scale = 1f };
                }
                case DuelZone.Monster:
                {
                    DuelMonsterState m = _engine.Me(controller).Monsters[Mathf.Clamp(card.Slot, 0, 4)];
                    bool defense = m != null && m.IsDefensePosition;
                    bool faceDown = m != null && m.IsFaceDown;
                    float lift = _engine.CurrentAttacker == m || _engine.CurrentAttackTarget == m ? 0.006f : 0f;
                    return new Pose
                    {
                        position = DuelMatLayout.MonsterZone(controller, card.Slot) + Vector3.up * (thickness * 0.5f + lift),
                        rotation = Flat(DuelMatLayout.Yaw(controller) + (defense ? 90f : 0f), faceDown),
                        scale = 1f
                    };
                }
                case DuelZone.SpellTrap:
                case DuelZone.FieldSpell:
                {
                    Vector3 zone = card.Zone == DuelZone.FieldSpell ? DuelMatLayout.FieldZone(controller) : DuelMatLayout.SpellTrapZone(controller, card.Slot);
                    DuelBackrowState s = _engine.FindBackrow(card);
                    bool faceDown = s == null || s.FaceDown;
                    float lift = s != null && s.Resolving ? 0.01f : 0f;
                    return new Pose { position = zone + Vector3.up * (thickness * 0.5f + lift), rotation = Flat(DuelMatLayout.Yaw(controller), faceDown), scale = 1f };
                }
                case DuelZone.Hand:
                    return card.Owner == _human ? HumanHandPose(card) : OpponentHandPose(card);
                default:
                    return new Pose { position = DuelMatLayout.DeckZone(card.Owner), rotation = Flat(yaw, true), scale = 1f };
            }
        }

        private Pose OpponentHandPose(DuelCard card)
        {
            List<DuelCard> hand = _engine.Me(card.Owner).Hand;
            int i = hand.IndexOf(card);
            Vector3 p = DuelMatLayout.OpponentHandCard(i, hand.Count);
            // Held upright, tilted back, backs facing the human player.
            float fan = (i - (hand.Count - 1) * 0.5f) * 3f;
            Quaternion rotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-72f, 0f, 0f) * Quaternion.Euler(0f, fan, 0f);
            return new Pose { position = p, rotation = rotation, scale = 1f };
        }

        /// <summary>Human hand: a fan held in front of the camera (always readable, in both views).</summary>
        private Pose HumanHandPose(DuelCard card)
        {
            List<DuelCard> hand = _engine.Me(card.Owner).Hand;
            int count = hand.Count;
            int i = hand.IndexOf(card);
            if (_camera == null) return new Pose { position = DuelMatLayout.DeckZone(card.Owner), rotation = Flat(0f, true), scale = 1f };

            Transform cam = _camera.transform;
            float distance = 0.34f;
            float halfHeight = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            float halfWidth = halfHeight * _camera.aspect;

            float scale = 0.78f;
            float cardW = DuelMatLayout.CardWidthMm * 0.001f * scale;
            float spacing = Mathf.Min(cardW * 0.92f, halfWidth * 1.3f / Mathf.Max(1, count - 1));
            float offset = (i - (count - 1) * 0.5f);
            bool hovered = card == HoveredCard;
            bool raised = hovered || (CardHighlight != null && CardHighlight(card).strength > 0.5f);

            Vector3 centre = cam.position + cam.forward * distance - cam.up * (halfHeight * 0.76f);
            Vector3 world = centre + cam.right * (offset * spacing) - cam.up * (offset * offset * 0.0012f)
                            + cam.up * (raised ? (hovered ? 0.032f : 0.012f) : 0f)
                            - cam.forward * (i * 0.0006f + (hovered ? 0.01f : 0f));
            float fanAngle = -offset * 3.2f;
            Vector3 cardUp = Quaternion.AngleAxis(fanAngle, cam.forward) * cam.up;
            Quaternion worldRot = Quaternion.LookRotation(cardUp, -cam.forward);

            return new Pose
            {
                position = _root.InverseTransformPoint(world),
                rotation = Quaternion.Inverse(_root.rotation) * worldRot,
                scale = hovered ? scale * 1.18f : scale
            };
        }

        public Vector3 CardLocalPosition(DuelCard card) =>
            card != null && _cards.TryGetValue(card.Uid, out DuelCardView v) ? v.transform.localPosition : Vector3.zero;

        public Vector3 CardWorldPosition(DuelCard card) =>
            card != null && _cards.TryGetValue(card.Uid, out DuelCardView v) ? v.transform.position : _root != null ? _root.position : Vector3.zero;

        public Vector3 SideWorldPosition(int player) =>
            _root != null ? _root.TransformPoint(DuelMatLayout.Local(player, 0f, DuelMatLayout.BackRowZ - 60f)) : Vector3.zero;

        // ============================================================== holograms

        private void SyncHolograms()
        {
            var wanted = new HashSet<int>();
            if (HologramsEnabled)
            {
                foreach (DuelistState d in _engine.Duelists)
                foreach (DuelMonsterState m in d.MonstersOnField)
                {
                    if (m.IsFaceDown) continue;
                    wanted.Add(m.Card.Uid);
                    if (!_holograms.TryGetValue(m.Card.Uid, out MonsterHologram h))
                    {
                        h = MonsterHologram.TryCreate(_cardRoot, m.Card);
                        _holograms[m.Card.Uid] = h;   // may be null: no model for this card
                    }
                    if (h == null) continue;
                    Vector3 basePos = _cards.TryGetValue(m.Card.Uid, out DuelCardView v) ? v.transform.localPosition : DuelMatLayout.MonsterZone(m.Card.Controller, m.Slot);
                    h.SetPose(basePos, DuelMatLayout.Yaw(m.Card.Controller), m.IsDefensePosition);
                }
            }

            foreach (int uid in _holograms.Keys.ToList())
            {
                if (wanted.Contains(uid)) continue;
                if (_holograms[uid] != null) Destroy(_holograms[uid].gameObject);
                _holograms.Remove(uid);
            }
        }

        // ============================================================== events -> effects

        private void OnEngineEvent(DuelEvent e)
        {
            if (_root == null) return;
            switch (e.Type)
            {
                case DuelEventType.NormalSummon:
                case DuelEventType.TributeSummon:
                case DuelEventType.SpecialSummon:
                case DuelEventType.FlipSummon:
                    if (e.Card != null)
                        DuelFlash.Spawn(_root, DuelMatLayout.MonsterZone(e.Card.Controller, Mathf.Max(0, e.Card.Slot)),
                            e.Card.Controller == _human ? DuelVisualResources.Cyan : DuelVisualResources.Violet, 1.6f, 0.6f);
                    break;
                case DuelEventType.CardActivated:
                    if (e.Card != null)
                        DuelFlash.Spawn(_root, CardLocalPosition(e.Card), e.Card.IsTrap ? DuelVisualResources.Magenta : new Color(0.2f, 1f, 0.7f), 1.4f, 0.55f);
                    break;
                case DuelEventType.Destroyed:
                    if (e.Card != null)
                        DuelFlash.Spawn(_root, CardLocalPosition(e.Card), new Color(1f, 0.35f, 0.15f), 1.8f, 0.5f);
                    break;
                case DuelEventType.AttackDeclared:
                    if (e.Card != null && _cards.TryGetValue(e.Card.Uid, out DuelCardView attacker))
                    {
                        Vector3 target = e.Other != null ? CardLocalPosition(e.Other)
                            : DuelMatLayout.Local(1 - e.Card.Controller, 0f, DuelMatLayout.BackRowZ - 80f);
                        attacker.Lunge(target);
                    }
                    break;
                case DuelEventType.Damage:
                    _shake = Mathf.Min(1f, 0.25f + e.Amount / 4000f);
                    break;
            }
        }

        // ============================================================== pointer input

        private void HandlePointer()
        {
            HoveredCard = null;
            HoveredZone = null;
            Mouse mouse = Mouse.current;
            if (mouse == null || _camera == null) return;

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUi)
            {
                Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
                RaycastHit[] hits = Physics.RaycastAll(ray, 4f, ~0, QueryTriggerInteraction.Collide);
                float bestCard = float.MaxValue, bestZone = float.MaxValue;
                foreach (RaycastHit hit in hits)
                {
                    DuelCardView view = hit.collider.GetComponent<DuelCardView>();
                    if (view != null && hit.distance < bestCard)
                    {
                        // Prefer the top card of a pile.
                        bestCard = hit.distance;
                        HoveredCard = view.Card;
                        continue;
                    }
                    DuelZoneView zone = hit.collider.GetComponent<DuelZoneView>();
                    if (zone != null && hit.distance < bestZone)
                    {
                        bestZone = hit.distance;
                        HoveredZone = zone;
                    }
                }
                HoveredCard = TopOfPile(HoveredCard);
            }

            if (overUi) return;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (HoveredCard != null) CardClicked?.Invoke(HoveredCard);
                else if (HoveredZone != null) ZoneClicked?.Invoke(HoveredZone);
            }
            if (mouse.rightButton.wasPressedThisFrame) RightClicked?.Invoke();
        }

        private DuelCard TopOfPile(DuelCard card)
        {
            if (card == null) return null;
            DuelistState owner = _engine.Me(card.Owner);
            return card.Zone switch
            {
                DuelZone.Graveyard => owner.Graveyard.LastOrDefault(),
                DuelZone.Banished => owner.Banished.LastOrDefault(),
                DuelZone.Deck => owner.Deck.LastOrDefault(),
                DuelZone.ExtraDeck => owner.ExtraDeck.LastOrDefault(),
                _ => card
            };
        }

        // ============================================================== camera

        private void TakeCamera()
        {
            _camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (_camera == null) return;
            _cameraOriginalParent = _camera.transform.parent;
            _cameraOriginalPosition = _camera.transform.position;
            _cameraOriginalRotation = _camera.transform.rotation;
            _cameraOriginalFov = _camera.fieldOfView;
            _cameraOriginalNear = _camera.nearClipPlane;
            _camera.nearClipPlane = 0.02f;

            _disabledCameraDrivers.Clear();
            foreach (Behaviour driver in _camera.GetComponents<Behaviour>())
            {
                if (driver is ThirdPersonCamera && driver.enabled)
                {
                    driver.enabled = false;
                    _disabledCameraDrivers.Add(driver);
                }
            }
        }

        private void ReleaseCamera()
        {
            if (_camera == null) return;
            _camera.transform.SetParent(_cameraOriginalParent, true);
            _camera.transform.position = _cameraOriginalPosition;
            _camera.transform.rotation = _cameraOriginalRotation;
            _camera.fieldOfView = _cameraOriginalFov;
            _camera.nearClipPlane = _cameraOriginalNear;
            foreach (Behaviour driver in _disabledCameraDrivers) if (driver != null) driver.enabled = true;
            _disabledCameraDrivers.Clear();
            _camera = null;
        }

        private void HidePlayer(bool hide)
        {
            DuelGenesis.Interaction.PlayerInteractor interactor = FindAnyObjectByType<DuelGenesis.Interaction.PlayerInteractor>();
            if (interactor != null)
            {
                interactor.enabled = !hide;
                if (hide && interactor.promptCanvas != null) interactor.promptCanvas.alpha = 0f;
            }

            if (hide)
            {
                _hiddenPlayerRenderers.Clear();
                ThirdPersonPlayerController player = FindAnyObjectByType<ThirdPersonPlayerController>();
                if (player == null) return;
                foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    _hiddenPlayerRenderers.Add(r);
                }
            }
            else
            {
                foreach (Renderer r in _hiddenPlayerRenderers) if (r != null) r.enabled = true;
                _hiddenPlayerRenderers.Clear();
            }
        }

        private void HandleCameraInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && !overUi)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) _zoomTarget = Mathf.Clamp01(_zoomTarget - Mathf.Sign(wheel) * 0.25f);
                if (mouse.middleButton.isPressed) _orbit = Mathf.Clamp(_orbit + mouse.delta.ReadValue().x * 0.15f, -30f, 30f);
            }
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame) ToggleCameraView();
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame) HologramsEnabled = !HologramsEnabled;
        }

        private void UpdateCamera()
        {
            if (_camera == null || _root == null) return;
            float dt = Time.unscaledDeltaTime;
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-6f * dt));
            _orbit = Mathf.Lerp(_orbit, 0f, (Mouse.current != null && Mouse.current.middleButton.isPressed) ? 0f : 1f - Mathf.Exp(-1.5f * dt));

            float surface = DuelMatLayout.SurfaceY;
            // Seated: eyes ~47 cm above the table, 60 cm from its centre (a real seated duelist).
            Vector3 seatedPos = new Vector3(0f, surface + 0.47f, -0.60f);
            Vector3 seatedLook = new Vector3(0f, surface, -0.03f);
            Vector3 overheadPos = new Vector3(0f, surface + 0.92f, -0.20f);
            Vector3 overheadLook = new Vector3(0f, surface, -0.04f);

            Vector3 localPos = Vector3.Lerp(seatedPos, overheadPos, _zoom);
            Vector3 localLook = Vector3.Lerp(seatedLook, overheadLook, _zoom);
            localPos = Quaternion.Euler(0f, _orbit, 0f) * (localPos - localLook) + localLook;

            Vector3 worldPos = _root.TransformPoint(localPos);
            Vector3 worldLook = _root.TransformPoint(localLook);
            if (_shake > 0f)
            {
                _shake = Mathf.Max(0f, _shake - dt * 2.2f);
                worldPos += UnityEngine.Random.insideUnitSphere * (_shake * _shake * 0.006f);
            }

            float enter = Mathf.Clamp01((Time.unscaledTime - _enterTime) / 0.9f);
            enter = enter * enter * (3f - 2f * enter);
            Quaternion targetRot = Quaternion.LookRotation(worldLook - worldPos, Vector3.up);
            _camera.transform.position = Vector3.Lerp(_cameraOriginalPosition, worldPos, enter);
            _camera.transform.rotation = Quaternion.Slerp(_cameraOriginalRotation, targetRot, enter);
            _camera.fieldOfView = Mathf.Lerp(_cameraOriginalFov, Mathf.Lerp(48f, 52f, _zoom), enter);
        }
    }
}
