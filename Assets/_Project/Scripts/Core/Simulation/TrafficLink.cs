using System;
using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Simulation
{
    /// <summary>A directed road: one segment driven in one direction.</summary>
    public readonly struct RoadDirection : IEquatable<RoadDirection>
    {
        public RoadDirection(SegmentId segment, bool forward)
        {
            Segment = segment;
            Forward = forward;
        }

        public SegmentId Segment { get; }
        public bool Forward { get; }

        public bool Equals(RoadDirection other) => Segment == other.Segment && Forward == other.Forward;
        public override bool Equals(object obj) => obj is RoadDirection other && Equals(other);
        public override int GetHashCode() => Segment.Value * 2 + (Forward ? 1 : 0);
        public override string ToString() => $"{Segment}{(Forward ? "→" : "←")}";
    }

    public enum LinkKind
    {
        /// <summary>A lane between junctions.</summary>
        Lane,

        /// <summary>A path through a junction from one lane to another.</summary>
        Connection
    }

    /// <summary>Traffic control a lane ends at (US rules).</summary>
    public enum ApproachControl
    {
        /// <summary>Priority road: drive on.</summary>
        None,

        /// <summary>Yield sign (roundabout entry): slow down and give way, no full stop needed.</summary>
        Yield,

        /// <summary>Stop sign: full stop, then give way.</summary>
        Stop,

        /// <summary>Traffic lights: go on green; on red a right turn may follow a full stop (right turn on red).</summary>
        Signal
    }

    /// <summary>Where two connections of a junction cross or merge, as distances along each.</summary>
    public readonly struct LinkConflict
    {
        public LinkConflict(TrafficLink other, float here, float there, bool isMerge)
        {
            Other = other;
            Here = here;
            There = there;
            IsMerge = isMerge;
        }

        public TrafficLink Other { get; }
        public float Here { get; }
        public float There { get; }

        /// <summary>Both connections end in the same lane: vehicles zip in one after the other instead of excluding each other.</summary>
        public bool IsMerge { get; }
    }

    /// <summary>A piece of drivable path — a lane or a junction connection — together with the vehicles on it.</summary>
    public sealed class TrafficLink
    {
        private readonly List<TrafficLink> _outgoing = new();
        private readonly List<TrafficLink> _incoming = new();
        private readonly List<LinkConflict> _conflicts = new();

        internal TrafficLink(int id, LinkKind kind, Polyline path, float speedLimit)
        {
            Id = id;
            Kind = kind;
            Path = path;
            SpeedLimit = speedLimit;
        }

        public int Id { get; }
        public LinkKind Kind { get; }
        public Polyline Path { get; }
        public float Length => Path.Length;

        /// <summary>Heights at the start and end of the path, meters (they differ on ramps).</summary>
        public float StartHeight { get; internal set; }

        public float EndHeight { get; internal set; }

        public float HeightAt(float distance) => math.lerp(StartHeight, EndHeight, Length > 0f ? math.saturate(distance / Length) : 0f);

        /// <summary>Meters per second.</summary>
        public float SpeedLimit { get; }

        /// <summary>Lanes: the connections leaving the lane end. Connections: the lane they lead to.</summary>
        public IReadOnlyList<TrafficLink> Outgoing => _outgoing;

        /// <summary>Lanes: the connections arriving at the lane start. Connections: the lane they leave.</summary>
        public IReadOnlyList<TrafficLink> Incoming => _incoming;

        // Lane data.
        public RoadDirection Road { get; internal set; }
        public int LaneIndex { get; internal set; }
        public float LaneWidth { get; internal set; }
        public NodeId EndNode { get; internal set; }
        public ApproachControl Control { get; internal set; }
        internal RoadInfo RoadInfo { get; set; }

        // Connection data.
        public TrafficLink From { get; internal set; }
        public TrafficLink To { get; internal set; }
        public NodeId Node { get; internal set; }
        public TurnDirection Turn { get; internal set; }

        /// <summary>Right of way inside the junction; higher goes first (see <see cref="JunctionRules"/>).</summary>
        public int Rank { get; internal set; }

        /// <summary>Traffic lights of the junction the lane ends at or the connection crosses; null without lights.</summary>
        public TrafficSignal Signal { get; internal set; }

        /// <summary>Connections at traffic lights: bit N is set when the connection has green in phase N.</summary>
        public int PhaseMask { get; internal set; }

        public bool GoesIn(int phase) => (PhaseMask & (1 << phase)) != 0;

        public IReadOnlyList<LinkConflict> Conflicts => _conflicts;

        /// <summary>Vehicles on the link, front (largest distance) first.</summary>
        internal List<Vehicle> Vehicles { get; } = new();

        /// <summary>Connections only: vehicles allowed to enter and not yet out (includes ones still on the approach lane).</summary>
        internal List<Vehicle> Reservations { get; } = new();

        internal void AddOutgoing(TrafficLink link)
        {
            _outgoing.Add(link);
            link._incoming.Add(this);
        }
        internal void AddConflict(LinkConflict conflict) => _conflicts.Add(conflict);

        /// <summary>World position and travel direction at a distance along the link.</summary>
        public void Sample(float distance, out float2 position, out float2 direction)
        {
            position = Path.PointAt(distance);
            direction = Path.DirectionAt(distance);
        }
    }
}
