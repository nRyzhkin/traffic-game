using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using Unity.Mathematics;
using Random = System.Random;

namespace TrafficGame.Core.Simulation
{
    /// <summary>What the view needs to draw one vehicle.</summary>
    public readonly struct VehicleSnapshot
    {
        public VehicleSnapshot(int id, float2 position, float height, float2 direction, float length, float width, VehicleKind kind, int colorIndex)
        {
            Id = id;
            Height = height;
            Position = position;
            Direction = direction;
            Length = length;
            Width = width;
            Kind = kind;
            ColorIndex = colorIndex;
        }

        /// <summary>Unique for the lifetime of the simulation; lets views follow a vehicle from frame to frame.</summary>
        public int Id { get; }

        /// <summary>Center of the vehicle.</summary>
        public float2 Position { get; }

        /// <summary>Meters above the ground (on bridges and ramps).</summary>
        public float Height { get; }

        public float2 Direction { get; }
        public float Length { get; }
        public float Width { get; }
        public VehicleKind Kind { get; }
        public int ColorIndex { get; }
    }

    /// <summary>How jammed a lane is, 0 (free flow) to 1 (standing).</summary>
    public readonly struct LaneCongestion
    {
        public LaneCongestion(Polyline path, float level, float height = 0f)
        {
            Path = path;
            Level = level;
            Height = height;
        }

        public Polyline Path { get; }
        public float Level { get; }

        /// <summary>Average height of the lane, meters.</summary>
        public float Height { get; }
    }

    public sealed class TrafficStatistics
    {
        public int Spawned { get; internal set; }
        public int CompletedTrips { get; internal set; }
        public int RemovedStuck { get; internal set; }

        /// <summary>Mean duration of completed trips, seconds.</summary>
        public float AverageTripSeconds { get; internal set; }
    }

    /// <summary>
    /// Microscopic traffic simulation. Vehicles follow each other with the Intelligent Driver Model, change lanes
    /// to reach their turns, obey junction right of way (<see cref="JunctionRules"/>) and route with A* that
    /// accounts for congestion. Driven by fixed-size <see cref="Step"/> calls; deterministic for a given seed.
    /// </summary>
    public sealed class TrafficSimulation
    {
        public const int ColorCount = 8;

        private const float StopLineMargin = 0.5f;
        private const float StoppedSpeed = 0.3f;
        private const float StopSignZone = 4f;
        private const float DecisionMargin = 15f;
        private const float EmergencyDeceleration = 9f;
        private const float SpeedSmoothing = 0.2f;
        private const float SpeedSampleInterval = 1f;
        private const float LaneChangeInterval = 0.5f;
        private const float MinLaneChangeRoom = 5f;
        private const float NoLaneChangeZone = 10f;
        private const int ApproachingVehiclesChecked = 3;

        /// <summary>How fast acceleration may build up (m/s³); braking is never delayed.</summary>
        private const float MaxJerk = 4f;

        /// <summary>Share of the comfortable deceleration used to slow down for a turn.</summary>
        private const float TurnBrakingShare = 0.6f;

        private readonly TrafficSettings _settings;
        private float _tripsTimedFrom;
        private readonly Random _random;
        private readonly List<Vehicle> _vehicles = new();
        private readonly Stack<Vehicle> _pool = new();
        private readonly List<Vehicle> _finished = new();
        private readonly List<RoadInfo> _routeBuffer = new();

        private TrafficNetwork _network;
        private Router _router;
        private int _nextVehicleId;
        private float _nextSpeedSample;

        public TrafficSimulation(TrafficNetwork network, TrafficSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _random = new Random(settings.RandomSeed);
            Rebuild(network ?? throw new ArgumentNullException(nameof(network)));
        }

        /// <summary>Simulated seconds since the start.</summary>
        public float Time { get; private set; }

        public int VehicleCount => _vehicles.Count;

        internal IReadOnlyList<Vehicle> Vehicles => _vehicles;

        public TrafficNetwork Network => _network;
        public TrafficStatistics Statistics { get; } = new();

        /// <summary>
        /// Starts the statistics afresh, for instance after a warm-up. Vehicles already on their way are timed from
        /// now, so waiting that happened before does not count.
        /// </summary>
        public void ResetStatistics()
        {
            Statistics.Spawned = 0;
            Statistics.CompletedTrips = 0;
            Statistics.RemovedStuck = 0;
            Statistics.AverageTripSeconds = 0f;
            _tripsTimedFrom = Time;
        }

