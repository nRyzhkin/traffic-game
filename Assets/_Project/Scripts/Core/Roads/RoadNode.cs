using System.Collections.Generic;
using Unity.Mathematics;

namespace TrafficGame.Core.Roads
{
    /// <summary>A point where road segments end: a junction, a bend or a dead end.</summary>
    public sealed class RoadNode
    {
        private readonly List<SegmentId> _segments = new();

        internal RoadNode(NodeId id, float2 position, float height)
        {
            Id = id;
            Position = position;
            Height = height;
        }

        public NodeId Id { get; }
        public float2 Position { get; }

        /// <summary>Meters above the ground (<see cref="RoadElevation"/>).</summary>
        public float Height { get; }
        public IReadOnlyList<SegmentId> Segments => _segments;
        public int Degree => _segments.Count;

        internal void Attach(SegmentId segment) => _segments.Add(segment);
        internal void Detach(SegmentId segment) => _segments.Remove(segment);
    }
}
