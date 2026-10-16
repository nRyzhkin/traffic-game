using System;
using System.Collections.Generic;
using TrafficGame.Core.Levels;
using TrafficGame.Presentation.Localization;

namespace TrafficGame.Presentation.Levels
{
    /// <summary>One thing the level asks for, as listed on its task card.</summary>
    public readonly struct IntroGoal
    {
        public IntroGoal(LocalizedText text, bool isMainGoal)
        {
            Text = text;
            IsMainGoal = isMainGoal;
        }

        public LocalizedText Text { get; }

        /// <summary>The goal itself rather than a condition for an extra star.</summary>
        public bool IsMainGoal { get; }
    }

    /// <summary>Task card shown when a level starts.</summary>
    public interface IIntroView
    {
        event Action StartRequested;

        /// <summary>The player would rather not play this level now.</summary>
        event Action BackRequested;

        /// <param name="caption">Small line above the title, like "LEVEL 4"; may be empty.</param>
        /// <param name="hint">A tip about a new tool; empty hides it.</param>
        void Show(LocalizedText caption, LocalizedText title, LocalizedText text, IReadOnlyList<IntroGoal> goals, LocalizedText hint);
        void Hide();
    }

    /// <summary>Shows the level's task, goal and star conditions, and reports whether the player starts or backs out.</summary>
    public sealed class IntroPresenter : IDisposable
    {
        private readonly IIntroView _view;

        public IntroPresenter(IIntroView view, Level level, int levelNumber)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            if (level == null)
                throw new ArgumentNullException(nameof(level));

            var goals = new List<IntroGoal>();
            if (level.Objective != null)
            {
                goals.Add(new IntroGoal(new LocalizedText(TextKeys.StarGoal, level.Objective.Trips), true));
                foreach (var star in level.Objective.Stars)
                    goals.Add(new IntroGoal(StarText.Describe(star), false));
            }

            _view.StartRequested += OnStart;
            _view.BackRequested += OnBack;
            _view.Show(levelNumber > 0 ? new LocalizedText(TextKeys.IntroCaption, levelNumber) : LocalizedText.None,
                new LocalizedText(level.TitleKey), new LocalizedText(level.IntroKey), goals,
                level.HintKey != null ? new LocalizedText(level.HintKey) : LocalizedText.None);
        }

        public event Action Started;

        /// <summary>The player went back to the level list instead.</summary>
        public event Action Declined;

        public void Dispose()
        {
            _view.StartRequested -= OnStart;
            _view.BackRequested -= OnBack;
            _view.Hide();
        }

        private void OnStart()
        {
            _view.Hide();
            Started?.Invoke();
        }

        private void OnBack()
        {
            _view.Hide();
            Declined?.Invoke();
        }
    }
}
