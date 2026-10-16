using TrafficGame.Core.Roads;

namespace TrafficGame.Views.Rendering
{
    /// <summary>
    /// Sorting orders of map renderers, bottom to top. Everything on an elevation level (roads, their markings,
    /// traffic on them) is drawn above everything on the levels below, so bridges cover the roads and cars under
    /// them; each level's shadow lies between the two.
    /// </summary>
    public static class MapLayers
    {
        public const int Ground = -1000;
        public const int Roads = 0;
        public const int Congestion = 20;
        public const int JunctionControls = 30;
        public const int Buildings = 50;
        public const int Vehicles = 100;
        public const int Preview = 2000;
        public const int Debug = 3000;

        /// <summary>Distance in sorting order between elevation levels.</summary>
        public const int LevelStride = 200;

        /// <summary>Shadows cast by a level's roads, just below them.</summary>
        public const int Shadows = -10;

        /// <summary>The sorting order of <paramref name="layer"/> on the level <paramref name="height"/> is drawn on.</summary>
        public static int At(int layer, float height) => layer + RoadElevation.LayerOf(height) * LevelStride;

        public static int OnLevel(int layer, int level) => layer + level * LevelStride;
    }
}
