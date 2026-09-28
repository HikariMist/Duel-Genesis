using DuelGenesis.Cards;
using DuelGenesis.Interaction;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// A duel table out in Genesis City. Each one has its own resident duelist (a fixed CPU deck seed).
    /// Interacting brings the playable table (seat, mat, cards, holograms) to this spot, hides the
    /// decorative table underneath, and starts the duel; afterwards the playable table goes home.
    /// </summary>
    public sealed class CityDuelTable : MonoBehaviour, IInteractable
    {
        public int opponentSeed = 1;
        public string tableName = "Street Duel Table";

        private DuelTableSeat _seat;
        private DuelBoardView _board;
        private DuelGameController _duel;
        private AmbientDuelTable _decor;
        private string _opponentName;
        private bool _hosting;

        public string OpponentName
        {
            get
            {
                if (_opponentName == null && CardDatabase.All != null && CardDatabase.All.Count > 0)
                    _opponentName = CpuDeckBuilder.Build(CardDatabase.All, opponentSeed).name;
                return _opponentName;
            }
        }

        public string InteractionPrompt
        {
            get
            {
                if (_hosting) return "Duel in progress";
                Resolve();
                if (_duel != null && _duel.IsActive) return tableName + " (another duel is running)";
                return OpponentName != null ? $"Duel the {OpponentName}" : "Start Duel";
            }
        }

        private void Resolve()
        {
            if (_seat == null) _seat = FindFirstObjectByType<DuelTableSeat>();
            if (_board == null) _board = FindFirstObjectByType<DuelBoardView>();
            if (_duel == null) _duel = FindFirstObjectByType<DuelGameController>();
            if (_decor == null) _decor = GetComponent<AmbientDuelTable>();
        }

        public void Interact(GameObject interactor)
        {
            Resolve();
            if (_hosting || _seat == null || _board == null || _duel == null || _duel.IsActive || _seat.IsOccupied) return;
            if (!_board.MoveTableTo(transform)) return;
            _decor?.SetVisible(false);
            _duel.NextCpuSeed = opponentSeed;
            _seat.Interact(interactor);
            if (_seat.IsOccupied)
            {
                _hosting = true;
                return;
            }
            // The duel did not start (e.g. no legal deck): put everything back.
            _duel.NextCpuSeed = null;
            _board.ReturnTableHome();
            _decor?.SetVisible(true);
        }

        private void Update()
        {
            if (!_hosting) return;
            if (_seat != null && _seat.IsOccupied) return;
            if (_duel != null && _duel.IsActive) return;
            _hosting = false;
            _board?.ReturnTableHome();
            _decor?.SetVisible(true);
        }
    }
}
