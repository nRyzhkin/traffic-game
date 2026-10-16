using Unity.Mathematics;

namespace TrafficGame.Core.Roads
{
    /// <summary>
    /// Road heights. The player builds on whole levels (0 = ground … <see cref="MaxLevel"/>); ramps between levels
    /// change height linearly along the curve, so points on them have heights in between.
    /// </summary>
    public static class RoadElevation
    {
        /// <summary>Height of one level, meters.</summary>
        public const float LevelHeight = 5f;

        /// <summary>Two levels above the ground are enough to cross anything twice (a road over a road over a road).</summary>
        public const int MaxLevel = 2;

        /// <summary>
        /// Roads whose heights at a crossing differ by at least this much pass over each other; closer than
        /// <see cref="SameHeight"/> they meet in a junction; anything in between cannot be built.
        /// </summary>
        public const float Clearance = LevelHeight;

        public const float SameHeight = 0.5f;

        public static float HeightOf(int level) => math.clamp(level, 0, MaxLevel) * LevelHeight;

        /// <summary>The level a height is drawn on: ramps change layer halfway between levels.</summary>
        public static int LayerOf(float height) => math.clamp((int)math.floor(height / LevelHeight + 0.5f), 0, MaxLevel);
    }
}
