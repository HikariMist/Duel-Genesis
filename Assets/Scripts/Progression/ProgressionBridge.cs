using DuelGenesis.Dueling;
using DuelGenesis.Shops;
using UnityEngine;

namespace DuelGenesis.Progression
{
    public class ProgressionBridge : MonoBehaviour
    {
        private DuelistProfile _profile;
        private PackOpeningUI _packOpening;
        private DuelGameController _duel;
        private DuelGameController _subscribedDuel;
        private bool _wasPackOpen;

        private void Start()
        {
            Resolve();
        }

        private void OnDestroy()
        {
            if (_subscribedDuel != null)
                _subscribedDuel.DuelFinished -= OnDuelFinished;
        }

        private void Update()
        {
            Resolve();

            if (_packOpening != null)
            {
                bool packOpen = _packOpening.IsOpen;
                if (packOpen && !_wasPackOpen)
                    _profile?.RecordPackOpened();
                _wasPackOpen = packOpen;
            }
        }

        private void Resolve()
        {
            if (_profile == null)
                _profile = Object.FindFirstObjectByType<DuelistProfile>();
            if (_packOpening == null)
                _packOpening = Object.FindFirstObjectByType<PackOpeningUI>();
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            if (_duel != null && _subscribedDuel != _duel)
            {
                if (_subscribedDuel != null)
                    _subscribedDuel.DuelFinished -= OnDuelFinished;

                _subscribedDuel = _duel;
                _subscribedDuel.DuelFinished += OnDuelFinished;
            }
        }

        private void OnDuelFinished(bool playerWon)
        {
            Resolve();
            _profile?.RecordDuelResult(playerWon);
        }
    }
}
