using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Player
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 6f;
        public float height = 2f;
        public float sensitivity = 0.15f;
        public float minPitch = -20f;
        public float maxPitch = 65f;
        public float followSmooth = 14f;

        private float _yaw;
        private float _pitch = 20f;
        private bool _lookEnabled = true;

        private void Start()
        {
            if (target != null)
                _yaw = target.eulerAngles.y;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (_lookEnabled)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw += delta.x * sensitivity;
                    _pitch -= delta.y * sensitivity;
                    _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
                }
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPosition = target.position + Vector3.up * height - rotation * Vector3.forward * distance;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
            transform.rotation = rotation;
        }

        public void SetLookEnabled(bool enabled)
        {
            _lookEnabled = enabled;

            if (!enabled)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
