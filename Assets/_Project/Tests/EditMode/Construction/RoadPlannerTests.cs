using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Construction;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    public sealed class RoadPlannerTests
    {
        [Test]
        public void StraightRoadOnEmptyMap_IsValidAndPricedByLength()
        {
            var plan = new ConstructionTestBed().Plan(float2.zero, new float2(100f, 0f));

            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
            Assert.That(plan.Cost, Is.EqualTo((long)(100f * TestRoads.CostPerMeter)));
            Assert.That(plan.Crossings, Is.Empty);
        }

        [Test]
        public void VeryShortRoad_IsRejected()
        {
            Assert.That(new ConstructionTestBed().Plan(float2.zero, new float2(3f, 0f)).Error, Is.EqualTo(BuildError.TooShort));
        }

        [Test]
        public void RoadAcrossExistingRoad_FindsOneCrossing()
        {
            var plan = ConstructionTestBed.WithEastWestRoad().Plan(new float2(0f, -50f), new float2(0f, 50f));

            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
            Assert.That(plan.Crossings.Count, Is.EqualTo(1));
            Assert.That(plan.Crossings[0].NewParameter, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(math.distance(plan.Crossings[0].Point, float2.zero), Is.LessThan(0.01f));
        }

        [Test]
        public void ShallowCrossing_IsRejected()
        {
            var plan = ConstructionTestBed.WithEastWestRoad().Plan(new float2(-60f, -10f), new float2(60f, 10f));

            Assert.That(plan.Error, Is.EqualTo(BuildError.AngleTooSharp));
        }

        [Test]
        public void RoadRunningAlongsideExistingRoad_Overlaps()
        {
            var plan = ConstructionTestBed.WithEastWestRoad().Plan(new float2(-50f, 7.5f), new float2(50f, 7.5f));

            Assert.That(plan.Error, Is.EqualTo(BuildError.OverlapsRoad));
        }

        [Test]
        public void RoadJoiningAtAShallowAllowedAngle_IsNotAnOverlap()
        {
            // 35° is above the minimum connection angle; near the joint the two surfaces touch for ~14 m.
            var bed = ConstructionTestBed.WithEastWestRoad();
            var direction = new float2(math.cos(math.radians(35f)), math.sin(math.radians(35f)));

            var plan = bed.Plan(new float2(-60f, 0.5f), new float2(-60f, 0f) + direction * 120f);

            Assert.That(plan.Request.Start.Kind, Is.EqualTo(AnchorKind.Segment));
            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
        }

        [Test]
        public void StreetDrawnThroughDeadEnds_JoinsThem()
        {
            // Three short driveways pointing east end at x = 0; a street drawn straight north through their ends.
            var network = new RoadNetwork();
            foreach (var y in new[] { -50f, 0f, 50f })
                network.AddStraightSegment(network.AddNode(new float2(-15f, y)), network.AddNode(new float2(0f, y)), TestRoads.TwoLane);
            var bed = new ConstructionTestBed(network);

            var plan = bed.Plan(new float2(0f, -50f), new float2(0f, 50f));
            Assert.That(plan.Error, Is.EqualTo(BuildError.None));
            Assert.That(plan.PassThroughs.Count, Is.EqualTo(1));

            new BuildRoadCommand(bed.Network, bed.Budget, plan).Execute();
            Assert.That(bed.Network.Nodes.Single(node => math.length(node.Position) < 0.01f).Degree, Is.EqualTo(3));
        }

        [Test]
        public void TightHairpin_IsRejected()
        {
            var plan = new ConstructionTestBed().Plan(float2.zero, new float2(0f, 10f), control: new float2(60f, 5f));

            Assert.That(plan.Error, Is.EqualTo(BuildError.CurveTooSharp));
        }

        [Test]
        public void RoadOutsideTheMap_IsRejected()
        {
            var plan = new ConstructionTestBed().Plan(new float2(900f, 0f), new float2(1100f, 0f));

            Assert.That(plan.Error, Is.EqualTo(BuildError.OutOfBounds));
        }

        [Test]
        public void UnaffordableRoad_IsRejectedButStillPriced()
        {
            var plan = new ConstructionTestBed(balance: 10).Plan(float2.zero, new float2(100f, 0f));

            Assert.That(plan.Error, Is.EqualTo(BuildError.InsufficientFunds));
            Assert.That(plan.Cost, Is.GreaterThan(10));
        }

        [Test]
        public void ContinuingFromDeadEnd_ReportsDeviationFromStraight()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            var plan = bed.Plan(new float2(100f, 0f), new float2(200f, 0f));

            Assert.That(plan.ConnectionAngle, Is.EqualTo(0f).Within(0.1f));
        }
    }
}
