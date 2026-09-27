using System;
using System.Collections.Generic;
using System.Reflection;
using DuelGenesis.Cards;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Physical/clickable front-end for DuelGameController. Hand actions enter a placement
    /// mode and the player chooses the exact physical Monster or Spell/Trap zone to use.
    /// </summary>
    public sealed class DuelPhysicalInputController : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private enum PlacementAction
        {
            None,
            NormalSummonAttack,
            NormalSetDefense,
            SpecialSummon,
            ActivateSpell,
            SetTrap
        }

        private DuelGameController _duel;
        private Transform _table;
        private Transform _handRoot;
        private Transform _targetRoot;
        private Camera _camera;

        private readonly List<DuelHandCardTarget> _handCards = new();
        private CardData _selectedHandCard;
        private DuelMonsterState _selectedMonster;
        private PlacementAction _placementAction;
        private CardData _placementCard;
        private bool _legacyUiSuppressed;
        private bool _wasActive;
        private string _handSignature = string.Empty;
        private float _nextSync;

        private FieldInfo _playerHandField;
        private FieldInfo _pendingAttackerField;
        private FieldInfo _pendingQuickChargeField;
        private FieldInfo _pendingArchmageField;
        private FieldInfo _discardingField;
        private FieldInfo _messageField;

        private MethodInfo _beginAttack;
        private MethodInfo _resolvePlayerAttackTarget;
        private MethodInfo _advancePhase;
        private MethodInfo _changePosition;
        private MethodInfo _playerUseMonsterEffect;
        private MethodInfo _resolveQuickChargeTarget;
        private MethodInfo _resolveArchmageTarget;
        private MethodInfo _playerNormalSummon;
        private MethodInfo _playerSpecialValkyrie;
        private MethodInfo _playerActivateSpell;
        private MethodInfo _playerSetTrap;
        private MethodInfo _playerDiscardForHandLimit;
        private MethodInfo _canNormalSummon;
        private MethodInfo _canSpecialValkyrie;
        private MethodInfo _canActivateSpell;
        private MethodInfo _canSetTrap;
        private MethodInfo _canAttack;
        private MethodInfo _canChangePosition;
        private MethodInfo _canUseMonsterEffect;
        private MethodInfo _clearPending;
        private MethodInfo _forfeit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (UnityEngine.Object.FindFirstObjectByType<DuelPhysicalInputController>() != null)
                return;

            GameObject host = new GameObject("Duel Physical Input Controller");
            host.AddComponent<DuelPhysicalInputController>();
        }

        private void Awake()
        {
            CacheReflection();
        }

        private void Start()
        {
            ResolveReferences();
            EnsureRoots();
        }

        private void Update()
        {
            ResolveReferences();
            EnsureRoots();

            bool active = _duel != null && _duel.IsActive;
            if (active && !_wasActive)
                EnterPhysicalDuel();
            else if (!active && _wasActive)
                ExitPhysicalDuel();

            _wasActive = active;
            if (!active)
                return;

            if (!_legacyUiSuppressed && _duel.enabled)
            {
                _duel.enabled = false;
                _legacyUiSuppressed = true;
            }

            ValidateSelections();
            if (Time.unscaledTime >= _nextSync)
            {
                _nextSync = Time.unscaledTime + 0.10f;
                SyncPhysicalHand();
                SyncTargetHitboxes();
            }

            for (int i = 0; i < _handCards.Count; i++)
                _handCards[i].SetSelected(_handCards[i].Card == _selectedHandCard);
        }

        private void OnDestroy()
        {
            RestoreLegacyBehaviour();
        }

        private void CacheReflection()
        {
            Type type = typeof(DuelGameController);
            _playerHandField = type.GetField("_playerHand", PrivateInstance);
            _pendingAttackerField = type.GetField("_pendingAttacker", PrivateInstance);
            _pendingQuickChargeField = type.GetField("_pendingQuickCharge", PrivateInstance);
            _pendingArchmageField = type.GetField("_pendingArchmage", PrivateInstance);
            _discardingField = type.GetField("_playerDiscardingForHandLimit", PrivateInstance);
            _messageField = type.GetField("_message", PrivateInstance);

            _beginAttack = type.GetMethod("BeginAttack", PrivateInstance);
            _resolvePlayerAttackTarget = type.GetMethod("ResolvePlayerAttackTarget", PrivateInstance);
            _advancePhase = type.GetMethod("AdvancePhase", PrivateInstance);
            _changePosition = type.GetMethod("ChangePosition", PrivateInstance);
            _playerUseMonsterEffect = type.GetMethod("PlayerUseMonsterEffect", PrivateInstance);
            _resolveQuickChargeTarget = type.GetMethod("ResolveQuickChargeTarget", PrivateInstance);
            _resolveArchmageTarget = type.GetMethod("ResolveArchmageTarget", PrivateInstance);
            _playerNormalSummon = type.GetMethod("PlayerNormalSummon", PrivateInstance);
            _playerSpecialValkyrie = type.GetMethod("PlayerSpecialValkyrie", PrivateInstance);
            _playerActivateSpell = type.GetMethod("PlayerActivateSpell", PrivateInstance);
            _playerSetTrap = type.GetMethod("PlayerSetTrap", PrivateInstance);
            _playerDiscardForHandLimit = type.GetMethod("PlayerDiscardForHandLimit", PrivateInstance);
            _canNormalSummon = type.GetMethod("CanNormalSummon", PrivateInstance);
            _canSpecialValkyrie = type.GetMethod("CanSpecialValkyrie", PrivateInstance);
            _canActivateSpell = type.GetMethod("CanActivateSpell", PrivateInstance);
            _canSetTrap = type.GetMethod("CanSetTrap", PrivateInstance);
            _canAttack = type.GetMethod("CanAttack", PrivateInstance);
            _canChangePosition = type.GetMethod("CanChangePosition", PrivateInstance);
            _canUseMonsterEffect = type.GetMethod("CanUseMonsterEffect", PrivateInstance);
            _clearPending = type.GetMethod("ClearPending", PrivateInstance);
            _forfeit = type.GetMethod("Forfeit", PrivateInstance);
        }

        private void ResolveReferences()
        {
            if (_duel == null)
                _duel = UnityEngine.Object.FindFirstObjectByType<DuelGameController>();

            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_camera == null)
                _camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
        }

        private void EnsureRoots()
        {
            if (_table == null)
                return;

            if (_handRoot == null)
            {
                Transform existing = _table.Find("DG Physical Player Hand");
                if (existing != null)
                    UnityEngine.Object.Destroy(existing.gameObject);

                GameObject root = new GameObject("DG Physical Player Hand");
                root.transform.SetParent(_table, false);
                _handRoot = root.transform;
                _handRoot.gameObject.SetActive(false);
            }

            if (_targetRoot == null)
            {
                Transform existing = _table.Find("DG Physical Target Hitboxes");
                if (existing != null)
                    UnityEngine.Object.Destroy(existing.gameObject);

                GameObject root = new GameObject("DG Physical Target Hitboxes");
                root.transform.SetParent(_table, false);
                _targetRoot = root.transform;
                BuildBackrowTargetHitboxes();
                _targetRoot.gameObject.SetActive(false);
            }
        }

        private void EnterPhysicalDuel()
        {
            DuelFieldSlotRegistry.Reset();

            if (_handRoot != null)
                _handRoot.gameObject.SetActive(true);
            if (_targetRoot != null)
                _targetRoot.gameObject.SetActive(true);

            _selectedHandCard = null;
            _selectedMonster = null;
            CancelPlacement();
            _handSignature = string.Empty;
            _nextSync = 0f;

            if (_duel != null && _duel.enabled)
            {
                _duel.enabled = false;
                _legacyUiSuppressed = true;
            }

            SyncPhysicalHand();
        }

        private void ExitPhysicalDuel()
        {
            _selectedHandCard = null;
            _selectedMonster = null;
            CancelPlacement();
            _handSignature = string.Empty;
            ClearChildren(_handRoot);
            _handCards.Clear();
            DuelFieldSlotRegistry.Reset();

            if (_handRoot != null)
                _handRoot.gameObject.SetActive(false);
            if (_targetRoot != null)
                _targetRoot.gameObject.SetActive(false);

            RestoreLegacyBehaviour();
        }

        private void RestoreLegacyBehaviour()
        {
            if (_duel != null && _legacyUiSuppressed)
                _duel.enabled = true;
            _legacyUiSuppressed = false;
        }

        private List<CardData> PlayerHand()
        {
            return _duel == null || _playerHandField == null
                ? null
                : _playerHandField.GetValue(_duel) as List<CardData>;
        }

        private void ValidateSelections()
        {
            List<CardData> hand = PlayerHand();
            if (_selectedHandCard != null && (hand == null || !hand.Contains(_selectedHandCard)))
                _selectedHandCard = null;
            if (_placementCard != null && (hand == null || !hand.Contains(_placementCard)))
                CancelPlacement();

            if (_selectedMonster != null && !ContainsMonster(_duel.PlayerMonsters, _selectedMonster))
                _selectedMonster = null;
        }

        private static bool ContainsMonster(IReadOnlyList<DuelMonsterState> list, DuelMonsterState target)
        {
            for (int i = 0; list != null && i < list.Count; i++)
                if (ReferenceEquals(list[i], target)) return true;
            return false;
        }

        private string BuildHandSignature(List<CardData> hand)
        {
            if (hand == null || hand.Count == 0)
                return "EMPTY";

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < hand.Count; i++)
                builder.Append(hand[i].id).Append('|');
            return builder.ToString();
        }

        private void SyncPhysicalHand()
        {
            if (_duel == null || _handRoot == null)
                return;

            List<CardData> hand = PlayerHand();
            string signature = BuildHandSignature(hand);
            if (signature == _handSignature)
                return;

            _handSignature = signature;
            ClearChildren(_handRoot);
            _handCards.Clear();

            if (hand == null || hand.Count == 0)
                return;

            float spacing = Mathf.Min(0.64f, 3.75f / Mathf.Max(1, hand.Count - 1));
            float startX = -spacing * (hand.Count - 1) * 0.5f;

            for (int i = 0; i < hand.Count; i++)
            {
                CardData card = hand[i];
                GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cardObject.name = "Hand Card - " + card.cardName;
                cardObject.transform.SetParent(_handRoot, false);
                cardObject.transform.localPosition = new Vector3(startX + spacing * i, DuelTabletopLayout.BoardSurfaceY + 0.11f, -1.63f);
                cardObject.transform.localRotation = Quaternion.Euler(-78f, 0f, 0f);
                cardObject.transform.localScale = DuelTabletopLayout.CardScale * 0.86f;

                ApplyCardMaterial(cardObject, ProductionCardArtRegistry.LoadDisplayTexture(card));

                Collider collider = cardObject.GetComponent<Collider>();
                if (collider == null)
                    collider = cardObject.AddComponent<BoxCollider>();

                DuelHandCardTarget target = cardObject.AddComponent<DuelHandCardTarget>();
                target.Configure(this, card);
                _handCards.Add(target);
            }
        }

        private void BuildBackrowTargetHitboxes()
        {
            if (_targetRoot == null)
                return;

            for (int i = 0; i < 5; i++)
            {
                GameObject hitbox = new GameObject("CPU Backrow Click Target " + (i + 1));
                hitbox.transform.SetParent(_targetRoot, false);
                hitbox.transform.localPosition = DuelTabletopLayout.BackrowZonePosition(i, false) + Vector3.up * 0.08f;
                BoxCollider collider = hitbox.AddComponent<BoxCollider>();
                collider.size = new Vector3(DuelTabletopLayout.ZoneScale.x, 0.18f, DuelTabletopLayout.ZoneScale.z);

                DuelBackrowClickTarget target = hitbox.AddComponent<DuelBackrowClickTarget>();
                target.Configure(this, i);
            }
        }

        private void SyncTargetHitboxes()
        {
            if (_targetRoot == null || _duel == null)
                return;

            bool archmageTargeting = PendingArchmage() != null;
            for (int i = 0; i < _targetRoot.childCount; i++)
            {
                GameObject child = _targetRoot.GetChild(i).gameObject;
                bool valid = archmageTargeting && i < _duel.CpuBackrow.Count;
                child.SetActive(valid);
                if (!valid) continue;

                int slot = DuelFieldSlotRegistry.GetBackrowSlot(_duel.CpuBackrow[i], false, _duel.CpuBackrow);
                child.transform.localPosition = DuelTabletopLayout.BackrowZonePosition(slot, false) + Vector3.up * 0.08f;
            }
        }

        public void ClickHandCard(CardData card)
        {
            if (_duel == null || !_duel.IsActive || card == null)
                return;

            if (IsDiscarding())
            {
                Invoke(_playerDiscardForHandLimit, card);
                _selectedHandCard = null;
                _handSignature = string.Empty;
                return;
            }

            if (HasPendingTarget())
                return;

            CancelPlacement();
            _selectedHandCard = card;
            _selectedMonster = null;
        }

        public bool ClickZone(DuelTabletopZone zone)
        {
            if (_duel == null || !_duel.IsActive || zone == null || _placementAction == PlacementAction.None)
                return false;

            if (!zone.PlayerSide)
            {
                SetMessage("Choose one of your own field zones.");
                return true;
            }

            if (!PlacementMatchesZone(zone))
            {
                SetMessage(PlacementInstruction());
                return true;
            }

            if ((zone.Kind == DuelTabletopZoneKind.Monster) &&
                DuelFieldSlotRegistry.IsMonsterSlotOccupied(_duel.PlayerMonsters, true, zone.Index))
            {
                SetMessage("That Monster Zone is already occupied. Choose another zone.");
                return true;
            }

            if (zone.Kind == DuelTabletopZoneKind.SpellTrap &&
                DuelFieldSlotRegistry.IsBackrowSlotOccupied(_duel.PlayerBackrow, true, zone.Index))
            {
                SetMessage("That Spell/Trap Zone is already occupied. Choose another zone.");
                return true;
            }

            CardData card = _placementCard;
            PlacementAction action = _placementAction;
            bool resolved = false;

            if (action == PlacementAction.NormalSummonAttack || action == PlacementAction.NormalSetDefense || action == PlacementAction.SpecialSummon)
            {
                List<DuelMonsterState> before = SnapshotMonsters(_duel.PlayerMonsters);
                if (action == PlacementAction.NormalSummonAttack)
                    Invoke(_playerNormalSummon, card, DuelMonsterPosition.FaceUpAttack);
                else if (action == PlacementAction.NormalSetDefense)
                    Invoke(_playerNormalSummon, card, DuelMonsterPosition.FaceDownDefense);
                else
                    Invoke(_playerSpecialValkyrie, card);

                DuelMonsterState added = FindNewMonster(before, _duel.PlayerMonsters);
                if (added != null)
                {
                    DuelFieldSlotRegistry.AssignMonster(added, true, zone.Index);
                    resolved = true;
                }
            }
            else if (action == PlacementAction.SetTrap || action == PlacementAction.ActivateSpell)
            {
                List<DuelBackrowState> before = SnapshotBackrow(_duel.PlayerBackrow);
                if (action == PlacementAction.SetTrap)
                    Invoke(_playerSetTrap, card);
                else
                    Invoke(_playerActivateSpell, card);

                DuelBackrowState added = FindNewBackrow(before, _duel.PlayerBackrow);
                if (added != null && zone.Kind == DuelTabletopZoneKind.SpellTrap)
                    DuelFieldSlotRegistry.AssignBackrow(added, true, zone.Index);

                // Normal/Quick-Play spells can resolve immediately to the GY and therefore
                // do not leave a permanent backrow state, but the chosen zone still served
                // as the activation location.
                resolved = PlayerHand() == null || !PlayerHand().Contains(card) || added != null || PendingQuickCharge() != null;
            }

            if (resolved)
            {
                _selectedHandCard = null;
                CancelPlacement();
                _handSignature = string.Empty;
            }

            return true;
        }

        private bool PlacementMatchesZone(DuelTabletopZone zone)
        {
            switch (_placementAction)
            {
                case PlacementAction.NormalSummonAttack:
                case PlacementAction.NormalSetDefense:
                case PlacementAction.SpecialSummon:
                    return zone.Kind == DuelTabletopZoneKind.Monster;
                case PlacementAction.SetTrap:
                    return zone.Kind == DuelTabletopZoneKind.SpellTrap;
                case PlacementAction.ActivateSpell:
                    return IsFieldSpell(_placementCard)
                        ? zone.Kind == DuelTabletopZoneKind.FieldSpell
                        : zone.Kind == DuelTabletopZoneKind.SpellTrap;
                default:
                    return false;
            }
        }

        private void BeginPlacement(CardData card, PlacementAction action)
        {
            _placementCard = card;
            _placementAction = action;
            SetMessage(PlacementInstruction());
        }

        private void CancelPlacement()
        {
            _placementAction = PlacementAction.None;
            _placementCard = null;
        }

        private string PlacementInstruction()
        {
            switch (_placementAction)
            {
                case PlacementAction.NormalSummonAttack:
                    return "Choose an empty Monster Zone for the Summon.";
                case PlacementAction.NormalSetDefense:
                    return "Choose an empty Monster Zone to Set the monster.";
                case PlacementAction.SpecialSummon:
                    return "Choose an empty Monster Zone for the Special Summon.";
                case PlacementAction.SetTrap:
                    return "Choose an empty Spell/Trap Zone to Set the card.";
                case PlacementAction.ActivateSpell:
                    return IsFieldSpell(_placementCard)
                        ? "Choose your Field Zone to activate this Field Spell."
                        : "Choose an empty Spell/Trap Zone to activate this Spell.";
                default:
                    return "Choose a field zone.";
            }
        }

        private static bool IsFieldSpell(CardData card)
        {
            if (card == null || card.kind != CardKind.Spell)
                return false;
            string line = card.typeLine ?? string.Empty;
            return line.IndexOf("Field", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<DuelMonsterState> SnapshotMonsters(IReadOnlyList<DuelMonsterState> list)
        {
            List<DuelMonsterState> result = new();
            for (int i = 0; list != null && i < list.Count; i++) result.Add(list[i]);
            return result;
        }

        private static List<DuelBackrowState> SnapshotBackrow(IReadOnlyList<DuelBackrowState> list)
        {
            List<DuelBackrowState> result = new();
            for (int i = 0; list != null && i < list.Count; i++) result.Add(list[i]);
            return result;
        }

        private static DuelMonsterState FindNewMonster(List<DuelMonsterState> before, IReadOnlyList<DuelMonsterState> after)
        {
            for (int i = 0; after != null && i < after.Count; i++)
            {
                DuelMonsterState candidate = after[i];
                bool existed = false;
                for (int j = 0; j < before.Count; j++)
                    if (ReferenceEquals(before[j], candidate)) { existed = true; break; }
                if (!existed) return candidate;
            }
            return null;
        }

        private static DuelBackrowState FindNewBackrow(List<DuelBackrowState> before, IReadOnlyList<DuelBackrowState> after)
        {
            for (int i = 0; after != null && i < after.Count; i++)
            {
                DuelBackrowState candidate = after[i];
                bool existed = false;
                for (int j = 0; j < before.Count; j++)
                    if (ReferenceEquals(before[j], candidate)) { existed = true; break; }
                if (!existed) return candidate;
            }
            return null;
        }

        public void ClickMonster(bool playerSide, DuelMonsterState monster)
        {
            if (_duel == null || !_duel.IsActive || monster == null)
                return;

            if (_placementAction != PlacementAction.None)
                return;

            if (playerSide)
            {
                if (PendingQuickCharge() != null)
                {
                    Invoke(_resolveQuickChargeTarget, monster);
                    _selectedMonster = null;
                    return;
                }

                if (PendingAttacker() != null)
                    return;

                _selectedMonster = monster;
                _selectedHandCard = null;
                return;
            }

            if (PendingAttacker() != null)
            {
                Invoke(_resolvePlayerAttackTarget, monster);
                _selectedMonster = null;
            }
        }

        public void ClickCpuBackrow(int index)
        {
            if (_duel == null || PendingArchmage() == null || index < 0 || index >= _duel.CpuBackrow.Count)
                return;

            Invoke(_resolveArchmageTarget, _duel.CpuBackrow[index]);
            _selectedMonster = null;
        }

        private DuelMonsterState PendingAttacker() => _pendingAttackerField?.GetValue(_duel) as DuelMonsterState;
        private CardData PendingQuickCharge() => _pendingQuickChargeField?.GetValue(_duel) as CardData;
        private DuelMonsterState PendingArchmage() => _pendingArchmageField?.GetValue(_duel) as DuelMonsterState;

        private bool IsDiscarding()
        {
            return _discardingField != null && _duel != null && (bool)_discardingField.GetValue(_duel);
        }

        private bool HasPendingTarget()
        {
            return PendingAttacker() != null || PendingQuickCharge() != null || PendingArchmage() != null;
        }

        private string Message()
        {
            return _messageField?.GetValue(_duel) as string ?? string.Empty;
        }

        private void SetMessage(string value)
        {
            if (_duel != null && _messageField != null)
                _messageField.SetValue(_duel, value ?? string.Empty);
        }

        private bool CallBool(MethodInfo method, params object[] args)
        {
            if (_duel == null || method == null)
                return false;
            try
            {
                object value = method.Invoke(_duel, args);
                return value is bool result && result;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private void Invoke(MethodInfo method, params object[] args)
        {
            if (_duel == null || method == null)
                return;
            try
            {
                method.Invoke(_duel, args);
                _handSignature = string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnGUI()
        {
            if (_duel == null || !_duel.IsActive)
                return;

            GUI.depth = -500;
            GUIStyle hud = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUIStyle message = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            DrawTopHud(hud, message);

            if (_duel.IsDuelOver)
            {
                DrawDuelOver(hud, message);
                return;
            }

            DrawPhaseButton();
            DrawContextActions(hud, message);
        }

        private void DrawTopHud(GUIStyle hud, GUIStyle message)
        {
            Rect top = new Rect(Screen.width * 0.5f - 360f, 12f, 720f, 74f);
            GenesisTheme.Box(top, new Color(0.025f, 0.035f, 0.065f, 0.90f));
            GUI.Label(new Rect(top.x + 10f, top.y + 5f, top.width - 20f, 28f),
                $"YOU  {_duel.PlayerLifePoints:N0} LP     •     TURN {_duel.TurnNumber}  {PhaseText()}     •     CPU  {_duel.CpuLifePoints:N0} LP", hud);
            GUI.Label(new Rect(top.x + 14f, top.y + 34f, top.width - 28f, 34f), Message(), message);
        }

        private string PhaseText()
        {
            if (IsDiscarding()) return "END PHASE";
            return _duel.Phase switch
            {
                DuelTurnPhase.Main => "MAIN PHASE",
                DuelTurnPhase.Battle => "BATTLE PHASE",
                DuelTurnPhase.Opponent => "CPU TURN",
                DuelTurnPhase.Finished => "DUEL COMPLETE",
                _ => _duel.Phase.ToString().ToUpperInvariant()
            };
        }

        private void DrawPhaseButton()
        {
            if (_duel.Phase == DuelTurnPhase.Opponent || _duel.Phase == DuelTurnPhase.Finished || IsDiscarding())
                return;

            string label = _duel.Phase == DuelTurnPhase.Main
                ? (_duel.TurnNumber == 1 ? "END TURN" : "BATTLE PHASE")
                : "END TURN";

            if (GenesisTheme.Button(new Rect(Screen.width - 190f, 22f, 160f, 34f), label, GenesisTheme.Cyan))
            {
                _selectedHandCard = null;
                _selectedMonster = null;
                CancelPlacement();
                Invoke(_advancePhase);
            }
        }

        private void DrawContextActions(GUIStyle hud, GUIStyle message)
        {
            if (_placementAction != PlacementAction.None)
            {
                Rect place = new Rect(Screen.width * 0.5f - 270f, Screen.height - 88f, 540f, 64f);
                GenesisTheme.Box(place, new Color(0.04f, 0.07f, 0.12f, 0.96f));
                GUI.Label(new Rect(place.x + 12f, place.y + 7f, place.width - 118f, 48f), PlacementInstruction(), hud);
                if (GenesisTheme.Button(new Rect(place.xMax - 98f, place.y + 16f, 82f, 32f), "CANCEL", GenesisTheme.Danger))
                    CancelPlacement();
                return;
            }

            if (HasPendingTarget())
            {
                Rect target = new Rect(Screen.width * 0.5f - 235f, Screen.height - 92f, 470f, 70f);
                GenesisTheme.Box(target, new Color(0.05f, 0.08f, 0.14f, 0.94f));

                string instruction = PendingAttacker() != null
                    ? "CLICK AN ENEMY MONSTER TO ATTACK IT"
                    : PendingQuickCharge() != null
                        ? "CLICK ONE OF YOUR MONSTERS"
                        : "CLICK AN ENEMY SPELL/TRAP";
                GUI.Label(new Rect(target.x + 12f, target.y + 7f, target.width - 122f, 50f), instruction, hud);
                if (GenesisTheme.Button(new Rect(target.xMax - 104f, target.y + 18f, 88f, 32f), "CANCEL", GenesisTheme.Danger))
                    Invoke(_clearPending);
                return;
            }

            if (IsDiscarding())
            {
                Rect discard = new Rect(Screen.width * 0.5f - 260f, Screen.height - 78f, 520f, 54f);
                GenesisTheme.Box(discard, new Color(0.18f, 0.05f, 0.07f, 0.94f));
                GUI.Label(discard, "CLICK A CARD IN YOUR HAND TO DISCARD IT", hud);
                return;
            }

            if (_selectedHandCard != null)
            {
                DrawHandActions(hud, message);
                return;
            }

            if (_selectedMonster != null)
            {
                DrawMonsterActions(hud, message);
                return;
            }

            Rect hint = new Rect(Screen.width * 0.5f - 285f, Screen.height - 55f, 570f, 34f);
            GenesisTheme.Box(hint, new Color(0.025f, 0.035f, 0.065f, 0.78f));
            GUI.Label(hint, "CLICK A CARD IN YOUR HAND OR A MONSTER ON THE FIELD", message);
        }

        private void DrawHandActions(GUIStyle hud, GUIStyle message)
        {
            CardData card = _selectedHandCard;
            Rect panel = new Rect(Screen.width * 0.5f - 330f, Screen.height - 112f, 660f, 90f);
            GenesisTheme.Box(panel, new Color(0.035f, 0.055f, 0.095f, 0.96f));
            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 26f), card.cardName, hud);

            float y = panel.y + 40f;
            if (card.kind == CardKind.Monster)
            {
                bool canSummon = CallBool(_canNormalSummon, card);
                GUI.enabled = canSummon;
                if (GenesisTheme.Button(new Rect(panel.x + 24f, y, 180f, 34f), "SUMMON — ATTACK", GenesisTheme.Cyan))
                    BeginPlacement(card, PlacementAction.NormalSummonAttack);
                if (GenesisTheme.Button(new Rect(panel.x + 214f, y, 180f, 34f), "SET — DEFENSE", GenesisTheme.Purple))
                    BeginPlacement(card, PlacementAction.NormalSetDefense);
                GUI.enabled = true;

                bool canSpecial = card.id == "DG020" && CallBool(_canSpecialValkyrie, card, true);
                GUI.enabled = canSpecial;
                if (GenesisTheme.Button(new Rect(panel.x + 404f, y, 150f, 34f), "SPECIAL", GenesisTheme.Gold))
                    BeginPlacement(card, PlacementAction.SpecialSummon);
                GUI.enabled = true;
            }
            else if (card.kind == CardKind.Spell)
            {
                bool canActivate = CallBool(_canActivateSpell, card);
                GUI.enabled = canActivate;
                if (GenesisTheme.Button(new Rect(panel.center.x - 100f, y, 200f, 34f), "ACTIVATE SPELL", GenesisTheme.Green))
                    BeginPlacement(card, PlacementAction.ActivateSpell);
                GUI.enabled = true;
            }
            else
            {
                bool canSet = CallBool(_canSetTrap, card);
                GUI.enabled = canSet;
                if (GenesisTheme.Button(new Rect(panel.center.x - 100f, y, 200f, 34f), "SET TRAP", GenesisTheme.Purple))
                    BeginPlacement(card, PlacementAction.SetTrap);
                GUI.enabled = true;
            }

            if (GenesisTheme.Button(new Rect(panel.xMax - 92f, panel.y + 6f, 76f, 26f), "CLOSE", GenesisTheme.Muted))
            {
                CancelPlacement();
                _selectedHandCard = null;
            }
        }

        private void DrawMonsterActions(GUIStyle hud, GUIStyle message)
        {
            DuelMonsterState monster = _selectedMonster;
            Rect panel = new Rect(Screen.width * 0.5f - 300f, Screen.height - 112f, 600f, 90f);
            GenesisTheme.Box(panel, new Color(0.035f, 0.055f, 0.095f, 0.96f));
            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 26f), monster.Card.cardName, hud);

            float y = panel.y + 40f;
            if (_duel.Phase == DuelTurnPhase.Battle)
            {
                bool canAttack = CallBool(_canAttack, monster);
                GUI.enabled = canAttack;
                if (GenesisTheme.Button(new Rect(panel.center.x - 105f, y, 210f, 34f), "ATTACK", GenesisTheme.Danger))
                    Invoke(_beginAttack, monster);
                GUI.enabled = true;
            }
            else if (_duel.Phase == DuelTurnPhase.Main)
            {
                bool canPosition = CallBool(_canChangePosition, monster);
                GUI.enabled = canPosition;
                if (GenesisTheme.Button(new Rect(panel.x + 82f, y, 180f, 34f), "CHANGE POSITION", GenesisTheme.Cyan))
                    Invoke(_changePosition, monster);

                bool canEffect = CallBool(_canUseMonsterEffect, monster, true);
                GUI.enabled = canEffect;
                if (GenesisTheme.Button(new Rect(panel.x + 278f, y, 180f, 34f), "USE EFFECT", GenesisTheme.Purple))
                    Invoke(_playerUseMonsterEffect, monster);
                GUI.enabled = true;
            }

            if (GenesisTheme.Button(new Rect(panel.xMax - 92f, panel.y + 6f, 76f, 26f), "CLOSE", GenesisTheme.Muted))
                _selectedMonster = null;
        }

        private void DrawDuelOver(GUIStyle hud, GUIStyle message)
        {
            Rect panel = new Rect(Screen.width * 0.5f - 270f, Screen.height * 0.5f - 75f, 540f, 150f);
            GenesisTheme.Box(panel, new Color(0.035f, 0.05f, 0.09f, 0.97f));
            GUI.Label(new Rect(panel.x + 16f, panel.y + 16f, panel.width - 32f, 66f), Message(), message);
            if (GenesisTheme.Button(new Rect(panel.center.x - 95f, panel.yMax - 52f, 190f, 34f), "RETURN TO TABLE", GenesisTheme.Purple))
                _duel.CloseDuel();
        }

        private static void ApplyCardMaterial(GameObject obj, Texture2D texture)
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
                UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }
    }

    public sealed class DuelHandCardTarget : MonoBehaviour
    {
        private DuelPhysicalInputController _controller;
        private Vector3 _baseScale;
        private Vector3 _basePosition;
        public CardData Card { get; private set; }

        public void Configure(DuelPhysicalInputController controller, CardData card)
        {
            _controller = controller;
            Card = card;
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
        }

        public void SetSelected(bool selected)
        {
            transform.localScale = selected ? _baseScale * 1.14f : _baseScale;
            Vector3 position = _basePosition;
            if (selected) position.y += 0.10f;
            transform.localPosition = position;
        }

        private void OnMouseDown() => _controller?.ClickHandCard(Card);

        private void OnMouseEnter()
        {
            if (_controller == null) return;
            transform.localScale = _baseScale * 1.08f;
        }

        private void OnMouseExit() => transform.localScale = _baseScale;
    }

    public sealed class DuelMonsterClickTarget : MonoBehaviour
    {
        private DuelPhysicalInputController _controller;
        private bool _playerSide;
        private DuelMonsterState _monster;

        public void Configure(bool playerSide, DuelMonsterState monster)
        {
            _playerSide = playerSide;
            _monster = monster;
            _controller = UnityEngine.Object.FindFirstObjectByType<DuelPhysicalInputController>();
        }

        private void OnMouseDown()
        {
            if (_controller == null)
                _controller = UnityEngine.Object.FindFirstObjectByType<DuelPhysicalInputController>();
            _controller?.ClickMonster(_playerSide, _monster);
        }
    }

    public sealed class DuelBackrowClickTarget : MonoBehaviour
    {
        private DuelPhysicalInputController _controller;
        private int _index;

        public void Configure(DuelPhysicalInputController controller, int index)
        {
            _controller = controller;
            _index = index;
        }

        private void OnMouseDown() => _controller?.ClickCpuBackrow(_index);
    }
}
