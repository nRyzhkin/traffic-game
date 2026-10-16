using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    /// <summary>Why a planned road cannot be built.</summary>
    public enum BuildError
    {
        None,
        TooShort,
        CurveTooSharp,
        AngleTooSharp,
        JunctionTooClose,
        OverlapsRoad,
        OutOfBounds,
        InsufficientFunds,

        /// <summary>The ramp climbs or falls too fast for its length.</summary>
        TooSteep,

        /// <summary>Crosses a road neither at the same height (a junction) nor a full level above or below it.</summary>
        HeightConflict
    }

    /// <summary>What the player asked to build.</summary>
    public readonly struct RoadBuildRequest
    {
        public RoadBuildRequest(RoadAnchor start, RoadAnchor end, float2? control, RoadProfile profile)
        {
            Start = start;
            End = end;
            Control = control;
            Profile = profile;
        }

        public RoadAnchor Start { get; }
        public RoadAnchor End { get; }

        /// <summary>Curve control point; null for a straight road.</summary>
        public float2? Control { get; }

        public RoadProfile Profile { get; }
    }

    /// <summary>A new road crossing an existing segment; a junction is created there.</summary>
    public readonly struct RoadCrossing
    {
        public RoadCrossing(SegmentId segment, float segmentParameter, float newParameter, float2 point)
        {
            Segment = segment;
            SegmentParameter = segmentParameter;
            NewParameter = newParameter;
            Point = point;
        }

        public SegmentId Segment { get; }
        public float SegmentParameter { get; }

        /// <summary>Curve parameter of the crossing on the new road.</summary>
        public float NewParameter { get; }

        public float2 Point { get; }
    }

    /// <summary>An existing node the new road runs through; the road is joined to it there.</summary>
    public readonly struct RoadPassThrough
    {
        public RoadPassThrough(NodeId node, float newParameter, float along)
        {
            Node = node;
            NewParameter = newParameter;
            Along = along;
        }

        public NodeId Node { get; }
        public float NewParameter { get; }
        public float Along { get; }
    }

    /// <summary>A validated proposal for a new road: its shape, price and whether it can be built.</summary>
    public sealed class RoadPlan
    {
        internal RoadPlan(RoadBuildRequest request, CubicBezier curve, Polyline path, long cost, BuildError error,
            float? connectionAngle, IReadOnlyList<RoadCrossing> crossings, IReadOnlyList<RoadPassThrough> passThroughs,
            float startHeight, float endHeight)
        {
            Request = request;
            StartHeight = startHeight;
            EndHeight = endHeight;
            Curve = curve;
            Path = path;
            Cost = cost;
            Error = error;
            ConnectionAngle = connectionAngle;
            Crossings = crossings;
            PassThroughs = passThroughs;
        }

        public RoadBuildRequest Request { get; }
        public CubicBezier Curve { get; }

        /// <summary>Heights at both ends, meters; the road changes height linearly with the curve parameter.</summary>
        public float StartHeight { get; }

        public float EndHeight { get; }

        public float HeightAt(float t) => math.lerp(StartHeight, EndHeight, math.saturate(t));

        /// <summary>Sampled centerline; null when start and end coincide.</summary>
        public Polyline Path { get; }

        public float Length => Path?.Length ?? 0f;
        public long Cost { get; }
        public BuildError Error { get; }
        public bool IsValid => Error == BuildError.None;

        /// <summary>Angle in degrees to the road at the start point (deviation from straight on at a dead end), if attached.</summary>
        public float? ConnectionAngle { get; }

        public IReadOnlyList<RoadCrossing> Crossings { get; }

        /// <summary>Existing nodes on the way (for example driveway ends a street is drawn straight through).</summary>
        public IReadOnlyList<RoadPassThrough> PassThroughs { get; }
    }
}
