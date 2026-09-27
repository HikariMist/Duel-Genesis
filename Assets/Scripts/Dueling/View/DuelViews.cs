using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DuelGenesis.Dueling
{
    /// <summary>A clickable zone on the mat (used for placing cards and highlighting targets).</summary>
    public sealed class DuelZoneView : MonoBehaviour
    {
        public int Player { get; private set; }
        public DuelZone Zone { get; private set; }
        public int Slot { get; private set; }

        private SpriteRenderer _glow;
        private Color _glowColor;
        private float _glowStrength;

        public static DuelZoneView Create(Transform parent, int player, DuelZone zone, int slot, Vector3 localPosition)
        {
            GameObject go = new GameObject($"Zone {player} {zone} {slot}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, DuelMatLayout.Yaw(player), 0f);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(DuelMatLayout.ZoneWidthMm, 3f, DuelMatLayout.ZoneHeightMm) * 0.001f;
            box.center = new Vector3(0f, 0.0015f, 0f);

            DuelZoneView view = go.AddComponent<DuelZoneView>();
            view.Player = player;
            view.Zone = zone;
            view.Slot = slot;

            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.0003f, 0f);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = new Vector3((DuelMatLayout.ZoneWidthMm + 14f) / 96f, (DuelMatLayout.ZoneHeightMm + 14f) / 128f, 1f);
            view._glow = glow.AddComponent<SpriteRenderer>();
            view._glow.sprite = DuelVisualResources.GlowSprite;
            view._glow.color = Color.clear;
            return view;
        }

        public void SetHighlight(Color color, float strength)
        {
            _glowColor = color;
            _glowStrength = strength;
        }

        private void Update()
        {
            float pulse = _glowStrength > 0f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            Color target = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowStrength * pulse);
            _glow.color = Color.Lerp(_glow.color, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }
    }

    /// <summary>
    /// A physical 3D card. It glides (with a small arc) to whatever pose the board assigns,
    /// so draws, summons, flips and trips to the Graveyard all animate automatically.
    /// </summary>
    public sealed class DuelCardView : MonoBehaviour
    {
        public DuelCard Card { get; private set; }
        public bool IsSettled => _t >= 1f && _lungeTime <= 0f;
        public bool IsHovered { get; set; }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Texture _face;
        private SpriteRenderer _glow;
        private Color _glowTarget = Color.clear;

        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private float _fromScale = 1f, _toScale = 1f;
        private float _t = 1f;
        private float _duration = 0.3f;
        private float _arc;

        private Vector3 _lungeTarget;
        private float _lungeTime;
        private const float LungeDuration = 0.42f;

        public static DuelCardView Create(Transform parent, DuelCard card)
        {
            GameObject go = new GameObject("Card " + card.Uid + " " + card.Name);
            go.transform.SetParent(parent, false);
            DuelCardView view = go.AddComponent<DuelCardView>();
            view.Card = card;

            go.AddComponent<MeshFilter>().sharedMesh = DuelVisualResources.CardMesh;
            view._renderer = go.AddComponent<MeshRenderer>();
            view._renderer.sharedMaterials = new[] { DuelVisualResources.FaceMaterial, DuelVisualResources.BackMaterial, DuelVisualResources.EdgeMaterial };
            view._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            view._block = new MaterialPropertyBlock();

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(DuelMatLayout.CardWidthMm, 2f, DuelMatLayout.CardHeightMm) * 0.001f;

            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, -0.0006f, 0f);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = new Vector3((DuelMatLayout.CardWidthMm + 22f) / 96f, (DuelMatLayout.CardHeightMm + 24f) / 128f, 1f);
            view._glow = glow.AddComponent<SpriteRenderer>();
            view._glow.sprite = DuelVisualResources.GlowSprite;
            view._glow.color = Color.clear;
            return view;
        }

        public void SetFace(Texture face)
        {
            if (_face == face) return;
            _face = face;
            _renderer.GetPropertyBlock(_block, 0);
            if (face != null)
            {
                _block.SetTexture(BaseMap, face);
                _block.SetTexture(MainTex, face);
            }
            _renderer.SetPropertyBlock(_block, 0);
        }

        public bool HasFace => _face != null;

        public void SetGlow(Color color, float strength) => _glowTarget = new Color(color.r, color.g, color.b, strength);

        public void SetPose(Vector3 localPosition, Quaternion localRotation, float scale, bool instant = false)
        {
            if (instant)
            {
                _toPos = localPosition; _toRot = localRotation; _toScale = scale;
                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = Vector3.one * scale;
                _t = 1f;
                return;
            }

            bool changed = (localPosition - _toPos).sqrMagnitude > 1e-9f ||
                           Quaternion.Angle(localRotation, _toRot) > 0.3f ||
                           Mathf.Abs(scale - _toScale) > 0.001f;
            if (!changed) return;

            _fromPos = transform.localPosition;
            _fromRot = transform.localRotation;
            _fromScale = transform.localScale.x;
            _toPos = localPosition;
            _toRot = localRotation;
            _toScale = scale;

            float distance = Vector3.Distance(_fromPos, _toPos);
            float turn = Quaternion.Angle(_fromRot, _toRot);
            bool hover = distance < 0.03f && turn < 1f;
            _duration = hover ? 0.12f : Mathf.Clamp(0.22f + distance * 0.8f + turn / 900f, 0.2f, 0.6f);
            _arc = hover ? 0f : Mathf.Clamp(distance * 0.35f + turn / 2500f, 0f, 0.09f);
            _t = 0f;
        }

        /// <summary>Attack animation: lunge towards a point (table-local) and return.</summary>
        public void Lunge(Vector3 localTarget)
        {
            _lungeTarget = localTarget;
            _lungeTime = LungeDuration;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + dt / _duration);
                float e = 1f - Mathf.Pow(1f - _t, 3f);
                Vector3 p = Vector3.LerpUnclamped(_fromPos, _toPos, e);
                p.y += Mathf.Sin(_t * Mathf.PI) * _arc;
                transform.localPosition = p;
                transform.localRotation = Quaternion.Slerp(_fromRot, _toRot, e);
                transform.localScale = Vector3.one * Mathf.Lerp(_fromScale, _toScale, e);
            }

            if (_lungeTime > 0f)
            {
                _lungeTime -= dt;
                float k = 1f - Mathf.Clamp01(_lungeTime / LungeDuration);
                float reach = k < 0.45f ? Mathf.SmoothStep(0f, 1f, k / 0.45f) : Mathf.SmoothStep(1f, 0f, (k - 0.45f) / 0.55f);
                Vector3 towards = Vector3.Lerp(_toPos, _lungeTarget, 0.55f);
                towards.y = _toPos.y + 0.03f;
                transform.localPosition = Vector3.Lerp(_toPos, towards, reach);
                if (_lungeTime <= 0f) transform.localPosition = _toPos;
            }

            _glow.color = Color.Lerp(_glow.color, _glowTarget, 1f - Mathf.Exp(-16f * dt));
        }
    }

    /// <summary>DMO monster model standing on a face-up monster card (a tabletop hologram).</summary>
    public sealed class MonsterHologram : MonoBehaviour
    {
        public const float TargetHeight = 0.085f;   // metres above the card, life-size table

        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private AnimationClip _clip;
        private float _phase;

        public DuelCard Card { get; private set; }

        public static MonsterHologram TryCreate(Transform parent, DuelCard card)
        {
            GameObject prefab = CardModelRegistry.LoadPrefab(card.Data);
            if (prefab == null) return null;

            GameObject holder = new GameObject("Monster Model - " + card.Name);
            holder.transform.SetParent(parent, false);
            GameObject model = Instantiate(prefab, holder.transform);
            model.name = "Model";
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // Viewer prefabs can carry scene furniture (cameras, listeners, UI, event systems): strip it.
            foreach (Camera c in model.GetComponentsInChildren<Camera>(true)) Destroy(c);
            foreach (AudioListener l in model.GetComponentsInChildren<AudioListener>(true)) Destroy(l);
            foreach (Light l in model.GetComponentsInChildren<Light>(true)) Destroy(l);
            foreach (Canvas c in model.GetComponentsInChildren<Canvas>(true)) Destroy(c.gameObject);
            foreach (UnityEngine.EventSystems.BaseInputModule m in model.GetComponentsInChildren<UnityEngine.EventSystems.BaseInputModule>(true)) Destroy(m);
            foreach (UnityEngine.EventSystems.EventSystem e in model.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)) Destroy(e);
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null) b.enabled = false;   // viewer scripts must not run on the table

            AdaptMaterials(model);
            Normalize(model.transform);

            MonsterHologram hologram = holder.AddComponent<MonsterHologram>();
            hologram.Card = card;
            hologram._phase = Random.value * 10f;
            hologram.PlayIdle(model, card.Data);
            return hologram;
        }

        private static void Normalize(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = WorldBounds(renderers);
            float largest = Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z) * 0.8f);
            if (largest <= 0.0001f) return;
            model.localScale *= TargetHeight / largest;

            // Stand the model on the card surface, centred on the card.
            bounds = WorldBounds(renderers);
            Vector3 bottom = model.parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            model.localPosition -= bottom;
        }

        private static Bounds WorldBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AdaptMaterials(GameObject model)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || source.shader == null || source.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                    Texture mainTexture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    Material converted = new Material(urpLit) { name = source.name + " (URP)" };
                    if (mainTexture != null) converted.SetTexture("_BaseMap", mainTexture);
                    converted.SetColor("_BaseColor", color);
                    converted.SetFloat("_Smoothness", 0.2f);
                    materials[i] = converted;
                    changed = true;
                }
                if (changed) renderer.materials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private void PlayIdle(GameObject model, CardData card)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null) return;
            AnimationClip[] clips = CardModelRegistry.LoadAnimationClips(card);
            if (clips == null || clips.Length == 0) return;

            AnimationClip[] usable = clips.Where(c => c != null && c.length > 0.01f).ToArray();
            if (usable.Length == 0) return;
            string[] hints = { "idle", "wait", "stand", "normal", "breath", "default" };
            _clip = hints.Select(h => usable.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(h))).FirstOrDefault(c => c != null) ?? usable[0];

            _graph = PlayableGraph.Create("DG Hologram " + card.cardName);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            _playable = AnimationClipPlayable.Create(_graph, _clip);
            _playable.SetApplyFootIK(false);
            output.SetSourcePlayable(_playable);
            _graph.Play();
        }

        public void SetPose(Vector3 localPosition, float yaw, bool defense)
        {
            float bob = Mathf.Sin(Time.time * 2.2f + _phase) * 0.002f;
            transform.localPosition = localPosition + new Vector3(0f, 0.002f + bob, 0f);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float s = defense ? 0.8f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * s, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void Update()
        {
            if (_graph.IsValid() && _clip != null && _playable.GetTime() >= _clip.length)
                _playable.SetTime(0d);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }

    /// <summary>Short-lived flash ring (summons, destruction, hits).</summary>
    public sealed class DuelFlash : MonoBehaviour
    {
        private SpriteRenderer _sprite;
        private float _life;
        private float _duration;
        private Color _color;
        private float _size;

        public static void Spawn(Transform parent, Vector3 localPosition, Color color, float size, float duration = 0.45f)
        {
            GameObject go = new GameObject("DG Flash");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition + new Vector3(0f, 0.004f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            DuelFlash flash = go.AddComponent<DuelFlash>();
            flash._sprite = go.AddComponent<SpriteRenderer>();
            flash._sprite.sprite = DuelVisualResources.GlowSprite;
            flash._color = color;
            flash._size = size;
            flash._duration = duration;
            flash._life = duration;
        }

        private void Update()
        {
            _life -= Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(_life / _duration);
            transform.localScale = Vector3.one * Mathf.Lerp(_size * 0.4f, _size * 1.6f, 1f - Mathf.Pow(1f - k, 2f));
            _sprite.color = new Color(_color.r, _color.g, _color.b, (1f - k) * 0.9f);
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
