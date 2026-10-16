using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;

namespace TrafficGame.Core.Lanes
{
    /// <summary>One lane of a segment, running between the junction areas in its direction of travel.</summary>
    public sealed class Lane
    {
        internal Lane(int id, SegmentId segment, bool isForward, int index, NodeId from, NodeId to, Polyline path)
        {
            Id = id;
            Segment = segment;
            IsForward = isForward;
            Index = index;
            From = from;
            To = to;
            Path = path;
        }

        /// <summary>Dense index within its <see cref="LaneGraph"/>.</summary>
        public int Id { get; }

        public SegmentId Segment { get; }

        /// <summary>True when the lane runs from the segment start to its end.</summary>
        public bool IsForward { get; }

        /// <summary>0 is the leftmost lane in the direction of travel (next to the divider).</summary>
        public int Index { get; }

        public NodeId From { get; }
        public NodeId To { get; }

        /// <summary>Centerline in the direction of travel.</summary>
        public Polyline Path { get; }

        /// <summary>Turns this lane leads to at the junction it ends at; none before bends and dead ends.</summary>
        public LaneArrows Arrows { get; internal set; }

        /// <summary>Every turn the junction offers from this lane's road.</summary>
        public LaneArrows AvailableArrows { get; internal set; }

        /// <summary>The arrows the lane has unless the player paints others.</summary>
        public LaneArrows DefaultArrows { get; internal set; }
    }
}