        /// <summary>Raised when a vehicle reaches its destination: origin, destination, trip duration in seconds.</summary>
        public event Action<NodeId, NodeId, float> TripCompleted;

        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Time += deltaTime;
            SampleRoadSpeeds();
            UpdateSignals(deltaTime);
            ComputeAccelerations(deltaTime);
            Integrate(deltaTime);
            ChangeLanesAndReroute(deltaTime);
            RemoveFinished();
            UpdatePoses(snap: false);
        }

        /// <summary>Starts a trip from the dead end <paramref name="entry"/> to <paramref name="exit"/>; false when there is no room or no route.</summary>
        public bool TrySpawn(NodeId entry, NodeId exit, VehicleKind kind)
        {
            if (_vehicles.Count >= _settings.MaxVehicles)
                return false;

            var spec = kind == VehicleKind.Truck ? _settings.Truck : _settings.Car;
            foreach (var road in _network.Roads)
            {
                if (road.From != entry)
                    continue;

                var lane = road.Lanes[_random.Next(road.Lanes.Count)];
                if (lane.Vehicles.Count > 0 && RearOf(lane.Vehicles[lane.Vehicles.Count - 1]) < spec.Length + spec.MinGap + 2f)
                    continue;
                if (IsBeingEntered(lane))
                    continue;
                if (!_router.TryFindRoute(road, exit, _routeBuffer))
                    continue;

                var vehicle = _pool.Count > 0 ? _pool.Pop() : new Vehicle();
                vehicle.Id = _nextVehicleId++;
                vehicle.Spec = spec;
                vehicle.SpeedFactor = 0.9f + (float)_random.NextDouble() * 0.2f;
                vehicle.ColorIndex = _random.Next(ColorCount);
                vehicle.Route.Clear();
                vehicle.Route.AddRange(_routeBuffer);
                vehicle.RouteIndex = 0;
                vehicle.Origin = entry;
                vehicle.Destination = exit;
                vehicle.SpawnTime = Time;
                vehicle.StandingSince = -1f;
                vehicle.NextReroute = Time + _settings.RerouteInterval;
                vehicle.LateralOffset = 0f;
                vehicle.Acceleration = 0f;
                vehicle.ResetJunctionState();
                vehicle.PreviousLink = null;
                vehicle.Link = lane;
                vehicle.Distance = spec.Length;
                vehicle.Speed = lane.Vehicles.Count > 0 ? lane.Vehicles[lane.Vehicles.Count - 1].Speed : lane.SpeedLimit * 0.5f;
                Insert(vehicle, lane);
                _vehicles.Add(vehicle);
                UpdatePose(vehicle, snap: true);
                Statistics.Spawned++;
                return true;
            }

            return false;
        }

        /// <summary>Whether a vehicle is turning into the lane start (say, turning around at the dead end it starts from).</summary>
        private static bool IsBeingEntered(TrafficLink lane)
        {
            foreach (var connection in lane.Incoming)
            {
                if (connection.Reservations.Count > 0)
                    return true;
            }

            return false;
        }

        /// <summary>Switches to a rebuilt network after a road edit; vehicles on surviving lanes keep driving, others are removed.</summary>
        public void Rebuild(TrafficNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _router = new Router(network);

            var survivors = new List<Vehicle>(_vehicles.Count);
            foreach (var vehicle in _vehicles)
            {
                if (TryMapOntoNetwork(vehicle))
                    survivors.Add(vehicle);
                else
                    _pool.Push(vehicle);
            }

            _vehicles.Clear();
            foreach (var vehicle in survivors)
            {
                vehicle.PreviousLink = null;
                Insert(vehicle, vehicle.Link);
                if (vehicle.Link.Kind == LinkKind.Connection)
                    vehicle.Link.Reservations.Add(vehicle);
                if (Reroute(vehicle))
                {
                    _vehicles.Add(vehicle);
                }
                else
                {
                    Detach(vehicle);
                    _pool.Push(vehicle);
                }
            }
        }

        /// <summary>
        /// Lists lanes whose traffic moves well below the speed limit. Roads slow only because of a stop sign at their end
        /// stay under the threshold; real queues show up.
        /// </summary>
        public void GetCongestion(List<LaneCongestion> result)
        {
            result.Clear();
            foreach (var road in _network.Roads)
            {
                var ratio = road.ObservedSpeed / road.SpeedLimit;
                var level = math.saturate(1.6f * (1f - ratio) - 0.4f);
                if (level <= 0.05f)
                    continue;

                foreach (var lane in road.Lanes)
                    result.Add(new LaneCongestion(lane.Path, level, (lane.StartHeight + lane.EndHeight) * 0.5f));
            }
        }

