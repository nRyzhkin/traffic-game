using NUnit.Framework;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Levels;

namespace TrafficGame.Tests.Levels
{
    public sealed class ProgressTests
    {
        [Test]
        public void LevelProgress_GradesStarsWhenTheGoalIsReached()
        {
            var budget = new Budget(1000);
            var objective = new LevelObjective(2, new[] { new StarCondition(300, null), new StarCondition(null, 20f) });
            var progress = new LevelProgress(objective, budget);
            var completed = 0;
            progress.Completed += () => completed++;

            budget.Spend(500);
            progress.RecordTrip(10f);
            Assert.That(progress.IsComplete, Is.False);

            progress.RecordTrip(20f);
            progress.RecordTrip(99f);

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(progress.Trips, Is.EqualTo(2), "Trips after the goal do not count.");
            Assert.That(progress.StarsMet, Is.EqualTo(new[] { false, true }), "Spent 500 > 300; average 15 s ≤ 20 s.");
            Assert.That(progress.Stars, Is.EqualTo(2));
        }

        [Test]
        public void PlayerProgress_KeepsBestResultAndUnlocksInOrder()
        {
            var order = new[] { "a", "b", "c" };
            var progress = new PlayerProgress();

            Assert.That(progress.IsUnlocked(order, "a"), Is.True);
            Assert.That(progress.IsUnlocked(order, "b"), Is.False);

            Assert.That(progress.Record("a", 2), Is.True);
            Assert.That(progress.Record("a", 1), Is.False);

            Assert.That(progress.BestStars("a"), Is.EqualTo(2));
            Assert.That(progress.IsUnlocked(order, "b"), Is.True);
            Assert.That(progress.IsUnlocked(order, "c"), Is.False);
        }

        [Test]
        public void PlayerProgress_SurvivesSaveAndLoadAndIgnoresGarbage()
        {
            var progress = new PlayerProgress();
            progress.Record("level", 3);

            var loaded = PlayerProgress.FromJson(progress.ToJson());

            Assert.That(loaded.BestStars("level"), Is.EqualTo(3));
            Assert.That(PlayerProgress.FromJson("{ broken").BestStars("level"), Is.Zero);
            Assert.That(PlayerProgress.FromJson("{\"schemaVersion\": 99}").BestStars("level"), Is.Zero);
        }
    }
}
