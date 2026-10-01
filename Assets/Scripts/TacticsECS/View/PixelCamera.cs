using UnityEngine;

namespace TacticsECS
{
    /// <summary>Integer texel scale and snapped camera origin without an additional package.</summary>
    [RequireComponent(typeof(Camera))]
    public class PixelCamera : MonoBehaviour
    {
        public float RequestedSize = 5f;
        public Vector3 RequestedPosition;
        public float WorldCellSize = 1f;
        private Camera _camera;
        private void Awake() => _camera = GetComponent<Camera>();
        private void LateUpdate() => Apply();
        public void Apply()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            float ppu = PixelSpriteCatalog.PixelsPerUnit / WorldCellSize;
            int scale = Mathf.Max(1, Mathf.FloorToInt(_camera.pixelHeight / (RequestedSize * 2f * ppu) + .0001f));
            _camera.orthographicSize = _camera.pixelHeight / (2f * ppu * scale);
            var position = RequestedPosition;
            position.x = Mathf.Round(position.x * ppu) / ppu;
            position.y = Mathf.Round(position.y * ppu) / ppu;
            transform.position = position;
        }
    }
}
