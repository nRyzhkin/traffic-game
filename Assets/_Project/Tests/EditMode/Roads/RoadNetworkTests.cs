using System;
using NUnit.Framework;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Roads
{
    public sealed class RoadNetworkTests
    {
        [Test]
        public void AddSegment_AttachesToBothNodes()
        {
            var network = new RoadNetwork();
            var a = network.AddNode(float2.zero);
            var b = network.AddNode(new float2(50f, 0f));

            var segment = network.AddStraightSegment(a, b, TestRoads.TwoLane);

            Assert.That(network.GetNode(a).Segments, Is.EquivalentTo(new[] { segment }));
            Assert.That(network.GetNode(b).Segments, Is.EquivalentTo(new[] { segment }));
            Assert.That(network.GetSegment(segment).Curve.P3, Is.EqualTo(new float2(50f, 0f)));
        }

        [Test]
        public void AddSegment_RejectsLoopOnOneNode()
        {
            var network = new RoadNetwork();
            var node = network.AddNode(float2.zero);

            Assert.Throws<ArgumentException>(() => network.AddStraightSegment(node, node, TestRoads.TwoLane));
        }

        [Test]
        public void RemoveSegment_RemovesNodesLeftWithoutSegments()
        {
            var network = TestRoads.StraightThroughNode(TestRoads.TwoLane);
            var westSegment = network.GetNode(new NodeId(1)).Segments[0];

            network.RemoveSegment(westSegment);

            Assert.That(network.Segments.Count, Is.EqualTo(1));
            Assert.That(network.Nodes.Count, Is.EqualTo(2));
        }

        [Test]
        public void Changes_RaiseChangedEvent()
        {
            var network = new RoadNetwork();
            var raised = 0;
            network.Changed += () => raised++;

            var a = network.AddNode(float2.zero);
            var b = network.AddNode(new float2(10f, 0f));
            network.RemoveSegment(network.AddStraightSegment(a, b, TestRoads.TwoLane));

            Assert.That(raised, Is.EqualTo(4));
        }
    }
}
