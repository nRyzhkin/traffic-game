using TrafficGame.Presentation.Cameras;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.Views.Cameras
{
    /// <summary>Orthographic camera looking down at the XY plane.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraView : MonoBehaviour, ICameraView
    {
        private const float CameraZ = -100f;

        private Camera _camera;

        public float2 ViewportSize => new(Camera.pixelWidth, Camera.pixelHeight);

        private Camera Camera
        {
            get
            {
                if (_camera == null)
                {
                    _camera = GetComponent<Camera>();
                    _camera.orthographic = true;
                }

                return _camera;
            }
        }

        public void Show(float2 center, float halfHeight)
        {
            transform.position = new Vector3(center.x, center.y, CameraZ);
            Camera.orthographicSize = halfHeight;
        }
    }
}