        /// <summary>
        /// Where to draw a vehicle, interpolated between the last two steps by <paramref name="alpha"/> (0–1). The body
        /// lies between its bumpers, so it follows curves and junction paths smoothly.
        /// </summary>
        public VehicleSnapshot GetVehicle(int index, float alpha)
        {
            var vehicle = _vehicles[index];
            var front = math.lerp(vehicle.PreviousFront, vehicle.Front, alpha);
            var rear = math.lerp(vehicle.PreviousRear, vehicle.Rear, alpha);
            var direction = math.normalizesafe(front - rear, new float2(1f, 0f));
            var height = vehicle.Link.HeightAt(vehicle.Distance - vehicle.Spec.Length * 0.5f);
            return new VehicleSnapshot(vehicle.Id, (front + rear) * 0.5f, height, direction, vehicle.Spec.Length, vehicle.Spec.Width, vehicle.Spec.Kind, vehicle.ColorIndex);
        }

        private void UpdatePoses(bool snap)
        {
            for (var i = 0; i < _vehicles.Count; i++)
                UpdatePose(_vehicles[i], snap);
        }

        /// <summary>Stores the bumper positions after a step; the rear is found on the previous link when it is still there.</summary>
        private static void UpdatePose(Vehicle vehicle, bool snap)
        {
            vehicle.Link.Sample(vehicle.Distance, out var front, out var direction);
            var offset = Geometry2D.Left(direction) * vehicle.LateralOffset;

            var rearDistance = vehicle.Distance - vehicle.Spec.Length;
            float2 rear;
            if (rearDistance >= 0f)
                rear = vehicle.Link.Path.PointAt(rearDistance);
            else if (vehicle.PreviousLink != null && vehicle.PreviousLink.Length + rearDistance >= 0f)
                rear = vehicle.PreviousLink.Path.PointAt(vehicle.PreviousLink.Length + rearDistance);
            else
                rear = vehicle.Link.Path.Start + vehicle.Link.Path.StartDirection * rearDistance;

            vehicle.PreviousFront = snap ? front + offset : vehicle.Front;
            vehicle.PreviousRear = snap ? rear + offset : vehicle.Rear;
            vehicle.Front = front + offset;
            vehicle.Rear = rear + offset;
        }

        private void SampleRoadSpeeds()
        {
            if (Time < _nextSpeedSample)
                return;
            _nextSpeedSample = Time + SpeedSampleInterval;

            foreach (var road in _network.Roads)
            {
                var sum = 0f;
                var count = 0;
                foreach (var lane in road.Lanes)
                {
                    foreach (var vehicle in lane.Vehicles)
                    {
                        sum += vehicle.Speed;
                        count++;
                    }
                }

                var sample = count > 0 ? sum / count : road.SpeedLimit;
                road.ObservedSpeed = math.lerp(road.ObservedSpeed, sample, SpeedSmoothing);
            }
        }

        private void UpdateSignals(float deltaTime)
        {
            foreach (var signal in _network.Signals)
            {
                if (signal.Update(deltaTime))
                    ReleaseStoppableVehicles(signal);
            }
        }

        /// <summary>When the light turns yellow, vehicles allowed in but still able to stop comfortably stop after all.</summary>
        private static void ReleaseStoppableVehicles(TrafficSignal signal)
        {
            foreach (var lane in signal.Lanes)
            {
                foreach (var vehicle in lane.Vehicles)
                {
                    if (!vehicle.Committed || signal.StateOf(vehicle.Next) == SignalState.Green || !CanStopBeforeLine(vehicle))
                        continue;

                    vehicle.Next.Reservations.Remove(vehicle);
                    vehicle.Committed = false;
                }
            }
        }

        private static bool CanStopBeforeLine(Vehicle vehicle)
        {
            var remaining = vehicle.Link.Length - vehicle.Distance - StopLineMargin;
            return remaining > vehicle.Speed * vehicle.Speed / (2f * vehicle.Spec.ComfortableDeceleration);
        }

        private void ComputeAccelerations(float deltaTime)
        {
            foreach (var link in _network.Links)
            {
                var vehicles = link.Vehicles;
                for (var i = 0; i < vehicles.Count; i++)
                {
                    var vehicle = vehicles[i];
                    var acceleration = FreeAcceleration(vehicle);
                    if (i > 0)
                    {
                        var leader = vehicles[i - 1];
                        acceleration = math.min(acceleration, Idm(vehicle, RearOf(leader) - vehicle.Distance, leader.Speed));
                    }
                    else
                    {
                        acceleration = math.min(acceleration, AccelerationAhead(vehicle));
                    }

                    if (link.Kind == LinkKind.Connection)
                        acceleration = math.min(acceleration, FollowMergingTraffic(vehicle, link, link.Length - vehicle.Distance));
                    else if (i > 0)
                        acceleration = math.min(acceleration, AnticipateTurn(vehicle));

                    // Speeding up builds gradually (no lurch when the way clears); braking applies and releases at once.
                    var current = math.max(vehicle.Acceleration, 0f);
                    if (acceleration > current)
                        acceleration = math.min(acceleration, current + MaxJerk * deltaTime);
                    vehicle.Acceleration = math.max(acceleration, -EmergencyDeceleration);
                }
            }
        }

