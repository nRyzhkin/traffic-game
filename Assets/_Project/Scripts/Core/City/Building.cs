using System;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.City
{
    public enum BuildingKind
    {
        /// <summary>Where trips start.</summary>
        House,

        /// <summary>Trip destination.</summary>
        Shop,

        /// <summary>Trip destination.</summary>
        Office
    }

    /// <summary>A building on the map. Its door is the dead end of its driveway, where its cars appear or arrive.</summary>
    public sealed class Building
    {
        public Building(int id, BuildingKind kind, float2 position, float rotation, NodeId door, float tripsPerMinute)
        {
            if (tripsPerMinute < 0f)
                throw new ArgumentOutOfRangeException(nameof(tripsPerMinute), tripsPerMinute, "Trip rate cannot be negative.");

            Id = id;
            Kind = kind;
            Position = position;
            Rotation = rotation;
            Door = door;
            TripsPerMinute = tripsPerMinute;
        }

        public int Id { get; }
        public BuildingKind Kind { get; }

        /// <summary>Center of the footprint.</summary>
        public float2 Position { get; }

        /// <summary>Counter-clockwise rotation of the footprint, radians.</summary>
        public float Rotation { get; }

        public NodeId Door { get; }

        /// <summary>Trips the building starts per minute (houses only).</summary>
        public float TripsPerMinute { get; }

        public bool IsOrigin => Kind == BuildingKind.House;
        public bool IsDestination => Kind != BuildingKind.House;

        /// <summary>Footprint in meters: width along the rotated X axis, depth along Y.</summary>
        public float2 Size => SizeOf(Kind);

        public static float2 SizeOf(BuildingKind kind) => kind switch
        {
            BuildingKind.House => new float2(12f, 10f),
            BuildingKind.Shop => new float2(30f, 22f),
            _ => new float2(22f, 22f)
        };

        public static float DefaultTripsPerMinute(BuildingKind kind) => kind == BuildingKind.House ? 3f : 0f;
    }
}
