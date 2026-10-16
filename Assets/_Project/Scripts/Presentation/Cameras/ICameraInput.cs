using System;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Cameras
{
    /// <summary>
    /// Device-independent camera gestures. Screen coordinates are pixels with the origin
    /// in the bottom-left corner and Y pointing up.
    /// </summary>
    public interface ICameraInput
    {
        /// <summary>The map was dragged by a screen-space delta.</summary>
        event Action<float2> Panned;

        /// <summary>The finger or button that dragged the map was released.</summary>
        event Action PanEnded;

        /// <summary>Continuous zoom (pinch) around a screen point; a factor above one zooms in.</summary>
        event Action<float2, float> Zoomed;

        /// <summary>A discrete zoom step (mouse wheel) around a screen point; the camera eases into it.</summary>
        event Action<float2, float> ZoomStepped;
    }
}
