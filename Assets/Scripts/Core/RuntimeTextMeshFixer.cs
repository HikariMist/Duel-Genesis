using UnityEngine;

namespace DuelGenesis.Core
{
    public class RuntimeTextMeshFixer : MonoBehaviour
    {
        private void Start()
        {
            FixSigns();
        }

        public static void FixSigns()
        {
            TextMesh[] meshes = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            foreach (TextMesh mesh in meshes)
            {
                if (mesh == null || mesh.font == null || !mesh.gameObject.name.StartsWith("DG Sign -"))
                    continue;

                MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = mesh.font.material;
            }
        }
    }
}
