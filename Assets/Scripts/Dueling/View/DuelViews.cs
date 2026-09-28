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

    /// <summary>
    /// Tabletop hologram for a face-up monster: the card's DMO 3D model standing on its card, or, for the
    /// few cards the model library does not cover, a floating holographic image of the card itself.
    /// </summary>
    public sealed class MonsterHologram : MonoBehaviour
    {
        public const float TargetHeight = 0.14f;    // metres above the card (a 14 cm figure on an 8.6 cm card)
        public const float MaxFootprint = 0.11f;    // keeps wide monsters from swallowing the neighbouring zones

        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private AnimationClip _clip;
        private AnimationClip _attackClip, _defenseClip;
        private bool _showingDefense;
        private float _phase;
        private Renderer _standee;
        private bool _standeeHasFace;

        public DuelCard Card { get; private set; }
        public bool IsModel { get; private set; }

        public static MonsterHologram TryCreate(Transform parent, DuelCard card)
        {
            GameObject prefab = CardModelRegistry.LoadPrefab(card.Data);
            GameObject holder = new GameObject("Monster Hologram - " + card.Name);
            holder.transform.SetParent(parent, false);
            MonsterHologram hologram = holder.AddComponent<MonsterHologram>();
            hologram.Card = card;
            hologram._phase = Random.value * 10f;

            if (prefab == null)
            {
                hologram.BuildStandee();
                return hologram;
            }

            // The model sits under a pivot we own. DMO clips animate the model root's own position, which
            // overwrote the scale-and-ground offset and left many monsters floating high above the card.
            Transform pivot = new GameObject("Model Pivot").transform;
            pivot.SetParent(holder.transform, false);
            GameObject model = Instantiate(prefab, pivot);
            model.name = "Model";
            PrepareModel(model);
            Normalize(model.transform, pivot);
            hologram.IsModel = true;
            hologram._pivot = pivot;
            hologram._model = model.transform;
            hologram._pivotBase = pivot.localPosition;
            hologram.PlayIdle(model, card.Data);
            hologram.BeginGrounding();
            return hologram;
        }

        /// <summary>Strips viewer furniture from a DMO prefab and makes its materials URP-safe.</summary>
        public static void PrepareModel(GameObject model)
        {
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) DestroySafe(c);
            // Viewer prefabs can carry scene furniture (cameras, listeners, UI, event systems): strip it.
            foreach (Camera c in model.GetComponentsInChildren<Camera>(true)) DestroySafe(c);
            foreach (AudioListener l in model.GetComponentsInChildren<AudioListener>(true)) DestroySafe(l);
            foreach (Light l in model.GetComponentsInChildren<Light>(true)) DestroySafe(l);
            foreach (Canvas c in model.GetComponentsInChildren<Canvas>(true)) DestroySafe(c.gameObject);
            foreach (UnityEngine.EventSystems.BaseInputModule m in model.GetComponentsInChildren<UnityEngine.EventSystems.BaseInputModule>(true)) DestroySafe(m);
            foreach (UnityEngine.EventSystems.EventSystem e in model.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)) DestroySafe(e);
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null) b.enabled = false;   // viewer scripts must not run on the table
            foreach (ParticleSystem ps in model.GetComponentsInChildren<ParticleSystem>(true))
            {
                // Stray effects blow up the bounds and never read at 14 cm.
                if (ps.transform != model.transform && ps.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0 &&
                    ps.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    ps.gameObject.SetActive(false);
                else if (ps.TryGetComponent(out ParticleSystemRenderer psr))
                    psr.enabled = false;
            }
            AdaptMaterials(model);
        }

        private static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>Scales the model to figure size and stands it on the card. The scale and offset go on
        /// <paramref name="target"/> (the model itself by default, or a pivot above it).</summary>
        public static void Normalize(Transform model, Transform target = null)
        {
            if (target == null) target = model;
            Renderer[] renderers = VisibleRenderers(model);
            if (renderers.Length == 0) return;
            Bounds bounds = CoreBounds(renderers);
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            float scale = Mathf.Min(TargetHeight / Mathf.Max(bounds.size.y, 0.0001f), MaxFootprint / Mathf.Max(footprint, 0.0001f));
            if (float.IsInfinity(scale) || scale <= 0f) return;
            target.localScale *= scale;

            // Stand the model on the card surface, centred on the card (using its body, not a long weapon or tail).
            bounds = CoreBounds(renderers);
            Bounds all = WorldBounds(renderers);
            Vector3 bottom = target.parent.InverseTransformPoint(new Vector3(bounds.center.x, all.min.y, bounds.center.z));
            target.localPosition -= bottom;
        }

        // ---------------------------------------------------------------- grounding on the animated pose

        private Transform _pivot, _model;
        private Vector3 _pivotBase;
        private float _groundUntil, _nextGroundSample;
        private float _lowestRaw = float.PositiveInfinity;
        private static Mesh _bakeBuffer;
        private static readonly List<Vector3> BakedVertices = new();

        /// <summary>Watches the playing clip for a moment and keeps its lowest point on the card surface.
        /// Bind-pose bounds lie for many DMO rigs (the idle pose crouches, hovers or is offset), so this measures
        /// the real skinned vertices.</summary>
        private void BeginGrounding()
        {
            float span = _clip != null ? Mathf.Clamp(_clip.length, 0.5f, 4f) : 0.5f;
            _groundUntil = Time.time + span + 0.3f;
            _nextGroundSample = Time.time + 0.1f;      // let the clip evaluate first: the bind pose is what lies
            _lowestRaw = float.PositiveInfinity;
        }

        private void LateUpdate()
        {
            if (_pivot == null || _model == null || Time.time > _groundUntil || Time.time < _nextGroundSample) return;
            _nextGroundSample = Time.time + 0.06f;
            if (!TryLowestPoint(out float lowest)) return;

            float offset = _pivot.localPosition.y - _pivotBase.y;
            float raw = lowest - offset;               // lowest point as if no grounding offset were applied
            if (raw >= _lowestRaw) return;
            _lowestRaw = raw;
            _pivot.localPosition = new Vector3(_pivotBase.x, _pivotBase.y - _lowestRaw, _pivotBase.z);
        }

        /// <summary>Lowest vertex of the visible model, in this hologram's local space.</summary>
        private bool TryLowestPoint(out float lowest)
        {
            lowest = float.PositiveInfinity;
            foreach (Renderer r in VisibleRenderers(_model))
            {
                if (r is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null) continue;
                    if (_bakeBuffer == null) _bakeBuffer = new Mesh { name = "DG Hologram Bake" };
                    smr.BakeMesh(_bakeBuffer, true);
                    _bakeBuffer.GetVertices(BakedVertices);
                    Matrix4x4 toLocal = transform.worldToLocalMatrix * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    foreach (Vector3 v in BakedVertices) lowest = Mathf.Min(lowest, toLocal.MultiplyPoint3x4(v).y);
                }
                else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                {
                    mf.sharedMesh.GetVertices(BakedVertices);
                    Matrix4x4 toLocal = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    foreach (Vector3 v in BakedVertices) lowest = Mathf.Min(lowest, toLocal.MultiplyPoint3x4(v).y);
                }
                else
                {
                    lowest = Mathf.Min(lowest, transform.InverseTransformPoint(r.bounds.min).y);
                }
            }
            return !float.IsInfinity(lowest);
        }

        /// <summary>
        /// Bounds of the model's body: the densest mesh plus every part that touches it. Long spears, whips,
        /// trails and detached effect meshes are left out so they do not shrink the monster to a speck.
        /// </summary>
        public static Bounds CoreBounds(Renderer[] renderers)
        {
            Renderer primary = renderers.OrderByDescending(VertexCount).First();
            Bounds core = primary.bounds;
            Bounds grow = core;
            grow.Expand(core.size * 0.25f);
            foreach (Renderer r in renderers)
            {
                if (r == primary) continue;
                Bounds b = r.bounds;
                // Accept parts that overlap the body and are not wildly larger than it in any direction.
                if (!grow.Intersects(b)) continue;
                if (b.size.x > core.size.x * 3f || b.size.y > core.size.y * 3f || b.size.z > core.size.z * 3f) continue;
                core.Encapsulate(b);
            }
            return core;
        }

        private static int VertexCount(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0;
        }

        public static Renderer[] VisibleRenderers(Transform model) =>
            model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();

        private static Bounds WorldBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static readonly string[] MainTexNames = { "_BaseMap", "_MainTex", "_MainTexture", "_Texture", "_Albedo", "_Diffuse" };

        private static void AdaptMaterials(GameObject model)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || source.shader == null) continue;
                    string shaderName = source.shader.name;
                    if (shaderName.StartsWith("Universal Render Pipeline/") || shaderName.StartsWith("Shader Graphs/")) continue;

                    Texture mainTexture = null;
                    foreach (string n in MainTexNames)
                        if (source.HasProperty(n) && (mainTexture = source.GetTexture(n)) != null) break;
                    Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    float mode = source.HasProperty("_Mode") ? source.GetFloat("_Mode") : 0f;
                    bool cutout = mode == 1f || source.IsKeywordEnabled("_ALPHATEST_ON") || shaderName.Contains("Cutout");
                    bool transparent = mode >= 2f || shaderName.Contains("Transparent");

                    Material converted = new Material(urpLit) { name = source.name + " (URP)" };
                    if (mainTexture != null) converted.SetTexture("_BaseMap", mainTexture);
                    converted.SetColor("_BaseColor", color);
                    converted.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? Mathf.Min(source.GetFloat("_Glossiness"), 0.4f) : 0.2f);
                    if (cutout || transparent)
                    {
                        // Hair cards, wings and fins: cut out and double sided reads far better than blending at this size.
                        converted.SetFloat("_AlphaClip", 1f);
                        converted.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
                        converted.SetFloat("_Cull", 0f);
                        converted.EnableKeyword("_ALPHATEST_ON");
                        converted.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    }
                    materials[i] = converted;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        // ---------------------------------------------------------------- card-image fallback

        private void BuildStandee()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Hologram Card";
            DestroySafe(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            float height = TargetHeight * 0.85f;
            float width = height * DuelMatLayout.CardWidthMm / DuelMatLayout.CardHeightMm;
            quad.transform.localScale = new Vector3(width, height, 1f);
            quad.transform.localPosition = new Vector3(0f, height * 0.5f + 0.012f, 0f);

            Material m = new Material(DuelVisualResources.UnlitShader) { name = "DG Hologram Card" };
            m.SetColor("_BaseColor", new Color(0.75f, 0.92f, 1f, 0.85f));
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            _standee = quad.GetComponent<Renderer>();
            _standee.sharedMaterial = m;
            _standee.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            TryApplyStandeeFace();

            // Projector glow under the floating card.
            GameObject glow = new GameObject("Projector Glow");
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.001f, 0f);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = Vector3.one * 0.55f;
            SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
            sr.sprite = DuelVisualResources.GlowSprite;
            sr.color = new Color(0.35f, 0.8f, 1f, 0.45f);
        }

        private void TryApplyStandeeFace()
        {
            if (_standee == null || _standeeHasFace || Card == null) return;
            if (!CardFaceCompositor.TryGetFace(Card.Data, out Texture2D face) || face == null) return;
            _standee.sharedMaterial.SetTexture("_BaseMap", face);
            _standeeHasFace = true;
        }

        // ---------------------------------------------------------------- animation

        private void PlayIdle(GameObject model, CardData card)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            AnimationClip[] clips = CardModelRegistry.LoadAnimationClips(card);
            _attackClip = PickIdleClip(clips);
            _defenseClip = PickDefenseClip(clips);
            _clip = _attackClip;
            if (_clip == null) return;

            _graph = PlayableGraph.Create("DG Hologram " + card.cardName);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            _playable = AnimationClipPlayable.Create(_graph, _clip);
            _playable.SetApplyFootIK(false);
            output.SetSourcePlayable(_playable);
            _graph.Play();
        }

        /// <summary>The DMO clip for defence position, if the character has one.</summary>
        public static AnimationClip PickDefenseClip(AnimationClip[] clips) =>
            clips?.FirstOrDefault(c => c != null && c.length > 0.01f && c.name.ToLowerInvariant().Contains("defense") && c.name.ToLowerInvariant().Contains("idle"));

        public static AnimationClip PickIdleClip(AnimationClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            AnimationClip[] usable = clips.Where(c => c != null && c.length > 0.01f).ToArray();
            if (usable.Length == 0) return null;
            // Attack-position idle first (most DMO characters have one), then any other idle-like loop.
            AnimationClip attackIdle = usable.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("attack_position_idle") && !c.name.ToLowerInvariant().Contains("no_wobble"))
                                       ?? usable.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("attack_position_idle"));
            if (attackIdle != null) return attackIdle;
            string[] hints = { "idle", "wait", "stand", "normal", "breath", "default", "loop" };
            string[] avoid = { "die", "dead", "death", "damage", "hit", "attack", "down" };
            return hints.Select(h => usable.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(h))).FirstOrDefault(c => c != null)
                   ?? usable.FirstOrDefault(c => !avoid.Any(a => c.name.ToLowerInvariant().Contains(a)))
                   ?? usable[0];
        }

        public void SetPose(Vector3 localPosition, float yaw, bool defense)
        {
            float bob = Mathf.Sin(Time.time * 2.2f + _phase) * (IsModel ? 0.002f : 0.004f);
            transform.localPosition = localPosition + new Vector3(0f, 0.002f + bob, 0f);
            if (IsModel)
            {
                // Defence position: play the character's guard clip when it has one, otherwise turn side-on like the card.
                if (defense != _showingDefense && _graph.IsValid())
                {
                    _showingDefense = defense;
                    AnimationClip next = defense && _defenseClip != null ? _defenseClip : _attackClip;
                    if (next != null && next != _clip) SwapClip(next);
                }
                bool turn = defense && _defenseClip == null;
                transform.localRotation = Quaternion.Euler(0f, yaw + (turn ? 90f : 0f), 0f);
            }
            else
            {
                // The holographic card always turns to face whoever is looking at the table.
                Camera cam = Camera.main;
                if (cam != null && transform.parent != null)
                {
                    Vector3 toCam = transform.parent.InverseTransformDirection(cam.transform.position - transform.position);
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 1e-6f)
                        transform.localRotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                }
            }
            float s = defense ? 0.8f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * s, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void SwapClip(AnimationClip next)
        {
            var output = (AnimationPlayableOutput)_graph.GetOutput(0);
            AnimationClipPlayable old = _playable;
            _playable = AnimationClipPlayable.Create(_graph, next);
            _playable.SetApplyFootIK(false);
            output.SetSourcePlayable(_playable);
            if (old.IsValid()) old.Destroy();
            _clip = next;
            if (_pivot != null)
            {
                _pivot.localPosition = _pivotBase;
                BeginGrounding();
            }
        }

        private void Update()
        {
            if (_graph.IsValid() && _clip != null && _playable.GetTime() >= _clip.length)
                _playable.SetTime(0d);
            if (_standee != null && !_standeeHasFace) TryApplyStandeeFace();
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
