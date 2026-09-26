using DuelGenesis.Dueling;
using DuelGenesis.Economy;
using DuelGenesis.Shops;
using UnityEngine;

namespace DuelGenesis.Progression
{
    public class ProgressionBridge : MonoBehaviour
    {
        private DuelistProfile _profile;
        private PackOpeningUI _packOpening;
        private DuelPrototype _duel;
        private GenesisWallet _wallet;

        private bool _wasPackOpen;
        private bool _duelRewardRecorded;
        private bool _duelSeenActive;
        private int _lastBalance;

        private void Start()
        {
            Resolve();
            if (_wallet != null)
            {
                _lastBalance = _wallet.GenesisCredits;
                _wallet.BalanceChanged += OnBalanceChanged;
            }
        }

        private void OnDestroy()
        {
            if (_wallet != null)
                _wallet.BalanceChanged -= OnBalanceChanged;
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

            if (_duel != null)
            {
                if (_duel.IsActive && !_duelSeenActive)
                {
                    _duelSeenActive = true;
                    _duelRewardRecorded = false;
                }

                if (!_duel.IsActive && _duelSeenActive)
                    _duelSeenActive = false;
            }
        }

        private void Resolve()
        {
            if (_profile == null)
                _profile = Object.FindFirstObjectByType<DuelistProfile>();
            if (_packOpening == null)
                _packOpening = Object.FindFirstObjectByType<PackOpeningUI>();
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelPrototype>();

            if (_wallet == null)
            {
                GenesisWallet found = Object.FindFirstObjectByType<GenesisWallet>();
                if (found != null)
                {
                    _wallet = found;
                    _lastBalance = _wallet.GenesisCredits;
                    _wallet.BalanceChanged += OnBalanceChanged;
                }
            }
        }

        private void OnBalanceChanged(int newBalance)
        {
            int delta = newBalance - _lastBalance;
            _lastBalance = newBalance;

            if (_profile == null || _duel == null || !_duel.IsActive || !_duel.IsDuelOver || _duelRewardRecorded)
                return;

            if (delta == 400)
            {
                _profile.RecordDuelResult(true);
                _duelRewardRecorded = true;
            }
            else if (delta == 200)
            {
                _profile.RecordDuelResult(false);
                _duelRewardRecorded = true;
            }
        }
    }
}
