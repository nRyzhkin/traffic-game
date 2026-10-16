using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.City;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using Unity.Mathematics;

namespace TrafficGame.Core.Levels
{
    /// <summary>A loaded level: its road network plus buildings, through traffic, goal and starting conditions.</summary>
    public sealed class Level
    {
        internal Level(string id, string titleKey, string introKey, string hintKey, RoadNetwork network, IReadOnlyList<Building> buildings,
            IReadOnlyList<(NodeId node, float tripsPerMinute)> edges, LevelObjective objective, long budget, float2? cameraCenter, float? cameraHalfHeight,
            float warmupSeconds)
        {
            WarmupSeconds = warmupSeconds;
            Id = id;
            TitleKey = titleKey;
            IntroKey = introKey;
            HintKey = hintKey;
            Network = network;
            Buildings = buildings;
            Edges = edges;
            Objective = objective;
            Budget = budget;
            CameraCenter = cameraCenter;
            CameraHalfHeight = cameraHalfHeight;
        }

        public string Id { get; }
        public string TitleKey { get; }
        public string IntroKey { get; }

        /// <summary>A tip shown on the task card, typically about the tool the level introduces; may be null.</summary>
        public string HintKey { get; }
        public RoadNetwork Network { get; }
        public IReadOnlyList<Building> Buildings { get; }
        public IReadOnlyList<(NodeId node, float tripsPerMinute)> Edges { get; }

        /// <summary>Null in a sandbox.</summary>
        public LevelObjective Objective { get; }

        /// <summary>Starting money; 0 means the game default.</summary>
        public long Budget { get; }

        public float2? CameraCenter { get; }
        public float? CameraHalfHeight { get; }

        /// <summary>Seconds of traffic simulated before the player sees the level.</summary>
        public float WarmupSeconds { get; }

        /// <summary>
        /// The rectangle the player has to see at the start: every building, with some room around it.
        /// False when the level has no buildings (the sandbox).
        /// </summary>
        public bool TryGetFocusBounds(float margin, out float2 min, out float2 max)
        {
            min = new float2(float.MaxValue);
            max = new float2(float.MinValue);
            foreach (var building in Buildings)
            {
                var half = math.length(building.Size) * 0.5f;
                min = math.min(min, building.Position - half);
                max = math.max(max, building.Position + half);
            }

            if (Buildings.Count == 0)
                return false;

            min -= margin;
            max += margin;
            return true;
        }

        /// <summary>Doors of buildings that trips can end at; only those trips count towards the goal.</summary>
        public IEnumerable<NodeId> DestinationDoors => Buildings.Where(building => building.IsDestination).Select(building => building.Door);

        /// <summary>Houses send trips to every destination building; map edges send through traffic to each other.</summary>
        public IReadOnlyList<TripSource> CreateTripSources()
        {
            var destinations = DestinationDoors.ToArray();
            var edges = Edges.Select(edge => edge.node).ToArray();

            return Buildings
                .Where(building => building.IsOrigin)
                .Select(house => new TripSource(house.Door, house.TripsPerMinute, destinations))
                .Concat(Edges.Select(edge => new TripSource(edge.node, edge.tripsPerMinute, edges.Where(other => other != edge.node).ToArray())))
                .ToArray();
        }
    }
}
