using DuelGenesis.Cards;
using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// A Spell/Trap card's own 3D model popping up over its zone when it's activated (Pot of Greed, Magical Hats...):
    /// it springs up, turns, bobs and then shrinks away. Models live in Resources/CardModels/{card id}.
    /// </summary>
    public sealed class CardModelShowcase : MonoBehaviour
    {
        private float _age, _life, _height;
        private Transform _pivot;
        private bool _greedy;

        public static CardModelShowcase TrySpawn(Transform parent, Vector3 local, DuelCard card, float height = 0.13f, float life = 3.6f)
        {
            if (card?.Data == null) return null;
            GameObject prefab = CardModelRegistry.LoadPrefab(card.Data.id);
            if (prefab == null) return null;
            var holder = new GameObject("Card Model - " + card.Name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = local + Vector3.up * 0.004f;
            holder.transform.localRotation = Quaternion.Euler(0f, DuelMatLayout.Yaw(card.Controller), 0f);
            var show = holder.AddComponent<CardModelShowcase>();
            show._pivot = new GameObject("Pivot").transform;
            show._pivot.SetParent(holder.transform, false);
            GameObject model = Instantiate(prefab, show._pivot);
            MonsterHologram.PrepareModel(model);
            show._life = life;
            show._height = height;          // the prefabs are normalised to 1 m tall
            show._greedy = card.Name == "Pot of Greed";
            show._pivot.localScale = Vector3.zero;
            return show;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f) { Destroy(gameObject); return; }
            // Spring in (with a little overshoot), hold, shrink out.
            float s = t < 0.18f ? Mathf.Sin(t / 0.18f * Mathf.PI * 0.62f) * 1.12f : t > 0.8f ? Mathf.SmoothStep(1f, 0f, (t - 0.8f) / 0.2f) : 1f;
            _pivot.localScale = Vector3.one * (_height * Mathf.Max(0f, s));
            float lift = Mathf.SmoothStep(0f, 0.035f, Mathf.Clamp01(t / 0.25f)) + Mathf.Sin(_age * 3.2f) * 0.004f;
            _pivot.localPosition = new Vector3(0f, lift, 0f);
            float spin = _age * 70f;
            // Pot of Greed laughs: a quick wobble while it "draws".
            float wobble = _greedy && t > 0.25f && t < 0.7f ? Mathf.Sin(_age * 26f) * 9f : 0f;
            _pivot.localRotation = Quaternion.Euler(0f, spin, wobble);
        }
    }

    /// <summary>A Magical Hat standing over a face-down monster until the Battle Phase ends.</summary>
    public sealed class MagicalHatCover : MonoBehaviour
    {
        public const string HatResource = "DuelGenesis/Models/MagicalHat";
        private static GameObject _prefab;
        private float _phase, _age;
        private Transform _pivot;

        public static MagicalHatCover Spawn(Transform parent)
        {
            if (_prefab == null) _prefab = Resources.Load<GameObject>(HatResource);
            if (_prefab == null) return null;
            var holder = new GameObject("Magical Hat");
            holder.transform.SetParent(parent, false);
            var cover = holder.AddComponent<MagicalHatCover>();
            cover._pivot = new GameObject("Pivot").transform;
            cover._pivot.SetParent(holder.transform, false);
            GameObject model = Instantiate(_prefab, cover._pivot);
            MonsterHologram.PrepareModel(model);
            cover._phase = Random.value * 6f;
            cover._pivot.localScale = Vector3.zero;
            return cover;
        }

        public void SetPose(Vector3 local, float yaw)
        {
            transform.localPosition = local + Vector3.up * 0.002f;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float grow = Mathf.Clamp01(_age / 0.35f);
            _pivot.localScale = Vector3.one * (0.1f * Mathf.SmoothStep(0f, 1f, grow));   // a 10 cm hat over an 8.6 cm card
            _pivot.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 2.4f + _phase)) * 0.006f, 0f);
            _pivot.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.3f + _phase) * 12f, 0f);
        }
    }
}
