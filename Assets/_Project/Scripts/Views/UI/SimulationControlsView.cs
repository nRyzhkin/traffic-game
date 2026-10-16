using System;
using System.Collections.Generic;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>Pause / 1× / 2× / 4× as a segmented control, and the chip saying the game fast-forwards on its own.</summary>
    public sealed class SimulationControlsView : ISimulationControlsView, IDisposable
    {
        private const string SelectedClass = "is-selected";
        private static readonly float[] Speeds = { 0f, 1f, 2f, 4f };

        private readonly ILocalizer _localizer;
        private readonly List<(float speed, Button button)> _buttons = new();
        private readonly SegmentThumb _thumb;
        private readonly VisualElement _boost;
        private readonly Label _boostLabel;

        public SimulationControlsView(VisualElement root, ILocalizer localizer)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            var group = root.Q("speed-buttons");
            _thumb = new SegmentThumb(group);
            foreach (var speed in Speeds)
            {
                var button = new Button(() => SpeedRequested?.Invoke(speed));
                button.AddToClassList("segment");
                if (speed == 0f)
                    button.Add(new IconElement(IconKind.Pause));
                else
                    button.text = $"{speed:0}×";

                group.Add(button);
                _buttons.Add((speed, button));
            }

            _boost = root.Q("boost");
            _boost.Insert(0, new IconElement(IconKind.FastForward));
            _boostLabel = _boost.Q<Label>("boost-label");
            Motion.Hide(_boost, instantly: true);
            _localizer.LanguageChanged += Render;
            Render();
        }

        public event Action<float> SpeedRequested;

        public void SetSpeed(float speed)
        {
            Button selected = null;
            foreach (var (value, button) in _buttons)
            {
                var isSelected = Mathf.Approximately(value, speed);
                button.EnableInClassList(SelectedClass, isSelected);
                if (isSelected)
                    selected = button;
            }

            _thumb.Select(selected);
        }

        public void SetBoosted(bool boosted) => Motion.SetShown(_boost, boosted);

        public void Dispose() => _localizer.LanguageChanged -= Render;

        private void Render() => _boostLabel.text = _localizer.Get(new LocalizedText(TextKeys.Boosted));
    }
}
