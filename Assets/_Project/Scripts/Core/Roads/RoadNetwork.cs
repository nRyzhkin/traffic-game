using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Geometry;
using Unity.Mathematics;

namespace TrafficGame.Core.Roads
{
    /// <summary>The editable road graph: nodes connected by curved segments.</summary>
    public sealed class RoadNetwork
    {
        private readonly Dictionary<NodeId, RoadNode> _nodes = new();
        private readonly Dictionary<SegmentId, RoadSegment> _segments = new();
        private readonly Dictionary<NodeId, JunctionSettings> _junctions = new();
        private int _nextNodeId = 1;
        private int _nextSegmentId = 1;
        private int _deferDepth;
        private bool _changePending;

        /// <summary>Raised after every structural change, or once at the end of a <see cref="DeferChanges"/> scope.</summary>
        public event Action Changed;

        public IReadOnlyCollection<RoadNode> Nodes => _nodes.Values;
        public IReadOnlyCollection<RoadSegment> Segments => _segments.Values;

        public RoadNode GetNode(NodeId id) =>
            _nodes.TryGetValue(id, out var node) ? node : throw new KeyNotFoundException($"{id} is not in the network.");

        public RoadSegment GetSegment(SegmentId id) =>
            _segments.TryGetValue(id, out var segment) ? segment : throw new KeyNotFoundException($"{id} is not in the network.");

        /// <summary>The player's choices for a junction; <see cref="JunctionSettings.Default"/> when there are none.</summary>
        public JunctionSettings GetJunctionSettings(NodeId id) =>
            _junctions.TryGetValue(id, out var settings) ? settings : JunctionSettings.Default;

        public void SetJunctionSettings(NodeId id, JunctionSettings settings)
        {
            GetNode(id);
            StoreJunctionSettings(id, settings ?? throw new ArgumentNullException(nameof(settings)));
            NotifyChanged();
        }

        public bool Contains(NodeId id) => _nodes.ContainsKey(id);
        public bool Contains(SegmentId id) => _segments.ContainsKey(id);

        /// <summary>Batches changes: <see cref="Changed"/> fires once when the returned scope is disposed.</summary>
        public IDisposable DeferChanges()
        {
            _deferDepth++;
            return new DeferScope(this);
        }

        /// <param name="height">Meters above the ground (<see cref="RoadElevation"/>).</param>
        public NodeId AddNode(float2 position, float height = 0f)
        {
            var id = new NodeId(_nextNodeId++);
            _nodes.Add(id, new RoadNode(id, position, height));
            NotifyChanged();
            return id;
        }

        /// <summary>Adds a curved segment; the curve runs from the start node through two control points to the end node.</summary>
        public SegmentId AddSegment(NodeId start, NodeId end, float2 control1, float2 control2, RoadProfile profile, SegmentTraits traits = SegmentTraits.None)
        {
            if (start == end)
                throw new ArgumentException("A segment must connect two different nodes.");
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var startNode = GetNode(start);
            var endNode = GetNode(end);
            var curve = new CubicBezier(startNode.Position, control1, control2, endNode.Position);
            var id = new SegmentId(_nextSegmentId++);
            Attach(new RoadSegment(id, start, end, curve, profile, traits, startNode.Height, endNode.Height));
            NotifyChanged();
            return id;
        }

        public SegmentId AddStraightSegment(NodeId start, NodeId end, RoadProfile profile)
        {
            var from = GetNode(start).Position;
            var to = GetNode(end).Position;
            return AddSegment(start, end, math.lerp(from, to, 1f / 3f), math.lerp(from, to, 2f / 3f), profile);
        }

        /// <summary>Removes a segment together with any node left without segments.</summary>
        public void RemoveSegment(SegmentId id)
        {
            var segment = Detach(id);
            RemoveIfOrphan(segment.Start);
            RemoveIfOrphan(segment.End);
            NotifyChanged();
        }

        /// <summary>
        /// Inserts a node into a segment at curve parameter <paramref name="t"/>, replacing the segment with two
        /// that trace the same curve. Returns the new node; <paramref name="first"/> starts at the old start node.
        /// </summary>
        public NodeId SplitSegment(SegmentId id, float t, out SegmentId first, out SegmentId second)
        {
            if (t <= 0f || t >= 1f)
                throw new ArgumentOutOfRangeException(nameof(t), t, "A split parameter must lie strictly inside the segment.");

            var segment = GetSegment(id);
            segment.Curve.Split(t, out var head, out var tail);
            var startSettings = GetJunctionSettings(segment.Start);
            var endSettings = GetJunctionSettings(segment.End);

            using (DeferChanges())
            {
                Detach(id);
                var node = AddNode(head.P3, segment.HeightAt(t));
                first = AddSegment(segment.Start, node, head.P1, head.P2, segment.Profile, segment.Traits);
                second = AddSegment(node, segment.End, tail.P1, tail.P2, segment.Profile, segment.Traits);

                // The halves keep the lane arrows the player painted at the old segment's ends.
                StoreJunctionSettings(segment.Start, startSettings.WithSegmentReplaced(id, first));
                StoreJunctionSettings(segment.End, endSettings.WithSegmentReplaced(id, second));
                return node;
            }
        }

