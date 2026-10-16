using System;

namespace TrafficGame.Core.Simulation
{
    public enum VehicleKind
    {
        Car,
        Truck
    }

    /// <summary>Size and driving behaviour of a vehicle class (Intelligent Driver Model parameters). SI units.</summary>
    public sealed class VehicleSpec
    {
        public VehicleSpec(VehicleKind kind, float length, float width, float maxAcceleration, float comfortableDeceleration,
            float timeHeadway, float minGap)
        {
            if (length <= 0f || width <= 0f || maxAcceleration <= 0f || comfortableDeceleration <= 0f || timeHeadway <= 0f || minGap < 0f)
                throw new ArgumentException("Vehicle parameters must be positive.");

            Kind = kind;
            Length = length;
            Width = width;
            MaxAcceleration = maxAcceleration;
            ComfortableDeceleration = comfortableDeceleration;
            TimeHeadway = timeHeadway;
            MinGap = minGap;
        }

        public VehicleKind Kind { get; }
        public float Length { get; }
        public float Width { get; }
        public float MaxAcceleration { get; }
        public float ComfortableDeceleration { get; }

        /// <summary>Desired time gap to the vehicle ahead, seconds.</summary>
        public float TimeHeadway { get; }

        /// <summary>Bumper-to-bumper gap kept when standing, meters.</summary>
        public float MinGap { get; }
    }

    /// <summary>Tuning of the traffic simulation. SI units.</summary>
    public sealed class TrafficSettings
    {
        public VehicleSpec Car { get; set; } = new(VehicleKind.Car, 4.5f, 1.9f, 2.0f, 3.0f, 1.2f, 2.0f);
        public VehicleSpec Truck { get; set; } = new(VehicleKind.Truck, 9f, 2.5f, 1.0f, 2.5f, 1.6f, 2.5f);

        /// <summary>Share of spawned vehicles that are trucks, 0–1.</summary>
        public float TruckShare { get; set; } = 0.1f;

        /// <summary>Lateral acceleration drivers accept in turns; sets the speed through junctions.</summary>
        public float ComfortableLateralAcceleration { get; set; } = 2.5f;

        /// <summary>Lowest speed allowed through a junction, however tight the turn.</summary>
        public float MinTurnSpeed { get; set; } = 4f;

        /// <summary>A yielding driver enters only if no priority vehicle can reach the conflict sooner than this, seconds.</summary>
        public float CriticalGap { get; set; } = 4f;

        /// <summary>After waiting this long a driver accepts any gap that is physically free (prevents priority deadlocks).</summary>
        public float MaxPatience { get; set; } = 15f;

        /// <summary>A vehicle standing still this long is removed (gridlock safety valve in the sandbox).</summary>
        public float GridlockTimeout { get; set; } = 120f;

        /// <summary>How often vehicles look for a better route, seconds.</summary>
        public float RerouteInterval { get; set; } = 20f;

        /// <summary>How far ahead (along the route) drivers look for the next vehicle, meters.</summary>
        public float LookAhead { get; set; } = 120f;

        /// <summary>Duration of the sideways move when changing lanes, seconds (visual only).</summary>
        public float LaneChangeDuration { get; set; } = 1.5f;

        public int MaxVehicles { get; set; } = 4000;
        public int RandomSeed { get; set; } = 12345;
    }
}
