using DuelGenesis.Player;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Restores player controls after a duel. Position restoration is owned by
    /// DuelTableSeat, because that component can save the true grounded location
    /// before the player is teleported to the duel seat.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class DuelPlayerReturnAnchor : MonoBehaviour
    {
        private DuelGameController _duel;
        private ThirdPersonPlayerController _player;
        private ThirdPersonCamera _cameraController;
        private bool _wasActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelPlayerReturnAnchor>() != null)
                return;

            GameObject host = new GameObject("Duel Player Return Anchor");
            host.AddComponent<DuelPlayerReturnAnchor>();
        }

        private void Update()
        {
            Resolve();
            bool active = _duel != null && _duel.IsActive;

            if (!active && _wasActive)
                RestorePlayer();

            _wasActive = active;
        }

        private void Resolve()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
            if (_player == null)
                _player = Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_cameraController == null)
                _cameraController = Object.FindFirstObjectByType<ThirdPersonCamera>();
        }

        public void RestorePlayer()
        {
            Resolve();

            if (_player != null)
                _player.SetMovementEnabled(true);

            if (_cameraController != null)
            {
                _cameraController.enabled = true;
                if (_player != null)
                    _cameraController.target = _player.transform;
                _cameraController.SetLookEnabled(true);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
