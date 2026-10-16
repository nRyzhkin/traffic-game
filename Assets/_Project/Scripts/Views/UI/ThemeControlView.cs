using System;
using System.Collections.Generic;
using TrafficGame.Presentation.Settings;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>Light / dark / follow-the-system segmented control in the menu.</summary>
    public sealed class ThemeControlView : IThemeControlView
    {
        private const string SelectedClass = "is-selected";
        private readonly List<(ThemeMode mode, Button button)> _buttons = new();
        private readonly SegmentThumb _thumb;

        public ThemeControlView(VisualElement container)
        {
            _thumb = new SegmentThumb(container);
            Add(container, ThemeMode.Light, IconKind.Sun);
            Add(container, ThemeMode.Dark, IconKind.Moon);
            Add(container, ThemeMode.System, IconKind.ThemeAuto);
        }

        public event Action<ThemeMode> ModeRequested;

        public void SetMode(ThemeMode mode)
        {
            foreach (var (value, button) in _buttons)
            {
                button.EnableInClassList(SelectedClass, value == mode);
                if (value == mode)
                    _thumb.Select(button);
            }
        }

        private void Add(VisualElement container, ThemeMode mode, IconKind icon)
        {
            var button = new Button(() => ModeRequested?.Invoke(mode));
            button.AddToClassList("segment");
            button.Add(new IconElement(icon));
            container.Add(button);
            _buttons.Add((mode, button));
        }
    }
}
