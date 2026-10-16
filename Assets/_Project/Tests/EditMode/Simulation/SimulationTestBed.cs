using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Simulation
{
    /// <summary>Runs a traffic simulation on a test network and checks physical invariants while it runs.</summary>
    internal sealed class SimulationTestBed
    {
        public const float TickSeconds = 0.05f;

        /// <param name="edgeTripsPerMinute">When positive, every dead end sends trips to all other dead ends at this rate.</param>
        public SimulationTestBed(RoadNetwork network, TrafficSettings settings = null, float edgeTripsPerMinute = 0f)
        {
            Network = network;
            Settings = settings ?? new TrafficSettings();
            Simulation = new TrafficSimulation(BuildTrafficNetwork(), Settings);

            if (edgeTripsPerMinute > 0f)
            {
                var traffic = Simulation.Network;
                var sources = traffic.Entries.Select(entry => new TripSource(entry, edgeTripsPerMinute, traffic.Exits)).ToArray();
                Trips = new TripGenerator(sources, Settings.TruckShare, Settings.RandomSeed);
            }
        }

        public RoadNetwork Network { get; }
        public TrafficSettings Settings { get; }
        public TrafficSimulation Simulation { get; }
        public TripGenerator Trips { get; }

        public TrafficNetwork BuildTrafficNetwork() =>
            TrafficNetwork.Build(Network, RoadLayout.Build(Network, TestRoads.Settings), Settings);

        /// <summary>Advances the simulation, failing the test if two vehicles ever overlap.</summary>
        public void Run(float seconds, bool checkCollisions = true)
        {
            var steps = (int)math.ceil(seconds / TickSeconds);
            for (var i = 0; i < steps; i++)
            {
                Trips?.Update(Simulation, TickSeconds);
                Simulation.Step(TickSeconds);
                if (checkCollisions && i % 4 == 0)
                    AssertNoOverlaps();
            }
        }

        /// <summary>Vehicle centers closer than a car's half length (lane neighbours are 3.5 m apart) mean a crash.</summary>
        public void AssertNoOverlaps()
        {
            var positions = Enumerable.Range(0, Simulation.VehicleCount)
                .Select(index => Simulation.GetVehicle(index, 0f).Position)
                .ToArray();

            for (var i = 0; i < positions.Length; i++)
            for (var j = i + 1; j < positions.Length; j++)
            {
                if (math.distance(positions[i], positions[j]) < 2f)
                    Assert.Fail($"Vehicles {i} and {j} overlap at {positions[i]} after {Simulation.Time:0.0}s: {Describe(i)} / {Describe(j)}.");
            }
        }

        private string Describe(int index)
        {
            var vehicle = Simulation.Vehicles[index];
            var link = vehicle.Link;
            var kind = link.Kind == LinkKind.Connection ? $"{link.Turn} connection" : $"lane {link.LaneIndex}";
            return $"{kind} #{link.Id} at {vehicle.Distance:0.0}/{link.Length:0.0} m, {vehicle.Speed:0.0} m/s";
        }

        /// <summary>A crossing of two roads with every arm ending at a map edge.</summary>
        public static RoadNetwork Crossing(RoadProfile northSouth, RoadProfile eastWest, float armLength = 150f)
        {
            var network = new RoadNetwork();
            var center = network.AddNode(float2.zero);
            network.AddStraightSegment(center, network.AddNode(new float2(armLength, 0f)), eastWest);
            network.AddStraightSegment(center, network.AddNode(new float2(-armLength, 0f)), eastWest);
            network.AddStraightSegment(center, network.AddNode(new float2(0f, armLength)), northSouth);
            network.AddStraightSegment(center, network.AddNode(new float2(0f, -armLength)), northSouth);
            return network;
        }

        /// <summary>An east–west through road with a side road joining from the south.</summary>
        public static RoadNetwork TJunction()
        {
            var network = new RoadNetwork();
            var center = network.AddNode(float2.zero);
            network.AddStraightSegment(network.AddNode(new float2(-150f, 0f)), center, TestRoads.TwoLane);
            network.AddStraightSegment(center, network.AddNode(new float2(150f, 0f)), TestRoads.TwoLane);
            network.AddStraightSegment(network.AddNode(new float2(0f, -150f)), center, TestRoads.TwoLane);
            return network;
        }

        public NodeId DeadEndAt(float2 position) =>
            Network.Nodes.Single(node => node.Degree == 1 && math.distance(node.Position, position) < 1f).Id;
    }
}
