using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using Unity.Mathematics;

namespace TrafficGame.Core.Roads
{
    /// <summary>Where a segment meets a junction: the trimmed end of the segment.</summary>
    public readonly struct JunctionApproach
    {
        internal JunctionApproach(SegmentId segment, bool atSegmentStart, float2 position, float2 direction, float halfWidth)
        {
            Segment = segment;
            AtSegmentStart = atSegmentStart;
            Position = position;
            Direction = direction;
            Left = position + Geometry2D.Left(direction) * halfWidth;
            Right = position - Geometry2D.Left(direction) * halfWidth;
        }

        public SegmentId Segment { get; }

        /// <summary>True when the junction's node is the start of the segment.</summary>
        public bool AtSegmentStart { get; }

        /// <summary>Centerline point at the trimmed end.</summary>
        public float2 Position { get; }

        /// <summary>Unit direction pointing away from the junction, along the segment.</summary>
        public float2 Direction { get; }

        /// <summary>Road edge corners, as seen when leaving the junction along <see cref="Direction"/>.</summary>
        public float2 Left { get; }

        public float2 Right { get; }
    }

    /// <summary>Drawable and drivable shape of a node: the area connecting its segments.</summary>
    public sealed class JunctionGeometry
    {
        internal JunctionGeometry(NodeId node, float2 center, float height, IReadOnlyList<JunctionApproach> approaches,
            IReadOnlyList<Polyline> curbs, IReadOnlyList<float2> outline)
        {
            Node = node;
            Center = center;
            Height = height;
            Approaches = approaches;
            Curbs = curbs;
            Outline = outline;
        }

        public NodeId Node { get; }
        public float2 Center { get; }

        /// <summary>Meters above the ground.</summary>
        public float Height { get; }

        /// <summary>Approaches sorted counter-clockwise around the node.</summary>
        public IReadOnlyList<JunctionApproach> Approaches { get; }

        /// <summary>Curb lines between neighbouring approaches; across the road end for a dead end.</summary>
        public IReadOnlyList<Polyline> Curbs { get; }

        /// <summary>Boundary of the junction surface, counter-clockwise; empty for a dead end.</summary>
        public IReadOnlyList<float2> Outline { get; }

        public bool IsDeadEnd => Approaches.Count == 1;
    }
}
