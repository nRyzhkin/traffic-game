using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;

namespace TrafficGame.Core.Lanes
{
    public enum TurnDirection
    {
        Straight,
        Left,
        Right,
        UTurn
    }

    /// <summary>A drivable path through a junction from the end of one lane to the start of another.</summary>
    public sealed class LaneConnection
    {
        internal LaneConnection(Lane from, Lane to, NodeId node, TurnDirection turn, Polyline path)
        {
            From = from;
            To = to;
            Node = node;
            Turn = turn;
            Path = path;
        }

        public Lane From { get; }
        public Lane To { get; }
        public NodeId Node { get; }
        public TurnDirection Turn { get; }
        public Polyline Path { get; }
    }
}
