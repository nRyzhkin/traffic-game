using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Lanes
{
    /// <summary>
    /// Creates lanes for every segment and connections through every junction from lane arrows: the player's
    /// where painted, otherwise the defaults — straight from all lanes, left turns from the leftmost lane, right turns
    /// from the rightmost. Bends join all lanes and dead ends turn around.
    /// </summary>
    internal static class LaneGraphBuilder
    {
        private static readonly float StraightTolerance = math.radians(35f);
        private const float HandleFactor = 0.4f;
        private const float MinHandle = 2f;
        private const float ConnectionSampleSpacing = 1f;

        public static LaneGraph Build(IEnumerable<SegmentGeometry> segments, IEnumerable<JunctionGeometry> junctions,
            Func<NodeId, JunctionSettings> settingsOf)
        {
            var lanes = new List<Lane>();
            var lanesBySegment = new Dictionary<SegmentId, List<Lane>>();
            foreach (var geometry in segments)
                lanesBySegment.Add(geometry.Segment.Id, CreateLanes(geometry, lanes));

            var connections = new List<LaneConnection>();
            foreach (var junction in junctions)
                ConnectJunction(junction, settingsOf(junction.Node), lanesBySegment, connections);

            return new LaneGraph(lanes, connections);
        }

        private static LaneArrows DefaultArrows(int index, int laneCount, LaneArrows available)
        {
            var arrows = available & LaneArrows.Straight;
            if (index == 0)
                arrows |= available & LaneArrows.Left;
            if (index == laneCount - 1)
                arrows |= available & LaneArrows.Right;
            if (arrows == LaneArrows.None) // a middle lane where the road ends: turn to the nearer side
                arrows = available & (index < laneCount * 0.5f ? LaneArrows.Left : LaneArrows.Right);
            return arrows;
        }

        public static LaneArrows ArrowOf(TurnDirection turn) => turn switch
        {
            TurnDirection.Left => LaneArrows.Left,
            TurnDirection.Right => LaneArrows.Right,
            TurnDirection.Straight => LaneArrows.Straight,
            _ => LaneArrows.None
        };

        private static List<Lane> CreateLanes(SegmentGeometry geometry, List<Lane> allLanes)
        {
            var segment = geometry.Segment;
            var profile = segment.Profile;
            var result = new List<Lane>(profile.LaneCount);

            for (var index = 0; index < profile.ForwardLanes; index++)
            {
                var path = geometry.Trimmed.Offset(profile.LaneCenterOffset(true, index));
                result.Add(new Lane(allLanes.Count + result.Count, segment.Id, true, index, segment.Start, segment.End, path));
            }

            for (var index = 0; index < profile.BackwardLanes; index++)
            {
                var path = geometry.Trimmed.Offset(profile.LaneCenterOffset(false, index)).Reversed();
                result.Add(new Lane(allLanes.Count + result.Count, segment.Id, false, index, segment.End, segment.Start, path));
            }

            allLanes.AddRange(result);
            return result;
        }

        private static void ConnectJunction(JunctionGeometry junction, JunctionSettings settings,
            IReadOnlyDictionary<SegmentId, List<Lane>> lanesBySegment, List<LaneConnection> connections)
        {
            Lane[] LanesOf(JunctionApproach approach, bool arriving) => lanesBySegment[approach.Segment]
                .Where(lane => arriving ? lane.To == junction.Node : lane.From == junction.Node)
                .OrderBy(lane => lane.Index)
                .ToArray();

            foreach (var entry in junction.Approaches)
            {
                var incoming = LanesOf(entry, arriving: true);
                if (incoming.Length == 0)
                    continue;

                if (junction.IsDeadEnd)
                {
                    var returning = LanesOf(entry, arriving: false);
                    if (returning.Length > 0)
                        ConnectAll(incoming, returning, junction.Node, TurnDirection.UTurn, connections);
                    continue;
                }

                var exits = junction.Approaches
                    .Where(exit => exit.Segment != entry.Segment)
                    .Select(exit => (lanes: LanesOf(exit, arriving: false), turn: Classify(entry, exit)))
                    .Where(exit => exit.lanes.Length > 0)
                    .ToArray();

                if (exits.Length == 1)
                {
                    ConnectAll(incoming, exits[0].lanes, junction.Node, exits[0].turn, connections);
                    continue;
                }

                var available = exits.Aggregate(LaneArrows.None, (arrows, exit) => arrows | ArrowOf(exit.turn));
                foreach (var lane in incoming)
                {
                    var arrows = settings.ArrowsOf(entry.Segment, lane.Index) & available;
                    lane.DefaultArrows = DefaultArrows(lane.Index, incoming.Length, available);
                    lane.Arrows = arrows != LaneArrows.None ? arrows : lane.DefaultArrows;
                    lane.AvailableArrows = available;
                }

                foreach (var (outgoing, turn) in exits)
                {
                    var arrow = ArrowOf(turn);
                    var group = incoming.Where(lane => (lane.Arrows & arrow) != 0).ToArray();
                    if (group.Length == 0)
                        continue;

                    if (turn == TurnDirection.Straight)
                    {
                        ConnectAll(group, outgoing, junction.Node, turn, connections);
                        continue;
                    }

                    // Turning lanes pair up with exit lanes from the side they turn to: the innermost left lane takes the
                    // innermost exit lane, the outermost right lane the outermost one.
                    for (var k = 0; k < group.Length; k++)
                    {
                        var exitIndex = math.min(k, outgoing.Length - 1);
                        connections.Add(turn == TurnDirection.Right
                            ? Connect(group[group.Length - 1 - k], outgoing[outgoing.Length - 1 - exitIndex], junction.Node, turn)
                            : Connect(group[k], outgoing[exitIndex], junction.Node, turn));
                    }
                }
            }
        }

        /// <summary>Spreads lanes evenly so that every incoming and every outgoing lane is used (splits and merges included).</summary>
        private static void ConnectAll(Lane[] incoming, Lane[] outgoing, NodeId node, TurnDirection turn, List<LaneConnection> connections)
        {
            var pairs = new List<(int from, int to)>();

            void AddPair(int from, int to)
            {
                if (!pairs.Contains((from, to)))
                    pairs.Add((from, to));
            }

            for (var from = 0; from < incoming.Length; from++)
                AddPair(from, Spread(from, incoming.Length, outgoing.Length));
            for (var to = 0; to < outgoing.Length; to++)
                AddPair(Spread(to, outgoing.Length, incoming.Length), to);

            foreach (var (from, to) in pairs.OrderBy(pair => pair.from).ThenBy(pair => pair.to))
                connections.Add(Connect(incoming[from], outgoing[to], node, turn));
        }

        private static int Spread(int index, int count, int targetCount) =>
            count == 1 ? 0 : (int)math.round(index * (targetCount - 1) / (float)(count - 1));

        private static LaneConnection Connect(Lane from, Lane to, NodeId node, TurnDirection turn)
        {
            var start = from.Path.End;
            var end = to.Path.Start;
            var distance = math.distance(start, end);
            // Nearly straight paths keep their handles within a third of the gap (longer ones would fold the curve back
            // on itself); real turns and U-turns get a minimum handle so they stay round.
            var nearlyStraight = math.dot(from.Path.EndDirection, to.Path.StartDirection) > 0.5f;
            var handle = nearlyStraight ? distance / 3f : math.max(distance * HandleFactor, MinHandle);
            var curve = CubicBezier.Hermite(start, from.Path.EndDirection, end, to.Path.StartDirection, handle);
            return new LaneConnection(from, to, node, turn, Polyline.FromBezier(curve, ConnectionSampleSpacing));
        }

        private static TurnDirection Classify(JunctionApproach entry, JunctionApproach exit)
        {
            var angle = Geometry2D.SignedAngle(-entry.Direction, exit.Direction);
            if (math.abs(angle) <= StraightTolerance)
                return TurnDirection.Straight;
            return angle > 0f ? TurnDirection.Left : TurnDirection.Right;
        }
    }
}
