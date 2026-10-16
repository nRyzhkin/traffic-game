using System.Collections.Generic;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Simulation
{
    /// <summary>State of one simulated vehicle. Instances are pooled by <see cref="TrafficSimulation"/>.</summary>
    internal sealed class Vehicle
    {
        public int Id;
        public VehicleSpec Spec;
        public float SpeedFactor;
        public int ColorIndex;

        public TrafficLink Link;

        /// <summary>The link driven before <see cref="Link"/>; the rear of the vehicle may still be on it.</summary>
        public TrafficLink PreviousLink;

        /// <summary>Position of the front bumper along <see cref="Link"/>, meters.</summary>
        public float Distance;

        public float Speed;
        public float Acceleration;

        /// <summary>Roads to drive; <see cref="RouteIndex"/> is the road of the current lane (or of the lane a connection leaves).</summary>
        public readonly List<RoadInfo> Route = new();
        public int RouteIndex;
        public NodeId Origin;
        public NodeId Destination;

        /// <summary>The connection chosen at the end of the current lane.</summary>
        public TrafficLink Next;

        /// <summary>Allowed into the junction ahead; holds a reservation on <see cref="Next"/>.</summary>
        public bool Committed;

        public float SpawnTime;
        public float StoppedAt;
        public float WaitingSince;
        public float StandingSince;
        public float NextReroute;

        /// <summary>Visual sideways offset left over from a lane change; positive is left. Decays to zero.</summary>
        public float LateralOffset;

        /// <summary>World positions of the bumpers after the previous and the latest step; drawing interpolates between them.</summary>
        public float2 PreviousFront, PreviousRear, Front, Rear;

        public RoadInfo CurrentRoad => Route[RouteIndex];
        public bool IsOnLastRoad => RouteIndex == Route.Count - 1;

        /// <summary>The connection the vehicle is going to take at the end of its lane: chosen, or the first towards its route.</summary>
        public TrafficLink IntendedConnection()
        {
            if (Next != null)
                return Next;
            if (IsOnLastRoad)
                return null;

            var nextRoad = Route[RouteIndex + 1];
            foreach (var connection in Link.Outgoing)
            {
                if (connection.To.RoadInfo == nextRoad)
                    return connection;
            }

            return null;
        }

        public void ResetJunctionState()
        {
            Next = null;
            Committed = false;
            StoppedAt = -1f;
            WaitingSince = -1f;
        }
    }
}
