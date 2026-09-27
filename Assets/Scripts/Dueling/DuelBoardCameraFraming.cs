using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Late-frame camera framing for the solid Master-Duel-style table.
    /// The angle is intentionally high enough to read the field but low enough
    /// to show the table body, raised rails and supports as real 3D geometry.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class DuelBoardCameraFraming : MonoBehaviour
    {
        private DuelGameController _duel;
        private Transform _table;
        private Camera _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<DuelBoardCameraFraming>() != null)
                return;

            GameObject host = new GameObject("Duel Board Camera Framing");
            host.AddComponent<DuelBoardCameraFraming>();
        }

        private void LateUpdate()
        {
            Resolve();
            if (_duel == null || !_duel.IsActive || _table == null || _camera == null)
                return;

            Vector3 position = _table.TransformPoint(DuelTabletopLayout.CameraLocalPosition);
            Vector3 target = _table.TransformPoint(DuelTabletopLayout.CameraTargetLocalPosition);
            Vector3 up = _table.up.sqrMagnitude > 0.1f ? _table.up : Vector3.up;

            _camera.transform.position = position;
            _camera.transform.rotation = Quaternion.LookRotation(target - position, up);
            _camera.fieldOfView = 42f;
        }

        private void Resolve()
        {
            if (_duel == null)
                _duel = Object.FindFirstObjectByType<DuelGameController>();

            if (_table == null)
            {
                GameObject tableObject = GameObject.Find("Duel Table Prototype");
                if (tableObject != null)
                    _table = tableObject.transform;
            }

            if (_camera == null)
                _camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        }
    }
}
