using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Levels;
using TrafficGame.Presentation.Localization;

namespace TrafficGame.Presentation.Levels
{
    /// <summary>A campaign level as listed in the menu.</summary>
    public readonly struct LevelCard
    {
        public LevelCard(string id, int number, LocalizedText title, int stars, int maxStars, bool unlocked)
        {
            Id = id;
            Number = number;
            Title = title;
            Stars = stars;
            MaxStars = maxStars;
            Unlocked = unlocked;
        }

        public string Id { get; }
        public int Number { get; }
        public LocalizedText Title { get; }
        public int Stars { get; }
        public int MaxStars { get; }
        public bool Unlocked { get; }
    }

    /// <summary>Title screen with the level list and the sandbox.</summary>
    public interface IMenuView
    {
        event Action<string> LevelChosen;
        event Action SandboxChosen;

        /// <param name="next">The level the "continue" button opens; null hides the button.</param>
        void Show(IReadOnlyList<LevelCard> levels, LevelCard? next);
        void Hide();
    }

    /// <summary>Lists campaign levels with best stars; locked levels cannot be chosen.</summary>
    public sealed class MenuPresenter : IDisposable
    {
        private readonly IMenuView _view;
        private readonly IReadOnlyList<Level> _campaign;
        private readonly PlayerProgress _progress;

        public MenuPresenter(IMenuView view, IReadOnlyList<Level> campaign, PlayerProgress progress)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _view.LevelChosen += OnLevelChosen;
            _view.SandboxChosen += OnSandboxChosen;
        }

        public event Action<Level> LevelChosen;
        public event Action SandboxChosen;

        public void Show()
        {
            var order = _campaign.Select(level => level.Id).ToArray();
            var cards = _campaign.Select((level, index) => new LevelCard(
                level.Id,
                index + 1,
                new LocalizedText(level.TitleKey ?? level.Id),
                _progress.BestStars(level.Id),
                level.Objective?.MaxStars ?? 0,
                _progress.IsUnlocked(order, level.Id))).ToArray();
            _view.Show(cards, NextLevel(cards));
        }

        public void Hide() => _view.Hide();

        /// <summary>The first open level still without stars, else the first open one that can earn more; null when all are done.</summary>
        private static LevelCard? NextLevel(IReadOnlyList<LevelCard> cards)
        {
            foreach (var card in cards)
            {
                if (card.Unlocked && card.Stars == 0)
                    return card;
            }

            foreach (var card in cards)
            {
                if (card.Unlocked && card.Stars < card.MaxStars)
                    return card;
            }

            return null;
        }

        public void Dispose()
        {
            _view.LevelChosen -= OnLevelChosen;
            _view.SandboxChosen -= OnSandboxChosen;
        }

        private void OnLevelChosen(string id)
        {
            var order = _campaign.Select(level => level.Id).ToArray();
            var level = _campaign.FirstOrDefault(candidate => candidate.Id == id);
            if (level != null && _progress.IsUnlocked(order, id))
                LevelChosen?.Invoke(level);
        }

        private void OnSandboxChosen() => SandboxChosen?.Invoke();
    }
}