        /// <summary>Captures the complete state, including id counters, for <see cref="Restore"/>.</summary>
        public RoadNetworkSnapshot CreateSnapshot() =>
            new(_nodes.Values.Select(node => (node.Id, node.Position, node.Height)).ToArray(),
                _segments.Values.Select(segment => (segment.Id, segment.Start, segment.End, segment.Curve, segment.Profile, segment.Traits)).ToArray(),
                _junctions.Select(pair => (pair.Key, pair.Value)).ToArray(),
                _nextNodeId, _nextSegmentId);

        /// <summary>Replaces the whole network with a previously captured state.</summary>
        public void Restore(RoadNetworkSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            _nodes.Clear();
            _segments.Clear();
            _junctions.Clear();
            foreach (var (id, position, height) in snapshot.Nodes)
                _nodes.Add(id, new RoadNode(id, position, height));
            foreach (var (id, start, end, curve, profile, traits) in snapshot.Segments)
                Attach(new RoadSegment(id, start, end, curve, profile, traits, _nodes[start].Height, _nodes[end].Height));
            foreach (var (id, settings) in snapshot.Junctions)
                _junctions.Add(id, settings);

            _nextNodeId = snapshot.NextNodeId;
            _nextSegmentId = snapshot.NextSegmentId;
            NotifyChanged();
        }

        private void Attach(RoadSegment segment)
        {
            _segments.Add(segment.Id, segment);
            _nodes[segment.Start].Attach(segment.Id);
            _nodes[segment.End].Attach(segment.Id);
        }

        private RoadSegment Detach(SegmentId id)
        {
            var segment = GetSegment(id);
            _segments.Remove(id);
            _nodes[segment.Start].Detach(id);
            _nodes[segment.End].Detach(id);
            StoreJunctionSettings(segment.Start, GetJunctionSettings(segment.Start).WithoutSegment(id));
            StoreJunctionSettings(segment.End, GetJunctionSettings(segment.End).WithoutSegment(id));
            return segment;
        }

        private void RemoveIfOrphan(NodeId id)
        {
            if (_nodes[id].Degree != 0)
                return;

            _nodes.Remove(id);
            _junctions.Remove(id);
        }

        private void StoreJunctionSettings(NodeId id, JunctionSettings settings)
        {
            if (settings.IsDefault)
                _junctions.Remove(id);
            else
                _junctions[id] = settings;
        }

        private void NotifyChanged()
        {
            if (_deferDepth > 0)
            {
                _changePending = true;
                return;
            }

            Changed?.Invoke();
        }

        private void EndDefer()
        {
            if (--_deferDepth > 0 || !_changePending)
                return;

            _changePending = false;
            Changed?.Invoke();
        }

        private sealed class DeferScope : IDisposable
        {
            private RoadNetwork _network;

            public DeferScope(RoadNetwork network) => _network = network;

            public void Dispose()
            {
                _network?.EndDefer();
                _network = null;
            }
        }
    }

    /// <summary>Immutable copy of a <see cref="RoadNetwork"/> state; used for undo.</summary>
    public sealed class RoadNetworkSnapshot
    {
        internal RoadNetworkSnapshot(
            IReadOnlyList<(NodeId id, float2 position, float height)> nodes,
            IReadOnlyList<(SegmentId id, NodeId start, NodeId end, CubicBezier curve, RoadProfile profile, SegmentTraits traits)> segments,
            IReadOnlyList<(NodeId id, JunctionSettings settings)> junctions,
            int nextNodeId, int nextSegmentId)
        {
            Nodes = nodes;
            Segments = segments;
            Junctions = junctions;
            NextNodeId = nextNodeId;
            NextSegmentId = nextSegmentId;
        }

        internal IReadOnlyList<(NodeId id, float2 position, float height)> Nodes { get; }
        internal IReadOnlyList<(SegmentId id, NodeId start, NodeId end, CubicBezier curve, RoadProfile profile, SegmentTraits traits)> Segments { get; }
        internal IReadOnlyList<(NodeId id, JunctionSettings settings)> Junctions { get; }
        internal int NextNodeId { get; }
        internal int NextSegmentId { get; }
    }
}
