using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    public sealed class ElevationTests
    {
        [Test]
        public void BridgeALevelUp_PassesOverTheRoadWithoutAJunction()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            var plan = bed.Plan(new float2(0f, -60f), 1, new float2(0f, 60f), 1);
            bed.Build(plan);

            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
            Assert.That(plan.Crossings, Is.Empty);
            Assert.That(bed.Network.Segments.Count, Is.EqualTo(2), "Neither road is split.");
            Assert.That(bed.Network.Nodes.Count(node => node.Height == RoadElevation.HeightOf(1)), Is.EqualTo(2));
        }

        [Test]
        public void RampThatIsTooShortForItsClimb_IsRejected()
        {
            var plan = new ConstructionTestBed().Plan(new float2(0f, 0f), 0, new float2(30f, 0f), 1);

            Assert.That(plan.Error, Is.EqualTo(BuildError.TooSteep));
        }

        [Test]
        public void RampCrossingARoadHalfwayUp_IsRejected()
        {
            var plan = ConstructionTestBed.WithEastWestRoad().Plan(new float2(0f, -60f), 0, new float2(0f, 60f), 1);

            Assert.That(plan.Error, Is.EqualTo(BuildError.HeightConflict));
        }

        [Test]
        public void RampOnTheGround_JoinsTheRoadBelowAndClimbs()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            var plan = bed.Plan(new float2(0f, 0f), 0, new float2(0f, 60f), 1);
            bed.Build(plan);

            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
            Assert.That(plan.Request.Start.Kind, Is.EqualTo(AnchorKind.Segment));
            var ramp = bed.Network.Segments.Single(segment => segment.IsRamp);
            Assert.That(ramp.HeightAt(0.5f), Is.EqualTo(RoadElevation.HeightOf(1) * 0.5f).Within(0.01f));
        }

        [Test]
        public void PointerOverARoadOnAnotherLevel_DoesNotSnapToIt()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            var anchor = bed.Snapper.Snap(new float2(10f, 1f), bed.Network, bed.Layout, RoadElevation.HeightOf(1));

            Assert.That(anchor.Kind, Is.EqualTo(AnchorKind.Free));
            Assert.That(anchor.Height, Is.EqualTo(RoadElevation.HeightOf(1)));
        }

        [Test]
        public void SplittingARamp_PutsTheNewNodeAtTheRampHeight()
        {
            var network = new RoadNetwork();
            var segment = network.AddStraightSegment(network.AddNode(float2.zero), network.AddNode(new float2(100f, 0f), 10f), TestRoads.TwoLane);

            var node = network.SplitSegment(segment, 0.25f, out var first, out var second);

            Assert.That(network.GetNode(node).Height, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(network.GetSegment(first).EndHeight, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(network.GetSegment(second).HeightAt(0f), Is.EqualTo(2.5f).Within(1e-4f));
        }

        [Test]
        public void ElevatedRoad_CostsMoreThanOnTheGround()
        {
            var bed = new ConstructionTestBed();

            var ground = bed.Plan(new float2(0f, 0f), 0, new float2(100f, 0f), 0);
            var bridge = bed.Plan(new float2(0f, 0f), 1, new float2(100f, 0f), 1);

            Assert.That(bridge.Cost, Is.EqualTo(ground.Cost * (1f + ConstructionTestBed.Rules.ElevatedCostFactor)).Within(1f));
        }

        [Test]
        public void VehiclesOnABridge_AreReportedAtItsHeight()
        {
            var network = new RoadNetwork();
            var west = network.AddNode(new float2(-100f, 0f), RoadElevation.HeightOf(1));
            var east = network.AddNode(new float2(100f, 0f), RoadElevation.HeightOf(1));
            network.AddStraightSegment(west, east, TestRoads.TwoLane);
            var traffic = TrafficNetwork.Build(network, RoadLayout.Build(network, TestRoads.Settings), new TrafficSettings());

            Assert.That(traffic.Links.All(link => link.StartHeight == RoadElevation.HeightOf(1) && link.EndHeight == RoadElevation.HeightOf(1)));
        }
    }
}
