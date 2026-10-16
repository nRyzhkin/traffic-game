using Unity.Mathematics;

namespace TrafficGame.Presentation.Cameras
{
    /// <summary>Orthographic top-down camera that shows the map.</summary>
    public interface ICameraView
    {
        /// <summary>Viewport size in screen pixels.</summary>
        float2 ViewportSize { get; }

        /// <summary>Places the camera over <paramref name="center"/>, showing <paramref name="halfHeight"/> world units above and below it.</summary>
        void Show(float2 center, float halfHeight);
    }
}
