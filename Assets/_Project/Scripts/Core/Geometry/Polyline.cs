using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace TrafficGame.Core.Geometry
{
    /// <summary>
    /// Immutable open polyline parameterized by arc length. When sampled from a curve it also remembers
    /// the curve parameter of every vertex, so arc-length positions can be mapped back onto the curve.
    /// </summary>
    public sealed class Polyline
    {
        private const float DuplicateEpsilon = 1e-5f;
        private const float MinMiterCosine = 0.5f;

        private readonly float2[] _points;
        private readonly float[] _distances;
        private readonly float[] _parameters;

        public Polyline(IReadOnlyList<float2> points) : this(points, null) { }

        private Polyline(IReadOnlyList<float2> points, IReadOnlyList<float> parameters)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            var uniquePoints = new List<float2>(points.Count);
            var uniqueParameters = parameters != null ? new List<float>(points.Count) : null;
            for (var i = 0; i < points.Count; i++)
            {
                if (uniquePoints.Count > 0 && math.distance(uniquePoints[uniquePoints.Count - 1], points[i]) <= DuplicateEpsilon)
                    continue;
                uniquePoints.Add(points[i]);
                uniqueParameters?.Add(parameters[i]);
            }

            if (uniquePoints.Count < 2)
                throw new ArgumentException("A polyline needs at least two distinct points.", nameof(points));

            _points = uniquePoints.ToArray();
            _parameters = uniqueParameters?.ToArray();
            _distances = new float[_points.Length];
            BoundsMin = BoundsMax = _points[0];
            for (var i = 1; i < _points.Length; i++)
            {
                _distances[i] = _distances[i - 1] + math.distance(_points[i - 1], _points[i]);
                BoundsMin = math.min(BoundsMin, _points[i]);
                BoundsMax = math.max(BoundsMax, _points[i]);
            }
        }

        public int Count => _points.Length;
        public float2 this[int index] => _points[index];
        public float Length => _distances[_distances.Length - 1];
        public float2 Start => _points[0];
        public float2 End => _points[_points.Length - 1];
        public float2 StartDirection => SegmentDirection(0);
        public float2 EndDirection => SegmentDirection(_points.Length - 2);
        public float2 BoundsMin { get; }
        public float2 BoundsMax { get; }

        /// <summary>True when the polyline was sampled from a curve and <see cref="ParameterAt"/> is available.</summary>
        public bool HasCurveParameters => _parameters != null;

        /// <summary>Samples a Bézier curve with segments no longer than <paramref name="maxSegmentLength"/>.</summary>
        public static Polyline FromBezier(CubicBezier curve, float maxSegmentLength)
        {
            if (maxSegmentLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxSegmentLength));

            var segments = math.clamp((int)math.ceil(curve.ApproximateLength / maxSegmentLength), 1, 256);
            var points = new float2[segments + 1];
            var parameters = new float[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                parameters[i] = i / (float)segments;
                points[i] = curve.Evaluate(parameters[i]);
            }

            return new Polyline(points, parameters);
        }

        /// <summary>Distance along the polyline at vertex <paramref name="index"/>.</summary>
        public float DistanceAt(int index) => _distances[index];

        public float2 PointAt(float distance)
        {
            var segment = FindSegment(distance, out var t);
            return math.lerp(_points[segment], _points[segment + 1], t);
        }

        /// <summary>Unit direction of travel at <paramref name="distance"/>.</summary>
        public float2 DirectionAt(float distance) => SegmentDirection(FindSegment(distance, out _));

        /// <summary>Curve parameter at <paramref name="distance"/>; only for polylines sampled from a curve.</summary>
        public float ParameterAt(float distance)
        {
            if (_parameters == null)
                throw new InvalidOperationException("This polyline was not sampled from a curve.");

            var segment = FindSegment(distance, out var t);
            return math.lerp(_parameters[segment], _parameters[segment + 1], t);
        }

        /// <summary>Arc-length distance of the point on the polyline closest to <paramref name="point"/>.</summary>
        public float ClosestDistance(float2 point, out float2 closest)
        {
            var bestDistanceSquared = float.MaxValue;
            var bestAlong = 0f;
            closest = _points[0];

            for (var i = 0; i < _points.Length - 1; i++)
            {
                var fraction = Geometry2D.ClosestFraction(_points[i], _points[i + 1], point);
                var candidate = math.lerp(_points[i], _points[i + 1], fraction);
                var distanceSquared = math.distancesq(candidate, point);
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestAlong = _distances[i] + fraction * (_distances[i + 1] - _distances[i]);
                closest = candidate;
            }

            return bestAlong;
        }

        /// <summary>Shortest distance from <paramref name="point"/> to the polyline.</summary>
        public float DistanceTo(float2 point)
        {
            ClosestDistance(point, out var closest);
            return math.distance(point, closest);
        }

        /// <summary>True when the bounding boxes, grown by <paramref name="margin"/>, overlap.</summary>
        public bool BoundsOverlap(Polyline other, float margin) =>
            math.all(BoundsMin - margin <= other.BoundsMax) && math.all(other.BoundsMin - margin <= BoundsMax);

        /// <summary>Every crossing with <paramref name="other"/>, as arc-length distances along both polylines.</summary>
        public List<(float alongThis, float alongOther, float2 point)> IntersectionsWith(Polyline other)
        {
            var result = new List<(float, float, float2)>();
            if (!BoundsOverlap(other, 0f))
                return result;

            for (var i = 0; i < _points.Length - 1; i++)
            for (var j = 0; j < other._points.Length - 1; j++)
            {
                if (!Geometry2D.TryIntersectSegments(_points[i], _points[i + 1], other._points[j], other._points[j + 1], out var s, out var u))
                    continue;

                var alongThis = math.lerp(_distances[i], _distances[i + 1], s);
                var alongOther = math.lerp(other._distances[j], other._distances[j + 1], u);
                result.Add((alongThis, alongOther, math.lerp(_points[i], _points[i + 1], s)));
            }

            return result;
        }

        /// <summary>
        /// Left-pointing offset direction at a vertex, scaled so that offsetting all vertices by the same
        /// distance keeps the offset segments parallel to the originals (miter join, clamped on sharp turns).
        /// </summary>
        public float2 VertexNormal(int index)
        {
            if (index == 0)
                return Geometry2D.Left(StartDirection);
            if (index == _points.Length - 1)
                return Geometry2D.Left(EndDirection);

            var incoming = SegmentDirection(index - 1);
            var outgoing = SegmentDirection(index);
            var bisector = math.normalizesafe(incoming + outgoing, outgoing);
            var normal = Geometry2D.Left(bisector);
            var cosine = math.max(MinMiterCosine, math.dot(normal, Geometry2D.Left(outgoing)));
            return normal / cosine;
        }

        /// <summary>Signed turning angle at an interior vertex, in radians; positive turns left.</summary>
        public float TurnAngleAt(int index) =>
            index <= 0 || index >= _points.Length - 1 ? 0f : Geometry2D.SignedAngle(SegmentDirection(index - 1), SegmentDirection(index));

        /// <summary>A parallel polyline <paramref name="offset"/> units to the left (negative: to the right).</summary>
        public Polyline Offset(float offset)
        {
            var points = new float2[_points.Length];
            for (var i = 0; i < _points.Length; i++)
                points[i] = _points[i] + VertexNormal(i) * offset;
            return new Polyline(points);
        }

        /// <summary>The part between two arc-length distances; <paramref name="from"/> must be below <paramref name="to"/>.</summary>
        public Polyline Slice(float from, float to)
        {
            from = math.clamp(from, 0f, Length);
            to = math.clamp(to, 0f, Length);
            if (to - from <= DuplicateEpsilon)
                throw new ArgumentException($"Empty slice [{from}, {to}] of a polyline of length {Length}.");

            var points = new List<float2> { PointAt(from) };
            for (var i = 1; i < _points.Length - 1; i++)
            {
                if (_distances[i] > from && _distances[i] < to)
                    points.Add(_points[i]);
            }

            points.Add(PointAt(to));
            return new Polyline(points);
        }

        public Polyline Reversed()
        {
            var points = new float2[_points.Length];
            for (var i = 0; i < _points.Length; i++)
                points[i] = _points[_points.Length - 1 - i];
            return new Polyline(points);
        }

        private float2 SegmentDirection(int segment) => math.normalize(_points[segment + 1] - _points[segment]);

        private int FindSegment(float distance, out float t)
        {
            distance = math.clamp(distance, 0f, Length);

            var index = Array.BinarySearch(_distances, distance);
            if (index < 0)
                index = ~index - 1;
            index = math.clamp(index, 0, _points.Length - 2);

            var segmentLength = _distances[index + 1] - _distances[index];
            t = segmentLength > 0f ? (distance - _distances[index]) / segmentLength : 0f;
            return index;
        }
    }
}
