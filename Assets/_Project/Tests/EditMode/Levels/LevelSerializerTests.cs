using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Levels;
using TrafficGame.Core.Roads;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Levels
{
    public sealed class LevelSerializerTests
    {
        private const string ValidLevel = @"{
            ""schemaVersion"": 1,
            ""name"": ""Test"",
            ""nodes"": [ { ""id"": 10, ""x"": 0, ""y"": 0 }, { ""id"": 20, ""x"": 100, ""y"": 0 }, { ""id"": 30, ""x"": 100, ""y"": 100 } ],
            ""segments"": [
                { ""start"": 10, ""end"": 20, ""profile"": ""two_lane"" },
                { ""start"": 20, ""end"": 30, ""profile"": ""four_lane"", ""control1"": [150, 0], ""control2"": [150, 50] }
            ]
        }";

        private static readonly RoadProfileCatalog Profiles = new(new[] { TestRoads.TwoLane, TestRoads.FourLane });

        [Test]
        public void CreateNetwork_BuildsNodesAndSegments()
        {
            var network = LevelSerializer.CreateNetwork(LevelSerializer.Deserialize(ValidLevel), Profiles);

            Assert.That(network.Nodes.Count, Is.EqualTo(3));
            var curved = network.Segments.Single(s => s.Profile == TestRoads.FourLane);
            Assert.That(curved.Curve.P1, Is.EqualTo(new float2(150f, 0f)));
            Assert.That(curved.Curve.P3, Is.EqualTo(new float2(100f, 100f)));
        }

        [Test]
        public void CreateNetwork_PutsTrafficLightsWhereTheLevelHasThem()
        {
            var data = LevelSerializer.Deserialize(ValidLevel);
            data.Nodes[1].Lights = true;
            data.Nodes[1].ProtectedLeft = true;

            var network = LevelSerializer.CreateNetwork(LevelSerializer.Deserialize(LevelSerializer.Serialize(data)), Profiles);

            var settings = network.Nodes.Select(node => network.GetJunctionSettings(node.Id)).ToArray();
            Assert.That(settings.Count(s => s.Control == JunctionControl.TrafficLights && s.ProtectedLeft), Is.EqualTo(1));
            Assert.That(settings.Count(s => s.IsDefault), Is.EqualTo(2));
        }

        [Test]
        public void SerializeThenDeserialize_PreservesContent()
        {
            var original = LevelSerializer.Deserialize(ValidLevel);

            var copy = LevelSerializer.Deserialize(LevelSerializer.Serialize(original));

            Assert.That(copy.Name, Is.EqualTo("Test"));
            Assert.That(copy.Nodes.Select(n => n.Id), Is.EqualTo(new[] { 10, 20, 30 }));
            Assert.That(copy.Segments[0].Control1, Is.Null);
            Assert.That(copy.Segments[1].Control2, Is.EqualTo(new[] { 150f, 50f }));
        }

        [Test]
        public void Deserialize_RejectsUnknownSchemaVersion()
        {
            Assert.Throws<LevelFormatException>(() => LevelSerializer.Deserialize(ValidLevel.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99")));
        }

        [Test]
        public void Deserialize_RejectsMalformedJson()
        {
            Assert.Throws<LevelFormatException>(() => LevelSerializer.Deserialize("{ not json"));
        }

        [Test]
        public void CreateNetwork_RejectsUnknownProfile()
        {
            var level = LevelSerializer.Deserialize(ValidLevel.Replace("\"four_lane\"", "\"six_lane\""));

            Assert.Throws<LevelFormatException>(() => LevelSerializer.CreateNetwork(level, Profiles));
        }

        [Test]
        public void CreateNetwork_RejectsMissingNode()
        {
            var level = LevelSerializer.Deserialize(ValidLevel.Replace("\"end\": 30", "\"end\": 31"));

            Assert.Throws<LevelFormatException>(() => LevelSerializer.CreateNetwork(level, Profiles));
        }
    }
}
