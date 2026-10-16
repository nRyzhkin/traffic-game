using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Economy;

namespace TrafficGame.Core.Levels
{
    /// <summary>An extra star: awarded when every limit it sets is met at the moment the goal is reached.</summary>
    public readonly struct StarCondition
    {
        public StarCondition(long? maxSpent, float? maxAverageTripSeconds)
        {
            MaxSpent = maxSpent;
            MaxAverageTripSeconds = maxAverageTripSeconds;
        }

        public long? MaxSpent { get; }
        public float? MaxAverageTripSeconds { get; }

        public bool IsMet(long spent, float averageTripSeconds) =>
            (!MaxSpent.HasValue || spent <= MaxSpent.Value) &&
            (!MaxAverageTripSeconds.HasValue || averageTripSeconds <= MaxAverageTripSeconds.Value);
    }

    /// <summary>What the player has to achieve: a number of completed trips; extra stars for doing it well.</summary>
    public sealed class LevelObjective
    {
        public LevelObjective(int trips, IEnumerable<StarCondition> stars)
        {
            if (trips < 1)
                throw new ArgumentOutOfRangeException(nameof(trips), trips, "A goal needs at least one trip.");
            Trips = trips;
            Stars = stars?.ToArray() ?? Array.Empty<StarCondition>();
        }

        public int Trips { get; }

        /// <summary>Conditions for the stars after the first one, which is given for reaching the goal.</summary>
        public IReadOnlyList<StarCondition> Stars { get; }

        public int MaxStars => 1 + Stars.Count;
    }

    /// <summary>Counts trips towards a <see cref="LevelObjective"/> and grades the result once it is reached.</summary>
    public sealed class LevelProgress
    {
        private readonly Budget _budget;
        private readonly long _startingBalance;
        private bool[] _starsMet = Array.Empty<bool>();

        public LevelProgress(LevelObjective objective, Budget budget)
        {
            Objective = objective ?? throw new ArgumentNullException(nameof(objective));
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _startingBalance = budget.Balance;
        }

        /// <summary>Raised after every counted trip.</summary>
        public event Action Changed;

        /// <summary>Raised once, when the goal is reached.</summary>
        public event Action Completed;

        public LevelObjective Objective { get; }
        public int Trips { get; private set; }
        public float AverageTripSeconds { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>Money spent on construction so far (refunds count back).</summary>
        public long Spent => _startingBalance - _budget.Balance;

        /// <summary>For each star condition, whether it was met when the goal was reached.</summary>
        public IReadOnlyList<bool> StarsMet => _starsMet;

        public int Stars => IsComplete ? 1 + _starsMet.Count(met => met) : 0;

        /// <summary>The stars the level would earn if it ended as it goes now (the goal star included).</summary>
        public int ProjectedStars => 1 + Objective.Stars.Count(star => star.IsMet(Spent, AverageTripSeconds));

        public void RecordTrip(float seconds)
        {
            if (IsComplete)
                return;

            Trips++;
            AverageTripSeconds += (seconds - AverageTripSeconds) / Trips;
            Changed?.Invoke();

            if (Trips < Objective.Trips)
                return;

            IsComplete = true;
            _starsMet = Objective.Stars.Select(star => star.IsMet(Spent, AverageTripSeconds)).ToArray();
            Completed?.Invoke();
        }
    }
}
