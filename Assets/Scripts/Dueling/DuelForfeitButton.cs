using System.Reflection;
using DuelGenesis.Player;
using DuelGenesis.UI;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Small permanent HUD control for leaving an active duel. It performs a hard
    /// return-to-world restore for several frames so the tabletop camera cannot leave
    /// the player looking at a black/invalid view after the duel closes.
    /// </summary>
    public sealed class DuelForfeitButton : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private DuelGameController _duel;
        private MethodInfo _forfeit;
        private ThirdPersonPlayerController _restorePlayer;
        private ThirdPersonCamera _restoreCameraController;
        private Camera _restoreCamera;
        private int _restoreFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelForfeitButton>() != null)
                return;

            GameObject host = new GameObject("Duel Forfeit Button");
            host.AddComponent<DuelForfeitButton>();
        }

        private void Awake()
        {
            _forfeit = typeof(DuelGameController).GetMethod("Forfeit", PrivateInstance);
        }

        private void Update()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();
        }

        private void LateUpdate()
        {
            if (_restoreFrames <= 0)
                return;

            RestoreWorldView();
            _restoreFrames--;
        }

        private void OnGUI()
        {
            if (_duel == null || !_duel.IsActive || _duel.IsDuelOver)
                return;

            GUI.depth = -1300;
            Rect button = new Rect(18f, 20f, 112f, 34f);
            if (GenesisTheme.Button(button, "FORFEIT", GenesisTheme.Danger))
                ForfeitAndReturn();
        }

        private void ForfeitAndReturn()
        {
            if (_duel == null)
                return;

            // Capture world references BEFORE CloseDuel clears its player/camera references.
            _restorePlayer = Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            _restoreCameraController = Object.FindFirstObjectByType<ThirdPersonCamera>();
            _restoreCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            _forfeit?.Invoke(_duel, null);
            _duel.CloseDuel();

            // The tabletop presentation restores itself on the next frame, so repeat our
            // world restore for a few LateUpdate frames and win the final camera position.
            _restoreFrames = 6;
            RestoreWorldView();
        }

        private void RestoreWorldView()
        {
            if (_restorePlayer == null)
                _restorePlayer = Object.FindFirstObjectByType<ThirdPersonPlayerController>();
            if (_restoreCameraController == null)
                _restoreCameraController = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (_restoreCamera == null)
                _restoreCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            if (_restorePlayer != null)
                _restorePlayer.SetMovementEnabled(true);

            if (_restoreCameraController != null)
            {
                _restoreCameraController.enabled = true;
                if (_restorePlayer != null)
                    _restoreCameraController.target = _restorePlayer.transform;
                _restoreCameraController.SetLookEnabled(true);
            }

            if (_restoreCamera != null)
            {
                _restoreCamera.gameObject.SetActive(true);
                _restoreCamera.enabled = true;

                if (_restorePlayer != null)
                {
                    float distance = _restoreCameraController != null ? _restoreCameraController.distance : 6f;
                    float height = _restoreCameraController != null ? _restoreCameraController.height : 2f;
                    float yaw = _restorePlayer.transform.eulerAngles.y;
                    Quaternion rotation = Quaternion.Euler(20f, yaw, 0f);
                    Vector3 position = _restorePlayer.transform.position + Vector3.up * height - rotation * Vector3.forward * distance;
                    _restoreCamera.transform.SetPositionAndRotation(position, rotation);
                }
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
