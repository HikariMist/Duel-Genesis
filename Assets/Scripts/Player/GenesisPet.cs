using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelGenesis.Player
{
    /// <summary>
    /// A pet that follows you round the city: Pot of Greed ("Pot of Greed (Yu-Gi-Oh!)" by Graveyart, CC BY 4.0).
    /// It hops along just behind your shoulder, keeps to the ground, turns to face where it's going, wobbles with a grin
    /// when you stop, and catches up (with a hop) if you get too far ahead or teleport. Y calls it or sends it home;
    /// the choice is remembered.
    /// </summary>
    public sealed class GenesisPet : MonoBehaviour
    {
        public const string PetResource = "DuelGenesis/Pets/PotOfGreed";
        private const string PrefKey = "dg.pet.potofgreed";

        public Vector3 offset = new Vector3(0.95f, 0f, -1.1f);   // right of and behind the player
        public float followSmooth = 0.28f;
        public float teleportDistance = 25f;

        private Transform _player, _body;
        private Vector3 _velocity;
        private float _hop, _idle;
        private bool _out;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Resources.Load<GameObject>(PetResource) == null || FindAnyObjectByType<GenesisPet>() != null) return;
            new GameObject("Genesis Pet").AddComponent<GenesisPet>();
        }

        private void Start()
        {
            _out = PlayerPrefs.GetInt(PrefKey, 1) == 1;
        }

        private void Update()
        {
            if (_player == null)
            {
                var pc = FindAnyObjectByType<ThirdPersonPlayerController>();
                if (pc == null) return;
                _player = pc.transform;
            }
            var k = Keyboard.current;
            if (k != null && k.yKey.wasPressedThisFrame)
            {
                _out = !_out;
                PlayerPrefs.SetInt(PrefKey, _out ? 1 : 0);
                PlayerPrefs.Save();
            }
            if (_out && _body == null) Spawn();
            if (!_out && _body != null) { Destroy(_body.gameObject); _body = null; }
            if (_body == null) return;

            float dt = Time.deltaTime;
            Vector3 target = _player.position + _player.rotation * offset;
            target.y = Ground(target);
            Vector3 flat = _body.position; flat.y = target.y;
            if (Vector3.Distance(flat, target) > teleportDistance)
            {
                _body.position = target;   // left behind (teleport, skateboard sprint): pop back to your side
                _velocity = Vector3.zero;
            }

            Vector3 before = _body.position;
            Vector3 next = Vector3.SmoothDamp(new Vector3(before.x, target.y, before.z), target, ref _velocity, followSmooth, 30f, dt);
            float speed = new Vector2(_velocity.x, _velocity.z).magnitude;

            // Hop while moving, rock and grin when idle.
            _hop += dt * Mathf.Lerp(6f, 12f, Mathf.Clamp01(speed / 6f));
            float hopHeight = speed > 0.4f ? Mathf.Abs(Mathf.Sin(_hop)) * Mathf.Lerp(0.08f, 0.22f, Mathf.Clamp01(speed / 6f)) : 0f;
            _idle = speed > 0.4f ? 0f : _idle + dt;
            _body.position = next + Vector3.up * hopHeight;

            Vector3 look = speed > 0.3f ? new Vector3(_velocity.x, 0f, _velocity.z) : (_player.position - _body.position);
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
            {
                Quaternion face = Quaternion.LookRotation(look.normalized, Vector3.up);
                float wobble = _idle > 0.5f ? Mathf.Sin(_idle * 5f) * 7f : 0f;
                float lean = speed > 0.4f ? Mathf.Sin(_hop * 2f) * 5f : 0f;
                _body.rotation = Quaternion.Slerp(_body.rotation, face * Quaternion.Euler(lean, 0f, wobble), 1f - Mathf.Exp(-8f * dt));
            }
            // A little squash when it lands.
            float squash = speed > 0.4f ? 1f - Mathf.Max(0f, 0.1f - hopHeight) * 0.9f : 1f + Mathf.Sin(_idle * 2.2f) * 0.02f;
            _body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
        }

        private void Spawn()
        {
            var prefab = Resources.Load<GameObject>(PetResource);
            if (prefab == null) return;
            var go = Instantiate(prefab, transform);
            go.name = "Pot of Greed (pet)";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            _body = go.transform;
            Vector3 p = _player.position + _player.rotation * offset;
            p.y = Ground(p);
            _body.position = p;
            _velocity = Vector3.zero;
        }

        /// <summary>Ground height under a point (raycast down from above), ignoring the player and the pet.</summary>
        private float Ground(Vector3 at)
        {
            var hits = Physics.RaycastAll(new Vector3(at.x, _player.position.y + 1.5f, at.z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(_player) || (_body != null && h.collider.transform.IsChildOf(_body))) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return float.IsNegativeInfinity(best) ? _player.position.y : best;
        }
    }
}
