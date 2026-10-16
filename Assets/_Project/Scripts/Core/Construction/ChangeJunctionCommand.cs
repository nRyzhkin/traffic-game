using System;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Roads;

namespace TrafficGame.Core.Construction
{
    /// <summary>Replaces a junction's settings (traffic lights, lane arrows) and charges or refunds the difference.</summary>
    public sealed class ChangeJunctionCommand : NetworkEditCommand
    {
        private readonly NodeId _node;
        private readonly JunctionSettings _settings;
        private readonly long _price;

        /// <param name="price">Money taken; negative for a refund.</param>
        public ChangeJunctionCommand(RoadNetwork network, Budget budget, NodeId node, JunctionSettings settings, long price) : base(network, budget)
        {
            _node = node;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _price = price;
        }

        protected override long Price => _price;

        protected override void Apply() => Network.SetJunctionSettings(_node, _settings);
    }
}
