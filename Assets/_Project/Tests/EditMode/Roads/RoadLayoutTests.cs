using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Roads
{
    public sealed class RoadLayoutTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void Crossing_PullsEveryArmBackPastTheCrossRoadEdgePlusPadding()
        {
            var layout = RoadLayout.Build(TestRoads.Crossing(TestRoads.TwoLane), TestRoads.Settings);
            var expected = TestRoads.TwoLane.HalfWidth + TestRoads.JunctionPadding;

            foreach (var segment in layout.Segments.Values)
            {
                Assert.That(segment.TrimStart, Is.EqualTo(expected).Within(Tolerance));
                Assert.That(segment.TrimEnd, Is.EqualTo(0f), "Dead ends are not trimmed.");
            }
        }

        [Test]
        public void Crossing_SortsApproachesCounterClockwise()
        {
            var layout = RoadLayout.Build(TestRoads.Crossing(TestRoads.TwoLane), TestRoads.Settings);
            var junction = layout.Junctions.Values.Single(j => j.Approaches.Count == 4);

            var angles = junction.Approaches.Select(a => math.atan2(a.Direction.y, a.Direction.x)).ToArray();

            Assert.That(angles, Is.Ordered);
            Assert.That(junction.Curbs.Count, Is.EqualTo(4));
            Assert.That(junction.Outline.Count, Is.GreaterThan(8));
        }

        [Test]
        public void StraightContinuation_KeepsOnlyTheMinimumGap()
        {
            var layout = RoadLayout.Build(TestRoads.StraightThroughNode(TestRoads.TwoLane), TestRoads.Settings);

            var trimsAtMiddle = layout.Segments.Values
                .Select(s => s.Segment.Start == new NodeId(2) ? s.TrimStart : s.TrimEnd);

            Assert.That(trimsAtMiddle, Is.All.EqualTo(TestRoads.MinJunctionTrim).Within(Tolerance));
        }

        [Test]
        public void DeadEnd_HasCapCurbAndNoSurface()
        {
            var layout = RoadLayout.Build(TestRoads.SingleSegment(TestRoads.TwoLane), TestRoads.Settings);

            foreach (var junction in layout.Junctions.Values)
            {
                Assert.That(junction.IsDeadEnd, Is.True);
                Assert.That(junction.Curbs.Count, Is.EqualTo(1));
                Assert.That(junction.Curbs[0].Length, Is.EqualTo(TestRoads.TwoLane.HalfWidth * 2f).Within(Tolerance));
                Assert.That(junction.Outline, Is.Empty);
            }
        }

        [Test]
        public void ShortArms_KeepADrivableMiddle()
        {
            var layout = RoadLayout.Build(TestRoads.Crossing(TestRoads.FourLane, armLength: 10f), TestRoads.Settings);

            foreach (var segment in layout.Segments.Values)
                Assert.That(segment.Trimmed.Length, Is.GreaterThanOrEqualTo(TestRoads.Settings.MinSegmentLength - Tolerance));
        }
    }
}
