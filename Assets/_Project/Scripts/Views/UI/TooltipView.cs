using System;
using System.Collections.Generic;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>A small dark label that follows a world point (the end of the road being drawn); the first line stands out.</summary>
    public sealed class TooltipView : ITooltipView
    {
        private const string LineClass = "tooltip__line";
        private const string ErrorClass = "tooltip__line--error";
        private const string FirstClass = "tooltip__line--first";
        private static readonly Vector2 Offset = new(18f, 18f);

        private readonly VisualElement _container;
        private readonly Camera _camera;
        private readonly ILocalizer _localizer;
        private readonly List<Label> _labels = new();

        public TooltipView(VisualElement container, Camera camera, ILocalizer localizer)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _camera = camera;
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            Motion.Hide(_container, instantly: true);
        }

        public void Show(float2 world, IReadOnlyList<(LocalizedText text, TooltipTone tone)> lines)
        {
            while (_labels.Count < lines.Count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(LineClass);
                _container.Add(label);
                _labels.Add(label);
            }

            for (var i = 0; i < _labels.Count; i++)
            {
                var visible = i < lines.Count;
                _labels[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                    continue;

                _labels[i].text = _localizer.Get(lines[i].text);
                _labels[i].EnableInClassList(ErrorClass, lines[i].tone == TooltipTone.Error);
                _labels[i].EnableInClassList(FirstClass, i == 0);
            }

            PlaceAt(world);
            Motion.SetShown(_container, lines.Count > 0);
        }

        public void Hide() => Motion.Hide(_container);

        private void PlaceAt(float2 world)
        {
            var panel = _container.panel;
            if (panel == null)
                return;

            var screen = _camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)) + Offset;
            _container.style.left = panelPosition.x;
            _container.style.top = panelPosition.y;
        }
    }
}
