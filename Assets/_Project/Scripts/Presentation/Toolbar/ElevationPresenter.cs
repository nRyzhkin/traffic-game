using System;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Tools;

namespace TrafficGame.Presentation.Toolbar
{
    /// <summary>Up/down stepper for the level roads and roundabouts are built on, shown while one of those tools is active.</summary>
    public interface IElevationView
    {
        event Action RaiseRequested;
        event Action LowerRequested;

        void Show(LocalizedText label, bool canRaise, bool canLower);
        void Hide();
    }

    public sealed class ElevationPresenter : IDisposable
    {
        private readonly IElevationView _view;
        private readonly ToolController _tools;
        private readonly ElevationSelection _elevation;

        public ElevationPresenter(IElevationView view, ToolController tools, ElevationSelection elevation)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));

            _view.RaiseRequested += _elevation.Raise;
            _view.LowerRequested += _elevation.Lower;
            _tools.Changed += Refresh;
            _elevation.Changed += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            _view.RaiseRequested -= _elevation.Raise;
            _view.LowerRequested -= _elevation.Lower;
            _tools.Changed -= Refresh;
            _elevation.Changed -= Refresh;
            _view.Hide();
        }

        private void Refresh()
        {
            if (_tools.Active is ToolKind.Road or ToolKind.CurvedRoad or ToolKind.Roundabout)
                _view.Show(new LocalizedText(TextKeys.ElevationLevel, _elevation.Level), _elevation.CanRaise, _elevation.CanLower);
            else
                _view.Hide();
        }
    }
}
