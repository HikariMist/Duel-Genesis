using UnityEngine;

namespace DuelGenesis.Core
{
    public class RuntimeTextMeshFixer : MonoBehaviour
    {
        private bool _fixed;

        private void Start()
        {
            _fixed = FixSigns() > 0;
        }

        private void LateUpdate()
        {
            if (_fixed) return;
            _fixed = FixSigns() > 0;
        }

        public static int FixSigns()
        {
            int fixedCount = 0;
            TextMesh[] meshes = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            foreach (TextMesh mesh in meshes)
            {
                if (mesh == null || mesh.font == null || !mesh.gameObject.name.StartsWith("DG Sign -"))
                    continue;

                MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = mesh.font.material;
                    fixedCount++;
                }
            }

            return fixedCount;
        }
    }
}
