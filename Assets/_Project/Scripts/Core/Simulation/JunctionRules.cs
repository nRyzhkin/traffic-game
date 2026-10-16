using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Simulation
{
    /// <summary>
    /// Default right of way at unsignalized junctions, after common US practice:
    /// a roundabout ring has priority and entries yield; otherwise the straight, widest road through the junction
    /// is the major road and side roads have stop signs; if no road clearly dominates it is an all-way stop.
    /// On the major road left turns yield to oncoming traffic.
    /// Junctions the player gave traffic lights get a phase per pair of opposite approaches (see <see cref="ApplySignals"/>).
    /// </summary>
    public static class JunctionRules
    {
        public const int MinorRank = 1;
        public const int MajorLeftRank = 2;
        public const int MajorRank = 3;

        private const float StraightCosine = -0.866f; // approaches more than 150° apart form a through road

        /// <summary>Whether traffic lights can run the junction: three or more roads and no roundabout ring.</summary>
        public static bool CanHaveSignals(JunctionGeometry junction, RoadNetwork network) =>
            junction.Approaches.Count >= 3 && junction.Approaches.All(approach => !network.GetSegment(approach.Segment).IsRoundabout);

        internal static void Apply(JunctionGeometry junction, RoadNetwork network, IReadOnlyList<TrafficLink> incomingLanes,
            IReadOnlyList<TrafficLink> connections)
        {
            var majorSegments = MajorSegments(junction, network, out var control);

            foreach (var lane in incomingLanes)
                lane.Control = majorSegments.Contains(lane.Road.Segment) ? ApproachControl.None : control;

            foreach (var connection in connections)
            {
                var major = majorSegments.Contains(connection.From.Road.Segment);
                connection.Rank = !major ? MinorRank : connection.Turn == TurnDirection.Left ? MajorLeftRank : MajorRank;
            }
        }

        /// <summary>
        /// Puts the junction under traffic lights. Opposite approaches share a phase (left turns yield to oncoming
        /// traffic), other approaches get one each, the widest road first. With protected left turns a pair of
        /// opposite approaches first gets a phase for its turning traffic only.
        /// </summary>
        internal static TrafficSignal ApplySignals(JunctionGeometry junction, RoadNetwork network, bool protectedLeft,
            IReadOnlyList<TrafficLink> incomingLanes, IReadOnlyList<TrafficLink> connections)
        {
            foreach (var lane in incomingLanes)
                lane.Control = ApproachControl.Signal;
            foreach (var connection in connections)
                connection.Rank = connection.Turn == TurnDirection.Left ? MajorLeftRank : MajorRank;

            var phases = new List<TrafficLink[]>();
            foreach (var group in OppositeGroups(junction, network, incomingLanes))
            {
                var groupConnections = connections.Where(connection => group.Contains(connection.From.Road.Segment)).ToArray();
                if (groupConnections.Length == 0)
                    continue;
                if (protectedLeft && group.Count == 2 && groupConnections.Any(connection => connection.Turn == TurnDirection.Left))
                    phases.Add(groupConnections.Where(connection => connection.Turn != TurnDirection.Straight).ToArray());
                phases.Add(groupConnections);
            }

            var signal = new TrafficSignal(junction.Node, incomingLanes.ToList(), math.max(phases.Count, 1));
            for (var phase = 0; phase < phases.Count; phase++)
            {
                foreach (var connection in phases[phase])
                    connection.PhaseMask |= 1 << phase;
            }

            foreach (var lane in incomingLanes)
                lane.Signal = signal;
            foreach (var connection in connections)
                connection.Signal = signal;
            return signal;
        }

        /// <summary>Approaches with arriving traffic, paired with the one opposite where there is one; widest first.</summary>
        private static IEnumerable<HashSet<SegmentId>> OppositeGroups(JunctionGeometry junction, RoadNetwork network,
            IReadOnlyList<TrafficLink> incomingLanes)
        {
            var approaches = junction.Approaches
                .Where(approach => incomingLanes.Any(lane => lane.Road.Segment == approach.Segment))
                .ToArray();

            var pairs = new List<(float dot, int a, int b)>();
            for (var i = 0; i < approaches.Length; i++)
            for (var j = i + 1; j < approaches.Length; j++)
            {
                var dot = math.dot(approaches[i].Direction, approaches[j].Direction);
                if (dot <= StraightCosine)
                    pairs.Add((dot, i, j));
            }

            var groups = new List<HashSet<SegmentId>>();
            var paired = new bool[approaches.Length];
            foreach (var (_, a, b) in pairs.OrderBy(pair => pair.dot))
            {
                if (paired[a] || paired[b])
                    continue;
                paired[a] = paired[b] = true;
                groups.Add(new HashSet<SegmentId> { approaches[a].Segment, approaches[b].Segment });
            }

            for (var i = 0; i < approaches.Length; i++)
            {
                if (!paired[i])
                    groups.Add(new HashSet<SegmentId> { approaches[i].Segment });
            }

            return groups.OrderByDescending(group => group.Sum(segment => LaneCount(network, segment)));
        }

        /// <summary>The segments whose traffic has priority, and the control on all others.</summary>
        private static HashSet<SegmentId> MajorSegments(JunctionGeometry junction, RoadNetwork network, out ApproachControl minorControl)
        {
            var approaches = junction.Approaches;
            var segments = approaches.Select(approach => approach.Segment).ToArray();

            // Bends and dead ends need no rules.
            if (approaches.Count <= 2)
            {
                minorControl = ApproachControl.None;
                return new HashSet<SegmentId>(segments);
            }

            var ring = segments.Where(id => network.GetSegment(id).IsRoundabout).ToArray();
            if (ring.Length > 0)
            {
                minorControl = ApproachControl.Yield;
                return new HashSet<SegmentId>(ring);
            }

            minorControl = ApproachControl.Stop;
            var pairs = new List<(int weight, SegmentId a, SegmentId b)>();
            for (var i = 0; i < approaches.Count; i++)
            for (var j = i + 1; j < approaches.Count; j++)
            {
                if (math.dot(approaches[i].Direction, approaches[j].Direction) > StraightCosine)
                    continue;
                var weight = LaneCount(network, approaches[i].Segment) + LaneCount(network, approaches[j].Segment);
                pairs.Add((weight, approaches[i].Segment, approaches[j].Segment));
            }

            var ordered = pairs.OrderByDescending(pair => pair.weight).ToArray();
            if (ordered.Length == 0 || (ordered.Length > 1 && ordered[0].weight == ordered[1].weight))
                return new HashSet<SegmentId>(); // equal roads cross: all-way stop

            return new HashSet<SegmentId> { ordered[0].a, ordered[0].b };
        }

        private static int LaneCount(RoadNetwork network, SegmentId segment) => network.GetSegment(segment).Profile.LaneCount;
    }
}
