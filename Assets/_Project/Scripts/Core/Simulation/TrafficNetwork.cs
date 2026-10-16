using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Simulation
{
    /// <summary>A directed road as seen by routing: its lanes and the roads reachable from its end.</summary>
    internal sealed class RoadInfo
    {
        public RoadInfo(int index, RoadDirection direction, NodeId from, NodeId to)
        {
            Index = index;
            Direction = direction;
            From = from;
            To = to;
        }

        public int Index { get; }
        public RoadDirection Direction { get; }
        public NodeId From { get; }
        public NodeId To { get; }
        public List<TrafficLink> Lanes { get; } = new();

        /// <summary>Connections from this road's lanes, grouped by the road they lead to.</summary>
        public Dictionary<RoadInfo, List<TrafficLink>> ConnectionsTo { get; } = new();

        public float Length => Lanes[0].Length;
        public float SpeedLimit => Lanes[0].SpeedLimit;
        public float2 EndPoint => Lanes[0].Path.End;

        /// <summary>Smoothed speed of the traffic on this road; free-flow speed when empty.</summary>
        public float ObservedSpeed { get; set; }

        /// <summary>Junction delay expected at the road end (stop signs, yielding), seconds.</summary>
        public float JunctionPenalty { get; set; }
    }

    /// <summary>
    /// The drivable structure the simulation runs on, built from a <see cref="RoadLayout"/>: lane and connection
    /// links, conflicts between connections inside every junction, right-of-way and the road graph for routing.
    /// </summary>
    public sealed class TrafficNetwork
    {
        private const float StopPenalty = 4f;
        private const float YieldPenalty = 1.5f;
        private const float SignalPenalty = 6f;

        private readonly List<TrafficLink> _links = new();
        private readonly List<RoadInfo> _roads = new();
        private readonly List<TrafficSignal> _signals = new();
        private readonly Dictionary<(SegmentId, bool, int), TrafficLink> _lanesByKey = new();
        private readonly Dictionary<(int fromLane, int toLane), TrafficLink> _connectionsByLanes = new();

        private TrafficNetwork() { }

        public IReadOnlyList<TrafficLink> Links => _links;

        /// <summary>Traffic lights, one per signalized junction.</summary>
        public IReadOnlyList<TrafficSignal> Signals => _signals;

        /// <summary>Dead ends where traffic enters the map (they have lanes leaving them).</summary>
        public IReadOnlyList<NodeId> Entries { get; private set; }

        /// <summary>Dead ends where traffic can leave the map (they have lanes arriving at them).</summary>
        public IReadOnlyList<NodeId> Exits { get; private set; }

        internal IReadOnlyList<RoadInfo> Roads => _roads;
        internal IReadOnlyDictionary<NodeId, float2> NodePositions { get; private set; }

        public static TrafficNetwork Build(RoadNetwork network, RoadLayout layout, TrafficSettings settings)
        {
            if (network == null)
                throw new ArgumentNullException(nameof(network));
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            var result = new TrafficNetwork();
            var lanes = result.CreateLanes(layout);
            result.CreateConnections(layout, lanes, settings);
            result.ApplyJunctionRules(network, layout);
            result.FindConflicts();

            result.NodePositions = network.Nodes.ToDictionary(node => node.Id, node => node.Position);
            var deadEnds = layout.Junctions.Values.Where(junction => junction.IsDeadEnd).Select(junction => junction.Node).ToArray();
            result.Entries = deadEnds.Where(node => result._roads.Any(road => road.From == node)).ToArray();
            result.Exits = deadEnds.Where(node => result._roads.Any(road => road.To == node)).ToArray();
            return result;
        }

        /// <summary>The lane with the same segment, direction and index, if it still exists.</summary>
        public bool TryGetLane(RoadDirection road, int index, out TrafficLink lane) =>
            _lanesByKey.TryGetValue((road.Segment, road.Forward, index), out lane);

        public bool TryGetConnection(TrafficLink fromLane, TrafficLink toLane, out TrafficLink connection) =>
            _connectionsByLanes.TryGetValue((fromLane.Id, toLane.Id), out connection);

        internal RoadInfo RoadOf(TrafficLink lane) => lane.RoadInfo;

        /// <summary>Whether any route leads from the dead end <paramref name="from"/> to <paramref name="to"/>.</summary>
        public bool CanReach(NodeId from, NodeId to)
        {
            var visited = new HashSet<RoadInfo>();
            var pending = new Queue<RoadInfo>(_roads.Where(road => road.From == from));
            while (pending.Count > 0)
            {
                var road = pending.Dequeue();
                if (!visited.Add(road))
                    continue;
                if (road.To == to)
                    return true;
                foreach (var next in road.ConnectionsTo.Keys)
                    pending.Enqueue(next);
            }

            return false;
        }

        private Dictionary<Lane, TrafficLink> CreateLanes(RoadLayout layout)
        {
            var links = new Dictionary<Lane, TrafficLink>();
            var roads = new Dictionary<RoadDirection, RoadInfo>();

            foreach (var lane in layout.Lanes.Lanes)
            {
                var profile = layout.Segments[lane.Segment].Segment.Profile;
                var direction = new RoadDirection(lane.Segment, lane.IsForward);
                if (!roads.TryGetValue(direction, out var road))
                {
                    road = new RoadInfo(_roads.Count, direction, lane.From, lane.To);
                    roads.Add(direction, road);
                    _roads.Add(road);
                }

                var geometry = layout.Segments[lane.Segment];
                var trimmedStart = geometry.HeightAt(geometry.TrimStart);
                var trimmedEnd = geometry.HeightAt(geometry.Centerline.Length - geometry.TrimEnd);
                var link = new TrafficLink(_links.Count, LinkKind.Lane, lane.Path, profile.SpeedLimit)
                {
                    StartHeight = lane.IsForward ? trimmedStart : trimmedEnd,
                    EndHeight = lane.IsForward ? trimmedEnd : trimmedStart,
                    Road = direction,
                    LaneIndex = lane.Index,
                    LaneWidth = profile.LaneWidth,
                    EndNode = lane.To,
                    RoadInfo = road
                };
                _links.Add(link);
                links.Add(lane, link);
                _lanesByKey.Add((direction.Segment, direction.Forward, lane.Index), link);
            }

            foreach (var road in _roads)
                road.Lanes.AddRange(_links.Where(link => link.RoadInfo == road).OrderBy(link => link.LaneIndex));
            foreach (var road in _roads)
                road.ObservedSpeed = road.SpeedLimit;

            return links;
        }

        private void CreateConnections(RoadLayout layout, IReadOnlyDictionary<Lane, TrafficLink> lanes, TrafficSettings settings)
        {
            foreach (var connection in layout.Lanes.Connections)
            {
                var from = lanes[connection.From];
                var to = lanes[connection.To];
                var turnSpeed = math.max(settings.MinTurnSpeed, math.sqrt(settings.ComfortableLateralAcceleration * TurnRadius(from, to, connection.Path)));
                var link = new TrafficLink(_links.Count, LinkKind.Connection, connection.Path,
                    math.min(turnSpeed, math.min(from.SpeedLimit, to.SpeedLimit)))
                {
                    StartHeight = from.EndHeight,
                    EndHeight = to.StartHeight,
                    From = from,
                    To = to,
                    Node = connection.Node,
                    Turn = connection.Turn
                };
                _links.Add(link);
                from.AddOutgoing(link);
                link.AddOutgoing(to);
                _connectionsByLanes[(from.Id, to.Id)] = link;

                if (!from.RoadInfo.ConnectionsTo.TryGetValue(to.RoadInfo, out var list))
                    from.RoadInfo.ConnectionsTo.Add(to.RoadInfo, list = new List<TrafficLink>());
                list.Add(link);
            }
        }

        private void ApplyJunctionRules(RoadNetwork network, RoadLayout layout)
        {
            var connectionsByNode = _links.Where(link => link.Kind == LinkKind.Connection).ToLookup(link => link.Node);
            var lanesByEnd = _links.Where(link => link.Kind == LinkKind.Lane).ToLookup(link => link.EndNode);

            foreach (var junction in layout.Junctions.Values)
            {
                var lanes = lanesByEnd[junction.Node].ToArray();
                var connections = connectionsByNode[junction.Node].ToArray();
                var settings = network.GetJunctionSettings(junction.Node);
                if (settings.Control == JunctionControl.TrafficLights && JunctionRules.CanHaveSignals(junction, network))
                    _signals.Add(JunctionRules.ApplySignals(junction, network, settings.ProtectedLeft, lanes, connections));
                else
                    JunctionRules.Apply(junction, network, lanes, connections);
            }

            foreach (var road in _roads)
            {
                road.JunctionPenalty = road.Lanes[0].Control switch
                {
                    ApproachControl.Stop => StopPenalty,
                    ApproachControl.Yield => YieldPenalty,
                    ApproachControl.Signal => SignalPenalty,
                    _ => 0f
                };
            }
        }

        /// <summary>Connections of a junction conflict where their paths cross or where they merge into the same lane.</summary>
        private void FindConflicts()
        {
            foreach (var group in _links.Where(link => link.Kind == LinkKind.Connection).GroupBy(link => link.Node))
            {
                var connections = group.ToArray();
                for (var i = 0; i < connections.Length; i++)
                for (var j = i + 1; j < connections.Length; j++)
                {
                    var a = connections[i];
                    var b = connections[j];
                    if (a.From == b.From)
                        continue; // same lane: plain car following until they part

                    if (a.To == b.To)
                    {
                        AddConflict(a, b, a.Length, b.Length, isMerge: true);
                        continue;
                    }

                    var hits = a.Path.IntersectionsWith(b.Path);
                    if (hits.Count == 0)
                        continue;

                    var first = hits.OrderBy(hit => hit.alongThis).First();
                    AddConflict(a, b, first.alongThis, first.alongOther, isMerge: false);
                }
            }
        }

        private static void AddConflict(TrafficLink a, TrafficLink b, float alongA, float alongB, bool isMerge)
        {
            a.AddConflict(new LinkConflict(b, alongA, alongB, isMerge));
            b.AddConflict(new LinkConflict(a, alongB, alongA, isMerge));
        }

        /// <summary>Average radius of a junction path: its length over the total change of heading between the two lanes.</summary>
        private static float TurnRadius(TrafficLink from, TrafficLink to, Polyline path)
        {
            var heading = math.abs(Geometry2D.SignedAngle(from.Path.EndDirection, to.Path.StartDirection));
            return heading < 1e-3f ? float.MaxValue : math.max(path.Length, 1f) / heading;
        }
    }
}
