using Unity.Mathematics;

namespace TrafficGame.Presentation
{
    /// <summary>Ease curves for animations: map 0–1 progress to 0–1 position.</summary>
    public static class Easing
    {
        public static float OutCubic(float t)
        {
            t = 1f - math.saturate(t);
            return 1f - t * t * t;
        }

        public static float InOutCubic(float t)
        {
            t = math.saturate(t);
            return t < 0.5f ? 4f * t * t * t : 1f - math.pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> with time constant <paramref name="time"/>.</summary>
        public static float Approach(float current, float target, float time, float deltaTime) =>
            time <= 0f ? target : math.lerp(current, target, 1f - math.exp(-deltaTime / time));
    }
}
