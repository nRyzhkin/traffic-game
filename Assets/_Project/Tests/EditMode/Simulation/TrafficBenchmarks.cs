using System.Text;
using NUnit.Framework;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Tests.Construction;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Simulation
{
    /// <summary>
    /// Throughput measurements for tuning; not run by default.
    /// Run with: unity command run_tests --mode EditMode --filter TrafficBenchmarks --include_explicit true
    /// </summary>
    [Explicit, Category("Benchmark")]
    public sealed class TrafficBenchmarks
    {
        [Test]
        public void SingleLaneRoundaboutThroughput()
        {
            var report = new StringBuilder();
            foreach (var (gap, spawn, seed) in new[] { (4f, 5f, 1), (4f, 5f, 2), (3f, 5f, 1), (3f, 5f, 2), (4f, 2f, 1), (4f, 10f, 1), (4f, 10f, 2) })
            {
                var bed = new SimulationTestBed(Roundabout(), new TrafficSettings { CriticalGap = gap, RandomSeed = seed }, spawn);
                bed.Run(300f, checkCollisions: false);
                var s = bed.Simulation.Statistics;
                report.AppendLine($"gap {gap}s, {spawn}/min/entry, seed {seed}: spawned {s.Spawned}, completed {s.CompletedTrips} " +
                                  $"({s.CompletedTrips * 12} veh/h), avg trip {s.AverageTripSeconds:0.0}s, in flight {bed.Simulation.VehicleCount}, stuck {s.RemovedStuck}");
            }

            Assert.Pass(report.ToString());
        }

        [Test]
        public void StepCostWithThousandsOfVehicles()
        {
            var report = new StringBuilder();
            var bed = new SimulationTestBed(Grid(10, 120f), new TrafficSettings { MaxVehicles = 3000 }, edgeTripsPerMinute: 40f);
            foreach (var target in new[] { 1000, 2000, 3000 })
            {
                while (bed.Simulation.VehicleCount < target && bed.Simulation.Time < 1800f)
                    bed.Run(5f, checkCollisions: false);

                var watch = System.Diagnostics.Stopwatch.StartNew();
                const int steps = 100;
                bed.Run(steps * SimulationTestBed.TickSeconds, checkCollisions: false);
                watch.Stop();
                var stepMs = watch.Elapsed.TotalMilliseconds / steps;
                // 20 steps per simulated second; at 60 fps that is 1/3 step per frame at x1 and 4/3 at x4.
                report.AppendLine($"{bed.Simulation.VehicleCount} vehicles: {stepMs:0.00} ms per step, " +
                                  $"{stepMs / 3:0.00} ms per frame at x1, {stepMs * 4 / 3:0.00} ms per frame at x4");
            }

            Assert.Pass(report.ToString());
        }

        /// <summary>A square grid of two-lane streets; every street ends in a map-edge entry/exit.</summary>
        private static RoadNetwork Grid(int size, float spacing)
        {
            var network = new RoadNetwork();
            var nodes = new NodeId[size + 2, size + 2];
            var offset = (size + 1) * spacing * 0.5f;
            for (var x = 0; x < size + 2; x++)
            for (var y = 0; y < size + 2; y++)
            {
                var corner = (x == 0 || x == size + 1) && (y == 0 || y == size + 1);
                if (!corner)
                    nodes[x, y] = network.AddNode(new float2(x * spacing - offset, y * spacing - offset));
            }

            for (var x = 0; x < size + 2; x++)
            for (var y = 0; y < size + 2; y++)
            {
                var inside = x >= 1 && x <= size && y >= 1 && y <= size;
                if (x + 1 < size + 2 && y >= 1 && y <= size && (inside || x + 1 <= size))
                    network.AddStraightSegment(nodes[x, y], nodes[x + 1, y], TestRoads.TwoLane);
                if (y + 1 < size + 2 && x >= 1 && x <= size && (inside || y + 1 <= size))
                    network.AddStraightSegment(nodes[x, y], nodes[x, y + 1], TestRoads.TwoLane);
            }

            return network;
        }

        private static RoadNetwork Roundabout()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane, armLength: 150f);
            var construction = new ConstructionTestBed(network);
            var ring = new RoadProfile("ring", 1, 0, TestRoads.LaneWidth, TestRoads.ShoulderWidth, TestRoads.CostPerMeter, 9f);
            var plan = new RoundaboutPlanner(ConstructionTestBed.Rules).Plan(float2.zero, 22f, ring, construction.Layout, construction.Budget);
            new BuildRoundaboutCommand(network, construction.Budget, plan).Execute();
            return network;
        }
    }
}
