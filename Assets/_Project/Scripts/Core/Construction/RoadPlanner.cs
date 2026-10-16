using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    /// <summary>
    /// Turns a build request into a <see cref="RoadPlan"/>: shape, heights, price, crossings and the first rule it breaks.
    /// Roads at the same height meet in junctions; roads a full level apart pass over each other.
    /// </summary>
    public sealed class RoadPlanner
    {
        private const float DuplicateCrossingDistance = 0.5f;
        private const float OverlapJunctionPadding = 4f;
        private const float PassThroughTolerance = 2f;

        /// <summary>Road higher than this counts as elevated for the price.</summary>
        private const float ElevatedFrom = 1f;

        private readonly RoadNetwork _network;
        private readonly RoadPlanningRules _rules;

        public RoadPlanner(RoadNetwork network, RoadPlanningRules rules)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public RoadPlan Plan(RoadBuildRequest request, RoadLayout layout, Budget budget)
        {
            var start = request.Start.Position;
            var end = request.End.Position;
            var curve = request.Control.HasValue
                ? CubicBezier.Quadratic(start, request.Control.Value, end)
                : CubicBezier.Line(start, end);

            var startHeight = HeightOf(request.Start);
            var endHeight = HeightOf(request.End);
            if (math.distance(start, end) < 1e-3f)
                return new RoadPlan(request, curve, null, 0, BuildError.TooShort, null, Array.Empty<RoadCrossing>(), Array.Empty<RoadPassThrough>(),
                    startHeight, endHeight);

            var path = Polyline.FromBezier(curve, _rules.SampleSpacing);
            var baseCost = request.Profile.CostOf(path.Length);
            var cost = baseCost + (long)math.round(baseCost * _rules.ElevatedCostFactor * ElevatedShare(startHeight, endHeight));
            var heights = new Heights(startHeight, endHeight);
            var connectionAngle = ConnectionAngle(request.Start, path.StartDirection, layout);
            var crossings = new List<RoadCrossing>();
            var passThroughs = new List<RoadPassThrough>();

            var error = Validate(request, path, heights, layout, crossings, passThroughs);
            if (error == BuildError.None && !budget.CanAfford(cost))
                error = BuildError.InsufficientFunds;

            return new RoadPlan(request, curve, path, cost, error, connectionAngle, crossings, passThroughs, startHeight, endHeight);
        }

        /// <summary>Height of the road an anchor attaches to, or the chosen height of a free anchor.</summary>
        public float HeightOf(RoadAnchor anchor) => anchor.Kind switch
        {
            AnchorKind.Node => _network.GetNode(anchor.Node).Height,
            AnchorKind.Segment => _network.GetSegment(anchor.Segment).HeightAt(anchor.Parameter),
            _ => anchor.Height
        };

        private BuildError Validate(RoadBuildRequest request, Polyline path, Heights heights, RoadLayout layout, List<RoadCrossing> crossings,
            List<RoadPassThrough> passThroughs)
        {
            if (path.Length < _rules.MinLength || IsSameNode(request.Start, request.End))
                return BuildError.TooShort;
            if (!IsInsideBounds(path))
                return BuildError.OutOfBounds;
            if (MinCurveRadius(path) < _rules.MinCurveRadius)
                return BuildError.CurveTooSharp;
            if (math.abs(heights.End - heights.Start) > path.Length * _rules.MaxGrade + 1e-3f)
                return BuildError.TooSteep;
            if (!HasClearConnection(request.Start, path.StartDirection, layout) ||
                !HasClearConnection(request.End, -path.EndDirection, layout))
                return BuildError.AngleTooSharp;

            var junctions = new List<JunctionZone>();
            if (request.Start.IsAttached)
                junctions.Add(new JunctionZone(0f, request.Start.Touches, MinAngle(request.Start, path.StartDirection, layout)));
            if (request.End.IsAttached)
                junctions.Add(new JunctionZone(path.Length, request.End.Touches, MinAngle(request.End, -path.EndDirection, layout)));

            var passError = FindPassThroughs(request, path, heights, layout, passThroughs, junctions);
            if (passError != BuildError.None)
                return passError;

            var crossingError = FindCrossings(request, path, heights, layout, crossings, junctions, passThroughs);
            if (crossingError != BuildError.None)
                return crossingError;

            var along = junctions.Select(junction => junction.Along).OrderBy(distance => distance).ToArray();
            for (var i = 1; i < along.Length; i++)
            {
                if (along[i] - along[i - 1] < _rules.MinJunctionSpacing)
                    return BuildError.JunctionTooClose;
            }

            return OverlapsExistingRoad(request.Profile, path, heights, layout, junctions) ? BuildError.OverlapsRoad : BuildError.None;
        }

        /// <summary>
        /// Existing nodes lying on the new road (within a couple of meters) become junctions of it — drawing a street
        /// straight through the ends of driveways connects them instead of being rejected.
        /// </summary>
        private BuildError FindPassThroughs(RoadBuildRequest request, Polyline path, Heights heights, RoadLayout layout,
            List<RoadPassThrough> passThroughs, List<JunctionZone> junctions)
        {
            foreach (var node in _network.Nodes)
            {
                if (IsAnchoredAt(request.Start, node.Id) || IsAnchoredAt(request.End, node.Id))
                    continue;
                if (math.any(node.Position < path.BoundsMin - PassThroughTolerance) || math.any(node.Position > path.BoundsMax + PassThroughTolerance))
                    continue;

                var along = path.ClosestDistance(node.Position, out var closest);
                if (math.distance(closest, node.Position) > PassThroughTolerance || along < 1f || along > path.Length - 1f)
                    continue;

                var levels = CompareHeights(heights.At(path.ParameterAt(along)), node.Height);
                if (levels == Levels.Apart)
                    continue;
                if (levels == Levels.Conflict)
                    return BuildError.HeightConflict;

                var anchor = RoadAnchor.AtNode(node.Id, node.Position);
                var direction = path.DirectionAt(along);
                var angle = math.min(MinAngle(anchor, direction, layout), MinAngle(anchor, -direction, layout));
                if (angle < _rules.MinConnectionAngle)
                    return BuildError.AngleTooSharp;

                passThroughs.Add(new RoadPassThrough(node.Id, path.ParameterAt(along), along));
                var nodeId = node.Id;
                junctions.Add(new JunctionZone(along, segment => segment.Start == nodeId || segment.End == nodeId, angle));
            }

            return BuildError.None;
        }

        private static bool IsAnchoredAt(RoadAnchor anchor, NodeId node) => anchor.Kind == AnchorKind.Node && anchor.Node == node;

        private BuildError FindCrossings(RoadBuildRequest request, Polyline path, Heights heights, RoadLayout layout,
            List<RoadCrossing> crossings, List<JunctionZone> junctions, IReadOnlyList<RoadPassThrough> passThroughs)
        {
            var halfWidth = request.Profile.HalfWidth;
            foreach (var geometry in layout.Segments.Values)
            {
                var existing = geometry.Centerline;
                var keepClear = halfWidth + geometry.Segment.Profile.HalfWidth + 1f;
                var lastAlong = float.NegativeInfinity;

                foreach (var (alongNew, alongExisting, point) in path.IntersectionsWith(existing).OrderBy(hit => hit.alongThis))
                {
                    // The road it starts or ends on is a connection, not a crossing.
                    if ((request.Start.Touches(geometry.Segment) && alongNew < keepClear) ||
                        (request.End.Touches(geometry.Segment) && path.Length - alongNew < keepClear))
                        continue;
                    if (alongNew - lastAlong < DuplicateCrossingDistance)
                        continue;

                    // A full level apart the roads pass over each other; closer they would collide.
                    var levels = CompareHeights(heights.At(path.ParameterAt(alongNew)),
                        geometry.Segment.HeightAt(existing.ParameterAt(alongExisting)));
                    if (levels == Levels.Apart)
                        continue;
                    if (levels == Levels.Conflict)
                        return BuildError.HeightConflict;
                    if (passThroughs.Any(pass => (geometry.Segment.Start == pass.Node || geometry.Segment.End == pass.Node) &&
                                                 math.abs(alongNew - pass.Along) < keepClear))
                        continue;
                    lastAlong = alongNew;

                    var angle = LineAngle(path.DirectionAt(alongNew), existing.DirectionAt(alongExisting));
                    if (angle < _rules.MinCrossingAngle)
                        return BuildError.AngleTooSharp;
                    if (alongExisting < _rules.MinJunctionSpacing || existing.Length - alongExisting < _rules.MinJunctionSpacing)
                        return BuildError.JunctionTooClose;

                    crossings.Add(new RoadCrossing(geometry.Segment.Id, existing.ParameterAt(alongExisting), path.ParameterAt(alongNew), point));
                    var crossed = geometry.Segment.Id;
                    junctions.Add(new JunctionZone(alongNew, segment => segment.Id == crossed, angle));
                }
            }

            return BuildError.None;
        }

        /// <summary>
        /// True when the new road runs over an existing one anywhere except where it legitimately joins or crosses it.
        /// Near a junction the surfaces of two roads meeting at angle α touch for clearance / sin α, so that stretch
        /// is exempt for the roads of that junction only.
        /// </summary>
        private bool OverlapsExistingRoad(RoadProfile profile, Polyline path, Heights heights, RoadLayout layout, IReadOnlyList<JunctionZone> junctions)
        {
            for (var along = 0f; along <= path.Length; along += _rules.SampleSpacing)
            {
                var point = path.PointAt(along);
                var height = heights.At(path.ParameterAt(along));
                foreach (var geometry in layout.Segments.Values)
                {
                    var clearance = profile.HalfWidth + geometry.Segment.Profile.HalfWidth;
                    if (junctions.Any(junction => junction.Exempts(geometry.Segment, along, clearance)))
                        continue;

                    var centerline = geometry.Centerline;
                    if (math.any(point < centerline.BoundsMin - clearance) || math.any(point > centerline.BoundsMax + clearance))
                        continue;
                    var alongExisting = centerline.ClosestDistance(point, out var closest);
                    if (math.distance(point, closest) >= clearance)
                        continue;
                    if (CompareHeights(height, geometry.Segment.HeightAt(centerline.ParameterAt(alongExisting))) != Levels.Apart)
                        return true;
                }
            }

            return false;
        }

        private bool HasClearConnection(RoadAnchor anchor, float2 newDirection, RoadLayout layout) =>
            MinAngle(anchor, newDirection, layout) >= _rules.MinConnectionAngle;

        /// <summary>Smallest angle (0–180°) between the new road leaving the anchor and the existing roads leaving it.</summary>
        private float MinAngle(RoadAnchor anchor, float2 newDirection, RoadLayout layout) =>
            RoadDirectionsAt(anchor, layout).Select(direction => Angle(newDirection, direction)).DefaultIfEmpty(180f).Min();

        private float? ConnectionAngle(RoadAnchor anchor, float2 newDirection, RoadLayout layout)
        {
            var directions = RoadDirectionsAt(anchor, layout).ToArray();
            if (directions.Length == 0)
                return null;

            // From a dead end the interesting number is how far the new road bends away from straight on.
            if (anchor.Kind == AnchorKind.Node && directions.Length == 1)
                return 180f - Angle(newDirection, directions[0]);

            return directions.Min(direction => LineAngle(newDirection, direction));
        }

        /// <summary>Directions of the existing roads leaving the anchor point.</summary>
        private IEnumerable<float2> RoadDirectionsAt(RoadAnchor anchor, RoadLayout layout)
        {
            switch (anchor.Kind)
            {
                case AnchorKind.Node:
                    foreach (var id in _network.GetNode(anchor.Node).Segments)
                    {
                        var centerline = layout.Segments[id].Centerline;
                        yield return layout.Segments[id].Segment.Start == anchor.Node ? centerline.StartDirection : -centerline.EndDirection;
                    }
                    break;
                case AnchorKind.Segment:
                    var segmentLine = layout.Segments[anchor.Segment].Centerline;
                    var tangent = segmentLine.DirectionAt(segmentLine.ClosestDistance(anchor.Position, out _));
                    yield return tangent;
                    yield return -tangent;
                    break;
            }
        }

        private bool IsInsideBounds(Polyline path) =>
            math.all(path.BoundsMin >= _rules.BoundsMin) && math.all(path.BoundsMax <= _rules.BoundsMax);

        private static bool IsSameNode(RoadAnchor a, RoadAnchor b) =>
            a.Kind == AnchorKind.Node && b.Kind == AnchorKind.Node && a.Node == b.Node;

        private static float MinCurveRadius(Polyline path)
        {
            var minRadius = float.MaxValue;
            for (var i = 1; i < path.Count - 1; i++)
            {
                var turn = math.abs(path.TurnAngleAt(i));
                if (turn < 1e-4f)
                    continue;

                var arm = (path.DistanceAt(i + 1) - path.DistanceAt(i - 1)) * 0.5f;
                minRadius = math.min(minRadius, arm / turn);
            }

            return minRadius;
        }

        private enum Levels
        {
            Same,
            Apart,
            Conflict
        }

        private static Levels CompareHeights(float a, float b)
        {
            var difference = math.abs(a - b);
            if (difference < RoadElevation.SameHeight)
                return Levels.Same;
            return difference >= RoadElevation.Clearance - 1e-3f ? Levels.Apart : Levels.Conflict;
        }

        /// <summary>Share of a road (by curve parameter) that runs elevated.</summary>
        private static float ElevatedShare(float startHeight, float endHeight)
        {
            var startUp = startHeight >= ElevatedFrom;
            var endUp = endHeight >= ElevatedFrom;
            if (startUp == endUp)
                return startUp ? 1f : 0f;

            var t = (ElevatedFrom - startHeight) / (endHeight - startHeight);
            return endUp ? 1f - t : t;
        }

        /// <summary>The new road's height profile: linear in the curve parameter.</summary>
        private readonly struct Heights
        {
            public Heights(float start, float end)
            {
                Start = start;
                End = end;
            }

            public float Start { get; }
            public float End { get; }

            public float At(float t) => math.lerp(Start, End, math.saturate(t));
        }

        /// <summary>A point where the new road meets existing roads.</summary>
        private readonly struct JunctionZone
        {
            private readonly Func<RoadSegment, bool> _touches;
            private readonly float _sinAngle;

            public JunctionZone(float along, Func<RoadSegment, bool> touches, float angleDegrees)
            {
                Along = along;
                _touches = touches;
                // From 90° on the roads diverge immediately; only the road widths overlap.
                _sinAngle = angleDegrees >= 90f ? 1f : math.max(math.sin(math.radians(angleDegrees)), 0.1f);
            }

            public float Along { get; }

            public bool Exempts(RoadSegment segment, float along, float clearance) =>
                _touches(segment) && math.abs(along - Along) < clearance / _sinAngle + OverlapJunctionPadding;
        }

        /// <summary>Angle between two directions, 0–180°.</summary>
        private static float Angle(float2 a, float2 b) => math.degrees(math.acos(math.clamp(math.dot(a, b), -1f, 1f)));

        /// <summary>Angle between two lines regardless of direction, 0–90°.</summary>
        private static float LineAngle(float2 a, float2 b)
        {
            var angle = Angle(a, b);
            return math.min(angle, 180f - angle);
        }
    }
}