        private void Integrate(float deltaTime)
        {
            for (var i = 0; i < _vehicles.Count; i++)
            {
                var vehicle = _vehicles[i];
                var speed = math.max(0f, vehicle.Speed + vehicle.Acceleration * deltaTime);
                vehicle.Distance += (vehicle.Speed + speed) * 0.5f * deltaTime;
                vehicle.Speed = speed;
                AdvanceAcrossLinks(vehicle);
                TrackStanding(vehicle);

                if (vehicle.LateralOffset != 0f)
                {
                    var step = vehicle.Link.LaneWidth / _settings.LaneChangeDuration * deltaTime;
                    vehicle.LateralOffset = math.abs(vehicle.LateralOffset) <= step ? 0f : vehicle.LateralOffset - math.sign(vehicle.LateralOffset) * step;
                }
            }
        }

        private void AdvanceAcrossLinks(Vehicle vehicle)
        {
            while (vehicle.Distance >= vehicle.Link.Length)
            {
                var link = vehicle.Link;
                var overflow = vehicle.Distance - link.Length;

                if (link.Kind == LinkKind.Lane)
                {
                    if (vehicle.IsOnLastRoad && link.EndNode == vehicle.Destination)
                    {
                        Finish(vehicle, completed: true);
                        return;
                    }

                    if (!vehicle.Committed)
                    {
                        // Should not happen (the stop line holds it back); never let a vehicle slip in uninvited.
                        vehicle.Distance = link.Length;
                        vehicle.Speed = 0f;
                        return;
                    }

                    Move(vehicle, vehicle.Next, overflow);
                }
                else
                {
                    link.Reservations.Remove(vehicle);
                    Move(vehicle, link.To, overflow);
                    vehicle.RouteIndex++;
                    vehicle.ResetJunctionState();
                }
            }
        }

        private void TrackStanding(Vehicle vehicle)
        {
            if (vehicle.Speed > 0.1f)
            {
                vehicle.StandingSince = -1f;
                return;
            }

            if (vehicle.StandingSince < 0f)
                vehicle.StandingSince = Time;
            else if (Time - vehicle.StandingSince > _settings.GridlockTimeout)
                Finish(vehicle, completed: false);
        }

        /// <summary>Acceleration towards the desired speed on an empty road.</summary>
        private float FreeAcceleration(Vehicle vehicle)
        {
            var desired = vehicle.Link.SpeedLimit * vehicle.SpeedFactor;
            var ratio = vehicle.Speed / math.max(desired, 0.1f);
            return vehicle.Spec.MaxAcceleration * (1f - ratio * ratio * ratio * ratio);
        }

        /// <summary>Intelligent Driver Model braking term for a gap to something moving at <paramref name="leaderSpeed"/>.</summary>
        private static float Idm(Vehicle vehicle, float gap, float leaderSpeed)
        {
            var spec = vehicle.Spec;
            var speed = vehicle.Speed;
            var desiredGap = spec.MinGap + math.max(0f, speed * spec.TimeHeadway +
                speed * (speed - leaderSpeed) / (2f * math.sqrt(spec.MaxAcceleration * spec.ComfortableDeceleration)));
            var ratio = desiredGap / math.max(gap, 0.1f);
            return spec.MaxAcceleration * (1f - ratio * ratio);
        }

        /// <summary>Constraints beyond the end of the vehicle's link: the junction ahead and the traffic behind it.</summary>
        private float AccelerationAhead(Vehicle vehicle)
        {
            var link = vehicle.Link;
            var remaining = link.Length - vehicle.Distance;

            if (link.Kind == LinkKind.Connection)
                return FollowAcross(vehicle, link.To, remaining);

            if (vehicle.IsOnLastRoad && link.EndNode == vehicle.Destination)
                return float.MaxValue;

            ChooseNextConnection(vehicle);
            if (vehicle.Next == null)
                return StopAtLine(vehicle, remaining);

            if (!vehicle.Committed && !TryCommit(vehicle, remaining))
            {
                // Undecided but still far away: drive on (easing off for the turn) and decide later.
                return remaining > DecisionDistance(vehicle) ? AnticipateTurn(vehicle) : StopAtLine(vehicle, remaining);
            }

            var acceleration = AnticipateTurn(vehicle);
            acceleration = math.min(acceleration, FollowMergingTraffic(vehicle, vehicle.Next, remaining + vehicle.Next.Length));
            return math.min(acceleration, FollowAcross(vehicle, vehicle.Next, remaining));
        }

