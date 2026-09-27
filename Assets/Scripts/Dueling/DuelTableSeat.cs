using DuelGenesis.Cards;
using DuelGenesis.Interaction;
using DuelGenesis.Player;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    public class DuelTableSeat : MonoBehaviour, IInteractable
    {
        public Transform seatPoint;
        public Transform standPoint;
        public string seatLabel = "Start Duel";

        private ThirdPersonPlayerController _seatedPlayer;
        private DuelGameController _duel;
        private Vector3 _returnPosition;
        private Quaternion _returnRotation;
        private bool _hasReturnPoint;
        private bool _duelWasActive;

        public string InteractionPrompt
        {
            get
            {
                if (_seatedPlayer == null) return seatLabel;
                if (_duel != null && _duel.IsActive) return "Duel in Progress";
                return "Leave Duel Table";
            }
        }

        public void Interact(GameObject interactor)
        {
            ThirdPersonPlayerController player = interactor.GetComponent<ThirdPersonPlayerController>();
            if (player == null) return;

            ResolveDuel();

            if (_seatedPlayer == null)
            {
                PlayerDeck deck = interactor.GetComponent<PlayerDeck>();
                PlayerCollection collection = interactor.GetComponent<PlayerCollection>();

                if (deck == null)
                {
                    Debug.LogWarning("Cannot duel yet: player deck system is missing.");
                    return;
                }

                if (!deck.Validate(collection, out string validation))
                {
                    Debug.LogWarning("Cannot duel yet: " + validation);
                    return;
                }

                // Save the real grounded location BEFORE moving the player to the seat.
                // This is the safest place to return after a forfeit, win or loss.
                _returnPosition = player.transform.position;
                _returnRotation = player.transform.rotation;
                _hasReturnPoint = true;

                _seatedPlayer = player;
                player.SetMovementEnabled(false);

                Transform target = seatPoint != null ? seatPoint : transform;
                player.Teleport(target.position, target.rotation);

                if (_duel == null || !_duel.StartDuel(interactor))
                {
                    Debug.LogWarning("The duel system could not start. Leaving the table.");
                    LeaveSeat();
                    return;
                }

                _duelWasActive = true;
                Debug.Log("Player sat at the Duel Table and started a DuelGameController duel.");
            }
            else if (_seatedPlayer == player && (_duel == null || !_duel.IsActive))
            {
                LeaveSeat();
            }
        }

        private void ResolveDuel()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
        }

        private void Update()
        {
            if (_seatedPlayer == null)
                return;

            ResolveDuel();
            bool active = _duel != null && _duel.IsActive;

            // Leaving a duel should immediately put the player back in the world.
            // Do not leave them parked at the seat waiting for another key press.
            if (!active && _duelWasActive)
            {
                LeaveSeat();
                return;
            }

            _duelWasActive = active;
        }

        private void LeaveSeat()
        {
            if (_seatedPlayer == null)
                return;

            _seatedPlayer.SetMovementEnabled(false);

            if (_hasReturnPoint)
            {
                _seatedPlayer.Teleport(_returnPosition, _returnRotation);
            }
            else if (standPoint != null)
            {
                _seatedPlayer.Teleport(standPoint.position, standPoint.rotation);
            }
            else
            {
                _seatedPlayer.Teleport(transform.position, transform.rotation);
            }

            _seatedPlayer.SetMovementEnabled(true);
            ThirdPersonCamera camera = Object.FindAnyObjectByType<ThirdPersonCamera>();
            if (camera != null) camera.SetLookEnabled(true);
            _seatedPlayer = null;
            _hasReturnPoint = false;
            _duelWasActive = false;
            Debug.Log("Player left the Duel Table and returned to the pre-duel position.");
        }
    }
}
