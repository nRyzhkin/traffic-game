using System;
using System.Collections.Generic;
using TrafficGame.Core.Levels;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Localization;

namespace TrafficGame.Presentation.Levels
{
    /// <summary>The goal card: what to do, how far it is, and how the level is doing on time.</summary>
    public interface IObjectiveView
    {
        /// <param name="detail">Second line (the average trip so far); empty hides it.</param>
        /// <param name="stars">Stars the level would earn right now, out of <paramref name="maxStars"/>; 0 of 0 hides them.</param>
        void Show(LocalizedText label, LocalizedText count, float fraction, LocalizedText detail, int stars, int maxStars);
        void Hide();
    }

    /// <summary>A number on the result card, like the money spent.</summary>
    public readonly struct ResultStatistic
    {
        public ResultStatistic(LocalizedText label, LocalizedText value, bool isMoney = false)
        {
            Label = label;
            Value = value;
            IsMoney = isMoney;
        }

        public LocalizedText Label { get; }
        public LocalizedText Value { get; }
        public bool IsMoney { get; }
    }

    /// <summary>One line of the result card: what a star asks for and whether it was earned.</summary>
    public readonly struct StarLine
    {
        public StarLine(LocalizedText text, bool earned)
        {
            Text = text;
            Earned = earned;
        }

        public LocalizedText Text { get; }
        public bool Earned { get; }
    }

    /// <summary>The card shown when the goal is reached.</summary>
    public interface IResultView
    {
        event Action RetryRequested;
        event Action NextRequested;
        event Action LevelsRequested;

        /// <param name="lines">One line per star, the goal first; their count is the most stars the level gives.</param>
        void Show(int stars, LocalizedText levelName, IReadOnlyList<StarLine> lines, IReadOnlyList<ResultStatistic> statistics,
            bool hasNextLevel);
        void Hide();
    }

    /// <summary>
    /// Counts trips that end at a building towards the level goal, shows progress, and when the goal is reached
    /// pauses the simulation and presents the stars.
    /// </summary>
    public sealed class ObjectivePresenter : IDisposable
    {
        private readonly LevelProgress _progress;
        private readonly TrafficSimulation _simulation;
        private readonly ISet<NodeId> _destinations;
        private readonly IObjectiveView _view;
        private readonly IResultView _result;
        private readonly bool _hasNextLevel;
        private readonly LocalizedText _levelName;

        public ObjectivePresenter(LevelProgress progress, TrafficSimulation simulation, ISet<NodeId> destinationDoors,
            IObjectiveView view, IResultView result, bool hasNextLevel, LocalizedText levelName)
        {
            _levelName = levelName;
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _destinations = destinationDoors ?? throw new ArgumentNullException(nameof(destinationDoors));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _result = result ?? throw new ArgumentNullException(nameof(result));
            _hasNextLevel = hasNextLevel;

            _simulation.TripCompleted += OnTripCompleted;
            _progress.Changed += ShowProgress;
            _progress.Completed += ShowResult;
            _result.Hide();
            ShowProgress();
        }

        /// <summary>Raised with the earned stars when the goal is reached.</summary>
        public event Action<int> Completed;

        public void Dispose()
        {
            _simulation.TripCompleted -= OnTripCompleted;
            _progress.Changed -= ShowProgress;
            _progress.Completed -= ShowResult;
            _view.Hide();
            _result.Hide();
        }

        private void OnTripCompleted(NodeId origin, NodeId destination, float seconds)
        {
            if (_destinations.Contains(destination))
                _progress.RecordTrip(seconds);
        }

        private void ShowProgress()
        {
            var goal = _progress.Objective.Trips;
            var started = _progress.Trips > 0;
            _view.Show(new LocalizedText(TextKeys.ObjectiveTrips), new LocalizedText(TextKeys.ObjectiveCount, _progress.Trips, goal),
                (float)_progress.Trips / goal,
                started ? new LocalizedText(TextKeys.ObjectiveAverage, _progress.AverageTripSeconds) : LocalizedText.None,
                started ? _progress.ProjectedStars : 0, started ? _progress.Objective.MaxStars : 0);
        }

        private void ShowResult()
        {
            var lines = new List<StarLine> { new(new LocalizedText(TextKeys.StarGoal, _progress.Objective.Trips), true) };
            for (var i = 0; i < _progress.Objective.Stars.Count; i++)
                lines.Add(new StarLine(StarText.Describe(_progress.Objective.Stars[i]), _progress.StarsMet[i]));

            var statistics = new[]
            {
                new ResultStatistic(new LocalizedText(TextKeys.ResultSpent), new LocalizedText(TextKeys.Money, _progress.Spent), isMoney: true),
                new ResultStatistic(new LocalizedText(TextKeys.ResultAverageTrip), new LocalizedText(TextKeys.Seconds, _progress.AverageTripSeconds)),
                new ResultStatistic(new LocalizedText(TextKeys.ResultTrips), new LocalizedText(TextKeys.Count, _progress.Trips))
            };

            _result.Show(_progress.Stars, _levelName, lines, statistics, _hasNextLevel);
            Completed?.Invoke(_progress.Stars);
        }
    }
}