        /// <summary>
        /// Eases off towards the speed of the junction path the vehicle is going to take: the desired speed follows a
        /// gentle braking curve that reaches the turn speed at the stop line, so slowing down starts smoothly and
        /// only as late as needed.
        /// </summary>
        private static float AnticipateTurn(Vehicle vehicle)
        {
            var connection = vehicle.IntendedConnection();
            if (connection == null)
                return float.MaxValue;

            var turnSpeed = connection.SpeedLimit * vehicle.SpeedFactor;
            var remaining = math.max(vehicle.Link.Length - vehicle.Distance, 0f);
            var deceleration = vehicle.Spec.ComfortableDeceleration * TurnBrakingShare;
            var desired = math.sqrt(turnSpeed * turnSpeed + 2f * deceleration * remaining);
            if (desired >= vehicle.Link.SpeedLimit * vehicle.SpeedFactor)
                return float.MaxValue;

            var ratio = vehicle.Speed / math.max(desired, 0.1f);
            return vehicle.Spec.MaxAcceleration * (1f - ratio * ratio * ratio * ratio);
        }

        /// <summary>
        /// Before a merge point, vehicles allowed onto the other merging connections that are nearer to it — already on
        /// the connection or still on its approach — count as leaders, so traffic zips into the shared lane one by one.
        /// </summary>
        private static float FollowMergingTraffic(Vehicle vehicle, TrafficLink connection, float distanceToMerge)
        {
            var acceleration = float.MaxValue;
            foreach (var conflict in connection.Conflicts)
            {
                if (!conflict.IsMerge)
                    continue;

                foreach (var other in conflict.Other.Reservations)
                {
                    var otherDistance = DistanceToEnd(other, conflict.Other);
                    if (otherDistance < distanceToMerge)
                        acceleration = math.min(acceleration, Idm(vehicle, distanceToMerge - otherDistance - other.Spec.Length, other.Speed));
                }
            }

            return acceleration;
        }

        /// <summary>Distance from a reserved vehicle to the end of its connection, wherever it is now.</summary>
        private static float DistanceToEnd(Vehicle vehicle, TrafficLink connection) =>
            vehicle.Link == connection
                ? connection.Length - vehicle.Distance
                : vehicle.Link.Length - vehicle.Distance + connection.Length;

        private float StopAtLine(Vehicle vehicle, float remaining) => Idm(vehicle, remaining - StopLineMargin, 0f);

        /// <summary>Follows the rearmost vehicle on the next links, looking at most <see cref="TrafficSettings.LookAhead"/> meters ahead.</summary>
        private float FollowAcross(Vehicle vehicle, TrafficLink next, float gap)
        {
            for (var hops = 0; next != null && hops < 2 && gap < _settings.LookAhead; hops++)
            {
                if (next.Vehicles.Count > 0)
                {
                    var leader = next.Vehicles[next.Vehicles.Count - 1];
                    return Idm(vehicle, gap + RearOf(leader), leader.Speed);
                }

                gap += next.Length;
                next = next.Kind == LinkKind.Connection ? next.To : null;
            }

            return float.MaxValue;
        }

        private float DecisionDistance(Vehicle vehicle) =>
            vehicle.Speed * vehicle.Speed / (2f * vehicle.Spec.ComfortableDeceleration) + DecisionMargin;

        /// <summary>Picks the connection towards the next road from the current lane, preferring the exit lane with most room.</summary>
        private void ChooseNextConnection(Vehicle vehicle)
        {
            if (vehicle.Next != null || vehicle.IsOnLastRoad)
                return;

            var nextRoad = vehicle.Route[vehicle.RouteIndex + 1];
            TrafficLink best = null;
            var bestRoom = float.MinValue;
            foreach (var connection in vehicle.Link.Outgoing)
            {
                if (connection.To.RoadInfo != nextRoad)
                    continue;

                var exit = connection.To;
                var room = exit.Vehicles.Count > 0 ? RearOf(exit.Vehicles[exit.Vehicles.Count - 1]) : exit.Length;
                if (room > bestRoom)
                {
                    best = connection;
                    bestRoom = room;
                }
            }

            vehicle.Next = best;
            if (best == null && vehicle.Link.Length - vehicle.Distance < MinLaneChangeRoom)
                DetourFromWrongLane(vehicle);
        }

