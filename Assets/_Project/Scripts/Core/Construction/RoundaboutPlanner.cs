using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    /// <summary>An existing road crossing the planned ring; it becomes an entry/exit of the roundabout.</summary>
    public readonly struct RingCrossing
    {
        public RingCrossing(SegmentId segment, float segmentParameter, float angle)
        {
            Segment = segment;
            SegmentParameter = segmentParameter;
            Angle = angle;
        }

        public SegmentId Segment { get; }
        public float SegmentParameter { get; }

        /// <summary>Polar angle of the crossing around the ring center, radians in [0, 2π).</summary>
        public float Angle { get; }
    }

    /// <summary>A validated proposal for a roundabout.</summary>
    public sealed class RoundaboutPlan
    {
        internal RoundaboutPlan(float2 center, float radius, float height, RoadProfile profile, Polyline ring, long cost, BuildError error,
            IReadOnlyList<RingCrossing> crossings)
        {
            Height = height;
            Center = center;
            Radius = radius;
            Profile = profile;
            Ring = ring;
            Cost = cost;
            Error = error;
            Crossings = crossings;
        }

        public float2 Center { get; }
        public float Radius { get; }

        /// <summary>The ring lies flat at this height, meters.</summary>
        public float Height { get; }

        public RoadProfile Profile { get; }

        /// <summary>The ring centerline, counter-clockwise, starting and ending at angle 0.</summary>
        public Polyline Ring { get; }

        public long Cost { get; }
        public BuildError Error { get; }
        public bool IsValid => Error == BuildError.None;
        public IReadOnlyList<RingCrossing> Crossings { get; }
    }

    /// <summary>
    /// Plans a one-way, counter-clockwise ring (right-hand traffic) on one height. Every road crossing the circle at
    /// that height becomes an approach and whatever lies inside it at that height will be removed; roads a full level
    /// above or below pass over or under it.
    /// </summary>
    public sealed class RoundaboutPlanner
    {
        private const int RingSamples = 96;
        private const float MinOuterPiece = 6f;
        private const float OverlapJunctionPadding = 4f;
        private const int MaxApproaches = 6;

        private readonly RoadPlanningRules _rules;

        public RoundaboutPlanner(RoadPlanningRules rules) => _rules = rules ?? throw new ArgumentNullException(nameof(rules));

        public RoundaboutPlan Plan(float2 center, float radius, RoadProfile profile, RoadLayout layout, Budget budget, float height = 0f)
        {
            if (radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var ring = SampleRing(center, radius);
            var baseCost = profile.CostOf(ring.Length);
            var cost = height >= 1f ? baseCost + (long)math.round(baseCost * _rules.ElevatedCostFactor) : baseCost;
            var crossings = new List<RingCrossing>();

            var error = Validate(center, radius, height, profile, ring, layout, crossings);
            if (error == BuildError.None && !budget.CanAfford(cost))
                error = BuildError.InsufficientFunds;

            return new RoundaboutPlan(center, radius, height, profile, ring, cost, error, crossings);
        }

        /// <summary>Whether a road at <paramref name="height"/> passes a full level above or below a ring at <paramref name="ringHeight"/>.</summary>
        public static bool PassesBy(float height, float ringHeight) => math.abs(height - ringHeight) >= RoadElevation.Clearance - 1e-3f;

        /// <summary>Whether a segment lies inside the ring at its height (it is removed when the ring is built).</summary>
        public static bool IsInside(RoadSegment segment, float2 center, float radius, float ringHeight) =>
            math.distance(segment.Curve.Evaluate(0.5f), center) < radius && !PassesBy(segment.HeightAt(0.5f), ringHeight);

        /// <summary>Polar angle of a point around a center, in [0, 2π).</summary>
        public static float AngleAround(float2 center, float2 point)
        {
            var angle = math.atan2(point.y - center.y, point.x - center.x);
            return angle < 0f ? angle + 2f * math.PI : angle;
        }

        private BuildError Validate(float2 center, float radius, float height, RoadProfile profile, Polyline ring, RoadLayout layout,
            List<RingCrossing> crossings)
        {
            if (math.any(ring.BoundsMin < _rules.BoundsMin) || math.any(ring.BoundsMax > _rules.BoundsMax))
                return BuildError.OutOfBounds;

            var zones = new List<(SegmentId segment, float angle, float sinAngle)>();
            foreach (var geometry in layout.Segments.Values)
            {
                var existing = geometry.Centerline;
                foreach (var (alongRing, alongExisting, point) in ring.IntersectionsWith(existing))
                {
                    var angle = AngleAround(center, point);
                    if (crossings.Any(c => c.Segment == geometry.Segment.Id && AngularDistance(c.Angle, angle) * radius < 0.5f))
                        continue;

                    var difference = math.abs(geometry.HeightAt(alongExisting) - height);
                    if (difference >= RoadElevation.SameHeight)
                    {
                        if (PassesBy(geometry.HeightAt(alongExisting), height))
                            continue;
                        return BuildError.HeightConflict;
                    }

                    var lineAngle = LineAngle(ring.DirectionAt(alongRing), existing.DirectionAt(alongExisting));
                    if (lineAngle < _rules.MinCrossingAngle)
                        return BuildError.AngleTooSharp;

                    // The piece of the road left outside the ring must still be a usable road.
                    var outsideIsAhead = math.distance(existing.PointAt(math.min(existing.Length, alongExisting + 1f)), center) > radius;
                    var outerPiece = outsideIsAhead ? existing.Length - alongExisting : alongExisting;
                    if (outerPiece < MinOuterPiece)
                        return BuildError.JunctionTooClose;

                    crossings.Add(new RingCrossing(geometry.Segment.Id, existing.ParameterAt(alongExisting), angle));
                    zones.Add((geometry.Segment.Id, angle, math.sin(math.radians(lineAngle))));
                }
            }

            if (crossings.Count > MaxApproaches)
                return BuildError.JunctionTooClose;

            var angles = crossings.Select(c => c.Angle).OrderBy(a => a).ToArray();
            for (var i = 0; i < angles.Length; i++)
            {
                var next = i + 1 < angles.Length ? angles[i + 1] : angles[0] + 2f * math.PI;
                if (angles.Length > 1 && (next - angles[i]) * radius < _rules.MinJunctionSpacing)
                    return BuildError.JunctionTooClose;
            }

            return OverlapsRoad(center, radius, height, profile, ring, layout, zones) ? BuildError.OverlapsRoad : BuildError.None;
        }

        /// <summary>The ring may not graze roads it does not properly cross; roads lying inside will be removed anyway.</summary>
        private bool OverlapsRoad(float2 center, float radius, float height, RoadProfile profile, Polyline ring, RoadLayout layout,
            IReadOnlyList<(SegmentId segment, float angle, float sinAngle)> zones)
        {
            for (var along = 0f; along < ring.Length; along += _rules.SampleSpacing)
            {
                var point = ring.PointAt(along);
                var angle = AngleAround(center, point);
                foreach (var geometry in layout.Segments.Values)
                {
                    if (IsInside(geometry.Segment, center, radius, height))
                        continue;

                    var clearance = profile.HalfWidth + geometry.Segment.Profile.HalfWidth;
                    var exempt = zones.Any(zone => zone.segment == geometry.Segment.Id &&
                                                   AngularDistance(zone.angle, angle) * radius < clearance / zone.sinAngle + OverlapJunctionPadding);
                    if (exempt)
                        continue;
                    var alongExisting = geometry.Centerline.ClosestDistance(point, out var closest);
                    if (math.distance(point, closest) < clearance && !PassesBy(geometry.HeightAt(alongExisting), height))
                        return true;
                }
            }

            return false;
        }

        private static Polyline SampleRing(float2 center, float radius)
        {
            var points = new float2[RingSamples + 1];
            for (var i = 0; i <= RingSamples; i++)
            {
                var angle = 2f * math.PI * i / RingSamples;
                points[i] = center + radius * new float2(math.cos(angle), math.sin(angle));
            }

            return new Polyline(points);
        }

        private static float AngularDistance(float a, float b)
        {
            var difference = math.abs(a - b) % (2f * math.PI);
            return math.min(difference, 2f * math.PI - difference);
        }

        private static float LineAngle(float2 a, float2 b)
        {
            var angle = math.degrees(math.acos(math.clamp(math.dot(a, b), -1f, 1f)));
            return math.min(angle, 180f - angle);
        }
    }
}
