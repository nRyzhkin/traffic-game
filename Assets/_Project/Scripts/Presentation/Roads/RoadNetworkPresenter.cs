using System;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Tools;

namespace TrafficGame.Presentation.Roads
{
    /// <summary>Keeps the road layout in sync with the network and pushes it to the views.</summary>
    public sealed class RoadNetworkPresenter : IRoadLayoutSource, IDisposable
    {
        private readonly RoadNetwork _network;
        private readonly RoadGeometrySettings _settings;
        private readonly IRoadNetworkView _roadView;
        private readonly ILaneGraphView _laneView;

        public RoadNetworkPresenter(RoadNetwork network, RoadGeometrySettings settings, IRoadNetworkView roadView, ILaneGraphView laneView)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _roadView = roadView ?? throw new ArgumentNullException(nameof(roadView));
            _laneView = laneView ?? throw new ArgumentNullException(nameof(laneView));

            _network.Changed += Refresh;
            Refresh();
        }

        public RoadLayout Layout { get; private set; }

        public event Action LayoutChanged;

        public void Dispose() => _network.Changed -= Refresh;

        private void Refresh()
        {
            Layout = RoadLayout.Build(_network, _settings);
            _roadView.Render(Layout);
            _laneView.Render(Layout.Lanes);
            LayoutChanged?.Invoke();
        }
    }
}
