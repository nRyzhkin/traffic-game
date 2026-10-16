using System;
using System.Collections.Generic;
using TrafficGame.Presentation.Levels;
using TrafficGame.Presentation.Localization;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>
    /// Dimmed backdrop shared by modal cards; visible while any of its cards is. The backdrop fades, cards rise
    /// (a bottom sheet slides up on phones held upright).
    /// </summary>
    public sealed class ModalLayer
    {
        private readonly VisualElement _backdrop;
        private readonly HashSet<VisualElement> _open = new();

        public ModalLayer(VisualElement backdrop)
        {
            _backdrop = backdrop;
            Motion.Hide(_backdrop, instantly: true);
        }

        public void Show(VisualElement card)
        {
            Motion.Show(card);
            _open.Add(card);
            Motion.Show(_backdrop);
        }

        /// <param name="instantly">Setting up: no animation.</param>
        public void Hide(VisualElement card, bool instantly = false)
        {
            Motion.Hide(card, instantly);
            _open.Remove(card);
            if (_open.Count == 0)
                Motion.Hide(_backdrop, instantly);
        }
    }

    /// <summary>Builders for the small repeated pieces of the level views.</summary>
    internal static class Elements
    {
        public static Label Label(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        public static VisualElement Row(string className, IconKind icon, string text, string textClass)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(className);
            row.Add(new IconElement(icon));
            row.Add(Label(text, textClass));
            return row;
        }

        /// <summary>
        /// Filled stars for the earned ones, outlines (class <c>is-off</c>) for the rest. Existing stars are updated in
        /// place; with <paramref name="pop"/> a star that has just been earned pops.
        /// </summary>
        public static void Stars(VisualElement container, int earned, int total, bool pop = false)
        {
            if (container.childCount != total)
            {
                container.Clear();
                for (var i = 0; i < total; i++)
                    container.Add(new IconElement(IconKind.StarOutline));
                pop = false;
            }

            for (var i = 0; i < total; i++)
            {
                var star = (IconElement)container[i];
                var on = i < earned;
                if (pop && on && star.Kind != IconKind.Star)
                    Motion.Enter(star, "star--pop", 160);
                star.Kind = on ? IconKind.Star : IconKind.StarOutline;
                star.EnableInClassList("is-off", !on);
            }
        }
    }

    /// <summary>Goal card at the top: "Trips 24 / 60", a progress bar and the average trip with the stars it is worth.</summary>
    public sealed class ObjectiveView : IObjectiveView, IDisposable
    {
        private readonly VisualElement _root;
        private readonly Label _label;
        private readonly Label _count;
        private readonly VisualElement _fill;
        private readonly VisualElement _rest;
        private readonly Label _detail;
        private readonly VisualElement _stars;
        private readonly ILocalizer _localizer;
        private LocalizedText _labelText;
        private LocalizedText _countText;
        private LocalizedText _detailText;

        public ObjectiveView(VisualElement root, ILocalizer localizer)
        {
            _localizer = localizer;
            _root = root.Q("objective");
            _label = _root.Q<Label>("objective-label");
            _count = _root.Q<Label>("objective-count");
            _fill = _root.Q("objective-fill");
            _rest = _root.Q("objective-rest");
            _detail = _root.Q<Label>("objective-detail");
            _stars = _root.Q("objective-stars");
            _root.Q("objective-head").Insert(0, new IconElement(IconKind.Flag));
            _localizer.LanguageChanged += Render;
            Motion.Hide(_root, instantly: true);
        }

        public void Show(LocalizedText label, LocalizedText count, float fraction, LocalizedText detail, int stars, int maxStars)
        {
            _labelText = label;
            _countText = count;
            _detailText = detail;
            fraction = Math.Clamp(fraction, 0f, 1f);
            _fill.style.width = Length.Percent(fraction * 100f);
            _rest.style.display = fraction >= 1f ? DisplayStyle.None : DisplayStyle.Flex;
            if (_stars.childCount != maxStars || CountEarned() != stars)
                Elements.Stars(_stars, stars, maxStars, pop: Motion.IsShown(_root));
            _detail.parent.style.display = detail.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            Motion.Show(_root);
            Render();
        }

        public void Hide() => Motion.Hide(_root);

        public void Dispose() => _localizer.LanguageChanged -= Render;

        private int CountEarned()
        {
            var earned = 0;
            foreach (var child in _stars.Children())
            {
                if (!child.ClassListContains("is-off"))
                    earned++;
            }

            return earned;
        }

        private void Render()
        {
            _label.text = _localizer.Get(_labelText);
            _count.text = _localizer.Get(_countText);
            _detail.text = _localizer.Get(_detailText);
        }
    }

    /// <summary>Task card shown when a level starts: caption, title, task, goals, a tip and the start button.</summary>
    public sealed class IntroView : IIntroView
    {
        private readonly ModalLayer _modal;
        private readonly VisualElement _card;
        private readonly Label _caption;
        private readonly Label _title;
        private readonly Label _text;
        private readonly VisualElement _goals;
        private readonly VisualElement _hint;
        private readonly Label _hintText;
        private readonly Label _start;
        private readonly Button _back;
        private readonly ILocalizer _localizer;

        public IntroView(VisualElement root, ModalLayer modal, ILocalizer localizer)
        {
            _modal = modal;
            _localizer = localizer;
            _card = root.Q("intro-card");
            _caption = _card.Q<Label>("intro-caption");
            _title = _card.Q<Label>("intro-title");
            _text = _card.Q<Label>("intro-text");
            _goals = _card.Q("intro-goals");
            _hint = _card.Q("intro-hint");
            _hintText = _card.Q<Label>("intro-hint-text");
            _hint.Insert(0, new IconElement(IconKind.Lightbulb));
            _card.Q("intro-icon").Add(new IconElement(IconKind.Flag));

            var start = _card.Q<Button>("intro-start");
            _start = start.Q<Label>("label");
            start.Add(new IconElement(IconKind.Forward));
            start.clicked += () => StartRequested?.Invoke();

            _back = _card.Q<Button>("intro-back");
            _back.Insert(0, new IconElement(IconKind.Back));
            _back.clicked += () => BackRequested?.Invoke();
            _modal.Hide(_card, instantly: true);
        }

        public event Action StartRequested;
        public event Action BackRequested;

        public void Show(LocalizedText caption, LocalizedText title, LocalizedText text, IReadOnlyList<IntroGoal> goals, LocalizedText hint)
        {
            _caption.text = _localizer.Get(caption);
            _caption.style.display = caption.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            _title.text = _localizer.Get(title);
            _text.text = _localizer.Get(text);

            _goals.Clear();
            foreach (var goal in goals)
            {
                var row = Elements.Row("goal", goal.IsMainGoal ? IconKind.Flag : IconKind.Star, _localizer.Get(goal.Text), "goal__text");
                row.EnableInClassList("goal--trips", goal.IsMainGoal);
                _goals.Add(row);
            }

            _hintText.text = _localizer.Get(hint);
            _hint.style.display = hint.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            _start.text = _localizer.Get(new LocalizedText(TextKeys.IntroStart));
            _back.Q<Label>("label").text = _localizer.Get(new LocalizedText(TextKeys.IntroBack));
            _modal.Show(_card);
        }

        public void Hide() => _modal.Hide(_card);
    }

    /// <summary>Result card: stars, what each star asked for, numbers and where to go next.</summary>
    public sealed class ResultView : IResultView
    {
        private readonly ModalLayer _modal;
        private readonly VisualElement _card;
        private readonly VisualElement _stars;
        private readonly VisualElement _lines;
        private readonly VisualElement _statistics;
        private readonly Button _next;
        private readonly Button _levels;
        private readonly Label _retryLabel;
        private readonly Label _nextLabel;
        private readonly ILocalizer _localizer;

        public ResultView(VisualElement root, ModalLayer modal, ILocalizer localizer)
        {
            _modal = modal;
            _localizer = localizer;
            _card = root.Q("result-card");
            _stars = _card.Q("result-stars");
            _lines = _card.Q("result-lines");
            _statistics = _card.Q("result-statistics");
            _levels = _card.Q<Button>("result-levels");

            var retry = _card.Q<Button>("result-retry");
            _retryLabel = retry.Q<Label>("label");
            retry.Insert(0, new IconElement(IconKind.Replay));
            _next = _card.Q<Button>("result-next");
            _nextLabel = _next.Q<Label>("label");
            _next.Add(new IconElement(IconKind.Forward));

            // The card leaves as soon as the player has chosen, while the next scene is prepared.
            retry.clicked += () => Choose(RetryRequested);
            _levels.clicked += () => Choose(LevelsRequested);
            _next.clicked += () => Choose(NextRequested);
            _modal.Hide(_card, instantly: true);
        }

        public event Action RetryRequested;
        public event Action NextRequested;
        public event Action LevelsRequested;

        public void Show(int stars, LocalizedText levelName, IReadOnlyList<StarLine> lines, IReadOnlyList<ResultStatistic> statistics,
            bool hasNextLevel)
        {
            Elements.Stars(_stars, stars, lines.Count);
            if (_stars.childCount == 3)
                _stars[1].AddToClassList("result__star--big");

            // Stars land one after another once the card is up.
            for (var i = 0; i < _stars.childCount; i++)
                Motion.Enter(_stars[i], "star--enter", 260 + 140 * i);

            _card.Q<Label>("result-title").text = _localizer.Get(new LocalizedText(TextKeys.ResultTitle));
            _card.Q<Label>("result-subtitle").text = _localizer.Get(levelName);

            _lines.Clear();
            foreach (var line in lines)
            {
                var row = Elements.Row("result__line", line.Earned ? IconKind.CheckCircle : IconKind.StarOutline, _localizer.Get(line.Text),
                    "result__line-text");
                row.EnableInClassList("result__line--missed", !line.Earned);
                _lines.Add(row);
            }

            _statistics.Clear();
            foreach (var statistic in statistics)
            {
                var tile = new VisualElement { pickingMode = PickingMode.Ignore };
                tile.AddToClassList("stat");
                tile.EnableInClassList("stat--money", statistic.IsMoney);
                tile.Add(Elements.Label(_localizer.Get(statistic.Label), "stat__label"));
                tile.Add(Elements.Label(_localizer.Get(statistic.Value), "stat__value"));
                _statistics.Add(tile);
            }

            _retryLabel.text = _localizer.Get(new LocalizedText(TextKeys.ResultRetry));
            _levels.text = _localizer.Get(new LocalizedText(TextKeys.ResultLevels));
            _nextLabel.text = _localizer.Get(new LocalizedText(TextKeys.ResultNext));
            _next.style.display = hasNextLevel ? DisplayStyle.Flex : DisplayStyle.None;
            _modal.Show(_card);
        }

        public void Hide() => _modal.Hide(_card);

        private void Choose(Action choice)
        {
            Hide();
            choice?.Invoke();
        }
    }

    /// <summary>Title menu: a side sheet (a bottom sheet on phones) over the live showcase map.</summary>
    public sealed class MenuView : IMenuView
    {
        private readonly VisualElement _menu;
        private readonly VisualElement _levels;
        private readonly Button _continue;
        private readonly Label _continueLabel;
        private readonly ILocalizer _localizer;
        private string _continueId;

        public MenuView(VisualElement root, ILocalizer localizer)
        {
            _localizer = localizer;
            _menu = root.Q("menu");
            _levels = _menu.Q("menu-levels");
            var scroll = _menu.Q<ScrollView>("menu-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            _menu.Q("menu-logo").Add(new IconElement(IconKind.Road));
            _menu.Q("menu-stars").Insert(0, new IconElement(IconKind.Star));

            _continue = _menu.Q<Button>("menu-continue");
            _continueLabel = _continue.Q<Label>("label");
            _continue.Insert(0, new IconElement(IconKind.Play));
            _continue.clicked += () =>
            {
                if (_continueId != null)
                    LevelChosen?.Invoke(_continueId);
            };

            var sandbox = _menu.Q<Button>("menu-sandbox");
            sandbox.Insert(0, new IconElement(IconKind.Sandbox));
            sandbox.Add(new IconElement(IconKind.ChevronRight));
            sandbox.clicked += () => SandboxChosen?.Invoke();
            Motion.Hide(_menu, instantly: true);
        }

        public event Action<string> LevelChosen;
        public event Action SandboxChosen;

        public void Show(IReadOnlyList<LevelCard> levels, LevelCard? next)
        {
            _menu.Q<Label>("menu-title").text = _localizer.Get(new LocalizedText(TextKeys.MenuTitle));
            _menu.Q<Label>("menu-subtitle").text = _localizer.Get(new LocalizedText(TextKeys.MenuSubtitle));
            _menu.Q<Label>("menu-campaign").text = _localizer.Get(new LocalizedText(TextKeys.MenuCampaign));
            _menu.Q<Label>("sandbox-title").text = _localizer.Get(new LocalizedText(TextKeys.MenuSandbox));
            _menu.Q<Label>("sandbox-subtitle").text = _localizer.Get(new LocalizedText(TextKeys.MenuSandboxDetails));

            _continueId = next?.Id;
            _continue.style.display = next.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (next.HasValue)
                _continueLabel.text = _localizer.Get(new LocalizedText(TextKeys.MenuContinue, _localizer.Get(next.Value.Title)));

            var stars = 0;
            var maxStars = 0;
            _levels.Clear();
            foreach (var level in levels)
            {
                stars += level.Stars;
                maxStars += level.MaxStars;
                _levels.Add(CreateCard(level, level.Id == _continueId && level.Stars == 0));
            }

            _menu.Q<Label>("menu-stars-label").text = _localizer.Get(new LocalizedText(TextKeys.MenuStars, stars, maxStars));

            // The sheet slides in and the level cards follow it in a quick cascade.
            var entering = !Motion.IsShown(_menu);
            Motion.Show(_menu);
            if (entering)
            {
                for (var i = 0; i < _levels.childCount; i++)
                    Motion.Enter(_levels[i], "level--enter", 120 + 35 * i);
            }
        }

        public void Hide() => Motion.Hide(_menu);

        private Button CreateCard(LevelCard level, bool isNew)
        {
            var id = level.Id;
            var card = new Button(() => LevelChosen?.Invoke(id));
            card.AddToClassList("level");
            card.EnableInClassList("level--current", isNew);
            card.EnableInClassList("level--locked", !level.Unlocked);
            card.SetEnabled(level.Unlocked);

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("level__top");
            top.Add(Elements.Label(level.Number.ToString(), "level__number"));
            if (!level.Unlocked)
            {
                var lockIcon = new IconElement(IconKind.Lock);
                lockIcon.AddToClassList("level__lock");
                top.Add(lockIcon);
            }
            else if (isNew)
            {
                top.Add(Elements.Label(_localizer.Get(new LocalizedText(TextKeys.MenuNew)), "level__badge"));
            }

            card.Add(top);
            card.Add(Elements.Label(_localizer.Get(level.Title), "level__title"));

            var stars = new VisualElement { pickingMode = PickingMode.Ignore };
            stars.AddToClassList("level__stars");
            if (level.Unlocked)
                Elements.Stars(stars, level.Stars, level.MaxStars);
            card.Add(stars);
            return card;
        }
    }
}
