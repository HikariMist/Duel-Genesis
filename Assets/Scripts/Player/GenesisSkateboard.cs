using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Player
{
    /// <summary>
    /// G hops on or off the skateboard ("SkateBoard Free Model" by marcos.driguez, CC BY 4.0). On the board the
    /// duelist stands side-on, W pushes, Shift pushes harder, S brakes, A/D carve and Space ollies; the board leans
    /// into turns. You step off automatically when a menu, shop or duel takes over.
    /// </summary>
    [RequireComponent(typeof(ThirdPersonPlayerController))]
    public sealed class GenesisSkateboard : MonoBehaviour
    {
        public const string BoardResource = "DuelGenesis/Skateboard/Skateboard";
        public float deckHeight = 0.12f;   // how far the rider is lifted
        public float stanceYaw = 78f;      // side-on stance

        private ThirdPersonPlayerController _mover;
        private Transform _board, _avatar;
        private GameObject _prefab;
        private float _roll, _pitch;
        private bool _wasGrounded = true;
        private Vector3 _avatarPos;
        private Quaternion _avatarRot = Quaternion.identity;

        public bool Riding => _mover != null && _mover.Riding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Resources.Load<GameObject>(BoardResource) == null) return;
            foreach (var p in FindObjectsByType<ThirdPersonPlayerController>(FindObjectsSortMode.None))
                if (p.GetComponent<GenesisSkateboard>() == null) p.gameObject.AddComponent<GenesisSkateboard>();
        }

        private void Awake()
        {
            _mover = GetComponent<ThirdPersonPlayerController>();
            _prefab = Resources.Load<GameObject>(BoardResource);
        }

        private void Update()
        {
            if (_mover == null) return;
            if (!_mover.MovementEnabled) { if (Riding) SetRiding(false); return; }
            var k = Keyboard.current;
            if (k != null && k.gKey.wasPressedThisFrame && (_mover.IsGrounded || Riding)) SetRiding(!Riding);
        }

        private void LateUpdate()
        {
            if (!Riding || _board == null) return;
            if (_avatar == null) FindAvatar();
            float dt = Time.deltaTime;
            // Lean into carves, tip the nose up for an ollie.
            _roll = Mathf.Lerp(_roll, -_mover.RideSteer * Mathf.Lerp(4f, 12f, Mathf.Clamp01(_mover.RideSpeedNow / 12f)), 1f - Mathf.Exp(-8f * dt));
            bool grounded = _mover.IsGrounded;
            _pitch = Mathf.Lerp(_pitch, grounded ? 0f : -14f, 1f - Mathf.Exp(-(grounded ? 10f : 16f) * dt));
            _wasGrounded = grounded;
            _board.localRotation = Quaternion.Euler(_pitch, 0f, _roll);
            if (_avatar != null)
            {
                _avatar.localPosition = _avatarPos + new Vector3(0f, deckHeight, 0f);
                _avatar.localRotation = _avatarRot * Quaternion.Euler(0f, stanceYaw, _roll * 0.6f);
            }
        }

        public void SetRiding(bool ride)
        {
            if (ride == Riding) return;
            FindAvatar();
            if (ride)
            {
                if (_prefab == null) return;
                var go = Instantiate(_prefab, transform);
                go.name = "Skateboard";
                _board = go.transform;
                _board.localRotation = Quaternion.identity;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                if (_avatar != null) { _avatarPos = _avatar.localPosition; _avatarRot = _avatar.localRotation; }
                // The board goes under the feet: the avatar's origin if there is one, else the bottom of the controller.
                var cc = GetComponent<CharacterController>();
                float feet = _avatar != null ? _avatarPos.y : cc != null ? cc.center.y - cc.height * 0.5f : 0f;
                _board.localPosition = new Vector3(0f, feet, 0f);
                _mover.Riding = true;
            }
            else
            {
                _mover.Riding = false;
                if (_board != null) Destroy(_board.gameObject);
                _board = null;
                _roll = _pitch = 0f;
                if (_avatar != null)
                {
                    _avatar.localPosition = _avatarPos;
                    _avatar.localRotation = _avatarRot;
                }
            }
        }

        private void FindAvatar()
        {
            if (_avatar == null) _avatar = transform.Find(GenesisAvatarDriver.AvatarChildName);
        }
    }
}
