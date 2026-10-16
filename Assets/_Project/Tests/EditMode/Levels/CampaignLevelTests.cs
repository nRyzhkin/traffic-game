using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using Unity.Mathematics;

namespace TrafficGame.Tests.Levels
{
    public sealed class CampaignLevelTests
    {
        private static IEnumerable<string> LevelFiles() =>
            Directory.GetFiles(LevelPlaythrough.CampaignFolder, "*.json").Select(Path.GetFileName).OrderBy(name => name);

        [TestCaseSource(nameof(LevelFiles))]
        public void Level_IsWellFormedAndTranslated(string file)
        {
            var level = new LevelPlaythrough(file).Level;

            Assert.That(level.Objective, Is.Not.Null, "Campaign levels have a goal.");
            Assert.That(level.Buildings.Any(building => building.IsOrigin), "Someone has to travel.");
            Assert.That(level.Buildings.Any(building => building.IsDestination), "Trips need somewhere to go.");
            Assert.That(level.Budget, Is.GreaterThan(0));
            Assert.That(level.Network.Segments.All(segment => segment.IsLocked), "Everything placed by the level is protected.");

            foreach (var language in new[] { "en", "ru" })
            {
                var texts = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText($"Assets/_Project/Localization/{language}.json"));
                Assert.That(texts.ContainsKey(level.TitleKey), $"{language} has no title for {file}.");
                Assert.That(texts.ContainsKey(level.IntroKey), $"{language} has no intro for {file}.");
            }
        }

        [Test]
        public void FirstRoad_IsSolvableWithThreeStarsByTheIntendedLayout()
        {
            var level = new LevelPlaythrough("01_first_road.json");
            level.Road(new float2(-103f, 50f), new float2(-103f, 0f));
            level.Road(new float2(-103f, 0f), new float2(-103f, -50f));
            level.Road(new float2(-103f, 0f), new float2(85f, 0f));

            var progress = level.Play();

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.Stars, Is.EqualTo(3), $"spent {progress.Spent}, average trip {progress.AverageTripSeconds:0.0}s");
        }

        [Test]
        public void RushHour_TrafficLightsEarnThreeStars()
        {
            var level = new LevelPlaythrough("04_rush_hour.json");
            level.TrafficLights(float2.zero);

            var progress = level.Play();

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.Stars, Is.EqualTo(3), $"spent {progress.Spent}, average trip {progress.AverageTripSeconds:0.0}s");
        }

        [Test]
        public void Overpass_AnOverpassEarnsThreeStarsAndACrossingDoesNot()
        {
            var overpass = new LevelPlaythrough("05_overpass.json");
            overpass.Road(new float2(0f, 190f), new float2(0f, 60f));
            overpass.Road(new float2(0f, 60f), 0, new float2(0f, 15f), 1);
            overpass.Road(new float2(0f, 15f), 1, new float2(0f, -15f), 1);
            overpass.Road(new float2(0f, -15f), 1, new float2(0f, -60f), 0);
            overpass.Road(new float2(0f, -60f), new float2(0f, -160f));
            var crossing = new LevelPlaythrough("05_overpass.json");
            crossing.Road(new float2(0f, 190f), new float2(0f, -160f));

            var withOverpass = overpass.Play();
            var withCrossing = crossing.Play();

            Assert.That(withOverpass.Stars, Is.EqualTo(3), $"average trip {withOverpass.AverageTripSeconds:0.0}s");
            Assert.That(withCrossing.Stars, Is.LessThan(3), $"average trip {withCrossing.AverageTripSeconds:0.0}s");
        }

        [Test]
        public void Flyover_AFlyoverOverTheCrossroadsEarnsThreeStars()
        {
            var level = new LevelPlaythrough("06_flyover.json");
            float2 Diagonal(float distance) => new float2(-1f, 1f) * (distance / math.sqrt(2f));
            level.Road(Diagonal(225f), Diagonal(105f));
            level.Road(Diagonal(105f), 0, Diagonal(60f), 1);
            level.Road(Diagonal(60f), 1, Diagonal(-60f), 1);
            level.Road(Diagonal(-60f), 1, Diagonal(-105f), 0);
            level.Road(Diagonal(-105f), Diagonal(-200f));

            var progress = level.Play();

            Assert.That(progress.Stars, Is.EqualTo(3), $"spent {progress.Spent}, average trip {progress.AverageTripSeconds:0.0}s");
        }

        [Test]
        public void RushHour_OpensWithTheJunctionAlreadyJammed()
        {
            var level = new LevelPlaythrough("04_rush_hour.json").Level;
            var settings = new TrafficSettings();
            var layout = RoadLayout.Build(level.Network, LevelPlaythrough.Geometry);
            var simulation = new TrafficSimulation(TrafficNetwork.Build(level.Network, layout, settings), settings);
            var trips = new TripGenerator(level.CreateTripSources(), settings.TruckShare, settings.RandomSeed);

            TrafficWarmup.Run(simulation, trips, level.WarmupSeconds, LevelPlaythrough.TickSeconds);

            var congestion = new List<LaneCongestion>();
            simulation.GetCongestion(congestion);
            Assert.That(level.WarmupSeconds, Is.GreaterThan(0f));
            Assert.That(congestion.Count(lane => lane.Level > 0.5f), Is.GreaterThanOrEqualTo(4), "The approaches show red queues at once.");
            Assert.That(simulation.Statistics.CompletedTrips, Is.Zero, "Trips made during the warm-up are not counted.");
        }

        [Test]
        public void TheJam_FlowsFromTheStartBeforeAnyChange()
        {
            var level = new LevelPlaythrough("03_the_jam.json");

            var progress = level.Play(maxSeconds: 120f);

            Assert.That(progress.Trips, Is.GreaterThan(0), "The prebuilt neighbourhoods are connected.");
        }
    }
}
