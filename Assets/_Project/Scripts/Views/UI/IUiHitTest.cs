using Unity.Mathematics;

namespace TrafficGame.Views.UI
{
    /// <summary>Tells map input whether a screen point is covered by an interactive UI element.</summary>
    public interface IUiHitTest
    {
        /// <param name="screenPosition">Screen pixels, origin bottom-left (Input System convention).</param>
        bool IsOverUi(float2 screenPosition);
    }

    /// <summary>Thresholds shared by the map input adapters, in screen pixels and seconds.</summary>
    public static class PointerGestures
    {
        /// <summary>A press that moves further than this becomes a drag (camera pan) instead of a click or tap.</summary>
        public const float DragSlop = 10f;

        public const float DoubleTapSeconds = 0.35f;
        public const float DoubleTapDistance = 40f;
    }
}
