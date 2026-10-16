using System;
using System.Collections.Generic;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Toolbar;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>Horizontally scrolling row of road type filter chips: name and price per meter; the chosen one is ticked.</summary>
    public sealed class RoadProfileBarView : IRoadProfileBarView, IDisposable
    {
        private const string SelectedClass = "is-selected";

        private readonly VisualElement _bar;
        private readonly ScrollView _items;
        private readonly ILocalizer _localizer;
        private IReadOnlyList<RoadProfileOption> _options = Array.Empty<RoadProfileOption>();
        private string _selectedId;

        public RoadProfileBarView(VisualElement root, ILocalizer localizer)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _bar = root.Q("profile-bar");
            _items = new ScrollView(ScrollViewMode.Horizontal)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _items.AddToClassList("profile-bar__items");
            _bar.Add(_items);

            _localizer.LanguageChanged += Render;
            Motion.Hide(_bar, instantly: true);
        }

        public event Action<string> ProfileChosen;

        public void Show(IReadOnlyList<RoadProfileOption> options, string selectedId)
        {
            var rebuild = !ReferenceEquals(options, _options);
            _options = options;
            _selectedId = selectedId;
            if (rebuild)
                Render();
            else
                MarkSelection();

            Motion.Show(_bar);
        }

        public void Hide() => Motion.Hide(_bar);

        public void Dispose() => _localizer.LanguageChanged -= Render;

        private void Render()
        {
            _items.Clear();
            foreach (var option in _options)
            {
                var id = option.Id;
                var chip = new Button(() => ProfileChosen?.Invoke(id)) { userData = id };
                chip.AddToClassList("chip");
                chip.Add(new IconElement(IconKind.Check));

                var name = new Label(_localizer.Get(option.Name)) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("chip__name");
                var details = new Label(_localizer.Get(option.Details)) { pickingMode = PickingMode.Ignore };
                details.AddToClassList("chip__details");

                chip.Add(name);
                chip.Add(details);
                _items.Add(chip);
            }

            MarkSelection();
        }

        private void MarkSelection()
        {
            foreach (var chip in _items.Children())
                chip.EnableInClassList(SelectedClass, (string)chip.userData == _selectedId);
        }
    }
}
