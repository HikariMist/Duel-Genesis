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
        public string seatLabel = "Sit at Duel Table";

        private ThirdPersonPlayerController _seatedPlayer;

        public string InteractionPrompt => _seatedPlayer == null ? seatLabel : "Leave Duel Table";

        public void Interact(GameObject interactor)
        {
            ThirdPersonPlayerController player = interactor.GetComponent<ThirdPersonPlayerController>();
            if (player == null) return;

            if (_seatedPlayer == null)
            {
                _seatedPlayer = player;
                player.SetMovementEnabled(false);

                Transform target = seatPoint != null ? seatPoint : transform;
                player.Teleport(target.position, target.rotation);
                Debug.Log("Player sat at the Duel Table. Duel-ready state entered.");
            }
            else if (_seatedPlayer == player)
            {
                LeaveSeat();
            }
        }

        private void Update()
        {
            if (_seatedPlayer == null) return;
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
