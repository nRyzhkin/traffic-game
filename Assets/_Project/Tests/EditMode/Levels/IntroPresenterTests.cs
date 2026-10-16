using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrafficGame.Presentation.Levels;
using TrafficGame.Presentation.Localization;

namespace TrafficGame.Tests.Levels
{
    public sealed class IntroPresenterTests
    {
        private FakeIntroView _view;
        private IntroPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeIntroView();
            _presenter = new IntroPresenter(_view, new LevelPlaythrough("01_first_road.json").Level, 1);
        }

        [Test]
        public void Start_HidesTheCardAndStartsTheLevel()
        {
            var started = false;
            _presenter.Started += () => started = true;

            _view.PressStart();

            Assert.That(started, Is.True);
            Assert.That(_view.Shown, Is.False);
        }

        [Test]
        public void Back_HidesTheCardAndDeclinesTheLevel()
        {
            var started = false;
            var declined = false;
            _presenter.Started += () => started = true;
            _presenter.Declined += () => declined = true;

            _view.PressBack();

            Assert.That(declined, Is.True);
            Assert.That(started, Is.False);
            Assert.That(_view.Shown, Is.False);
        }

        private sealed class FakeIntroView : IIntroView
        {
            public event Action StartRequested;
            public event Action BackRequested;

            public bool Shown { get; private set; }

            public void Show(LocalizedText caption, LocalizedText title, LocalizedText text, IReadOnlyList<IntroGoal> goals, LocalizedText hint) =>
                Shown = true;

            public void Hide() => Shown = false;

            public void PressStart() => StartRequested?.Invoke();
            public void PressBack() => BackRequested?.Invoke();
        }
    }
}
