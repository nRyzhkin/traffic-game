using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    public enum GuideKind
    {
        None,

        /// <summary>Straight on from a road ending at the start point.</summary>
        Continuation,

        /// <summary>At a right angle to a road at the start point or near the pointer.</summary>
        Perpendicular,

        /// <summary>Parallel to a road near the pointer.</summary>
        Parallel
    }

    /// <summary>An alignment line the road end was snapped onto.</summary>
    public readonly struct RoadGuide
    {
        public RoadGuide(GuideKind kind, float2 origin, float2 direction)
        {
            Kind = kind;
            Origin = origin;
            Direction = direction;
        }

        public GuideKind Kind { get; }
        public float2 Origin { get; }
        public float2 Direction { get; }

        public static RoadGuide None => default;
    }

    /// <summary>Aligns the direction of a new road with the roads around it when the pointer is close to such an alignment.</summary>
    public sealed class RoadGuides
    {
        private readonly float _snapAngle;
        private readonly float _searchRadius;

        /// <param name="snapAngleDegrees">Maximum deviation from a guide direction that still snaps.</param>
        /// <param name="searchRadius">Roads closer than this to the pointer offer parallel/perpendicular guides.</param>
        public RoadGuides(float snapAngleDegrees, float searchRadius)
        {
            _snapAngle = math.radians(snapAngleDegrees);
            _searchRadius = searchRadius;
        }

        /// <summary>Returns <paramref name="target"/> projected onto the best matching guide from <paramref name="start"/>, or unchanged.</summary>
        public float2 Apply(RoadAnchor start, float2 target, RoadNetwork network, RoadLayout layout, out RoadGuide guide)
        {
            guide = RoadGuide.None;
            var offset = target - start.Position;
            var length = math.length(offset);
            if (length < 1e-3f)
                return target;

            var direction = offset / length;
            var bestAngle = _snapAngle;
            foreach (var (kind, candidate) in Candidates(start, target, network, layout))
            {
                var angle = math.acos(math.clamp(math.dot(direction, candidate), -1f, 1f));
                if (angle >= bestAngle)
                    continue;

                bestAngle = angle;
                guide = new RoadGuide(kind, start.Position, candidate);
            }

            return guide.Kind == GuideKind.None ? target : start.Position + guide.Direction * math.dot(offset, guide.Direction);
        }

        private List<(GuideKind kind, float2 direction)> Candidates(RoadAnchor start, float2 target, RoadNetwork network, RoadLayout layout)
        {
            var candidates = new List<(GuideKind, float2)>();

            void AddPerpendiculars(float2 direction)
            {
                candidates.Add((GuideKind.Perpendicular, Geometry2D.Left(direction)));
                candidates.Add((GuideKind.Perpendicular, -Geometry2D.Left(direction)));
            }

            if (start.Kind == AnchorKind.Node)
            {
                foreach (var id in network.GetNode(start.Node).Segments)
                {
                    var outward = OutwardDirection(layout.Segments[id], start.Node);
                    candidates.Add((GuideKind.Continuation, -outward));
                    AddPerpendiculars(outward);
                }
            }
            else if (start.Kind == AnchorKind.Segment)
            {
                var centerline = layout.Segments[start.Segment].Centerline;
                var along = centerline.ClosestDistance(start.Position, out _);
                AddPerpendiculars(centerline.DirectionAt(along));
            }

            SegmentGeometry nearest = null;
            var nearestDistance = _searchRadius;
            foreach (var geometry in layout.Segments.Values)
            {
                if (start.Touches(geometry.Segment))
                    continue;

                var distance = geometry.Centerline.DistanceTo(target);
                if (distance < nearestDistance)
                {
                    nearest = geometry;
                    nearestDistance = distance;
                }
            }

            if (nearest != null)
            {
                var tangent = nearest.Centerline.DirectionAt(nearest.Centerline.ClosestDistance(target, out _));
                candidates.Add((GuideKind.Parallel, tangent));
                candidates.Add((GuideKind.Parallel, -tangent));
                AddPerpendiculars(tangent);
            }

            return candidates;
        }

        private static float2 OutwardDirection(SegmentGeometry geometry, NodeId node) =>
            geometry.Segment.Start == node ? geometry.Centerline.StartDirection : -geometry.Centerline.EndDirection;
    }
}
