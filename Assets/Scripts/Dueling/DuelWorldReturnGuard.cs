using DuelGenesis.Player;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Safety net for every duel exit. The prototype accumulated several camera layers;
    /// this guard makes the final state deterministic by restoring the player camera and
    /// movement for several LateUpdate frames after IsActive becomes false.
    /// </summary>
    public sealed class DuelWorldReturnGuard : MonoBehaviour
    {
        private DuelGameController _duel;
        private ThirdPersonPlayerController _player;
        private ThirdPersonCamera _cameraController;
        private Camera _camera;
        private bool _wasActive;
        private int _restoreFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelWorldReturnGuard>() != null)
                return;

            GameObject host = new GameObject("Duel World Return Guard");
            host.AddComponent<DuelWorldReturnGuard>();
        }

        private void Update()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            bool active = _duel != null && _duel.IsActive;
            if (active)
                CaptureWorldReferences();

            if (!active && _wasActive)
                _restoreFrames = 8;

            _wasActive = active;
        }

        private void LateUpdate()
        {
            if (_restoreFrames <= 0)
                return;

            RestoreWorld();
            _restoreFrames--;
        }

        private void CaptureWorldReferences()
        {
            if (_player == null)
                _player = Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_cameraController == null)
                _cameraController = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (_camera == null)
                _camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        }

        private void RestoreWorld()
        {
            CaptureWorldReferences();

            if (_player != null)
                _player.SetMovementEnabled(true);

            if (_cameraController != null)
            {
                _cameraController.enabled = true;
                if (_player != null)
                    _cameraController.target = _player.transform;
                _cameraController.SetLookEnabled(true);
            }

            if (_camera != null)
            {
                _camera.gameObject.SetActive(true);
                _camera.enabled = true;

                if (_player != null)
                {
                    float distance = _cameraController != null ? _cameraController.distance : 6f;
                    float height = _cameraController != null ? _cameraController.height : 2f;
                    float yaw = _player.transform.eulerAngles.y;
                    Quaternion rotation = Quaternion.Euler(20f, yaw, 0f);
                    Vector3 position = _player.transform.position + Vector3.up * height - rotation * Vector3.forward * distance;
                    _camera.transform.SetPositionAndRotation(position, rotation);
                }
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
