namespace TrafficGame.Presentation.Cameras
{
    /// <summary>How much of each screen edge the UI covers, in screen pixels.</summary>
    public readonly struct ScreenInsets
    {
        public ScreenInsets(float top, float bottom, float left, float right)
        {
            Top = top;
            Bottom = bottom;
            Left = left;
            Right = right;
        }

        public float Top { get; }
        public float Bottom { get; }
        public float Left { get; }
        public float Right { get; }
    }

    /// <summary>The part of the screen the map can be seen through.</summary>
    public interface IMapViewport
    {
        ScreenInsets MapInsets { get; }
    }
}
