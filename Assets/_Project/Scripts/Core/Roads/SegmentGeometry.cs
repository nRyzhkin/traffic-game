using TrafficGame.Core.Geometry;

namespace TrafficGame.Core.Roads
{
    /// <summary>Sampled centerline of a segment and the part of it left after the junctions at both ends.</summary>
    public sealed class SegmentGeometry
    {
        internal SegmentGeometry(RoadSegment segment, Polyline centerline, float trimStart, float trimEnd)
        {
            Segment = segment;
            Centerline = centerline;
            TrimStart = trimStart;
            TrimEnd = trimEnd;
            Trimmed = centerline.Slice(trimStart, centerline.Length - trimEnd);
        }

        public RoadSegment Segment { get; }

        /// <summary>The whole centerline from the start node to the end node.</summary>
        public Polyline Centerline { get; }

        /// <summary>Height at a distance along <see cref="Centerline"/>.</summary>
        public float HeightAt(float along) => Segment.HeightAt(Centerline.ParameterAt(along));

        public float TrimStart { get; }
        public float TrimEnd { get; }

        /// <summary>The centerline between the junction areas; this is where lanes and markings run.</summary>
        public Polyline Trimmed { get; }
    }
}
