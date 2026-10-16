using System;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Localization;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>Removes the road under the pointer and refunds part of its price.</summary>
    public sealed class BulldozeToolPresenter : IMapTool
    {
        private readonly ConstructionServices _services;
        private readonly float _refundRatio;
        private readonly IRoadPreviewView _preview;
        private readonly ITooltipView _tooltip;
        private SegmentId? _target;
        private long _refund;

        public BulldozeToolPresenter(ConstructionServices services, float refundRatio, IRoadPreviewView preview, ITooltipView tooltip)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _refundRatio = math.saturate(refundRatio);
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
        }

        public void Activate() { }

        public void Deactivate()
        {
            _target = null;
            _preview.Hide();
            _tooltip.Hide();
        }

        public void PointerMoved(float2 world)
        {
            var layout = _services.Layout.Layout;
            if (!_services.Snapper.TryPickSegment(world, layout, out var id))
            {
                Deactivate();
                return;
            }

            var geometry = layout.Segments[id];
            _preview.ShowRemoval(geometry);
            if (geometry.Segment.IsLocked)
            {
                _target = null;
                _tooltip.Show(world, new[] { (new LocalizedText(TextKeys.ErrorLocked), TooltipTone.Error) });
                return;
            }

            _target = id;
            _refund = (long)math.floor(geometry.Segment.Profile.CostOf(geometry.Centerline.Length) * _refundRatio);
            _tooltip.Show(world, new[] { (new LocalizedText(TextKeys.Refund, _refund), TooltipTone.Normal) });
        }

        public void PrimaryClick(float2 world)
        {
            PointerMoved(world);
            if (!_target.HasValue)
                return;

            _services.History.Execute(new BulldozeCommand(_services.Network, _services.Budget, _target.Value, _refund));
            PointerMoved(world);
        }

        public bool Cancel() => false;

        public bool CanConfirm => _target.HasValue;
        public bool ActsOnTap => false;
    }
}
