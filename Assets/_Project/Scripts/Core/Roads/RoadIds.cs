using System;

namespace TrafficGame.Core.Roads
{
    /// <summary>Identifier of a <see cref="RoadNode"/> within its network.</summary>
    public readonly struct NodeId : IEquatable<NodeId>
    {
        public NodeId(int value) => Value = value;

        public int Value { get; }

        public bool Equals(NodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is NodeId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Node#{Value}";

        public static bool operator ==(NodeId left, NodeId right) => left.Equals(right);
        public static bool operator !=(NodeId left, NodeId right) => !left.Equals(right);
    }

    /// <summary>Identifier of a <see cref="RoadSegment"/> within its network.</summary>
    public readonly struct SegmentId : IEquatable<SegmentId>
    {
        public SegmentId(int value) => Value = value;

        public int Value { get; }

        public bool Equals(SegmentId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SegmentId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Segment#{Value}";

        public static bool operator ==(SegmentId left, SegmentId right) => left.Equals(right);
        public static bool operator !=(SegmentId left, SegmentId right) => !left.Equals(right);
    }
}
