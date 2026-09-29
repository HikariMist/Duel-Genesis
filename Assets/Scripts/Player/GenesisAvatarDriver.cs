using UnityEngine;

namespace DuelGenesis.Player
{
    /// <summary>
    /// Bridges the player's CharacterController and the UMA avatar ("Genesis Avatar" child):
    /// hides the blockout capsule once an avatar exists and feeds movement into the avatar's
    /// Animator (UMA Locomotion controller: "Speed" 0..1, "Direction" -1..1).
    /// Works with any humanoid animator that exposes those float parameters; does nothing otherwise.
    /// </summary>
    public class GenesisAvatarDriver : MonoBehaviour
    {
        public const string AvatarChildName = "Genesis Avatar";
        public float runSpeed = 5f;

        private Transform _avatar;
        private Animator _animator;
        private bool _hasSpeed, _hasDirection;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private float _speed, _turn;
        private MeshRenderer _capsule;

        private void Awake()
        {
            _avatar = transform.Find(AvatarChildName);
            _capsule = GetComponent<MeshRenderer>();
            _lastPosition = transform.position;
            _lastYaw = transform.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (_avatar == null) return;

            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                // UMA builds the character (and its Animator setup) a few frames after load.
                _animator = _avatar.GetComponentInChildren<Animator>();
                if (_animator == null || _animator.runtimeAnimatorController == null) return;
                _hasSpeed = _hasDirection = false;
                foreach (AnimatorControllerParameter p in _animator.parameters)
                {
                    if (p.type != AnimatorControllerParameterType.Float) continue;
                    if (p.name == "Speed") _hasSpeed = true;
                    if (p.name == "Direction") _hasDirection = true;
                }
                _animator.applyRootMotion = false;
                // Only hide the capsule once there is a character to replace it.
                if (_capsule != null && _avatar.GetComponentInChildren<SkinnedMeshRenderer>() != null) _capsule.enabled = false;
            }

            if (_capsule != null && _capsule.enabled && _avatar.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                _capsule.enabled = false;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;
            float yaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(_lastYaw, yaw);
            _lastYaw = yaw;

            float k = 1f - Mathf.Exp(-10f * dt);
            _speed = Mathf.Lerp(_speed, delta.magnitude / dt, k);
            _turn = Mathf.Lerp(_turn, Mathf.Clamp(yawDelta / dt / 180f, -1f, 1f), k);

            float normalized = Mathf.Clamp01(_speed / runSpeed);
            if (_hasSpeed) _animator.SetFloat("Speed", normalized < 0.04f ? 0f : normalized);
            if (_hasDirection) _animator.SetFloat("Direction", _turn);
            _animator.speed = normalized > 0.1f ? Mathf.Lerp(0.75f, 1.15f, normalized) : 1f;
        }
    }
}
