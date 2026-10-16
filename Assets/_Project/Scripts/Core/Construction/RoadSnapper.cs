using System.Linq;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    /// <summary>
    /// Snaps pointer positions to nodes and roads on the level being built on, and picks roads and junctions under the
    /// pointer (the topmost where roads pass over each other).
    /// </summary>
    public sealed class RoadSnapper
    {
        private readonly float _snapMargin;
        private readonly float _endExclusion;

        /// <param name="snapMargin">How far outside a road's edge the pointer still snaps to it.</param>
        /// <param name="endExclusion">Within this distance of a segment end the pointer snaps to the end node instead.</param>
        public RoadSnapper(float snapMargin, float endExclusion)
        {
            _snapMargin = snapMargin;
            _endExclusion = endExclusion;
        }

        /// <param name="height">The height being built at: only roads within half a level of it snap; a free point gets it.</param>
        public RoadAnchor Snap(float2 point, RoadNetwork network, RoadLayout layout, float height = 0f)
        {
            RoadNode bestNode = null;
            var bestNodeDistance = float.MaxValue;
            foreach (var node in network.Nodes)
            {
                if (!IsOnLevel(node.Height, height))
                    continue;

                var radius = node.Segments.Max(id => network.GetSegment(id).Profile.HalfWidth) + _snapMargin;
                var distance = math.distance(point, node.Position);
                if (distance < radius && distance < bestNodeDistance)
                {
                    bestNode = node;
                    bestNodeDistance = distance;
                }
            }

            if (bestNode != null)
                return RoadAnchor.AtNode(bestNode.Id, bestNode.Position);

            if (!TryFindClosestSegment(point, layout, _snapMargin, height, out var geometry, out var along, out var closest))
                return RoadAnchor.Free(point, height);

            var segment = geometry.Segment;
            if (along < _endExclusion && IsOnLevel(segment.StartHeight, height))
                return RoadAnchor.AtNode(segment.Start, network.GetNode(segment.Start).Position);
            if (along > geometry.Centerline.Length - _endExclusion && IsOnLevel(segment.EndHeight, height))
                return RoadAnchor.AtNode(segment.End, network.GetNode(segment.End).Position);

            return RoadAnchor.OnSegment(segment.Id, geometry.Centerline.ParameterAt(along), closest);
        }

        /// <summary>The segment whose surface is under <paramref name="point"/>, if any.</summary>
        public bool TryPickSegment(float2 point, RoadLayout layout, out SegmentId segment)
        {
            var found = TryFindClosestSegment(point, layout, 0f, null, out var geometry, out _, out _);
            segment = found ? geometry.Segment.Id : default;
            return found;
        }

        /// <summary>
        /// The junction of three or more roads under the point: its area or a few meters around it. Where junction
        /// areas are close together the nearest center wins.
        /// </summary>
        public bool TryPickJunction(float2 point, RoadLayout layout, out JunctionGeometry junction)
        {
            junction = null;
            var bestDistance = float.MaxValue;
            var bestHeight = float.MinValue;
            foreach (var candidate in layout.Junctions.Values)
            {
                if (candidate.Approaches.Count < 3)
                    continue;

                var reach = _snapMargin;
                foreach (var approach in candidate.Approaches)
                    reach = math.max(reach, math.distance(candidate.Center, approach.Position) + _snapMargin);

                var distance = math.distance(point, candidate.Center);
                var height = candidate.Height;
                if (distance < reach && IsBetter(height, distance, bestHeight, bestDistance))
                {
                    junction = candidate;
                    bestDistance = distance;
                    bestHeight = height;
                }
            }

            return junction != null;
        }

        /// <param name="level">Only roads at this height count; null takes the topmost road under the point.</param>
        private static bool TryFindClosestSegment(float2 point, RoadLayout layout, float margin, float? level,
            out SegmentGeometry best, out float bestAlong, out float2 bestClosest)
        {
            best = null;
            bestAlong = 0f;
            bestClosest = point;
            var bestDistance = float.MaxValue;
            var bestHeight = float.MinValue;

            foreach (var geometry in layout.Segments.Values)
            {
                var reach = geometry.Segment.Profile.HalfWidth + margin;
                if (math.any(point < geometry.Centerline.BoundsMin - reach) || math.any(point > geometry.Centerline.BoundsMax + reach))
                    continue;

                var along = geometry.Centerline.ClosestDistance(point, out var closest);
                var distance = math.distance(point, closest);
                if (distance >= reach)
                    continue;

                var height = geometry.Segment.HeightAt(geometry.Centerline.ParameterAt(along));
                if (level.HasValue ? !IsOnLevel(height, level.Value) || distance >= bestDistance : !IsBetter(height, distance, bestHeight, bestDistance))
                    continue;

                best = geometry;
                bestAlong = along;
                bestClosest = closest;
                bestDistance = distance;
                bestHeight = height;
            }

            return best != null;
        }

        private static bool IsOnLevel(float height, float level) => math.abs(height - level) < RoadElevation.LevelHeight * 0.5f;

        /// <summary>Higher roads cover lower ones; at the same height the closer one wins.</summary>
        private static bool IsBetter(float height, float distance, float bestHeight, float bestDistance) =>
            height > bestHeight + RoadElevation.SameHeight || (height > bestHeight - RoadElevation.SameHeight && distance < bestDistance);
    }
}
