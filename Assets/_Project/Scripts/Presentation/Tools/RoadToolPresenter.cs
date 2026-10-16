using System;
using System.Collections.Generic;
using TrafficGame.Core.Commands;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Localization;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>
    /// Draws roads. Straight mode: click the start, click the end. Curved mode: start, curve control point, end.
    /// After each road the next one starts where the previous ended, so roads can be chained.
    /// On touch screens nothing is shown until the first tap, since there is no hover. Free road ends are built on the
    /// selected elevation level; changing it moves the end being aimed.
    /// </summary>
    public sealed class RoadToolPresenter : IMapTool
    {
        private readonly bool _curved;
        private readonly ConstructionServices _services;
        private readonly IRoadPreviewView _preview;
        private readonly ITooltipView _tooltip;

        private RoadAnchor? _start;
        private float2? _control;
        private float2 _pointer;
        private RoadPlan _plan;
        private float2 _pendingControl;

        public RoadToolPresenter(bool curved, ConstructionServices services, IRoadPreviewView preview, ITooltipView tooltip)
        {
            _curved = curved;
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
        }

        private bool _hasPointer;

        private bool IsChoosingControl => _curved && _start.HasValue && !_control.HasValue;

        public bool CanConfirm => !_start.HasValue ? _hasPointer : IsChoosingControl || _plan is { IsValid: true };
        public bool ActsOnTap => false;

        public void Activate()
        {
            _services.Elevation.Changed += Refresh;
            Refresh();
        }

        public void Deactivate()
        {
            _services.Elevation.Changed -= Refresh;
            _start = null;
            _control = null;
            _plan = null;
            _preview.Hide();
            _tooltip.Hide();
        }

        public void PointerMoved(float2 world)
        {
            _pointer = world;
            _hasPointer = true;
            Refresh();
        }

        public void PrimaryClick(float2 world)
        {
            PointerMoved(world);

            if (!_start.HasValue)
            {
                _start = Snap(world);
            }
            else if (IsChoosingControl)
            {
                _control = _pendingControl;
            }
            else if (_plan is { IsValid: true })
            {
                var command = new BuildRoadCommand(_services.Network, _services.Budget, _plan);
                _services.History.Execute(command);
                _start = RoadAnchor.AtNode(command.EndNode, _services.Network.GetNode(command.EndNode).Position);
                _control = null;
            }

            Refresh();
        }

        public bool Cancel()
        {
            if (_control.HasValue)
                _control = null;
            else if (_start.HasValue)
                _start = null;
            else
                return false;

            Refresh();
            return true;
        }

        private void Refresh()
        {
            DropStaleStart();
            var layout = _services.Layout.Layout;

            if (!_start.HasValue)
            {
                _plan = null;
                if (!_hasPointer)
                {
                    _preview.Hide();
                    _tooltip.Hide();
                    return;
                }

                _preview.ShowAnchor(Snap(_pointer));
                _tooltip.Hide();
                return;
            }

            var start = _start.Value;
            RoadGuide guide;
            RoadAnchor end;
            if (IsChoosingControl)
            {
                // The control point is placed like the end of a straight road, so the same guides apply.
                _pendingControl = _services.Guides.Apply(start, _pointer, _services.Network, layout, out guide);
                end = RoadAnchor.Free(_pendingControl, _services.Elevation.Height);
            }
            else
            {
                end = Snap(_pointer);
                guide = RoadGuide.None;
                if (end.Kind == AnchorKind.Free && !_control.HasValue)
                    end = RoadAnchor.Free(_services.Guides.Apply(start, _pointer, _services.Network, layout, out guide), _services.Elevation.Height);
            }

            var request = new RoadBuildRequest(start, end, _control, _services.Profiles.Current);
            _plan = _services.Planner.Plan(request, layout, _services.Budget);
            _preview.ShowPlan(_plan, guide, _control);
            _tooltip.Show(end.Position, DescribePlan(_plan, guide, IsChoosingControl));
        }

        private static List<(LocalizedText, TooltipTone)> DescribePlan(RoadPlan plan, RoadGuide guide, bool choosingControl)
        {
            var lines = new List<(LocalizedText, TooltipTone)>();
            if (!choosingControl)
            {
                var costTone = plan.Error == BuildError.InsufficientFunds ? TooltipTone.Error : TooltipTone.Normal;
                lines.Add((new LocalizedText(TextKeys.Cost, plan.Cost), costTone));
            }

            if (plan.ConnectionAngle.HasValue)
                lines.Add((new LocalizedText(TextKeys.Angle, plan.ConnectionAngle.Value), TooltipTone.Normal));

            var startLevel = plan.StartHeight / RoadElevation.LevelHeight;
            var endLevel = plan.EndHeight / RoadElevation.LevelHeight;
            if (math.abs(endLevel - startLevel) > 0.01f)
                lines.Add((new LocalizedText(TextKeys.Ramp, startLevel, endLevel), TooltipTone.Normal));
            else if (endLevel > 0.01f)
                lines.Add((new LocalizedText(TextKeys.Elevated, endLevel), TooltipTone.Normal));

            var guideKey = TextKeys.Guide(guide.Kind);
            if (guideKey != null)
                lines.Add((new LocalizedText(guideKey), TooltipTone.Normal));

            var errorKey = TextKeys.Error(plan.Error);
            if (!choosingControl && errorKey != null && plan.Error != BuildError.InsufficientFunds)
                lines.Add((new LocalizedText(errorKey), TooltipTone.Error));

            return lines;
        }

        private RoadAnchor Snap(float2 world) =>
            _services.Snapper.Snap(world, _services.Network, _services.Layout.Layout, _services.Elevation.Height);

        /// <summary>Undo can delete the node or segment the road was started from.</summary>
        private void DropStaleStart()
        {
            if (!_start.HasValue)
                return;

            var start = _start.Value;
            var stale = (start.Kind == AnchorKind.Node && !_services.Network.Contains(start.Node)) ||
                        (start.Kind == AnchorKind.Segment && !_services.Network.Contains(start.Segment));
            if (!stale)
                return;

            _start = null;
            _control = null;
        }
    }
}
