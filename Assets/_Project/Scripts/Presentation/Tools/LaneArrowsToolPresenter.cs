using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Localization;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>
    /// Paints lane arrows: a click (or tap) on a junction selects it and shows a marker per arriving lane; each click
    /// on a marker moves that lane to the next combination of turns the junction offers. Free of charge.
    /// </summary>
    public sealed class LaneArrowsToolPresenter : IMapTool
    {
        private const float MarkerSetback = 5f;
        private const float MarkerPickRadius = 2.5f;

        /// <summary>Order in which a lane's arrows change; combinations the junction does not offer are skipped.</summary>
        private static readonly LaneArrows[] Cycle =
        {
            LaneArrows.Straight,
            LaneArrows.Left,
            LaneArrows.Right,
            LaneArrows.Left | LaneArrows.Straight,
            LaneArrows.Straight | LaneArrows.Right,
            LaneArrows.Left | LaneArrows.Straight | LaneArrows.Right,
            LaneArrows.Left | LaneArrows.Right
        };

        private readonly ConstructionServices _services;
        private readonly IJunctionPreviewView _preview;
        private readonly ITooltipView _tooltip;
        private readonly List<Lane> _lanes = new();
        private readonly List<LaneArrowMarker> _markers = new();
        private NodeId? _selected;
        private bool _hovering;
        private int _highlighted = -1;

        public LaneArrowsToolPresenter(ConstructionServices services, IJunctionPreviewView preview, ITooltipView tooltip)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
        }

        public bool CanConfirm => _selected.HasValue ? _highlighted >= 0 : _hovering;
        public bool ActsOnTap => true;

        /// <summary>The junction whose lanes are being edited.</summary>
        public NodeId? Selected => _selected;

        public void Activate() { }

        public void Deactivate()
        {
            _selected = null;
            _hovering = false;
            _highlighted = -1;
            _preview.Hide();
            _tooltip.Hide();
        }

        public void PointerMoved(float2 world)
        {
            var layout = _services.Layout.Layout;
            _highlighted = -1;
            _hovering = false;

            if (_selected.HasValue && layout.Junctions.TryGetValue(_selected.Value, out var selected) && CollectLanes(selected) > 0)
            {
                _highlighted = MarkerAt(world);
                _preview.ShowLaneArrows(selected, _markers, _highlighted);
                if (_highlighted >= 0)
                    _tooltip.Show(_markers[_highlighted].Position, new[] { (new LocalizedText(TextKeys.ChangeArrows), TooltipTone.Normal) });
                else
                    _tooltip.Hide();
                return;
            }

            _selected = null;
            if (_services.Snapper.TryPickJunction(world, layout, out var junction) && CollectLanes(junction) > 0)
            {
                _hovering = true;
                _preview.ShowLaneArrows(junction, _markers, -1);
                _tooltip.Show(junction.Center, new[] { (new LocalizedText(TextKeys.EditLanes), TooltipTone.Normal) });
                return;
            }

            _preview.Hide();
            _tooltip.Hide();
        }

        public void PrimaryClick(float2 world)
        {
            PointerMoved(world);
            if (_selected.HasValue && _highlighted >= 0)
            {
                ChangeArrows(_lanes[_highlighted]);
            }
            else if (_services.Snapper.TryPickJunction(world, _services.Layout.Layout, out var junction))
            {
                _selected = junction.Node;
            }
            else
            {
                _selected = null;
            }

            PointerMoved(world);
        }

        public bool Cancel()
        {
            if (!_selected.HasValue)
                return false;

            Deactivate();
            return true;
        }

        private int CollectLanes(JunctionGeometry junction)
        {
            _lanes.Clear();
            _markers.Clear();
            foreach (var lane in _services.Layout.Layout.Lanes.Lanes)
            {
                if (lane.To != junction.Node || lane.Arrows == LaneArrows.None)
                    continue;

                var setback = math.min(MarkerSetback, lane.Path.Length * 0.5f);
                _lanes.Add(lane);
                _markers.Add(new LaneArrowMarker(lane.Path.End - lane.Path.EndDirection * setback, lane.Path.EndDirection, lane.Arrows));
            }

            return _lanes.Count;
        }

        private int MarkerAt(float2 world)
        {
            var best = -1;
            var bestDistance = MarkerPickRadius;
            for (var i = 0; i < _markers.Count; i++)
            {
                var distance = math.distance(world, _markers[i].Position);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void ChangeArrows(Lane lane)
        {
            var options = Cycle.Where(arrows => (arrows & ~lane.AvailableArrows) == 0).ToArray();
            if (options.Length < 2)
                return;

            var next = options[(Array.IndexOf(options, lane.Arrows) + 1) % options.Length];
            var painted = next == lane.DefaultArrows ? LaneArrows.None : next;
            var network = _services.Network;
            var settings = network.GetJunctionSettings(lane.To).WithArrows(lane.Segment, lane.Index, painted);
            _services.History.Execute(new ChangeJunctionCommand(network, _services.Budget, lane.To, settings, price: 0));
        }
    }
}
