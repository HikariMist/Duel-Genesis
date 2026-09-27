using UnityEngine;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// Late-frame camera framing for the solid duel table.
    /// The view stays readable for card play while showing enough of the table body
    /// and front legs that the duel clearly takes place on a real piece of furniture.
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
            _camera.fieldOfView = 45f;
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
