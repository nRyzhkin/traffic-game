using System;
using System.Collections.Generic;
using TrafficGame.Core.Roads;
using Unity.Mathematics;
using Random = System.Random;

namespace TrafficGame.Core.Simulation
{
    /// <summary>A place that starts trips at a steady rate towards one of its destinations.</summary>
    public sealed class TripSource
    {
        public TripSource(NodeId origin, float tripsPerMinute, IReadOnlyList<NodeId> destinations)
        {
            Origin = origin;
            TripsPerMinute = tripsPerMinute;
            Destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        }

        public NodeId Origin { get; }
        public float TripsPerMinute { get; }
        public IReadOnlyList<NodeId> Destinations { get; }
    }

    /// <summary>
    /// Starts trips from sources with exponentially distributed gaps (a Poisson process). A trip that cannot start —
    /// no free room at the door or no route yet — is retried on the next step, so unconnected buildings simply wait.
    /// </summary>
    public sealed class TripGenerator
    {
        private readonly IReadOnlyList<TripSource> _sources;
        private readonly float _truckShare;
        private readonly Random _random;
        private readonly float[] _nextTrip;
        private float _time;

        public TripGenerator(IReadOnlyList<TripSource> sources, float truckShare, int seed)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            _truckShare = math.saturate(truckShare);
            _random = new Random(seed);
            _nextTrip = new float[sources.Count];
            for (var i = 0; i < sources.Count; i++)
                _nextTrip[i] = NextGap(sources[i]);
        }

        public IReadOnlyList<TripSource> Sources => _sources;

        public void Update(TrafficSimulation simulation, float deltaTime)
        {
            _time += deltaTime;
            for (var i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (_time < _nextTrip[i] || source.Destinations.Count == 0 || source.TripsPerMinute <= 0f)
                    continue;

                var destination = source.Destinations[_random.Next(source.Destinations.Count)];
                if (destination == source.Origin)
                    continue;

                var kind = _random.NextDouble() < _truckShare ? VehicleKind.Truck : VehicleKind.Car;
                if (simulation.TrySpawn(source.Origin, destination, kind))
                    _nextTrip[i] = _time + NextGap(source);
            }
        }

        private float NextGap(TripSource source) =>
            source.TripsPerMinute <= 0f ? float.MaxValue : -60f / source.TripsPerMinute * math.log(1f - (float)_random.NextDouble() * 0.999f);
    }
}
