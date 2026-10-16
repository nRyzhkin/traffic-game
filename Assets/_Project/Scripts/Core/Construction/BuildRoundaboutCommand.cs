using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    /// <summary>
    /// Builds a planned roundabout: splits every road crossing the circle at its height, removes what is inside it at that height and
    /// joins the crossing nodes with counter-clockwise arcs (extra nodes keep each arc at 90° or less).
    /// </summary>
    public sealed class BuildRoundaboutCommand : NetworkEditCommand
    {
        private const float MaxArc = math.PI / 2f;

        private readonly RoundaboutPlan _plan;

        public BuildRoundaboutCommand(RoadNetwork network, Budget budget, RoundaboutPlan plan) : base(network, budget)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (!plan.IsValid)
                throw new ArgumentException($"Cannot build an invalid plan ({plan.Error}).", nameof(plan));
        }

        protected override long Price => _plan.Cost;

        protected override void Apply()
        {
            var ringNodes = SplitCrossingRoads();
            RemoveEverythingInside();
            AddIntermediateNodes(ringNodes);
            AddArcs(ringNodes.OrderBy(node => node.angle).ToList());
        }

        private List<(float angle, NodeId node)> SplitCrossingRoads()
        {
            var ringNodes = new List<(float, NodeId)>();
            foreach (var group in _plan.Crossings.GroupBy(crossing => crossing.Segment))
            {
                // Split from the far end backwards so the remaining head keeps a simple parameterization.
                var head = group.Key;
                var headEnd = 1f;
                foreach (var crossing in group.OrderByDescending(c => c.SegmentParameter))
                {
                    var node = Network.SplitSegment(head, crossing.SegmentParameter / headEnd, out head, out _);
                    headEnd = crossing.SegmentParameter;
                    ringNodes.Add((crossing.Angle, node));
                }
            }

            return ringNodes;
        }

        private void RemoveEverythingInside()
        {
            var inside = Network.Segments
                .Where(segment => RoundaboutPlanner.IsInside(segment, _plan.Center, _plan.Radius, _plan.Height))
                .Select(segment => segment.Id)
                .ToArray();
            foreach (var segment in inside)
                Network.RemoveSegment(segment);
        }

        private void AddIntermediateNodes(List<(float angle, NodeId node)> ringNodes)
        {
            if (ringNodes.Count == 0)
            {
                ringNodes.Add((0f, Network.AddNode(PointAt(0f), _plan.Height)));
                ringNodes.Add((math.PI, Network.AddNode(PointAt(math.PI), _plan.Height)));
            }

            var angles = ringNodes.Select(node => node.angle).OrderBy(angle => angle).ToArray();
            for (var i = 0; i < angles.Length; i++)
            {
                var from = angles[i];
                var to = i + 1 < angles.Length ? angles[i + 1] : angles[0] + 2f * math.PI;
                var pieces = (int)math.ceil((to - from) / MaxArc - 1e-4f);
                for (var piece = 1; piece < pieces; piece++)
                {
                    var angle = (from + (to - from) * piece / pieces) % (2f * math.PI);
                    ringNodes.Add((angle, Network.AddNode(PointAt(angle), _plan.Height)));
                }
            }
        }

        private void AddArcs(IReadOnlyList<(float angle, NodeId node)> ringNodes)
        {
            for (var i = 0; i < ringNodes.Count; i++)
            {
                var (fromAngle, fromNode) = ringNodes[i];
                var (toAngle, toNode) = ringNodes[(i + 1) % ringNodes.Count];
                if (toAngle <= fromAngle)
                    toAngle += 2f * math.PI;

                // Standard cubic approximation of a circular arc.
                var handle = 4f / 3f * math.tan((toAngle - fromAngle) / 4f) * _plan.Radius;
                var control1 = PointAt(fromAngle) + Tangent(fromAngle) * handle;
                var control2 = PointAt(toAngle) - Tangent(toAngle) * handle;
                Network.AddSegment(fromNode, toNode, control1, control2, _plan.Profile, SegmentTraits.Roundabout);
            }
        }

        private float2 PointAt(float angle) => _plan.Center + _plan.Radius * new float2(math.cos(angle), math.sin(angle));

        private static float2 Tangent(float angle) => new(-math.sin(angle), math.cos(angle));
    }
}
