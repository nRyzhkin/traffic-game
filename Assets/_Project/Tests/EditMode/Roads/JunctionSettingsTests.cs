using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Roads
{
    public sealed class JunctionSettingsTests
    {
        private static readonly NodeId Center = new(1);

        [Test]
        public void Snapshot_RestoresJunctionSettings()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var snapshot = network.CreateSnapshot();
            network.SetJunctionSettings(Center, JunctionSettings.Default.WithControl(JunctionControl.TrafficLights, protectedLeft: true));
            var withLights = network.CreateSnapshot();

            network.Restore(snapshot);
            Assert.That(network.GetJunctionSettings(Center).IsDefault, Is.True);

            network.Restore(withLights);
            Assert.That(network.GetJunctionSettings(Center).Control, Is.EqualTo(JunctionControl.TrafficLights));
            Assert.That(network.GetJunctionSettings(Center).ProtectedLeft, Is.True);
        }

        [Test]
        public void SplittingAnArrivingSegment_KeepsItsLaneArrows()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var east = network.Segments.Single(segment => network.GetNode(segment.End).Position.x > 50f).Id;
            network.SetJunctionSettings(Center, JunctionSettings.Default.WithArrows(east, 0, LaneArrows.Right));

            network.SplitSegment(east, 0.5f, out var first, out _);

            Assert.That(network.GetJunctionSettings(Center).ArrowsOf(first, 0), Is.EqualTo(LaneArrows.Right));
            Assert.That(network.GetJunctionSettings(Center).Arrows.Count(), Is.EqualTo(1));
        }

        [Test]
        public void RemovingASegment_DropsItsLaneArrows()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var east = network.Segments.Single(segment => network.GetNode(segment.End).Position.x > 50f).Id;
            network.SetJunctionSettings(Center, JunctionSettings.Default.WithArrows(east, 0, LaneArrows.Right));

            network.RemoveSegment(east);

            Assert.That(network.GetJunctionSettings(Center).IsDefault, Is.True);
        }

        [Test]
        public void ProtectedLeft_NeedsTrafficLights()
        {
            var settings = JunctionSettings.Default.WithControl(JunctionControl.Auto, protectedLeft: true);

            Assert.That(settings.ProtectedLeft, Is.False);
            Assert.That(settings.IsDefault, Is.True);
        }

        [Test]
        public void SettingsChange_NotifiesTheNetworkListeners()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var changes = 0;
            network.Changed += () => changes++;

            network.SetJunctionSettings(Center, JunctionSettings.Default.WithControl(JunctionControl.TrafficLights, false));

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(math.all(network.GetNode(Center).Position == float2.zero));
        }
    }
}