        /// <summary>Stuck in a lane that does not lead to the planned road: take any way out and plan again from there.</summary>
        private void DetourFromWrongLane(Vehicle vehicle)
        {
            var outgoing = vehicle.Link.Outgoing;
            if (outgoing.Count == 0)
                return;

            var detour = outgoing[_random.Next(outgoing.Count)];
            var current = vehicle.CurrentRoad;
            if (!_router.TryFindRoute(detour.To.RoadInfo, vehicle.Destination, _routeBuffer))
                return;

            vehicle.Route.Clear();
            vehicle.Route.Add(current);
            vehicle.Route.AddRange(_routeBuffer);
            vehicle.RouteIndex = 0;
            vehicle.Next = detour;
        }

        /// <summary>Decides at the stop line whether the vehicle may enter its next connection now (US right-of-way).</summary>
        private bool TryCommit(Vehicle vehicle, float remaining)
        {
            if (remaining > DecisionDistance(vehicle))
                return false;

            var control = vehicle.Link.Control;
            if (control == ApproachControl.Signal && !SignalLetsGo(vehicle, remaining))
                return false;

            if ((control == ApproachControl.Stop || IsRightOnRed(vehicle.Next)) && vehicle.StoppedAt < 0f)
            {
                if (vehicle.Speed > StoppedSpeed || remaining > StopSignZone)
                    return false;
                vehicle.StoppedAt = Time;
            }

            if (vehicle.WaitingSince < 0f)
                vehicle.WaitingSince = Time;

            if (!CanEnter(vehicle, vehicle.Next))
                return false;

            vehicle.Committed = true;
            vehicle.Next.Reservations.Add(vehicle);
            return true;
        }

        /// <summary>
        /// Green: go. Yellow: go only when stopping would take hard braking, or when a left turn waiting at the line
        /// can clear out now that oncoming traffic stops. Red: only a right turn, after a full stop.
        /// </summary>
        private static bool SignalLetsGo(Vehicle vehicle, float remaining)
        {
            var connection = vehicle.Next;
            switch (connection.Signal.StateOf(connection))
            {
                case SignalState.Green:
                    return true;
                case SignalState.Yellow:
                    return !CanStopBeforeLine(vehicle) ||
                           (connection.Turn == TurnDirection.Left && vehicle.WaitingSince >= 0f && remaining <= StopSignZone);
                default:
                    return connection.Turn == TurnDirection.Right;
            }
        }

        private static bool IsRightOnRed(TrafficLink connection) =>
            connection.Signal != null && connection.Turn == TurnDirection.Right && connection.Signal.StateOf(connection) == SignalState.Red;

        private static bool HasGo(TrafficLink connection) =>
            connection.Signal == null || connection.Signal.StateOf(connection) != SignalState.Red;

        private bool CanEnter(Vehicle vehicle, TrafficLink connection)
        {
            // Don't block the box: where waiting inside the junction could block crossing traffic (or the vehicle has to
            // yield anyway), the exit lane must have room for the whole vehicle. Priority traffic on merge-only paths,
            // such as the ring of a roundabout, keeps rolling; requiring room there would let a full ring lock itself.
            var exit = connection.To;
            var mayBlockOthers = connection.Rank == JunctionRules.MinorRank || HasCrossingConflict(connection);
            if (mayBlockOthers && exit.Vehicles.Count > 0 && RearOf(exit.Vehicles[exit.Vehicles.Count - 1]) < vehicle.Spec.Length + vehicle.Spec.MinGap)
                return false;

            var ownRemaining = vehicle.Link.Length - vehicle.Distance + connection.Length;
            foreach (var conflict in connection.Conflicts)
            {
                foreach (var other in conflict.Other.Reservations)
                {
                    if (conflict.IsMerge)
                    {
                        // Zipper merge: fine unless both would reach the merge point side by side.
                        if (math.abs(DistanceToEnd(other, conflict.Other) - ownRemaining) < vehicle.Spec.Length + vehicle.Spec.MinGap + 2f)
                            return false;
                        continue;
                    }

                    var cleared = other.Link == conflict.Other && RearOf(other) > conflict.There + 1f;
                    if (!cleared)
                        return false;
                }
            }

            var patient = Time - vehicle.WaitingSince < _settings.MaxPatience;
            if (!patient)
                return true;

            foreach (var conflict in connection.Conflicts)
            {
                if (YieldsTo(vehicle, connection, conflict.Other))
                    return false;
            }

            return true;
        }

