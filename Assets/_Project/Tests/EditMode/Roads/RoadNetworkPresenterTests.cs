using NUnit.Framework;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Roads
{
    public sealed class RoadNetworkPresenterTests
    {
        [Test]
        public void RendersOnCreationAndAfterEveryChange()
        {
            var network = TestRoads.SingleSegment(TestRoads.TwoLane);
            var roadView = new FakeRoadView();
            var laneView = new FakeLaneView();

            using var presenter = new RoadNetworkPresenter(network, TestRoads.Settings, roadView, laneView);
            Assert.That(roadView.RenderCount, Is.EqualTo(1));
            Assert.That(laneView.Last.Lanes.Count, Is.EqualTo(2));

            network.AddNode(new float2(500f, 500f));

            Assert.That(roadView.RenderCount, Is.EqualTo(2));
            Assert.That(roadView.Last, Is.SameAs(presenter.Layout));
        }

        [Test]
        public void Dispose_StopsRendering()
        {
            var network = TestRoads.SingleSegment(TestRoads.TwoLane);
            var roadView = new FakeRoadView();
            var presenter = new RoadNetworkPresenter(network, TestRoads.Settings, roadView, new FakeLaneView());

            presenter.Dispose();
            network.AddNode(float2.zero);

            Assert.That(roadView.RenderCount, Is.EqualTo(1));
        }

        private sealed class FakeRoadView : IRoadNetworkView
        {
            public int RenderCount { get; private set; }
            public RoadLayout Last { get; private set; }

            public void Render(RoadLayout layout)
            {
                RenderCount++;
                Last = layout;
            }
        }

        private sealed class FakeLaneView : ILaneGraphView
        {
            public LaneGraph Last { get; private set; }

            public void Render(LaneGraph graph) => Last = graph;
        }
    }
}
