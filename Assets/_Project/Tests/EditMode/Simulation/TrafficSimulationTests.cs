using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Tests.Construction;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Simulation
{
    public sealed class TrafficSimulationTests
    {
        [Test]
        public void SingleVehicle_DrivesToItsExitAtAboutTheSpeedLimit()
        {
            var bed = new SimulationTestBed(TestRoads.SingleSegment(TestRoads.TwoLane));

            Assert.That(bed.Simulation.TrySpawn(new NodeId(1), new NodeId(2), VehicleKind.Car), Is.True);
            bed.Run(30f);

            Assert.That(bed.Simulation.Statistics.CompletedTrips, Is.EqualTo(1));
            Assert.That(bed.Simulation.VehicleCount, Is.Zero);
            // 100 m at 15 m/s plus accelerating from half speed.
            Assert.That(bed.Simulation.Statistics.AverageTripSeconds, Is.InRange(6f, 12f));
        }

        [Test]
        public void TurningThroughAJunction_IsDrawnWithoutJumpsOrLurches()
        {
            var bed = new SimulationTestBed(SimulationTestBed.TJunction());
            var west = bed.DeadEndAt(new float2(-150f, 0f));
            var south = bed.DeadEndAt(new float2(0f, -150f));
            Assert.That(bed.Simulation.TrySpawn(south, west, VehicleKind.Car), Is.True, "Side road, stop, left turn onto the through road.");

            var simulation = bed.Simulation;
            var previous = simulation.GetVehicle(0, 0f);
            var previousAcceleration = 0f;
            var maxSpeed = 0f;
            while (simulation.VehicleCount > 0 && simulation.Time < 60f)
            {
                simulation.Step(SimulationTestBed.TickSeconds);
                if (simulation.VehicleCount == 0)
                    break;

                var vehicle = simulation.Vehicles[0];
                maxSpeed = math.max(maxSpeed, vehicle.Speed);
                // Braking may release at once; speeding up must build gradually.
                Assert.That(vehicle.Acceleration - math.max(previousAcceleration, 0f), Is.LessThanOrEqualTo(4f * SimulationTestBed.TickSeconds + 1e-3f),
                    $"Acceleration jumped at {simulation.Time:0.00}s.");
                previousAcceleration = vehicle.Acceleration;

                for (var alpha = 0.25f; alpha <= 1f; alpha += 0.25f)
                {
                    var sample = simulation.GetVehicle(0, alpha);
                    var moved = math.distance(previous.Position, sample.Position);
                    var turned = math.degrees(math.acos(math.clamp(math.dot(previous.Direction, sample.Direction), -1f, 1f)));
                    Assert.That(moved, Is.LessThan(maxSpeed * SimulationTestBed.TickSeconds * 0.25f + 0.05f), $"Position jumped at {simulation.Time:0.00}s.");
                    Assert.That(turned, Is.LessThan(6f), $"Heading jumped at {simulation.Time:0.00}s.");
                    previous = sample;
                }
            }

            Assert.That(simulation.Statistics.CompletedTrips, Is.EqualTo(1));
        }

        [Test]
        public void Queue_BehindAStopSign_KeepsGapsAndAllVehiclesStop()
        {
            var bed = new SimulationTestBed(SimulationTestBed.TJunction());
            var south = bed.DeadEndAt(new float2(0f, -150f));
            var east = bed.DeadEndAt(new float2(150f, 0f));

            for (var i = 0; i < 6; i++)
            {
                bed.Simulation.TrySpawn(south, east, VehicleKind.Car);
                bed.Run(1.5f);
            }

            var sawStop = false;
            for (var t = 0; t < 400 && bed.Simulation.VehicleCount > 0; t++)
            {
                bed.Run(0.25f);
                sawStop |= bed.Simulation.Vehicles.Any(vehicle => vehicle.StoppedAt >= 0f);
            }

            Assert.That(sawStop, Is.True, "Side road traffic must come to a full stop.");
            Assert.That(bed.Simulation.Statistics.CompletedTrips, Is.EqualTo(6));
        }

        [TestCase(false, 1, TestName = "AllWayStopCrossing_FlowsWithoutCrashOrGridlock(seed 1)")]
        [TestCase(false, 2, TestName = "AllWayStopCrossing_FlowsWithoutCrashOrGridlock(seed 2)")]
        [TestCase(false, 3, TestName = "AllWayStopCrossing_FlowsWithoutCrashOrGridlock(seed 3)")]
        [TestCase(true, 1, TestName = "AvenueCrossing_FlowsWithoutCrashOrGridlock(seed 1)")]
        [TestCase(true, 2, TestName = "AvenueCrossing_FlowsWithoutCrashOrGridlock(seed 2)")]
        [TestCase(true, 3, TestName = "AvenueCrossing_FlowsWithoutCrashOrGridlock(seed 3)")]
        public void BusyCrossing_FlowsWithoutCrashesOrGridlock(bool avenue, int seed)
        {
            var bed = new SimulationTestBed(SimulationTestBed.Crossing(TestRoads.TwoLane, avenue ? TestRoads.FourLane : TestRoads.TwoLane),
                new TrafficSettings { RandomSeed = seed }, edgeTripsPerMinute: 10f);

            bed.Run(240f);

            Assert.That(bed.Simulation.Statistics.CompletedTrips, Is.GreaterThan(60));
            Assert.That(bed.Simulation.Statistics.RemovedStuck, Is.Zero);
        }

        [TestCase(false, false, 1, TestName = "SignalizedCrossing_FlowsWithoutRedLightRunning(streets, seed 1)")]
        [TestCase(true, false, 2, TestName = "SignalizedCrossing_FlowsWithoutRedLightRunning(avenue, seed 2)")]
        [TestCase(true, true, 3, TestName = "SignalizedCrossing_FlowsWithoutRedLightRunning(avenue, protected left, seed 3)")]
        public void SignalizedCrossing_FlowsWithoutRedLightRunning(bool avenue, bool protectedLeft, int seed)
        {
            var network = JunctionRulesTests.WithLights(
                SimulationTestBed.Crossing(TestRoads.TwoLane, avenue ? TestRoads.FourLane : TestRoads.TwoLane), protectedLeft);
            var bed = new SimulationTestBed(network, new TrafficSettings { RandomSeed = seed }, edgeTripsPerMinute: 10f);
            var simulation = bed.Simulation;
            var signal = simulation.Network.Signals.Single();
            var links = new Dictionary<int, TrafficLink>();
            var sawRightOnRed = false;

            for (var step = 0; step < 240f / SimulationTestBed.TickSeconds; step++)
            {
                bed.Run(SimulationTestBed.TickSeconds, checkCollisions: step % 4 == 0);
                foreach (var vehicle in simulation.Vehicles)
                {
                    var link = vehicle.Link;
                    if (links.TryGetValue(vehicle.Id, out var previous) && previous != link && link.Kind == LinkKind.Connection && link.Signal == signal &&
                        signal.StateOf(link) == SignalState.Red && signal.Interval == SignalInterval.Green)
                    {
                        Assert.That(link.Turn, Is.EqualTo(TurnDirection.Right), $"Vehicle {vehicle.Id} ran a red light at {simulation.Time:0.0}s.");
                        sawRightOnRed = true;
                    }

                    links[vehicle.Id] = link;
                }
            }

            Assert.That(simulation.Statistics.CompletedTrips, Is.GreaterThan(60));
            Assert.That(simulation.Statistics.RemovedStuck, Is.Zero);
            Assert.That(sawRightOnRed, Is.True, "Right turns go on red after stopping.");
        }

        [Test]
        public void LoneVehicle_GetsItsGreenWithoutWaitingForAFullCycle()
        {
            var bed = new SimulationTestBed(JunctionRulesTests.WithLights(SimulationTestBed.TJunction(), protectedLeft: false));
            var south = bed.DeadEndAt(new float2(0f, -150f));
            var west = bed.DeadEndAt(new float2(-150f, 0f));
            Assert.That(bed.Simulation.TrySpawn(south, west, VehicleKind.Car), Is.True);

            bed.Run(60f);

            Assert.That(bed.Simulation.Statistics.CompletedTrips, Is.EqualTo(1));
            Assert.That(bed.Simulation.Statistics.AverageTripSeconds,
                Is.LessThan(35f + TrafficSignal.MinGreen + TrafficSignal.YellowTime + TrafficSignal.AllRedTime));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Roundabout_FlowsWithoutCrashesOrGridlock(int seed)
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane, armLength: 150f);
            var construction = new ConstructionTestBed(network);
            var ring = new RoadProfile("ring", 1, 0, TestRoads.LaneWidth, TestRoads.ShoulderWidth, TestRoads.CostPerMeter, 9f);
            var plan = new RoundaboutPlanner(ConstructionTestBed.Rules).Plan(float2.zero, 22f, ring, construction.Layout, construction.Budget);
            new BuildRoundaboutCommand(network, construction.Budget, plan).Execute();
            // 4 entries × 5 vehicles/min = 1200 vehicles/h, within a single-lane roundabout's capacity (HCM: ~1200–1400).
            var bed = new SimulationTestBed(network, new TrafficSettings { RandomSeed = seed }, edgeTripsPerMinute: 5f);

            bed.Run(300f);

            var statistics = bed.Simulation.Statistics;
            Assert.That(statistics.CompletedTrips, Is.GreaterThan(statistics.Spawned * 0.8f), "Demand below capacity must be served.");
            Assert.That(statistics.RemovedStuck, Is.Zero);
        }

        [Test]
        public void Router_AvoidsACongestedRoad()
        {
            // Two equal routes from west to east: through the north arc or through the south arc.
            var network = new RoadNetwork();
            var west = network.AddNode(new float2(-200f, 0f));
            var a = network.AddNode(new float2(-100f, 0f));
            var b = network.AddNode(new float2(100f, 0f));
            var east = network.AddNode(new float2(200f, 0f));
            network.AddStraightSegment(west, a, TestRoads.TwoLane);
            var north = network.AddSegment(a, b, new float2(-60f, 80f), new float2(60f, 80f), TestRoads.TwoLane);
            network.AddSegment(a, b, new float2(-60f, -80f), new float2(60f, -80f), TestRoads.TwoLane);
            network.AddStraightSegment(b, east, TestRoads.TwoLane);
            var bed = new SimulationTestBed(network);
            var traffic = bed.Simulation.Network;
            var router = new Router(traffic);
            var route = new System.Collections.Generic.List<RoadInfo>();
            var start = traffic.Roads.Single(road => road.From == west);

            traffic.Roads.Single(road => road.Direction.Segment == north && road.Direction.Forward).ObservedSpeed = 1f;
            Assert.That(router.TryFindRoute(start, east, route), Is.True);

            Assert.That(route.Any(road => road.Direction.Segment == north), Is.False);
        }

        [Test]
        public void EditingRoads_KeepsVehiclesOnRoadsThatSurvive()
        {
            var bed = new SimulationTestBed(SimulationTestBed.Crossing(TestRoads.TwoLane, TestRoads.TwoLane), edgeTripsPerMinute: 8f);
            bed.Run(30f);
            var before = bed.Simulation.VehicleCount;
            Assert.That(before, Is.GreaterThan(0));

            bed.Network.AddStraightSegment(bed.Network.AddNode(new float2(400f, 400f)), bed.Network.AddNode(new float2(500f, 400f)), TestRoads.TwoLane);
            bed.Simulation.Rebuild(bed.BuildTrafficNetwork());

            Assert.That(bed.Simulation.VehicleCount, Is.EqualTo(before));
            bed.Run(30f);
        }
    }
}
