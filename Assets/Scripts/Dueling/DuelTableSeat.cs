using DuelGenesis.Cards;
using DuelGenesis.Interaction;
using DuelGenesis.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Dueling
{
    public class DuelTableSeat : MonoBehaviour, IInteractable
    {
        public Transform seatPoint;
        public Transform standPoint;
        public string seatLabel = "Start Duel";

        private ThirdPersonPlayerController _seatedPlayer;
        private DuelPrototype _duel;

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

                if (deck == null || !deck.Validate(collection, out string validation))
                {
                    Debug.LogWarning("Cannot duel yet: " + validation);
                    return;
                }

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

                Debug.Log("Player sat at the Duel Table and started a prototype duel.");
            }
            else if (_seatedPlayer == player && (_duel == null || !_duel.IsActive))
            {
                LeaveSeat();
            }
        }

        private void ResolveDuel()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelPrototype>();
        }

        private void Update()
        {
            if (_seatedPlayer == null) return;
            ResolveDuel();

            if (_duel != null && _duel.IsActive)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
                LeaveSeat();
        }

        private void LeaveSeat()
        {
            if (_seatedPlayer == null) return;

            Transform target = standPoint != null ? standPoint : transform;
            _seatedPlayer.Teleport(target.position, target.rotation);
            _seatedPlayer.SetMovementEnabled(true);
            _seatedPlayer = null;
            Debug.Log("Player left the Duel Table.");
        }
    }
}
