using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.City;
using TrafficGame.Presentation.Simulation;

namespace TrafficGame.Presentation.City
{
    /// <summary>Draws buildings; ones no trip can use yet get a warning badge.</summary>
    public interface IBuildingsView
    {
        void Render(IReadOnlyList<Building> buildings, IReadOnlyCollection<int> disconnected);
    }

    /// <summary>
    /// Tells the player which buildings still need a road: a house is connected when it can reach any destination,
    /// a destination when any house can reach it. Re-evaluated whenever the roads change.
    /// </summary>
    public sealed class BuildingsPresenter : IDisposable
    {
        private readonly IReadOnlyList<Building> _buildings;
        private readonly SimulationPresenter _simulation;
        private readonly IBuildingsView _view;

        public BuildingsPresenter(IReadOnlyList<Building> buildings, SimulationPresenter simulation, IBuildingsView view)
        {
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _view = view ?? throw new ArgumentNullException(nameof(view));

            _simulation.NetworkRebuilt += Refresh;
            Refresh();
        }

        /// <summary>Ids of buildings not yet usable by any trip.</summary>
        public IReadOnlyCollection<int> Disconnected { get; private set; } = Array.Empty<int>();

        public void Dispose() => _simulation.NetworkRebuilt -= Refresh;

        private void Refresh()
        {
            var network = _simulation.Simulation.Network;
            var houses = _buildings.Where(building => building.IsOrigin).ToArray();
            var destinations = _buildings.Where(building => building.IsDestination).ToArray();

            Disconnected = _buildings
                .Where(building => building.IsOrigin
                    ? !destinations.Any(destination => network.CanReach(building.Door, destination.Door))
                    : !houses.Any(house => network.CanReach(house.Door, building.Door)))
                .Select(building => building.Id)
                .ToArray();

            _view.Render(_buildings, Disconnected);
        }
    }
}
