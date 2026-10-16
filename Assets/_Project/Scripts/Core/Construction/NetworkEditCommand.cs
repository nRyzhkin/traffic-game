using System;
using TrafficGame.Core.Commands;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Roads;

namespace TrafficGame.Core.Construction
{
    /// <summary>
    /// Base for commands that edit the road network and move money. The first execution performs the edit;
    /// undo and redo restore snapshots taken around it, so ids stay identical across redo.
    /// </summary>
    public abstract class NetworkEditCommand : ICommand
    {
        private RoadNetworkSnapshot _before;
        private RoadNetworkSnapshot _after;

        protected NetworkEditCommand(RoadNetwork network, Budget budget)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
        }

        protected RoadNetwork Network { get; }
        protected Budget Budget { get; }

        /// <summary>Money taken by the edit; negative values are refunds.</summary>
        protected abstract long Price { get; }

        public void Execute()
        {
            if (_after == null)
            {
                _before = Network.CreateSnapshot();
                using (Network.DeferChanges())
                    Apply();
                _after = Network.CreateSnapshot();
            }
            else
            {
                Network.Restore(_after);
            }

            Charge(Price);
        }

        public void Undo()
        {
            if (_before == null)
                throw new InvalidOperationException("Cannot undo a command that was never executed.");

            Network.Restore(_before);
            Charge(-Price);
        }

        /// <summary>Performs the edit on <see cref="Network"/>; called once.</summary>
        protected abstract void Apply();

        private void Charge(long amount)
        {
            if (amount > 0)
                Budget.Spend(amount);
            else if (amount < 0)
                Budget.Refund(-amount);
        }
    }
}
