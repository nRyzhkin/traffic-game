using System;
using System.Collections.Generic;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Localization;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>
    /// Places a roundabout centered on the pointer, or on the junction under it to replace that junction, on the
    /// selected elevation level: it joins only the roads at that height.
    /// </summary>
    public sealed class RoundaboutToolPresenter : IMapTool
    {
        private readonly ConstructionServices _services;
        private readonly RoundaboutPlanner _planner;
        private readonly RoadProfile _ringProfile;
        private readonly float _radius;
        private readonly IRoadPreviewView _preview;
        private readonly ITooltipView _tooltip;
        private RoundaboutPlan _plan;
        private float2? _pointer;

        public RoundaboutToolPresenter(ConstructionServices services, RoundaboutPlanner planner, RoadProfile ringProfile, float radius,
            IRoadPreviewView preview, ITooltipView tooltip)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _ringProfile = ringProfile ?? throw new ArgumentNullException(nameof(ringProfile));
            _radius = radius;
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
        }

        public bool CanConfirm => _plan is { IsValid: true };
        public bool ActsOnTap => false;

        public void Activate() => _services.Elevation.Changed += OnElevationChanged;

        public void Deactivate()
        {
            _services.Elevation.Changed -= OnElevationChanged;
            _pointer = null;
            _plan = null;
            _preview.Hide();
            _tooltip.Hide();
        }

        public void PointerMoved(float2 world)
        {
            _pointer = world;
            var layout = _services.Layout.Layout;
            var height = _services.Elevation.Height;
            var anchor = _services.Snapper.Snap(world, _services.Network, layout, height);
            var center = anchor.Kind == AnchorKind.Node ? anchor.Position : world;

            _plan = _planner.Plan(center, _radius, _ringProfile, layout, _services.Budget, height);
            _preview.ShowRoundabout(_plan);

            var costTone = _plan.Error == BuildError.InsufficientFunds ? TooltipTone.Error : TooltipTone.Normal;
            var lines = new List<(LocalizedText, TooltipTone)> { (new LocalizedText(TextKeys.Cost, _plan.Cost), costTone) };
            var errorKey = TextKeys.Error(_plan.Error);
            if (errorKey != null && _plan.Error != BuildError.InsufficientFunds)
                lines.Add((new LocalizedText(errorKey), TooltipTone.Error));
            _tooltip.Show(center + new float2(_radius, 0f), lines);
        }

        public void PrimaryClick(float2 world)
        {
            PointerMoved(world);
            if (!CanConfirm)
                return;

            _services.History.Execute(new BuildRoundaboutCommand(_services.Network, _services.Budget, _plan));
            PointerMoved(world);
        }

        public bool Cancel() => false;

        private void OnElevationChanged()
        {
            if (_pointer.HasValue)
                PointerMoved(_pointer.Value);
        }
    }
}
