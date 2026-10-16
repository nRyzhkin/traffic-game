using TrafficGame.Core.Economy;
using TrafficGame.Core.Roads;

namespace TrafficGame.Core.Construction
{
    /// <summary>Removes a segment (and nodes left empty) and refunds part of its price.</summary>
    public sealed class BulldozeCommand : NetworkEditCommand
    {
        private readonly SegmentId _segment;
        private readonly long _refund;

        public BulldozeCommand(RoadNetwork network, Budget budget, SegmentId segment, long refund) : base(network, budget)
        {
            _segment = segment;
            _refund = refund;
        }

        protected override long Price => -_refund;

        protected override void Apply() => Network.RemoveSegment(_segment);
    }
}
