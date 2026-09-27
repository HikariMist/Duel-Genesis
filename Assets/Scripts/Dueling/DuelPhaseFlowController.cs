using System;
using System.Reflection;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public enum DuelPlayerFlowPhase
    {
        Draw,
        MainPhase1,
        Battle,
        MainPhase2,
        End,
        Opponent,
        Finished
    }

    /// <summary>
    /// Presents a real Yu-Gi-Oh-style player turn flow in front of the existing duel engine:
    /// Draw -> Main Phase 1 -> Battle -> Main Phase 2 -> End.
    /// The prototype engine still uses Main/Battle/Opponent internally, so this coordinator
    /// maps Main Phase 1 and Main Phase 2 onto its existing Main rules without duplicating them.
    /// </summary>
    public sealed class DuelPhaseFlowController : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private DuelGameController _duel;
        private FieldInfo _enginePhaseField;
        private FieldInfo _discardingField;
        private FieldInfo _messageField;
        private MethodInfo _endPlayerTurn;

        private DuelPlayerFlowPhase _phase = DuelPlayerFlowPhase.Draw;
        private int _trackedTurn = -1;
        private bool _wasActive;

        public DuelPlayerFlowPhase CurrentPhase => _phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelPhaseFlowController>() != null)
                return;

            GameObject host = new GameObject("Duel Phase Flow Controller");
            host.AddComponent<DuelPhaseFlowController>();
        }

        private void Awake()
        {
            Type type = typeof(DuelGameController);
            _enginePhaseField = type.GetField("_phase", PrivateInstance);
            _discardingField = type.GetField("_playerDiscardingForHandLimit", PrivateInstance);
            _messageField = type.GetField("_message", PrivateInstance);
            _endPlayerTurn = type.GetMethod("EndPlayerTurn", PrivateInstance);
        }

        private void Update()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            bool active = _duel != null && _duel.IsActive;
            if (active && !_wasActive)
                BeginDuelFlow();
            else if (!active && _wasActive)
                ResetFlow();

            _wasActive = active;
            if (!active)
                return;

            if (_duel.IsDuelOver)
            {
                _phase = DuelPlayerFlowPhase.Finished;
                return;
            }

            // EndPlayerTurn/CPU processing is synchronous in the prototype engine.
            // When control returns with a new player turn already prepared, show Draw Phase first.
            if (_duel.TurnNumber != _trackedTurn && _duel.Phase == DuelTurnPhase.Main && !IsDiscarding())
                BeginPlayerDrawPhase();
        }

        private void BeginDuelFlow()
        {
            _trackedTurn = _duel.TurnNumber;
            _phase = DuelPlayerFlowPhase.Draw;
            SetEnginePhase(DuelTurnPhase.Opponent);
            SetMessage("Turn 1 — DRAW PHASE. Opening hand is ready. The starting player does not draw on the first turn.");
        }

        private void ResetFlow()
        {
            _phase = DuelPlayerFlowPhase.Draw;
            _trackedTurn = -1;
        }

        private void BeginPlayerDrawPhase()
        {
            _trackedTurn = _duel.TurnNumber;
            _phase = DuelPlayerFlowPhase.Draw;
            SetEnginePhase(DuelTurnPhase.Opponent);
            SetMessage($"Turn {_trackedTurn} — DRAW PHASE. Your drawn card has been added to your hand.");
        }

        private bool IsDiscarding()
        {
            return _duel != null && _discardingField != null && (bool)_discardingField.GetValue(_duel);
        }

        private void SetEnginePhase(DuelTurnPhase phase)
        {
            if (_duel != null && _enginePhaseField != null)
                _enginePhaseField.SetValue(_duel, phase);
        }

        private void SetMessage(string message)
        {
            if (_duel != null && _messageField != null)
                _messageField.SetValue(_duel, message);
        }

        private void AdvancePhase()
        {
            if (_duel == null || !_duel.IsActive || _duel.IsDuelOver || IsDiscarding())
                return;

            switch (_phase)
            {
                case DuelPlayerFlowPhase.Draw:
                    _phase = DuelPlayerFlowPhase.MainPhase1;
                    SetEnginePhase(DuelTurnPhase.Main);
                    SetMessage($"Turn {_duel.TurnNumber} — MAIN PHASE 1. Summon, Set, or activate cards.");
                    break;

                case DuelPlayerFlowPhase.MainPhase1:
                    if (_duel.TurnNumber == 1)
                    {
                        _phase = DuelPlayerFlowPhase.End;
                        SetEnginePhase(DuelTurnPhase.Opponent);
                        SetMessage("END PHASE. Battle Phase is skipped on the first turn.");
                    }
                    else
                    {
                        _phase = DuelPlayerFlowPhase.Battle;
                        SetEnginePhase(DuelTurnPhase.Battle);
                        SetMessage("BATTLE PHASE. Select one of your monsters, choose ATTACK, then choose a target.");
                    }
                    break;

                case DuelPlayerFlowPhase.Battle:
                    _phase = DuelPlayerFlowPhase.MainPhase2;
                    SetEnginePhase(DuelTurnPhase.Main);
                    SetMessage("MAIN PHASE 2. You may Summon, Set, change positions, or activate cards before ending your turn.");
                    break;

                case DuelPlayerFlowPhase.MainPhase2:
                    _phase = DuelPlayerFlowPhase.End;
                    SetEnginePhase(DuelTurnPhase.Opponent);
                    SetMessage("END PHASE. Resolve the hand limit, then end your turn.");
                    break;

                case DuelPlayerFlowPhase.End:
                    ResolveEndPhase();
                    break;
            }
        }

        private void ResolveEndPhase()
        {
            if (_duel == null || _endPlayerTurn == null)
                return;

            // EndPlayerTurn checks the five-card hand limit first. If a discard is required,
            // DuelPhysicalInputController already allows clicking hand cards to discard them.
            _endPlayerTurn.Invoke(_duel, null);

            if (IsDiscarding())
            {
                _phase = DuelPlayerFlowPhase.End;
                SetEnginePhase(DuelTurnPhase.Opponent);
                return;
            }

            // The CPU turn runs synchronously. Update() will detect the new player turn and
            // place the presentation back into Draw Phase on the next frame.
            _phase = DuelPlayerFlowPhase.Opponent;
        }

        private string NextButtonLabel()
        {
            return _phase switch
            {
                DuelPlayerFlowPhase.Draw => "MAIN PHASE 1",
                DuelPlayerFlowPhase.MainPhase1 => _duel != null && _duel.TurnNumber == 1 ? "END PHASE" : "BATTLE PHASE",
                DuelPlayerFlowPhase.Battle => "MAIN PHASE 2",
                DuelPlayerFlowPhase.MainPhase2 => "END PHASE",
                DuelPlayerFlowPhase.End => "END TURN",
                _ => string.Empty
            };
        }

        private void OnGUI()
        {
            if (_duel == null || !_duel.IsActive || _duel.IsDuelOver)
                return;

            GUI.depth = -1200;

            DrawPhaseHeader();
            DrawPhaseTrack();
            DrawAdvanceControl();
        }

        private void DrawPhaseHeader()
        {
            Rect panel = new Rect(Screen.width * 0.5f - 385f, 10f, 770f, 78f);
            GenesisTheme.Box(panel, new Color(0.02f, 0.03f, 0.06f, 0.97f));

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUIStyle phase = new GUIStyle(title)
            {
                fontSize = 20,
                normal = { textColor = GenesisTheme.Cyan }
            };

            GUI.Label(new Rect(panel.x + 10f, panel.y + 4f, panel.width - 20f, 30f),
                $"YOU {_duel.PlayerLifePoints:N0} LP        TURN {_duel.TurnNumber}        CPU {_duel.CpuLifePoints:N0} LP", title);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 36f, panel.width - 20f, 32f), PhaseLabel(_phase), phase);
        }

        private void DrawPhaseTrack()
        {
            const float width = 104f;
            const float gap = 6f;
            float total = width * 5f + gap * 4f;
            float x = (Screen.width - total) * 0.5f;
            float y = 96f;

            DrawPhaseBox(new Rect(x + (width + gap) * 0f, y, width, 30f), "DRAW", _phase == DuelPlayerFlowPhase.Draw);
            DrawPhaseBox(new Rect(x + (width + gap) * 1f, y, width, 30f), "MAIN 1", _phase == DuelPlayerFlowPhase.MainPhase1);
            DrawPhaseBox(new Rect(x + (width + gap) * 2f, y, width, 30f), "BATTLE", _phase == DuelPlayerFlowPhase.Battle);
            DrawPhaseBox(new Rect(x + (width + gap) * 3f, y, width, 30f), "MAIN 2", _phase == DuelPlayerFlowPhase.MainPhase2);
            DrawPhaseBox(new Rect(x + (width + gap) * 4f, y, width, 30f), "END", _phase == DuelPlayerFlowPhase.End);
        }

        private static void DrawPhaseBox(Rect rect, string label, bool active)
        {
            Color color = active ? GenesisTheme.Cyan : new Color(0.11f, 0.13f, 0.18f, 0.96f);
            GenesisTheme.Box(rect, color);
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = active ? Color.black : Color.white }
            };
            GUI.Label(rect, label, style);
        }

        private void DrawAdvanceControl()
        {
            if (_phase == DuelPlayerFlowPhase.Opponent || _phase == DuelPlayerFlowPhase.Finished || IsDiscarding())
                return;

            string label = NextButtonLabel();
            if (string.IsNullOrEmpty(label))
                return;

            // This panel intentionally covers the old prototype phase button underneath it.
            Rect panel = new Rect(Screen.width - 235f, 12f, 220f, 126f);
            GenesisTheme.Box(panel, new Color(0.02f, 0.03f, 0.06f, 0.99f));

            GUIStyle small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = GenesisTheme.Muted }
            };
            GUI.Label(new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, 28f), "NEXT PHASE", small);

            if (GenesisTheme.Button(new Rect(panel.x + 14f, panel.y + 44f, panel.width - 28f, 56f), label, GenesisTheme.Cyan))
                AdvancePhase();
        }

        private static string PhaseLabel(DuelPlayerFlowPhase phase)
        {
            return phase switch
            {
                DuelPlayerFlowPhase.Draw => "DRAW PHASE",
                DuelPlayerFlowPhase.MainPhase1 => "MAIN PHASE 1",
                DuelPlayerFlowPhase.Battle => "BATTLE PHASE",
                DuelPlayerFlowPhase.MainPhase2 => "MAIN PHASE 2",
                DuelPlayerFlowPhase.End => "END PHASE",
                DuelPlayerFlowPhase.Opponent => "OPPONENT TURN",
                DuelPlayerFlowPhase.Finished => "DUEL COMPLETE",
                _ => phase.ToString().ToUpperInvariant()
            };
        }
    }
}
