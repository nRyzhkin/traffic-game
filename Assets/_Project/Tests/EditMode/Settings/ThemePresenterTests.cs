using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrafficGame.Presentation.Settings;

namespace TrafficGame.Tests.Settings
{
    public sealed class ThemePresenterTests
    {
        [Test]
        public void SystemMode_FollowsTheSystemAndItsChanges()
        {
            var system = new FakeSystem { PrefersDark = true };
            var view = new FakeView();
            using var presenter = new ThemePresenter(system, new FakeControl(), new[] { view }, ThemeMode.System);

            Assert.That(view.Applied, Is.EqualTo(new[] { true }));

            system.Switch(false);
            Assert.That(view.Applied, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ChosenMode_OverridesTheSystemAndIsReportedForSaving()
        {
            var system = new FakeSystem { PrefersDark = false };
            var view = new FakeView();
            var control = new FakeControl();
            using var presenter = new ThemePresenter(system, control, new[] { view }, ThemeMode.System);
            var saved = new List<ThemeMode>();
            presenter.ModeChanged += saved.Add;

            control.Request(ThemeMode.Dark);
            system.Switch(false);

            Assert.That(view.Applied, Is.EqualTo(new[] { false, true }));
            Assert.That(control.Shown, Is.EqualTo(ThemeMode.Dark));
            Assert.That(saved, Is.EqualTo(new[] { ThemeMode.Dark }));
        }

        [TestCase("Dark", ThemeMode.Dark)]
        [TestCase("light", ThemeMode.Light)]
        [TestCase("", ThemeMode.System)]
        [TestCase("7", ThemeMode.System)]
        [TestCase("purple", ThemeMode.System)]
        public void SavedValues_ParseToAModeWithSystemAsTheFallback(string value, ThemeMode expected) =>
            Assert.That(ThemePresenter.Parse(value), Is.EqualTo(expected));

        private sealed class FakeSystem : ISystemTheme
        {
            public bool PrefersDark { get; set; }
            public event Action Changed;

            public void Switch(bool dark)
            {
                PrefersDark = dark;
                Changed?.Invoke();
            }
        }

        private sealed class FakeView : IThemeView
        {
            public List<bool> Applied { get; } = new();
            public void ApplyTheme(bool dark) => Applied.Add(dark);
        }

        private sealed class FakeControl : IThemeControlView
        {
            public event Action<ThemeMode> ModeRequested;
            public ThemeMode Shown { get; private set; }
            public void SetMode(ThemeMode mode) => Shown = mode;
            public void Request(ThemeMode mode) => ModeRequested?.Invoke(mode);
        }
    }
}
