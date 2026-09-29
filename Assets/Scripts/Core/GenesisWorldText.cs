using UnityEngine;

namespace DuelGenesis.Core
{
    /// <summary>
    /// Gives a world-space TextMesh sign a material that respects depth (DuelGenesis/WorldText), so the text
    /// is hidden behind walls and a two-sided sign doesn't show its back text mirrored through the panel.
    /// All signs share one material; it follows the font texture when the dynamic font rebuilds it.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh))]
    public sealed class GenesisWorldText : MonoBehaviour
    {
        private static Material _material;
        private static bool _hooked;

        private void OnEnable() => Apply();

        private void Start() => Apply();

        private void Apply()
        {
            TextMesh text = GetComponent<TextMesh>();
            MeshRenderer r = GetComponent<MeshRenderer>();
            if (text == null || r == null || text.font == null) return;
            Material m = MaterialFor(text.font);
            if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
        }

        private static Material MaterialFor(Font font)
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("DuelGenesis/WorldText");
                if (shader == null) return font.material;
                _material = new Material(shader) { name = "DG World Text", hideFlags = HideFlags.DontSave };
            }
            if (!_hooked)
            {
                Font.textureRebuilt += f =>
                {
                    if (_material != null && f != null && f.material != null) _material.mainTexture = f.material.mainTexture;
                };
                _hooked = true;
            }
            _material.mainTexture = font.material.mainTexture;
            return _material;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession() => _material = null;
    }
}
