using System;

namespace TrafficGame.Core.Roads
{
    /// <summary>
    /// Cross-section of a road. Traffic drives on the right: forward lanes (from the segment start to its end)
    /// lie right of the divider, backward lanes left of it. Lane index 0 is the lane next to the divider,
    /// i.e. the leftmost lane in its direction of travel. Lateral offsets are measured to the left of the
    /// segment's centerline.
    /// </summary>
    public sealed class RoadProfile
    {
        public RoadProfile(string id, int forwardLanes, int backwardLanes, float laneWidth, float shoulderWidth, float costPerMeter, float speedLimit)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Profile id is required.", nameof(id));
            if (forwardLanes < 1)
                throw new ArgumentOutOfRangeException(nameof(forwardLanes), forwardLanes, "A road needs at least one forward lane.");
            if (backwardLanes < 0)
                throw new ArgumentOutOfRangeException(nameof(backwardLanes), backwardLanes, "Lane count cannot be negative.");
            if (laneWidth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(laneWidth), laneWidth, "Lane width must be positive.");
            if (shoulderWidth < 0f)
                throw new ArgumentOutOfRangeException(nameof(shoulderWidth), shoulderWidth, "Shoulder width cannot be negative.");
            if (costPerMeter < 0f)
                throw new ArgumentOutOfRangeException(nameof(costPerMeter), costPerMeter, "Cost cannot be negative.");
            if (speedLimit <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speedLimit), speedLimit, "Speed limit must be positive.");

            Id = id;
            ForwardLanes = forwardLanes;
            BackwardLanes = backwardLanes;
            LaneWidth = laneWidth;
            ShoulderWidth = shoulderWidth;
            CostPerMeter = costPerMeter;
            SpeedLimit = speedLimit;
        }

        public string Id { get; }
        public int ForwardLanes { get; }
        public int BackwardLanes { get; }
        public float LaneWidth { get; }
        public float ShoulderWidth { get; }

        /// <summary>Construction price per meter of centerline, in dollars.</summary>
        public float CostPerMeter { get; }

        /// <summary>Speed limit in meters per second.</summary>
        public float SpeedLimit { get; }

        public bool IsOneWay => BackwardLanes == 0;
        public int LaneCount => ForwardLanes + BackwardLanes;
        public float HalfWidth => LaneCount * LaneWidth * 0.5f + ShoulderWidth;

        /// <summary>Lateral offset of the line separating the two directions (or of the left lane edge on one-way roads).</summary>
        public float DividerOffset => LaneCount * LaneWidth * 0.5f - BackwardLanes * LaneWidth;

        /// <summary>Price of a road of this profile with the given centerline length, rounded up to whole dollars.</summary>
        public long CostOf(float length) => (long)Math.Ceiling(length * CostPerMeter);

        public int LaneCountIn(bool forward) => forward ? ForwardLanes : BackwardLanes;

        /// <summary>Lateral offset of the center of a lane.</summary>
        public float LaneCenterOffset(bool forward, int index) =>
            DividerOffset + (forward ? -1f : 1f) * (index + 0.5f) * LaneWidth;

        /// <summary>Lateral offset of the boundary between lane <paramref name="index"/> - 1 and <paramref name="index"/> of one direction.</summary>
        public float LaneBoundaryOffset(bool forward, int index) =>
            DividerOffset + (forward ? -1f : 1f) * index * LaneWidth;
    }
}