        private static bool HasCrossingConflict(TrafficLink connection)
        {
            foreach (var conflict in connection.Conflicts)
            {
                if (!conflict.IsMerge)
                    return true;
            }

            return false;
        }

        /// <summary>Whether an uncommitted vehicle approaching <paramref name="other"/> goes before us.</summary>
        private bool YieldsTo(Vehicle vehicle, TrafficLink connection, TrafficLink other)
        {
            // Traffic held by a red light does not count; turning right on red gives way to everyone with a green.
            if (!HasGo(other))
                return false;
            var rank = IsRightOnRed(connection) ? JunctionRules.MinorRank : connection.Rank;

            var approach = other.From.Vehicles;
            for (var i = 0; i < approach.Count && i < ApproachingVehiclesChecked; i++)
            {
                var rival = approach[i];
                if (rival.Committed || rival.IntendedConnection() != other)
                    continue;

                if (other.Rank > rank)
                {
                    var eta = (other.From.Length - rival.Distance) / math.max(rival.Speed, 0.5f);
                    if (eta < _settings.CriticalGap)
                        return true;
                }
                else if (other.Rank == rank)
                {
                    // Equal standing: whoever stopped first goes first (all-way stop); otherwise the one nearer the line.
                    if (rival.StoppedAt >= 0f)
                    {
                        if (vehicle.StoppedAt < 0f || rival.StoppedAt < vehicle.StoppedAt)
                            return true;
                    }
                    else if (other.From.Control is ApproachControl.None or ApproachControl.Signal)
                    {
                        var rivalRemaining = other.From.Length - rival.Distance;
                        var ownRemaining = vehicle.Link.Length - vehicle.Distance;
                        if (rivalRemaining < ownRemaining || (rivalRemaining == ownRemaining && rival.Id < vehicle.Id))
                            return true;
                    }
                }
            }

            return false;
        }

        private void ChangeLanesAndReroute(float deltaTime)
        {
            // Spread the work: each vehicle is looked at about every LaneChangeInterval seconds.
            var slots = math.max(1, (int)math.round(LaneChangeInterval / deltaTime));
            var slot = (int)math.round(Time / deltaTime) % slots;

            for (var i = 0; i < _vehicles.Count; i++)
            {
                var vehicle = _vehicles[i];
                if (vehicle.Id % slots != slot || vehicle.Link.Kind != LinkKind.Lane || vehicle.Committed)
                    continue;

                if (Time >= vehicle.NextReroute)
                {
                    vehicle.NextReroute = Time + _settings.RerouteInterval * (0.8f + 0.4f * (float)_random.NextDouble());
                    Reroute(vehicle);
                }

                TryChangeLane(vehicle);
            }
        }

        /// <summary>Moves one lane towards a lane that leads to the next road on the route, when the gaps are safe.</summary>
        private void TryChangeLane(Vehicle vehicle)
        {
            if (vehicle.IsOnLastRoad)
                return;

            ChooseNextConnection(vehicle);
            if (vehicle.Next != null)
                return;

            var lane = vehicle.Link;
            // Not right after entering a lane: vehicles still emerging behind would drive into the one moving over.
            if (vehicle.Distance < vehicle.Spec.Length + NoLaneChangeZone)
                return;

            var road = vehicle.CurrentRoad;
            var nextRoad = vehicle.Route[vehicle.RouteIndex + 1];
            if (!road.ConnectionsTo.TryGetValue(nextRoad, out var connections) || lane.Length - vehicle.Distance < MinLaneChangeRoom)
                return;

            var target = -1;
            foreach (var connection in connections)
            {
                var index = connection.From.LaneIndex;
                if (target < 0 || math.abs(index - lane.LaneIndex) < math.abs(target - lane.LaneIndex))
                    target = index;
            }

            if (target < 0 || target == lane.LaneIndex)
                return;

            var step = target > lane.LaneIndex ? 1 : -1;
            var neighbour = road.Lanes[lane.LaneIndex + step];
            var distance = vehicle.Distance * neighbour.Length / lane.Length;
            if (!IsGapSafe(vehicle, neighbour, distance))
                return;

            Detach(vehicle);
            vehicle.Link = neighbour;
            vehicle.Distance = distance;
            Insert(vehicle, neighbour);
            // Higher lane indices lie further right, so the vehicle starts out to the left of its new lane.
            vehicle.LateralOffset += step * neighbour.LaneWidth;
        }

