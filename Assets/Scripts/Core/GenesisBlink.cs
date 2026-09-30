using UnityEngine;

namespace DuelGenesis.Core
{
    /// <summary>Slowly pulses a light on and off, like an aviation warning beacon on a tower.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class GenesisBlink : MonoBehaviour
    {
        public float period = 1.6f;
        [Range(0.05f, 0.95f)] public float onShare = 0.35f;

        private Light _light;
        private float _intensity;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _intensity = _light.intensity;
        }

        private void Update()
        {
            float phase = Mathf.Repeat(Time.time / Mathf.Max(0.1f, period), 1f);
            float on = phase < onShare ? Mathf.Sin(phase / onShare * Mathf.PI) : 0f;
            _light.intensity = _intensity * on;
        }
    }
}
