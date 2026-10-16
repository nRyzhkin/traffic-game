using System;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Toolbar;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>The ▼ Height 1 ▲ stepper at the start of the road type row.</summary>
    public sealed class ElevationView : IElevationView, IDisposable
    {
        private readonly VisualElement _root;
        private readonly Label _label;
        private readonly Button _up;
        private readonly Button _down;
        private readonly ILocalizer _localizer;
        private LocalizedText _text;

        public ElevationView(VisualElement root, ILocalizer localizer)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _root = root.Q("elevation");
            _label = _root.Q<Label>("elevation-label");
            _down = _root.Q<Button>("elevation-down");
            _up = _root.Q<Button>("elevation-up");
            _down.Add(new IconElement(IconKind.ChevronDown));
            _up.Add(new IconElement(IconKind.ChevronUp));
            _down.clicked += () => LowerRequested?.Invoke();
            _up.clicked += () => RaiseRequested?.Invoke();
            _localizer.LanguageChanged += Render;
            Motion.Hide(_root, instantly: true);
        }

        public event Action RaiseRequested;
        public event Action LowerRequested;

        public void Show(LocalizedText label, bool canRaise, bool canLower)
        {
            _text = label;
            _up.SetEnabled(canRaise);
            _down.SetEnabled(canLower);
            Render();
            Motion.Show(_root);
        }

        public void Hide() => Motion.Hide(_root);

        public void Dispose() => _localizer.LanguageChanged -= Render;

        private void Render() => _label.text = _localizer.Get(_text);
    }
}
