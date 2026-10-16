using NUnit.Framework;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    public sealed class SnappingTests
    {
        [Test]
        public void PointerNearNode_SnapsToNode()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            var anchor = bed.Snapper.Snap(new float2(103f, 2f), bed.Network, bed.Layout);

            Assert.That(anchor.Kind, Is.EqualTo(AnchorKind.Node));
            Assert.That(anchor.Position, Is.EqualTo(new float2(100f, 0f)));
        }

        [Test]
        public void PointerOnRoad_SnapsOntoCenterline()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            var anchor = bed.Snapper.Snap(new float2(25f, 3f), bed.Network, bed.Layout);

            Assert.That(anchor.Kind, Is.EqualTo(AnchorKind.Segment));
            Assert.That(math.distance(anchor.Position, new float2(25f, 0f)), Is.LessThan(0.01f));
            Assert.That(anchor.Parameter, Is.EqualTo(0.625f).Within(0.01f));
        }

        [Test]
        public void PointerOnRoadNearItsEnd_SnapsToEndNode()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            var anchor = bed.Snapper.Snap(new float2(92f, 3f), bed.Network, bed.Layout);

            Assert.That(anchor.Kind, Is.EqualTo(AnchorKind.Node));
        }

        [Test]
        public void PointerAwayFromRoads_StaysFree()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();

            Assert.That(bed.Snapper.Snap(new float2(0f, 40f), bed.Network, bed.Layout).Kind, Is.EqualTo(AnchorKind.Free));
        }

        [Test]
        public void SplitSegment_KeepsTheCurveShape()
        {
            var network = new RoadNetwork();
            var a = network.AddNode(float2.zero);
            var b = network.AddNode(new float2(100f, 0f));
            var original = network.GetSegment(network.AddSegment(a, b, new float2(30f, 40f), new float2(70f, 40f), TestRoads.TwoLane)).Curve;

            var middle = network.SplitSegment(network.GetNode(a).Segments[0], 0.5f, out var first, out var second);

            Assert.That(math.distance(network.GetNode(middle).Position, original.Evaluate(0.5f)), Is.LessThan(1e-4f));
            Assert.That(math.distance(network.GetSegment(first).Curve.Evaluate(0.5f), original.Evaluate(0.25f)), Is.LessThan(1e-3f));
            Assert.That(math.distance(network.GetSegment(second).Curve.Evaluate(0.5f), original.Evaluate(0.75f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void Guides_SnapContinuationFromDeadEnd()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();
            var layout = bed.Layout;
            var guides = new RoadGuides(snapAngleDegrees: 4f, searchRadius: 60f);
            var start = bed.Snapper.Snap(new float2(100f, 0f), bed.Network, layout);

            var snapped = guides.Apply(start, new float2(200f, 4f), bed.Network, layout, out var guide);

            Assert.That(guide.Kind, Is.EqualTo(GuideKind.Continuation));
            Assert.That(math.distance(snapped, new float2(200f, 0f)), Is.LessThan(0.2f));
        }

        [Test]
        public void Guides_SnapPerpendicularFromDeadEnd()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();
            var layout = bed.Layout;
            var guides = new RoadGuides(snapAngleDegrees: 4f, searchRadius: 60f);
            var start = bed.Snapper.Snap(new float2(100f, 0f), bed.Network, layout);

            var snapped = guides.Apply(start, new float2(103f, 100f), bed.Network, layout, out var guide);

            Assert.That(guide.Kind, Is.EqualTo(GuideKind.Perpendicular));
            Assert.That(snapped.x, Is.EqualTo(100f).Within(0.01f));
        }
    }
}
