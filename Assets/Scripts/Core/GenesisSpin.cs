using UnityEngine;

namespace DuelGenesis.Core
{
    /// <summary>Spins (and optionally bobs) decor: holo rings, sweeping stage lights, trophies.</summary>
    public sealed class GenesisSpin : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 30f, 0f);
        public float bobHeight;
        public float bobSpeed = 1f;
        public bool sweep;          // swing back and forth instead of turning all the way round
        public float sweepAngle = 35f;

        private Vector3 _home;
        private Quaternion _rest;

        private void Start()
        {
            _home = transform.localPosition;
            _rest = transform.localRotation;
        }

        private void Update()
        {
            if (sweep)
            {
                float s = Mathf.Sin(Time.time * bobSpeed) * sweepAngle;
                transform.localRotation = _rest * Quaternion.Euler(degreesPerSecond.normalized * s);
            }
            else transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
            if (bobHeight > 0f) transform.localPosition = _home + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        }
    }
}
