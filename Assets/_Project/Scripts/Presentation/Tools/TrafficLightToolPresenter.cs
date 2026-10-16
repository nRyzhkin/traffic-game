using System;
using System.Collections.Generic;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Localization;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>
    /// Steps the junction under the pointer through its control options: traffic lights, then lights with protected
    /// left turns, then back to signs (with a partial refund).
    /// </summary>
    public sealed class TrafficLightToolPresenter : IMapTool
    {
        private readonly ConstructionServices _services;
        private readonly long _cost;
        private readonly float _refundRatio;
        private readonly IJunctionPreviewView _preview;
        private readonly ITooltipView _tooltip;
        private NodeId? _target;
        private JunctionSettings _next;
        private long _price;

        public TrafficLightToolPresenter(ConstructionServices services, long cost, float refundRatio, IJunctionPreviewView preview,
            ITooltipView tooltip)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _cost = cost;
            _refundRatio = math.saturate(refundRatio);
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
        }

        public bool CanConfirm => _target.HasValue;
        public bool ActsOnTap => false;

        public void Activate() { }

        public void Deactivate()
        {
            _target = null;
            _preview.Hide();
            _tooltip.Hide();
        }

        public void PointerMoved(float2 world)
        {
            var network = _services.Network;
            if (!_services.Snapper.TryPickJunction(world, _services.Layout.Layout, out var junction))
            {
                Deactivate();
                return;
            }

            _target = null;
            if (!JunctionRules.CanHaveSignals(junction, network))
            {
                _preview.ShowJunction(junction, valid: false);
                _tooltip.Show(junction.Center, new[] { (new LocalizedText(TextKeys.ErrorRoundaboutLights), TooltipTone.Error) });
                return;
            }

            var settings = network.GetJunctionSettings(junction.Node);
            string key;
            if (settings.Control != JunctionControl.TrafficLights)
            {
                key = TextKeys.AddLights;
                _next = settings.WithControl(JunctionControl.TrafficLights, protectedLeft: false);
                _price = _cost;
            }
            else if (!settings.ProtectedLeft)
            {
                key = TextKeys.ProtectedLeft;
                _next = settings.WithControl(JunctionControl.TrafficLights, protectedLeft: true);
                _price = 0;
            }
            else
            {
                key = TextKeys.RemoveLights;
                _next = settings.WithControl(JunctionControl.Auto, protectedLeft: false);
                _price = -(long)math.floor(_cost * _refundRatio);
            }

            var affordable = _price <= 0 || _services.Budget.CanAfford(_price);
            var lines = new List<(LocalizedText, TooltipTone)> { (new LocalizedText(key), TooltipTone.Normal) };
            if (_price > 0)
                lines.Add((new LocalizedText(TextKeys.Cost, _price), affordable ? TooltipTone.Normal : TooltipTone.Error));
            else if (_price < 0)
                lines.Add((new LocalizedText(TextKeys.Refund, -_price), TooltipTone.Normal));

            _preview.ShowJunction(junction, affordable);
            _tooltip.Show(junction.Center, lines);
            if (affordable)
                _target = junction.Node;
        }

        public void PrimaryClick(float2 world)
        {
            PointerMoved(world);
            if (!_target.HasValue)
                return;

            _services.History.Execute(new ChangeJunctionCommand(_services.Network, _services.Budget, _target.Value, _next, _price));
            PointerMoved(world);
        }

        public bool Cancel() => false;
    }
}
