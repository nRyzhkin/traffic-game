using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Lanes;
using Unity.Mathematics;

namespace TrafficGame.Core.Roads
{
    /// <summary>
    /// Geometry derived from a <see cref="RoadNetwork"/>: trimmed segments, junction areas and the lane graph.
    /// Rebuilt whenever the network changes.
    /// </summary>
    public sealed class RoadLayout
    {
        private const float CurbSampleSpacing = 1f;

        private RoadLayout(IReadOnlyDictionary<SegmentId, SegmentGeometry> segments,
            IReadOnlyDictionary<NodeId, JunctionGeometry> junctions, LaneGraph lanes)
        {
            Segments = segments;
            Junctions = junctions;
            Lanes = lanes;
        }

        public IReadOnlyDictionary<SegmentId, SegmentGeometry> Segments { get; }
        public IReadOnlyDictionary<NodeId, JunctionGeometry> Junctions { get; }
        public LaneGraph Lanes { get; }

        public static RoadLayout Build(RoadNetwork network, RoadGeometrySettings settings)
        {
            var centerlines = network.Segments.ToDictionary(
                segment => segment.Id,
                segment => Polyline.FromBezier(segment.Curve, settings.SampleSpacing));

            var trims = new Dictionary<(SegmentId, bool), float>();
            foreach (var node in network.Nodes)
                ComputeTrims(network, node, centerlines, settings, trims);

            var segments = new Dictionary<SegmentId, SegmentGeometry>();
            foreach (var segment in network.Segments)
            {
                var centerline = centerlines[segment.Id];
                var (trimStart, trimEnd) = FitTrims(trims[(segment.Id, true)], trims[(segment.Id, false)],
                    centerline.Length, settings.MinSegmentLength);
                segments.Add(segment.Id, new SegmentGeometry(segment, centerline, trimStart, trimEnd));
            }

            var junctions = network.Nodes.ToDictionary(node => node.Id, node => BuildJunction(node, segments));
            var lanes = LaneGraphBuilder.Build(segments.Values, junctions.Values, network.GetJunctionSettings);
            return new RoadLayout(segments, junctions, lanes);
        }

        /// <summary>
        /// For every pair of neighbouring roads around the node, finds where the left edge of one meets
        /// the right edge of the next and pulls both roads back from the node to that point plus padding.
        /// </summary>
        private static void ComputeTrims(RoadNetwork network, RoadNode node, IReadOnlyDictionary<SegmentId, Polyline> centerlines,
            RoadGeometrySettings settings, IDictionary<(SegmentId, bool), float> trims)
        {
            var approaches = node.Segments
                .Select(id =>
                {
                    var segment = network.GetSegment(id);
                    var atStart = segment.Start == node.Id;
                    var centerline = centerlines[id];
                    var direction = atStart ? centerline.StartDirection : -centerline.EndDirection;
                    return (id, atStart, direction, halfWidth: segment.Profile.HalfWidth);
                })
                .OrderBy(approach => Angle(approach.direction))
                .ToArray();

            if (approaches.Length == 1)
            {
                trims[(approaches[0].id, approaches[0].atStart)] = 0f;
                return;
            }

            var required = new float[approaches.Length];
            for (var i = 0; i < approaches.Length; i++)
            {
                var j = (i + 1) % approaches.Length;
                var a = approaches[i];
                var b = approaches[j];
                var leftEdgeOfA = node.Position + Geometry2D.Left(a.direction) * a.halfWidth;
                var rightEdgeOfB = node.Position - Geometry2D.Left(b.direction) * b.halfWidth;

                if (!Geometry2D.TryIntersectLines(leftEdgeOfA, a.direction, rightEdgeOfB, b.direction, out var s, out var u))
                    continue;
                required[i] = math.max(required[i], s);
                required[j] = math.max(required[j], u);
            }

            // A gentle bend needs almost no junction area; a sharp one or a crossing needs rounded corners.
            var padding = approaches.Length == 2
                ? settings.JunctionPadding * math.saturate(1f + math.dot(approaches[0].direction, approaches[1].direction))
                : settings.JunctionPadding;

            for (var i = 0; i < approaches.Length; i++)
            {
                var trim = math.clamp(required[i] + padding, settings.MinJunctionTrim, settings.MaxJunctionTrim);
                trims[(approaches[i].id, approaches[i].atStart)] = trim;
            }
        }

        /// <summary>Shrinks both trims proportionally when together they would eat the whole segment.</summary>
        private static (float start, float end) FitTrims(float start, float end, float length, float minRemaining)
        {
            var available = length - minRemaining;
            if (available <= 0f)
                return (length * 0.25f, length * 0.25f);

            var total = start + end;
            if (total <= available)
                return (start, end);

            var scale = available / total;
            return (start * scale, end * scale);
        }

        private static JunctionGeometry BuildJunction(RoadNode node, IReadOnlyDictionary<SegmentId, SegmentGeometry> segments)
        {
            var approaches = node.Segments
                .Select(id => CreateApproach(segments[id], node.Id))
                .OrderBy(approach => Angle(approach.Position - node.Position))
                .ToArray();

            if (approaches.Length == 1)
            {
                var deadEnd = approaches[0];
                var cap = new Polyline(new[] { deadEnd.Left, deadEnd.Right });
                return new JunctionGeometry(node.Id, node.Position, node.Height, approaches, new[] { cap }, new float2[0]);
            }

            var curbs = new List<Polyline>();
            var outline = new List<float2>();
            for (var i = 0; i < approaches.Length; i++)
            {
                var a = approaches[i];
                var b = approaches[(i + 1) % approaches.Length];
                outline.Add(a.Right);
                outline.Add(a.Left);

                var chord = math.distance(a.Left, b.Right);
                if (chord < 1e-3f)
                    continue;

                var curve = CubicBezier.Hermite(a.Left, -a.Direction, b.Right, b.Direction, ArcHandle(chord, -a.Direction, b.Direction));
                var curb = Polyline.FromBezier(curve, CurbSampleSpacing);
                curbs.Add(curb);
                for (var p = 1; p < curb.Count - 1; p++)
                    outline.Add(curb[p]);
            }

            return new JunctionGeometry(node.Id, node.Position, node.Height, approaches, curbs, outline);
        }

        /// <summary>
        /// Handle length that makes a cubic between two points with the given travel directions trace a circular arc:
        /// (4/3)·tan(θ/4)·r with r = chord / (2·sin(θ/2)); it tends to chord/3 for a straight line.
        /// </summary>
        private static float ArcHandle(float chord, float2 startDirection, float2 endDirection)
        {
            var turn = math.abs(Geometry2D.SignedAngle(startDirection, endDirection));
            if (turn < 1e-3f)
                return chord / 3f;

            return 4f / 3f * math.tan(turn / 4f) * chord / (2f * math.sin(turn / 2f));
        }

        private static JunctionApproach CreateApproach(SegmentGeometry geometry, NodeId node)
        {
            var atStart = geometry.Segment.Start == node;
            var centerline = geometry.Centerline;
            var distance = atStart ? geometry.TrimStart : centerline.Length - geometry.TrimEnd;
            var direction = centerline.DirectionAt(distance) * (atStart ? 1f : -1f);
            return new JunctionApproach(geometry.Segment.Id, atStart, centerline.PointAt(distance), direction,
                geometry.Segment.Profile.HalfWidth);
        }

        private static float Angle(float2 direction) => math.atan2(direction.y, direction.x);
    }
}
