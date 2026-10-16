using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Lanes
{
    public sealed class LaneGraphTests
    {
        private static readonly NodeId CrossingCenter = new(1);

        [Test]
        public void TwoLaneRoad_DrivesOnTheRight()
        {
            var graph = Build(TestRoads.SingleSegment(TestRoads.TwoLane));

            var eastbound = graph.Lanes.Single(lane => lane.IsForward);
            var westbound = graph.Lanes.Single(lane => !lane.IsForward);

            Assert.That(eastbound.Path.Start.y, Is.LessThan(0f), "Eastbound traffic keeps to the south side.");
            Assert.That(eastbound.Path.StartDirection.x, Is.GreaterThan(0f));
            Assert.That(westbound.Path.Start.y, Is.GreaterThan(0f));
            Assert.That(westbound.Path.StartDirection.x, Is.LessThan(0f));
        }

        [Test]
        public void DeadEnds_TurnTrafficAround()
        {
            var graph = Build(TestRoads.SingleSegment(TestRoads.TwoLane));

            Assert.That(graph.Connections.Count, Is.EqualTo(2));
            Assert.That(graph.Connections.Select(c => c.Turn), Is.All.EqualTo(TurnDirection.UTurn));
        }

        [Test]
        public void OneWayRoad_HasLanesOnlyInItsDirection()
        {
            var graph = Build(TestRoads.SingleSegment(TestRoads.OneWay));

            Assert.That(graph.Lanes.Count, Is.EqualTo(2));
            Assert.That(graph.Lanes.All(lane => lane.IsForward));
            Assert.That(graph.Connections, Is.Empty, "Nothing can turn around against a one-way street.");
        }

        [Test]
        public void TwoLaneCrossing_OffersLeftStraightAndRightFromEveryApproach()
        {
            var graph = Build(TestRoads.Crossing(TestRoads.TwoLane));

            var atCenter = graph.Connections.Where(c => c.Node == CrossingCenter).ToArray();

            Assert.That(atCenter.Length, Is.EqualTo(12));
            foreach (var group in atCenter.GroupBy(c => c.From))
                Assert.That(group.Select(c => c.Turn), Is.EquivalentTo(new[] { TurnDirection.Left, TurnDirection.Straight, TurnDirection.Right }));
        }

        [Test]
        public void FourLaneCrossing_TurnsFromOuterAndInnerLanesOnly()
        {
            var graph = Build(TestRoads.Crossing(TestRoads.FourLane));
            var atCenter = graph.Connections.Where(c => c.Node == CrossingCenter).ToArray();

            Assert.That(atCenter.Where(c => c.Turn == TurnDirection.Right), Is.All.Matches<LaneConnection>(c => c.From.Index == 1 && c.To.Index == 1));
            Assert.That(atCenter.Where(c => c.Turn == TurnDirection.Left), Is.All.Matches<LaneConnection>(c => c.From.Index == 0 && c.To.Index == 0));
            Assert.That(atCenter.Count(c => c.Turn == TurnDirection.Straight), Is.EqualTo(8));
        }

        [Test]
        public void Connections_JoinLaneEndsSeamlessly()
        {
            var graph = Build(TestRoads.Crossing(TestRoads.FourLane));

            foreach (var connection in graph.Connections)
            {
                Assert.That(math.distance(connection.Path.Start, connection.From.Path.End), Is.LessThan(1e-3f));
                Assert.That(math.distance(connection.Path.End, connection.To.Path.Start), Is.LessThan(1e-3f));
                Assert.That(graph.OutgoingFrom(connection.From), Does.Contain(connection));
                Assert.That(graph.IncomingTo(connection.To), Does.Contain(connection));
            }
        }

        [Test]
        public void TwoLaneCrossing_LanesCarryAllAvailableArrowsByDefault()
        {
            var graph = Build(TestRoads.Crossing(TestRoads.TwoLane));

            var arriving = graph.Lanes.Where(lane => lane.To == CrossingCenter).ToArray();
            var all = LaneArrows.Left | LaneArrows.Straight | LaneArrows.Right;
            Assert.That(arriving.Select(lane => lane.Arrows), Is.All.EqualTo(all));
            Assert.That(arriving.Select(lane => lane.AvailableArrows), Is.All.EqualTo(all));
        }

        [Test]
        public void PaintedArrows_LimitTheTurnsOfTheLane()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var east = EastArm(network);
            network.SetJunctionSettings(CrossingCenter, JunctionSettings.Default.WithArrows(east, 0, LaneArrows.Straight));
            var graph = Build(network);

            var fromEast = graph.Connections.Where(c => c.Node == CrossingCenter && c.From.Segment == east).ToArray();
            Assert.That(fromEast.Select(c => c.Turn), Is.EquivalentTo(new[] { TurnDirection.Straight }));
            Assert.That(graph.Connections.Count(c => c.Node == CrossingCenter), Is.EqualTo(10));
        }

        [Test]
        public void TwoLeftLanes_TurnSideBySideIntoTwoExitLanes()
        {
            var network = TestRoads.Crossing(TestRoads.FourLane);
            var east = EastArm(network);
            network.SetJunctionSettings(CrossingCenter, JunctionSettings.Default
                .WithArrows(east, 0, LaneArrows.Left)
                .WithArrows(east, 1, LaneArrows.Left | LaneArrows.Straight | LaneArrows.Right));
            var graph = Build(network);

            var lefts = graph.Connections.Where(c => c.Node == CrossingCenter && c.From.Segment == east && c.Turn == TurnDirection.Left)
                .OrderBy(c => c.From.Index).ToArray();
            Assert.That(lefts.Select(c => (c.From.Index, c.To.Index)), Is.EqualTo(new[] { (0, 0), (1, 1) }));
            Assert.That(graph.Connections.Count(c => c.From.Segment == east && c.From.Index == 0 && c.Node == CrossingCenter), Is.EqualTo(1));
        }

        [Test]
        public void ArrowsWithoutAMatchingExit_FallBackToTheDefault()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var east = EastArm(network);
            network.SetJunctionSettings(CrossingCenter, JunctionSettings.Default.WithArrows(east, 0, LaneArrows.Straight));
            network.RemoveSegment(WestArm(network));
            var graph = Build(network);

            var lane = graph.Lanes.Single(l => l.Segment == east && l.To == CrossingCenter);
            Assert.That(lane.Arrows, Is.EqualTo(LaneArrows.Left | LaneArrows.Right), "The T-junction has no straight exit any more.");
        }

        private static SegmentId EastArm(RoadNetwork network) => ArmTowards(network, new float2(1f, 0f));

        private static SegmentId WestArm(RoadNetwork network) => ArmTowards(network, new float2(-1f, 0f));

        private static SegmentId ArmTowards(RoadNetwork network, float2 direction) =>
            network.Segments.Single(segment => math.dot(network.GetNode(segment.End).Position, direction) > 50f).Id;

        private static LaneGraph Build(RoadNetwork network) => RoadLayout.Build(network, TestRoads.Settings).Lanes;
    }
}
