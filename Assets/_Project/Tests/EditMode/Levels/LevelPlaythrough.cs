using System.IO;
using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Levels;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using Unity.Mathematics;

namespace TrafficGame.Tests.Levels
{
    /// <summary>
    /// Plays a campaign level headlessly: builds roads like the player would (snapping to positions) and runs the
    /// traffic until the goal is reached. Mirrors the game's settings so results can be used to tune star limits.
    /// </summary>
    internal sealed class LevelPlaythrough
    {
        public const string CampaignFolder = "Assets/_Project/Levels/Campaign";
        public const float TickSeconds = 0.05f;

        // Same profiles as GameConfig (prices in $/m, speed limits converted from mph).
        public static readonly RoadProfileCatalog Profiles = new(new[]
        {
            new RoadProfile("two_lane", 1, 1, 3.5f, 0.5f, 4f, 35f * 0.44704f),
            new RoadProfile("four_lane", 2, 2, 3.5f, 0.5f, 8f, 45f * 0.44704f),
            new RoadProfile("one_way_two_lane", 2, 0, 3.5f, 0.5f, 6f, 30f * 0.44704f),
            new RoadProfile("one_way_one_lane", 1, 0, 3.5f, 0.5f, 3f, 20f * 0.44704f)
        });

        public static readonly RoadGeometrySettings Geometry = new(4f, 0.5f, 60f, 2f);
        private static readonly RoadPlanningRules Rules = new(8f, 12f, 30f, 25f, 12f, 2f, new float2(-1000f), new float2(1000f));

        private readonly RoadSnapper _snapper = new(3f, 12f);
        private readonly RoadPlanner _planner;
        private readonly RoadNetworkSnapshot _initial;

        public LevelPlaythrough(string levelFile)
        {
            var path = Path.Combine(CampaignFolder, levelFile);
            Level = LevelSerializer.CreateLevel(Path.GetFileNameWithoutExtension(path), LevelSerializer.Deserialize(File.ReadAllText(path)), Profiles);
            Budget = new Budget(Level.Budget);
            Progress = new LevelProgress(Level.Objective, Budget);
            _planner = new RoadPlanner(Level.Network, Rules);
            _initial = Level.Network.CreateSnapshot();
        }

        public Level Level { get; }
        public Budget Budget { get; }

        /// <summary>Created before anything is built, so spending is measured like in the game.</summary>
        public LevelProgress Progress { get; }
        public RoadLayout Layout => RoadLayout.Build(Level.Network, Geometry);

        /// <summary>Builds a road between two points the way the road tool does; fails the test if it is not allowed.</summary>
        public void Road(float2 from, float2 to, string profile = "two_lane") => Road(from, 0, to, 0, profile);

        /// <summary>A road whose ends are built on the given elevation levels, as with the height stepper.</summary>
        public void Road(float2 from, int fromLevel, float2 to, int toLevel, string profile = "two_lane")
        {
            var layout = Layout;
            Profiles.TryGet(profile, out var roadProfile);
            var request = new RoadBuildRequest(_snapper.Snap(from, Level.Network, layout, RoadElevation.HeightOf(fromLevel)),
                _snapper.Snap(to, Level.Network, layout, RoadElevation.HeightOf(toLevel)), null, roadProfile);
            var plan = _planner.Plan(request, layout, Budget);
            Assert.That(plan.Error, Is.EqualTo(BuildError.None), $"Road {from} → {to}");
            new BuildRoadCommand(Level.Network, Budget, plan).Execute();
        }

        public void Roundabout(float2 center, float radius = 22f)
        {
            Profiles.TryGet("one_way_one_lane", out var ring);
            var plan = new RoundaboutPlanner(Rules).Plan(center, radius, ring, Layout, Budget);
            Assert.That(plan.Error, Is.EqualTo(BuildError.None), $"Roundabout at {center}");
            new BuildRoundaboutCommand(Level.Network, Budget, plan).Execute();
        }

        /// <summary>Runs traffic until the goal is reached or the time runs out; returns the progress.</summary>
        /// <summary>Puts traffic lights on the junction nearest to the point, paying what the game charges for them.</summary>
        public void TrafficLights(float2 at, bool protectedLeft = false, long cost = 400)
        {
            var node = Level.Network.Nodes.Where(n => n.Degree >= 3).OrderBy(n => math.distance(n.Position, at)).First();
            var settings = JunctionSettings.Default.WithControl(JunctionControl.TrafficLights, protectedLeft);
            new ChangeJunctionCommand(Level.Network, Budget, node.Id, settings, cost).Execute();
        }

        public LevelProgress Play(float maxSeconds = 900f, int seed = 1)
        {
            var settings = new TrafficSettings { RandomSeed = seed };
            var trips = new TripGenerator(Level.CreateTripSources(), settings.TruckShare, seed);

            // As in the game: the warm-up runs on the roads the level starts with, the player's changes come after it.
            var solution = Level.Network.CreateSnapshot();
            Level.Network.Restore(_initial);
            var simulation = new TrafficSimulation(TrafficNetwork.Build(Level.Network, Layout, settings), settings);
            TrafficWarmup.Run(simulation, trips, Level.WarmupSeconds, TickSeconds);
            Level.Network.Restore(solution);
            simulation.Rebuild(TrafficNetwork.Build(Level.Network, Layout, settings));

            var progress = Progress;
            var doors = Level.DestinationDoors.ToHashSet();
            simulation.TripCompleted += (_, destination, seconds) =>
            {
                if (doors.Contains(destination))
                    progress.RecordTrip(seconds);
            };

            for (var time = 0f; time < maxSeconds && !progress.IsComplete; time += TickSeconds)
            {
                trips.Update(simulation, TickSeconds);
                simulation.Step(TickSeconds);
            }

            PlayedSeconds = simulation.Time;
            Bottlenecks = string.Join("; ", simulation.Network.Roads
                .OrderBy(road => road.ObservedSpeed / road.SpeedLimit)
                .Take(4)
                .Select(road => $"{Level.Network.GetNode(road.From).Position}→{Level.Network.GetNode(road.To).Position} " +
                                $"{road.ObservedSpeed / road.SpeedLimit:P0} ({road.Lanes[0].Control})"));
            return progress;
        }

        public float PlayedSeconds { get; private set; }

        /// <summary>The slowest roads at the end of <see cref="Play"/>, for diagnosing levels.</summary>
        public string Bottlenecks { get; private set; }
    }
}
