using System;
using TrafficGame.Core.Geometry;
using Unity.Mathematics;

namespace TrafficGame.Core.Roads
{
    /// <summary>Special properties of a segment.</summary>
    [Flags]
    public enum SegmentTraits
    {
        None = 0,

        /// <summary>Part of a roundabout ring: traffic on it has priority over entering traffic.</summary>
        Roundabout = 1,

        /// <summary>Placed by the level; the player cannot bulldoze it (but may join or cross it).</summary>
        Locked = 2
    }

    /// <summary>
    /// A road between two nodes. Its curve starts at <see cref="Start"/> and ends at <see cref="End"/>; its height
    /// changes linearly with the curve parameter between the heights of the two nodes (a ramp when they differ).
    /// </summary>
    public sealed class RoadSegment
    {
        internal RoadSegment(SegmentId id, NodeId start, NodeId end, CubicBezier curve, RoadProfile profile, SegmentTraits traits,
            float startHeight, float endHeight)
        {
            Id = id;
            Start = start;
            End = end;
            Curve = curve;
            Profile = profile;
            Traits = traits;
            StartHeight = startHeight;
            EndHeight = endHeight;
        }

        public SegmentId Id { get; }
        public NodeId Start { get; }
        public NodeId End { get; }
        public CubicBezier Curve { get; }
        public RoadProfile Profile { get; }

        public SegmentTraits Traits { get; }
        public float StartHeight { get; }
        public float EndHeight { get; }
        public bool IsRamp => math.abs(EndHeight - StartHeight) > 1e-3f;

        /// <summary>Height at curve parameter <paramref name="t"/> (0 at <see cref="Start"/>, 1 at <see cref="End"/>).</summary>
        public float HeightAt(float t) => math.lerp(StartHeight, EndHeight, math.saturate(t));
        public bool IsRoundabout => (Traits & SegmentTraits.Roundabout) != 0;
        public bool IsLocked => (Traits & SegmentTraits.Locked) != 0;

        public NodeId OtherEnd(NodeId node)
        {
            if (node == Start)
                return End;
            if (node == End)
                return Start;
            throw new ArgumentException($"{node} is not an end of {Id}.", nameof(node));
        }
    }
}
