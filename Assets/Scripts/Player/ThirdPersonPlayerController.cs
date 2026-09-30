using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 4.2f;     // normal
        public float sprintSpeed = 7.5f;   // hold Shift
        public float slowWalkSpeed = 1.6f; // hold Ctrl
        public float rotationSpeed = 12f;
        public float gravity = -20f;
        public float jumpHeight = 1.2f;

        [Header("Skateboard (G)")]
        public float rideSpeed = 11f;        // cruising on W
        public float rideBoostSpeed = 16f;   // pushing hard with Shift
        public float rideAcceleration = 7f;
        public float rideTurnRate = 120f;    // degrees per second at low speed

        /// <summary>True while standing on the skateboard (set by GenesisSkateboard).</summary>
        public bool Riding { get; set; }
        /// <summary>Current board speed (m/s) and steering (-1..1), for the board's lean and the animation.</summary>
        public float RideSpeedNow => _rideSpeed;
        public float RideSteer { get; private set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        private float _rideSpeed;

        private CharacterController _controller;
        private float _verticalVelocity;
        private bool _movementEnabled = true;

        public bool MovementEnabled => _movementEnabled;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            // When movement is disabled (title screen, sitting at the duel table, etc.)
            // the CharacterController must stay exactly where it was placed. The old
            // implementation kept applying gravity here and could pull the player down
            // through a table while the duel camera was active.
            if (!_movementEnabled)
            {
                _verticalVelocity = 0f;
                return;
            }

            ApplyGravityAndJump();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (Riding) { Ride(keyboard); return; }
            _rideSpeed = 0f;

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = cam != null ? cam.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 move = forward * input.y + right * input.x;
            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            float speed = keyboard.ctrlKey.isPressed ? slowWalkSpeed
                        : keyboard.shiftKey.isPressed && input.y >= 0f ? sprintSpeed
                        : moveSpeed;
            Vector3 velocity = move * speed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>Board handling: W pushes (Shift pushes harder), S brakes, A/D carve; momentum carries you when you let go.</summary>
        private void Ride(Keyboard keyboard)
        {
            float dt = Time.deltaTime;
            float steer = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            RideSteer = Mathf.MoveTowards(RideSteer, steer, 4f * dt);
            bool push = keyboard.wKey.isPressed, brake = keyboard.sKey.isPressed;
            float top = keyboard.shiftKey.isPressed ? rideBoostSpeed : rideSpeed;
            if (push) _rideSpeed = Mathf.MoveTowards(_rideSpeed, top, rideAcceleration * dt);
            else _rideSpeed = Mathf.MoveTowards(_rideSpeed, 0f, (brake ? 10f : 1.1f) * dt);
            if (push && _rideSpeed > top) _rideSpeed = Mathf.MoveTowards(_rideSpeed, top, 3f * dt);   // ease off after a boost

            // Carving: tighter at low speed, wider when fast.
            float rate = rideTurnRate * Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(0f, rideBoostSpeed, _rideSpeed));
            transform.Rotate(0f, RideSteer * rate * dt, 0f, Space.World);

            Vector3 velocity = transform.forward * _rideSpeed;
            velocity.y = _verticalVelocity;
            CollisionFlags hit = _controller.Move(velocity * dt);
            if ((hit & CollisionFlags.Sides) != 0) _rideSpeed *= 0.35f;   // bumped into something
        }

        private void ApplyGravityAndJump()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            Keyboard keyboard = Keyboard.current;
            if (_controller.isGrounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _verticalVelocity += gravity * Time.deltaTime;
        }

        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;
            if (!enabled)
                _verticalVelocity = 0f;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
            _verticalVelocity = 0f;
        }
    }
}