        private static bool IsGapSafe(Vehicle vehicle, TrafficLink lane, float distance)
        {
            foreach (var other in lane.Vehicles)
            {
                if (other.Distance >= distance)
                {
                    if (RearOf(other) - distance < vehicle.Spec.MinGap + vehicle.Speed * 0.3f)
                        return false;
                }
                else if (distance - vehicle.Spec.Length - other.Distance < other.Spec.MinGap + other.Speed * 0.5f)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Plans the rest of the trip again from the current road; picks a new exit if the old one vanished.</summary>
        private bool Reroute(Vehicle vehicle)
        {
            var current = vehicle.Link.Kind == LinkKind.Lane ? vehicle.Link.RoadInfo : vehicle.Link.From.RoadInfo;
            if (!_network.Exits.Contains(vehicle.Destination) && _network.Exits.Count > 0)
                vehicle.Destination = _network.Exits[_random.Next(_network.Exits.Count)];

            if (!_router.TryFindRoute(current, vehicle.Destination, _routeBuffer))
                return false;

            // A vehicle inside a junction keeps its connection; the route must continue through its exit lane.
            if (vehicle.Link.Kind == LinkKind.Connection && (_routeBuffer.Count < 2 || _routeBuffer[1] != vehicle.Link.To.RoadInfo))
                return _router.TryFindRoute(vehicle.Link.To.RoadInfo, vehicle.Destination, _routeBuffer) && ReplaceRoute(vehicle, current);

            vehicle.Route.Clear();
            vehicle.Route.AddRange(_routeBuffer);
            vehicle.RouteIndex = 0;
            if (vehicle.Link.Kind == LinkKind.Lane && !vehicle.Committed)
                vehicle.Next = null;
            return true;
        }

        private bool ReplaceRoute(Vehicle vehicle, RoadInfo current)
        {
            vehicle.Route.Clear();
            vehicle.Route.Add(current);
            vehicle.Route.AddRange(_routeBuffer);
            vehicle.RouteIndex = 0;
            return true;
        }

        private bool TryMapOntoNetwork(Vehicle vehicle)
        {
            var old = vehicle.Link;
            if (old.Kind == LinkKind.Lane)
            {
                if (!_network.TryGetLane(old.Road, old.LaneIndex, out var lane))
                    return false;
                vehicle.Distance = math.min(vehicle.Distance * lane.Length / old.Length, lane.Length);
                vehicle.Link = lane;
                vehicle.ResetJunctionState();
                return true;
            }

            if (!_network.TryGetLane(old.From.Road, old.From.LaneIndex, out var from) ||
                !_network.TryGetLane(old.To.Road, old.To.LaneIndex, out var to) ||
                !_network.TryGetConnection(from, to, out var connection))
                return false;

            vehicle.Distance = math.min(vehicle.Distance * connection.Length / old.Length, connection.Length);
            vehicle.Link = connection;
            vehicle.Next = null;
            vehicle.Committed = false;
            return true;
        }

        private void Finish(Vehicle vehicle, bool completed)
        {
            if (_finished.Contains(vehicle))
                return;

            _finished.Add(vehicle);
            if (completed)
            {
                Statistics.CompletedTrips++;
                var duration = Time - math.max(vehicle.SpawnTime, _tripsTimedFrom);
                Statistics.AverageTripSeconds += (duration - Statistics.AverageTripSeconds) / Statistics.CompletedTrips;
                TripCompleted?.Invoke(vehicle.Origin, vehicle.Destination, duration);
            }
            else
            {
                Statistics.RemovedStuck++;
            }
        }

        private void RemoveFinished()
        {
            foreach (var vehicle in _finished)
            {
                Detach(vehicle);
                _vehicles.Remove(vehicle);
                _pool.Push(vehicle);
            }

            _finished.Clear();
        }

        private static float RearOf(Vehicle vehicle) => vehicle.Distance - vehicle.Spec.Length;

        private static void Move(Vehicle vehicle, TrafficLink link, float distance)
        {
            vehicle.Link.Vehicles.Remove(vehicle);
            vehicle.PreviousLink = vehicle.Link;
            vehicle.Link = link;
            vehicle.Distance = distance;
            Insert(vehicle, link);
        }

        /// <summary>Keeps the link's list ordered front first.</summary>
        private static void Insert(Vehicle vehicle, TrafficLink link)
        {
            var list = link.Vehicles;
            var index = list.Count;
            while (index > 0 && list[index - 1].Distance < vehicle.Distance)
                index--;
            list.Insert(index, vehicle);
        }

        private static void Detach(Vehicle vehicle)
        {
            vehicle.Link.Vehicles.Remove(vehicle);
            vehicle.Next?.Reservations.Remove(vehicle);
            if (vehicle.Link.Kind == LinkKind.Connection)
                vehicle.Link.Reservations.Remove(vehicle);
        }
    }
}
