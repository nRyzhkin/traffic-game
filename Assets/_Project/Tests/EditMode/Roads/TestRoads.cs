using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Roads
{
    /// <summary>Small road networks shared by tests.</summary>
    internal static class TestRoads
    {
        public const float LaneWidth = 3.5f;
        public const float ShoulderWidth = 0.5f;
        public const float JunctionPadding = 4f;
        public const float MinJunctionTrim = 0.5f;
        public const float CostPerMeter = 10f;
        public const float SpeedLimit = 15f;

        public static readonly RoadProfile TwoLane = new("two_lane", 1, 1, LaneWidth, ShoulderWidth, CostPerMeter, SpeedLimit);
        public static readonly RoadProfile FourLane = new("four_lane", 2, 2, LaneWidth, ShoulderWidth, CostPerMeter, SpeedLimit);
        public static readonly RoadProfile OneWay = new("one_way", 2, 0, LaneWidth, ShoulderWidth, CostPerMeter, SpeedLimit);

        public static readonly RoadGeometrySettings Settings = new(JunctionPadding, MinJunctionTrim, maxJunctionTrim: 60f, sampleSpacing: 2f);

        public static readonly float2[] CompassDirections =
        {
            new(1f, 0f), new(0f, 1f), new(-1f, 0f), new(0f, -1f)
        };

        /// <summary>A crossing at the origin; every arm starts at the center and ends in a dead end.</summary>
        public static RoadNetwork Crossing(RoadProfile profile, float armLength = 100f)
        {
            var network = new RoadNetwork();
            var center = network.AddNode(float2.zero);
            foreach (var direction in CompassDirections)
                network.AddStraightSegment(center, network.AddNode(direction * armLength), profile);
            return network;
        }

        /// <summary>One straight road along +X from (-100, 0) to (100, 0), split at the origin.</summary>
        public static RoadNetwork StraightThroughNode(RoadProfile profile)
        {
            var network = new RoadNetwork();
            var west = network.AddNode(new float2(-100f, 0f));
            var middle = network.AddNode(float2.zero);
            var east = network.AddNode(new float2(100f, 0f));
            network.AddStraightSegment(west, middle, profile);
            network.AddStraightSegment(middle, east, profile);
            return network;
        }

        public static RoadNetwork SingleSegment(RoadProfile profile)
        {
            var network = new RoadNetwork();
            network.AddStraightSegment(network.AddNode(float2.zero), network.AddNode(new float2(100f, 0f)), profile);
            return network;
        }
    }
}
