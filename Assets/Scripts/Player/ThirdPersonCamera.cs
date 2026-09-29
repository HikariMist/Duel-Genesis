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
            Vector3 pivot = target.position + Vector3.up * height;
            Vector3 back = -(rotation * Vector3.forward);
            float wanted = distance;

            // Camera collision: pull in in front of any wall between the player and the camera (shop interiors,
            // alleys), so the view never ends up inside or behind a building.
            float allowed = wanted;
            int count = Physics.SphereCastNonAlloc(pivot, 0.25f, back, _hits, wanted, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(target) || _hits[i].distance <= 0f) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0.3f, _hits[i].distance - 0.15f));
            }
            Vector3 desiredPosition = pivot + back * allowed;

            // Follow smoothly, but never lag behind a wall: snap in when the camera has to come closer.
            float current = Vector3.Distance(transform.position, pivot);
            transform.position = current > allowed + 0.05f
                ? desiredPosition
                : Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
            transform.rotation = rotation;
        }

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Points the camera the way <paramref name="forward"/> faces (e.g. into a room after a door).</summary>
        public void SnapBehind(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f || target == null) return;
            _yaw = Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
            _pitch = 12f;
            transform.position = target.position + Vector3.up * height;
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
