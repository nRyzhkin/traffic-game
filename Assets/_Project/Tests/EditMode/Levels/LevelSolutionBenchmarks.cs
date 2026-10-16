using System;
using System.Text;
using NUnit.Framework;
using TrafficGame.Core.Levels;
using Unity.Mathematics;

namespace TrafficGame.Tests.Levels
{
    /// <summary>
    /// Plays typical player solutions of each campaign level and reports time to goal, spending and trip times —
    /// the numbers the star limits in the level files are tuned from. Not run by default:
    /// unity command run_tests --mode EditMode --filter LevelSolutionBenchmarks --include_explicit true
    /// </summary>
    [Explicit, Category("Benchmark")]
    public sealed class LevelSolutionBenchmarks
    {
        private static readonly int[] Seeds = { 1, 2, 3 };

        [Test]
        public void FirstRoad() => Report("01_first_road.json",
            ("collector street", level =>
            {
                level.Road(new float2(-103f, 50f), new float2(-103f, 0f));
                level.Road(new float2(-103f, 0f), new float2(-103f, -50f));
                level.Road(new float2(-103f, 0f), new float2(85f, 0f));
            }),
            ("a road from every house", level =>
            {
                level.Road(new float2(-103f, 0f), new float2(85f, 0f));
                level.Road(new float2(-103f, 50f), new float2(-40f, 0f));
                level.Road(new float2(-103f, -50f), new float2(-40f, 0f));
            }),
            ("collector drawn in one stroke", level =>
            {
                level.Road(new float2(-103f, 50f), new float2(-103f, -50f));
                level.Road(new float2(-103f, 0f), new float2(85f, 0f));
            }));

        [Test]
        public void CrossTheAvenue() => Report("02_cross_the_avenue.json",
            ("one crossing", level =>
            {
                level.Road(new float2(-123f, 90f), new float2(-123f, -90f));
                level.Road(new float2(-123f, 0f), new float2(122f, 0f));
                level.Road(new float2(122f, 60f), new float2(122f, -60f));
            }),
            ("two crossings", level =>
            {
                level.Road(new float2(-123f, 90f), new float2(-123f, -90f));
                level.Road(new float2(-123f, 60f), new float2(122f, 60f));
                level.Road(new float2(-123f, -60f), new float2(122f, -60f));
            }),
            ("one crossing with a roundabout", level =>
            {
                level.Road(new float2(-123f, 90f), new float2(-123f, -90f));
                level.Road(new float2(-123f, 0f), new float2(122f, 0f));
                level.Road(new float2(122f, 60f), new float2(122f, -60f));
                level.Roundabout(new float2(0f, 0f));
            }),
            ("one crossing with traffic lights", level =>
            {
                level.Road(new float2(-123f, 90f), new float2(-123f, -90f));
                level.Road(new float2(-123f, 0f), new float2(122f, 0f));
                level.Road(new float2(122f, 60f), new float2(122f, -60f));
                level.TrafficLights(new float2(0f, 0f));
            }));

        [Test]
        public void TheJam() => Report("03_the_jam.json",
            ("leave it as it is", _ => { }),
            ("roundabout", level => level.Roundabout(float2.zero)),
            ("big roundabout", level => level.Roundabout(float2.zero, 30f)),
            ("traffic lights", level => level.TrafficLights(float2.zero)),
            ("traffic lights, protected left", level => level.TrafficLights(float2.zero, protectedLeft: true)));

        [Test]
        public void RushHour() => Report("04_rush_hour.json",
            ("leave it as it is", _ => { }),
            ("traffic lights", level => level.TrafficLights(float2.zero)),
            ("traffic lights, protected left", level => level.TrafficLights(float2.zero, protectedLeft: true)),
            ("roundabout", level => level.Roundabout(float2.zero, 30f)));

        [Test]
        public void Overpass() => Report("05_overpass.json",
            ("crossing", level => level.Road(new float2(0f, 190f), new float2(0f, -160f))),
            ("crossing with traffic lights", level =>
            {
                level.Road(new float2(0f, 190f), new float2(0f, -160f));
                level.TrafficLights(float2.zero);
            }),
            ("crossing with a roundabout", level =>
            {
                level.Road(new float2(0f, 190f), new float2(0f, -160f));
                level.Roundabout(float2.zero, 30f);
            }),
            ("overpass", level =>
            {
                level.Road(new float2(0f, 190f), new float2(0f, 60f));
                level.Road(new float2(0f, 60f), 0, new float2(0f, 15f), 1);
                level.Road(new float2(0f, 15f), 1, new float2(0f, -15f), 1);
                level.Road(new float2(0f, -15f), 1, new float2(0f, -60f), 0);
                level.Road(new float2(0f, -60f), new float2(0f, -160f));
            }));

        [Test]
        public void Flyover() => Report("06_flyover.json",
            ("leave it", _ => { }),
            ("streets into the avenues", level =>
            {
                level.Road(Diagonal(225f), Diagonal(105f));
                level.Road(Diagonal(105f), new float2(0f, 60f));
                level.Road(Diagonal(-200f), Diagonal(-105f));
                level.Road(Diagonal(-105f), new float2(0f, -60f));
            }),
            ("street through the crossroads", level => level.Road(Diagonal(225f), Diagonal(-200f))),
            ("flyover over the crossroads", level =>
            {
                level.Road(Diagonal(225f), Diagonal(105f));
                level.Road(Diagonal(105f), 0, Diagonal(60f), 1);
                level.Road(Diagonal(60f), 1, Diagonal(-60f), 1);
                level.Road(Diagonal(-60f), 1, Diagonal(-105f), 0);
                level.Road(Diagonal(-105f), Diagonal(-200f));
            }));

        /// <summary>A point on level 6's diagonal street, <paramref name="distance"/> meters north-west of the crossroads.</summary>
        private static float2 Diagonal(float distance) => new float2(-1f, 1f) * (distance / math.sqrt(2f));

        private static void Report(string file, params (string name, Action<LevelPlaythrough> build)[] solutions)
        {
            var report = new StringBuilder();
            foreach (var (name, build) in solutions)
            {
                foreach (var seed in Seeds)
                {
                    var level = new LevelPlaythrough(file);
                    build(level);
                    var progress = level.Play(seed: seed);
                    report.AppendLine($"{name} (seed {seed}): {Describe(progress, level.PlayedSeconds)}");
                    report.AppendLine($"    slowest: {level.Bottlenecks}");
                }
            }

            Assert.Pass(report.ToString());
        }

        private static string Describe(LevelProgress progress, float seconds) =>
            $"{(progress.IsComplete ? "goal" : "NOT reached")} after {seconds:0}s, trips {progress.Trips}, spent ${progress.Spent}, " +
            $"average trip {progress.AverageTripSeconds:0.0}s, stars {progress.Stars}";
    }
}
