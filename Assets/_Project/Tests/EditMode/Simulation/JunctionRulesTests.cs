using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Tests.Construction;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Simulation
{
    public sealed class JunctionRulesTests
    {
        [Test]
        public void TJunction_ThroughRoadHasPrioritySideRoadStops()
        {
            var bed = new SimulationTestBed(SimulationTestBed.TJunction());
            var lanes = IncomingLanes(bed, new NodeId(1));

            Assert.That(lanes.Where(lane => IsSideRoad(bed, lane)).Select(lane => lane.Control), Is.All.EqualTo(ApproachControl.Stop));
            Assert.That(lanes.Where(lane => !IsSideRoad(bed, lane)).Select(lane => lane.Control), Is.All.EqualTo(ApproachControl.None));
        }

        [Test]
        public void EqualRoadsCrossing_IsAllWayStop()
        {
            var bed = new SimulationTestBed(SimulationTestBed.Crossing(TestRoads.TwoLane, TestRoads.TwoLane));

            Assert.That(IncomingLanes(bed, new NodeId(1)).Select(lane => lane.Control), Is.All.EqualTo(ApproachControl.Stop));
        }

        [Test]
        public void AvenueCrossingStreet_AvenueHasPriorityAndItsLeftTurnsYield()
        {
            var bed = new SimulationTestBed(SimulationTestBed.Crossing(TestRoads.TwoLane, TestRoads.FourLane));
            var connections = bed.Simulation.Network.Links.Where(link => link.Kind == LinkKind.Connection && link.Node == new NodeId(1)).ToArray();

            var avenue = connections.Where(c => bed.Network.GetSegment(c.From.Road.Segment).Profile == TestRoads.FourLane).ToArray();
            Assert.That(avenue.Where(c => c.Turn == TurnDirection.Left).Select(c => c.Rank), Is.All.EqualTo(JunctionRules.MajorLeftRank));
            Assert.That(avenue.Where(c => c.Turn != TurnDirection.Left).Select(c => c.Rank), Is.All.EqualTo(JunctionRules.MajorRank));
            Assert.That(connections.Except(avenue).Select(c => c.Rank), Is.All.EqualTo(JunctionRules.MinorRank));
        }

        [Test]
        public void TrafficLights_GiveOppositeApproachesACommonPhase()
        {
            var bed = new SimulationTestBed(WithLights(SimulationTestBed.Crossing(TestRoads.TwoLane, TestRoads.FourLane), protectedLeft: false));
            var signal = bed.Simulation.Network.Signals.Single();
            var connections = ConnectionsAt(bed, new NodeId(1));

            Assert.That(signal.PhaseCount, Is.EqualTo(2));
            Assert.That(IncomingLanes(bed, new NodeId(1)).Select(lane => lane.Control), Is.All.EqualTo(ApproachControl.Signal));
            Assert.That(connections.Select(c => c.PhaseMask), Is.All.EqualTo(1).Or.EqualTo(2), "Every connection goes in exactly one phase.");

            var avenue = connections.Where(c => bed.Network.GetSegment(c.From.Road.Segment).Profile == TestRoads.FourLane).ToArray();
            Assert.That(avenue.Select(c => c.PhaseMask), Is.All.EqualTo(1), "The wider road goes first.");
            Assert.That(avenue.Where(c => c.Turn == TurnDirection.Left).Select(c => c.Rank), Is.All.EqualTo(JunctionRules.MajorLeftRank),
                "Left turns on green yield to oncoming traffic.");
        }

        [Test]
        public void ProtectedLeft_AddsALeftTurnPhaseBeforeEachPair()
        {
            var bed = new SimulationTestBed(WithLights(SimulationTestBed.Crossing(TestRoads.TwoLane, TestRoads.FourLane), protectedLeft: true));
            var signal = bed.Simulation.Network.Signals.Single();
            var connections = ConnectionsAt(bed, new NodeId(1));

            Assert.That(signal.PhaseCount, Is.EqualTo(4));
            Assert.That(connections.Where(c => c.Turn == TurnDirection.Straight).Select(c => c.PhaseMask), Is.All.EqualTo(2).Or.EqualTo(8));
            Assert.That(connections.Where(c => c.Turn == TurnDirection.Left).Select(c => c.PhaseMask), Is.All.EqualTo(3).Or.EqualTo(12));
        }

        [Test]
        public void TJunctionLights_GiveTheSideRoadItsOwnPhase()
        {
            var bed = new SimulationTestBed(WithLights(SimulationTestBed.TJunction(), protectedLeft: true));

            Assert.That(bed.Simulation.Network.Signals.Single().PhaseCount, Is.EqualTo(3), "Through road: left-turn phase and main phase; side road: one.");
        }

        [Test]
        public void Roundabout_EntriesYieldToTheRing()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var construction = new ConstructionTestBed(network);
            var ring = new RoadProfile("ring", 1, 0, TestRoads.LaneWidth, TestRoads.ShoulderWidth, TestRoads.CostPerMeter, 9f);
            var plan = new RoundaboutPlanner(ConstructionTestBed.Rules).Plan(float2.zero, 22f, ring, construction.Layout, construction.Budget);
            new BuildRoundaboutCommand(network, construction.Budget, plan).Execute();

            var bed = new SimulationTestBed(network);
            var lanes = bed.Simulation.Network.Links.Where(link => link.Kind == LinkKind.Lane && bed.Network.GetNode(link.EndNode).Degree == 3).ToArray();

            Assert.That(lanes.Where(lane => bed.Network.GetSegment(lane.Road.Segment).IsRoundabout).Select(l => l.Control), Is.All.EqualTo(ApproachControl.None));
            Assert.That(lanes.Where(lane => !bed.Network.GetSegment(lane.Road.Segment).IsRoundabout).Select(l => l.Control), Is.All.EqualTo(ApproachControl.Yield));
        }

        [Test]
        public void CrossingPaths_ConflictButLanesLeavingTheSameLaneDoNot()
        {
            var bed = new SimulationTestBed(SimulationTestBed.Crossing(TestRoads.TwoLane, TestRoads.TwoLane));
            var connections = bed.Simulation.Network.Links.Where(link => link.Kind == LinkKind.Connection && link.Node == new NodeId(1)).ToArray();

            foreach (var connection in connections)
            {
                Assert.That(connection.Conflicts.Any(conflict => conflict.Other.From == connection.From), Is.False);
                if (connection.Turn == TurnDirection.Straight)
                    Assert.That(connection.Conflicts.Count, Is.GreaterThan(0));
            }
        }

        private static TrafficLink[] IncomingLanes(SimulationTestBed bed, NodeId node) =>
            bed.Simulation.Network.Links.Where(link => link.Kind == LinkKind.Lane && link.EndNode == node).ToArray();

        private static bool IsSideRoad(SimulationTestBed bed, TrafficLink lane)
        {
            var segment = bed.Network.GetSegment(lane.Road.Segment);
            var other = bed.Network.GetNode(segment.OtherEnd(lane.EndNode)).Position;
            return math.abs(other.x) < 1f;
        }

        internal static RoadNetwork WithLights(RoadNetwork network, bool protectedLeft)
        {
            foreach (var node in network.Nodes.Where(node => node.Degree >= 3).ToArray())
                network.SetJunctionSettings(node.Id, JunctionSettings.Default.WithControl(JunctionControl.TrafficLights, protectedLeft));
            return network;
        }

        private static TrafficLink[] ConnectionsAt(SimulationTestBed bed, NodeId node) =>
            bed.Simulation.Network.Links.Where(link => link.Kind == LinkKind.Connection && link.Node == node).ToArray();
    }
}
