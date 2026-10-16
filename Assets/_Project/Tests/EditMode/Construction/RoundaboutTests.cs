using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    public sealed class RoundaboutTests
    {
        private const float Radius = 22f;
        private static readonly RoadProfile Ring = new("ring", 1, 0, TestRoads.LaneWidth, TestRoads.ShoulderWidth, TestRoads.CostPerMeter, 9f);

        [Test]
        public void OnEmptyGround_BuildsAClosedCounterClockwiseRing()
        {
            var bed = new ConstructionTestBed();

            Build(bed, float2.zero);

            Assert.That(bed.Network.Segments.Count, Is.EqualTo(4));
            Assert.That(bed.Network.Nodes.All(node => node.Degree == 2));
            foreach (var segment in bed.Network.Segments)
            {
                var start = RoundaboutPlanner.AngleAround(float2.zero, segment.Curve.P0);
                var end = RoundaboutPlanner.AngleAround(float2.zero, segment.Curve.P3);
                Assert.That((end - start + 2f * math.PI) % (2f * math.PI), Is.EqualTo(math.PI / 2f).Within(1e-3f), "Each arc runs counter-clockwise.");
            }
        }

        [Test]
        public void OnACrossing_ReplacesTheJunctionWithFourApproaches()
        {
            var bed = new ConstructionTestBed(TestRoads.Crossing(TestRoads.TwoLane));

            Build(bed, float2.zero);

            Assert.That(bed.Network.Segments.Count, Is.EqualTo(8), "Four arcs and four shortened arms.");
            Assert.That(bed.Network.Nodes.Count(node => node.Degree == 3), Is.EqualTo(4));
            Assert.That(bed.Network.Nodes.Any(node => math.length(node.Position) < 1f), Is.False, "The old junction is gone.");
        }

        [Test]
        public void UnderABridge_JoinsOnlyTheRoadsOnItsOwnLevel()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();
            bed.Build(bed.Plan(new float2(0f, -80f), 1, new float2(0f, 80f), 1));
            var bridge = bed.Network.Segments.Single(segment => segment.StartHeight > 0f).Id;

            var plan = new RoundaboutPlanner(ConstructionTestBed.Rules).Plan(float2.zero, Radius, Ring, bed.Layout, bed.Budget);
            new BuildRoundaboutCommand(bed.Network, bed.Budget, plan).Execute();

            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
            Assert.That(plan.Crossings.Count, Is.EqualTo(2), "Only the road on the ground becomes an approach.");
            Assert.That(bed.Network.Contains(bridge), Is.True, "The bridge passes over the ring and stays.");
            Assert.That(bed.Network.Segments.Where(segment => segment.IsRoundabout).All(segment => segment.StartHeight == 0f));
        }

        [Test]
        public void OnTheLevelAbove_PassesOverTheGroundRoads()
        {
            var bed = new ConstructionTestBed(TestRoads.Crossing(TestRoads.TwoLane));

            var plan = new RoundaboutPlanner(ConstructionTestBed.Rules).Plan(new float2(0f, 0f), Radius, Ring, bed.Layout, bed.Budget,
                RoadElevation.HeightOf(1));
            new BuildRoundaboutCommand(bed.Network, bed.Budget, plan).Execute();

            Assert.That(plan.Crossings, Is.Empty);
            Assert.That(bed.Network.Nodes.Any(node => math.length(node.Position) < 1f), Is.True, "The junction below is kept.");
            Assert.That(bed.Network.Segments.Count(segment => segment.IsRoundabout), Is.EqualTo(4));
        }

        [Test]
        public void RingLanes_EnterByTurningRightAndNeverDriveAgainstTheRing()
        {
            var bed = new ConstructionTestBed(TestRoads.Crossing(TestRoads.TwoLane));
            Build(bed, float2.zero);

            var graph = RoadLayout.Build(bed.Network, TestRoads.Settings).Lanes;
            var ringNode = bed.Network.Nodes.First(node => node.Degree == 3).Id;
            var turns = graph.Connections.Where(c => c.Node == ringNode).Select(c => c.Turn).ToArray();

            Assert.That(turns.Length, Is.EqualTo(3), "Ring on, ring to road, road to ring.");
            Assert.That(turns.Count(turn => turn == TurnDirection.Right), Is.EqualTo(2));
        }

        [Test]
        public void OverAStraightRoad_ConnectsBothSides()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            Build(bed, float2.zero);

            Assert.That(bed.Network.Nodes.Count(node => node.Degree == 3), Is.EqualTo(2));
            Assert.That(bed.Network.Segments.Count, Is.EqualTo(6), "Four arcs and two road pieces.");
        }

        [Test]
        public void ApproachesTooCloseTogether_AreRejected()
        {
            var network = new RoadNetwork();
            var a = network.AddNode(new float2(0f, -100f));
            network.AddStraightSegment(a, network.AddNode(new float2(0f, 100f)), TestRoads.TwoLane);
            network.AddStraightSegment(network.AddNode(new float2(8f, -100f)), network.AddNode(new float2(8f, 100f)), TestRoads.TwoLane);
            var bed = new ConstructionTestBed(network);

            var plan = Plan(bed, float2.zero);

            Assert.That(plan.Error, Is.EqualTo(BuildError.JunctionTooClose));
        }

        private static RoundaboutPlan Plan(ConstructionTestBed bed, float2 center) =>
            new RoundaboutPlanner(ConstructionTestBed.Rules).Plan(center, Radius, Ring, bed.Layout, bed.Budget);

        private static void Build(ConstructionTestBed bed, float2 center)
        {
            var plan = Plan(bed, center);
            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
            new BuildRoundaboutCommand(bed.Network, bed.Budget, plan).Execute();
        }
    }
}
