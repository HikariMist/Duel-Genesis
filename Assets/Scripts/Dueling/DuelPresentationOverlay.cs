using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelPresentationOverlay : MonoBehaviour
    {
        private DuelGameController _duel;
        private bool _wasActive;
        private DuelTurnPhase _lastPhase;
        private int _lastTurn;
        private int _lastPlayerLP;
        private int _lastCpuLP;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private int _playerDelta;
        private int _cpuDelta;
        private float _deltaUntil;

        private void Update()
        {
            if (_duel == null)
                _duel = UnityEngine.Object.FindFirstObjectByType<DuelGameController>();
            if (_duel == null) return;

            if (_duel.IsActive && !_wasActive)
            {
                _wasActive = true;
                _lastPhase = _duel.Phase;
                _lastTurn = _duel.TurnNumber;
                _lastPlayerLP = _duel.PlayerLifePoints;
                _lastCpuLP = _duel.CpuLifePoints;
                ShowBanner("DUEL START", 1.35f);
                return;
            }

            if (!_duel.IsActive)
            {
                _wasActive = false;
                return;
            }

            if (_duel.TurnNumber != _lastTurn || _duel.Phase != _lastPhase)
            {
                _lastTurn = _duel.TurnNumber;
                _lastPhase = _duel.Phase;
                ShowBanner(BannerForPhase(_duel.Phase, _duel.TurnNumber), 1.0f);
            }

            int playerLP = _duel.PlayerLifePoints;
            int cpuLP = _duel.CpuLifePoints;
            int playerChange = playerLP - _lastPlayerLP;
            int cpuChange = cpuLP - _lastCpuLP;
            if (playerChange != 0 || cpuChange != 0)
            {
                _playerDelta = playerChange;
                _cpuDelta = cpuChange;
                _deltaUntil = Time.unscaledTime + 1.35f;
                _lastPlayerLP = playerLP;
                _lastCpuLP = cpuLP;
            }
        }

        private void ShowBanner(string text, float duration)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + duration;
        }

        private static string BannerForPhase(DuelTurnPhase phase, int turn)
        {
            return phase switch
            {
                DuelTurnPhase.Main => $"TURN {turn} — YOUR MAIN PHASE",
                DuelTurnPhase.Battle => "BATTLE PHASE",
                DuelTurnPhase.Opponent => "CPU TURN",
                DuelTurnPhase.Finished => "DUEL COMPLETE",
                _ => phase.ToString().ToUpperInvariant()
            };
        }

        private void OnGUI()
        {
            if (_duel == null || !_duel.IsActive) return;
            GUI.depth = -460;

            GUIStyle bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUIStyle deltaStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            if (Time.unscaledTime < _bannerUntil && !string.IsNullOrWhiteSpace(_banner))
            {
                float width = Mathf.Min(620f, Screen.width - 80f);
                Rect banner = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.18f, width, 64f);
                GenesisTheme.Box(banner, new Color(0.08f, 0.10f, 0.18f, 0.92f));
                GUI.Label(banner, _banner, bannerStyle);
            }

            if (Time.unscaledTime < _deltaUntil)
            {
                if (_playerDelta != 0)
                {
                    deltaStyle.normal.textColor = _playerDelta < 0 ? GenesisTheme.Danger : GenesisTheme.Green;
                    string text = _playerDelta > 0 ? $"+{_playerDelta} LP" : $"{_playerDelta} LP";
                    GUI.Label(new Rect(32f, 76f, 200f, 42f), text, deltaStyle);
                }

                if (_cpuDelta != 0)
                {
                    deltaStyle.normal.textColor = _cpuDelta < 0 ? GenesisTheme.Danger : GenesisTheme.Green;
                    string text = _cpuDelta > 0 ? $"+{_cpuDelta} LP" : $"{_cpuDelta} LP";
                    GUI.Label(new Rect(Screen.width - 232f, 76f, 200f, 42f), text, deltaStyle);
                }
            }
        }
    }
}
