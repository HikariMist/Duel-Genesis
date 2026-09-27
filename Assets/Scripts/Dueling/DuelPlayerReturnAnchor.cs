using DuelGenesis.Player;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Saves the player's exact grounded world position when a duel begins and
    /// restores it before movement resumes when the duel closes. This prevents
    /// the player from being left at a camera/table position and falling through
    /// the world after Forfeit, win, loss, or Return to Table.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class DuelPlayerReturnAnchor : MonoBehaviour
    {
        private DuelGameController _duel;
        private ThirdPersonPlayerController _player;
        private ThirdPersonCamera _cameraController;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private bool _hasAnchor;
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

            if (active && !_wasActive)
                CaptureAnchor();
            else if (!active && _wasActive)
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

        private void CaptureAnchor()
        {
            Resolve();
            if (_player == null)
                return;

            _savedPosition = _player.transform.position;
            _savedRotation = _player.transform.rotation;
            _hasAnchor = true;
        }

        public void RestorePlayer()
        {
            Resolve();
            if (_player == null)
                return;

            // Teleport first while movement is disabled so gravity cannot advance the
            // controller from an invalid duel/table position before we restore control.
            _player.SetMovementEnabled(false);

            if (_hasAnchor)
                _player.Teleport(_savedPosition, _savedRotation);

            _player.SetMovementEnabled(true);

            if (_cameraController != null)
                _cameraController.SetLookEnabled(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _hasAnchor = false;
        }
    }
}
