using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    public enum AnchorKind
    {
        /// <summary>An empty spot; a new node is created there.</summary>
        Free,

        /// <summary>An existing node.</summary>
        Node,

        /// <summary>A point inside an existing segment; the segment is split there.</summary>
        Segment
    }

    /// <summary>Where a new road starts or ends, after snapping.</summary>
    public readonly struct RoadAnchor
    {
        private RoadAnchor(AnchorKind kind, float2 position, NodeId node, SegmentId segment, float parameter, float height)
        {
            Kind = kind;
            Position = position;
            Height = height;
            Node = node;
            Segment = segment;
            Parameter = parameter;
        }

        public AnchorKind Kind { get; }
        public float2 Position { get; }

        /// <summary>
        /// Height of a free anchor (the level the player builds on). Attached anchors take the height of the road
        /// they attach to (<see cref="RoadPlanner.HeightOf"/>).
        /// </summary>
        public float Height { get; }

        /// <summary>The node, when <see cref="Kind"/> is <see cref="AnchorKind.Node"/>.</summary>
        public NodeId Node { get; }

        /// <summary>The segment, when <see cref="Kind"/> is <see cref="AnchorKind.Segment"/>.</summary>
        public SegmentId Segment { get; }

        /// <summary>Curve parameter on <see cref="Segment"/>.</summary>
        public float Parameter { get; }

        public bool IsAttached => Kind != AnchorKind.Free;

        public static RoadAnchor Free(float2 position, float height = 0f) => new(AnchorKind.Free, position, default, default, 0f, height);

        public static RoadAnchor AtNode(NodeId node, float2 position) => new(AnchorKind.Node, position, node, default, 0f, 0f);

        public static RoadAnchor OnSegment(SegmentId segment, float parameter, float2 position) =>
            new(AnchorKind.Segment, position, default, segment, parameter, 0f);

        /// <summary>True when <paramref name="segment"/> is the anchor's segment or touches the anchor's node.</summary>
        public bool Touches(RoadSegment segment) => Kind switch
        {
            AnchorKind.Node => segment.Start == Node || segment.End == Node,
            AnchorKind.Segment => segment.Id == Segment,
            _ => false
        };
    }
}
