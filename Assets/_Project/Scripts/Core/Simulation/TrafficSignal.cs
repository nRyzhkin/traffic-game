using System.Collections.Generic;
using TrafficGame.Core.Roads;

namespace TrafficGame.Core.Simulation
{
    /// <summary>What a signal shows to one junction connection.</summary>
    public enum SignalState
    {
        Red,
        Yellow,
        Green
    }

    /// <summary>The part of a signal cycle a phase is in.</summary>
    public enum SignalInterval
    {
        Green,
        Yellow,

        /// <summary>Everyone red for a moment so the junction clears before the next phase.</summary>
        AllRed
    }

    /// <summary>
    /// Actuated traffic lights of one junction, after common US practice: phases run in a fixed order, a phase
    /// without waiting vehicles is skipped, green lasts at least <see cref="MinGreen"/> and is extended while
    /// vehicles keep arriving, up to <see cref="MaxGreen"/> when others wait. With nobody waiting elsewhere
    /// the green rests where it is. Which connections go in which phase is set by <see cref="TrafficLink.PhaseMask"/>.
    /// </summary>
    public sealed class TrafficSignal
    {
        public const float MinGreen = 6f;
        public const float MaxGreen = 30f;
        public const float YellowTime = 3f;
        public const float AllRedTime = 1.5f;

        /// <summary>Vehicles this close to the stop line keep their green going.</summary>
        public const float DetectionDistance = 30f;

        private const int VehiclesCheckedForDemand = 3;

        private readonly List<TrafficLink> _lanes;

        internal TrafficSignal(NodeId node, List<TrafficLink> lanes, int phaseCount)
        {
            Node = node;
            _lanes = lanes;
            PhaseCount = phaseCount;
        }

        public NodeId Node { get; }

        /// <summary>The lanes arriving at the junction.</summary>
        public IReadOnlyList<TrafficLink> Lanes => _lanes;

        public int PhaseCount { get; }
        public int Phase { get; private set; }
        public SignalInterval Interval { get; private set; }

        /// <summary>Seconds since the current interval began.</summary>
        public float Elapsed { get; private set; }

        public SignalState StateOf(TrafficLink connection)
        {
            if (Interval == SignalInterval.AllRed || !connection.GoesIn(Phase))
                return SignalState.Red;
            return Interval == SignalInterval.Green ? SignalState.Green : SignalState.Yellow;
        }

        /// <summary>The most permissive state among the lane's connections, as its signal head shows it.</summary>
        public SignalState StateOfLane(TrafficLink lane)
        {
            var best = SignalState.Red;
            foreach (var connection in lane.Outgoing)
            {
                var state = StateOf(connection);
                if (state > best)
                    best = state;
            }

            return best;
        }

        /// <summary>Advances the timing; returns true when a green has just turned yellow.</summary>
        internal bool Update(float deltaTime)
        {
            Elapsed += deltaTime;
            switch (Interval)
            {
                case SignalInterval.Green:
                    if (Elapsed < MinGreen)
                        return false;
                    if (NextPhaseWithDemand() < 0)
                        return false; // nobody waits elsewhere: rest in green
                    if (Elapsed < MaxGreen && HasArrivingTraffic(Phase))
                        return false;
                    Begin(SignalInterval.Yellow);
                    return true;

                case SignalInterval.Yellow:
                    if (Elapsed >= YellowTime)
                        Begin(SignalInterval.AllRed);
                    return false;

                default:
                    if (Elapsed < AllRedTime)
                        return false;
                    var next = NextPhaseWithDemand();
                    Phase = next >= 0 ? next : (Phase + 1) % PhaseCount;
                    Begin(SignalInterval.Green);
                    return false;
            }
        }

        private void Begin(SignalInterval interval)
        {
            Interval = interval;
            Elapsed = 0f;
        }

        /// <summary>The first phase after the current one, in cycle order, that vehicles are waiting for; -1 if none.</summary>
        private int NextPhaseWithDemand()
        {
            for (var offset = 1; offset < PhaseCount; offset++)
            {
                var phase = (Phase + offset) % PhaseCount;
                if (HasDemand(phase))
                    return phase;
            }

            return -1;
        }

        private bool HasDemand(int phase)
        {
            foreach (var lane in _lanes)
            {
                var vehicles = lane.Vehicles;
                for (var i = 0; i < vehicles.Count && i < VehiclesCheckedForDemand; i++)
                {
                    var connection = vehicles[i].IntendedConnection();
                    if (connection != null && connection.GoesIn(phase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Whether uncommitted vehicles close to the stop line still want to go in this phase.</summary>
        private bool HasArrivingTraffic(int phase)
        {
            foreach (var lane in _lanes)
            {
                foreach (var vehicle in lane.Vehicles)
                {
                    if (lane.Length - vehicle.Distance > DetectionDistance)
                        break;
                    if (vehicle.Committed)
                        continue;

                    var connection = vehicle.IntendedConnection();
                    if (connection != null && connection.GoesIn(phase))
                        return true;
                }
            }

            return false;
        }
    }
}
