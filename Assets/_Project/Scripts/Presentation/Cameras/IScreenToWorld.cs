using Unity.Mathematics;

namespace TrafficGame.Presentation.Cameras
{
    /// <summary>Converts screen pixels (origin bottom-left) to world coordinates.</summary>
    public interface IScreenToWorld
    {
        float2 ScreenToWorld(float2 screenPoint);
    }
}
