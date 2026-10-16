using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;

namespace TrafficGame.Core.Construction
{
    /// <summary>
    /// Builds a planned road: splits the roads it starts on, ends on or crosses, creates junction nodes
    /// and adds the new road as one segment per stretch between junctions.
    /// </summary>
    public sealed class BuildRoadCommand : NetworkEditCommand
    {
        private readonly RoadPlan _plan;

        public BuildRoadCommand(RoadNetwork network, Budget budget, RoadPlan plan) : base(network, budget)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (!plan.IsValid)
                throw new ArgumentException($"Cannot build an invalid plan ({plan.Error}).", nameof(plan));
        }

        /// <summary>The node at the end of the new road, known after the first execution.</summary>
        public NodeId EndNode { get; private set; }

        protected override long Price => _plan.Cost;

        protected override void Apply()
        {
            var request = _plan.Request;
            var splitNodes = SplitExistingSegments(request);

            var startNode = ResolveNode(request.Start, _plan.StartHeight, splitNodes, SplitKey.Start);
            var endNode = ResolveNode(request.End, _plan.EndHeight, splitNodes, SplitKey.End);

            var stops = new List<(float parameter, NodeId node)> { (0f, startNode) };
            stops.AddRange(_plan.Crossings
                .Select((crossing, index) => (crossing.NewParameter, splitNodes[SplitKey.Crossing(index)]))
                .Concat(_plan.PassThroughs.Select(pass => (pass.NewParameter, pass.Node)))
                .OrderBy(stop => stop.NewParameter));
            stops.Add((1f, endNode));

            AddPieces(stops, request.Profile);
            EndNode = endNode;
        }

        /// <summary>Splits every existing segment the new road touches; returns the created node for each split point.</summary>
        private Dictionary<SplitKey, NodeId> SplitExistingSegments(RoadBuildRequest request)
        {
            var splits = new List<(SegmentId segment, float parameter, SplitKey key)>();
            if (request.Start.Kind == AnchorKind.Segment)
                splits.Add((request.Start.Segment, request.Start.Parameter, SplitKey.Start));
            if (request.End.Kind == AnchorKind.Segment)
                splits.Add((request.End.Segment, request.End.Parameter, SplitKey.End));
            for (var i = 0; i < _plan.Crossings.Count; i++)
                splits.Add((_plan.Crossings[i].Segment, _plan.Crossings[i].SegmentParameter, SplitKey.Crossing(i)));

            var nodes = new Dictionary<SplitKey, NodeId>();
            foreach (var group in splits.GroupBy(split => split.segment))
            {
                // Split from the far end backwards so the remaining head keeps a simple parameterization.
                var head = group.Key;
                var headEnd = 1f;
                foreach (var (_, parameter, key) in group.OrderByDescending(split => split.parameter))
                {
                    nodes[key] = Network.SplitSegment(head, parameter / headEnd, out head, out _);
                    headEnd = parameter;
                }
            }

            return nodes;
        }

        private NodeId ResolveNode(RoadAnchor anchor, float height, IReadOnlyDictionary<SplitKey, NodeId> splitNodes, SplitKey key) => anchor.Kind switch
        {
            AnchorKind.Node => anchor.Node,
            AnchorKind.Segment => splitNodes[key],
            _ => Network.AddNode(anchor.Position, height)
        };

        private void AddPieces(IReadOnlyList<(float parameter, NodeId node)> stops, RoadProfile profile)
        {
            var remaining = _plan.Curve;
            var consumed = 0f;
            for (var i = 1; i < stops.Count; i++)
            {
                CubicBezier piece;
                if (i == stops.Count - 1)
                {
                    piece = remaining;
                }
                else
                {
                    remaining.Split((stops[i].parameter - consumed) / (1f - consumed), out piece, out remaining);
                    consumed = stops[i].parameter;
                }

                Network.AddSegment(stops[i - 1].node, stops[i].node, piece.P1, piece.P2, profile);
            }
        }

        private readonly struct SplitKey : IEquatable<SplitKey>
        {
            private readonly int _value;

            private SplitKey(int value) => _value = value;

            public static SplitKey Start => new(-1);
            public static SplitKey End => new(-2);
            public static SplitKey Crossing(int index) => new(index);

            public bool Equals(SplitKey other) => _value == other._value;
            public override bool Equals(object obj) => obj is SplitKey other && Equals(other);
            public override int GetHashCode() => _value;
        }
    }
}
