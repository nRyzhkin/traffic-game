using Unity.Mathematics;

namespace TrafficGame.Core.Geometry
{
    /// <summary>Small planar helpers. Angles are counter-clockwise positive.</summary>
    public static class Geometry2D
    {
        private const float ParallelEpsilon = 1e-5f;

        /// <summary>Z component of the 3D cross product; positive when <paramref name="b"/> is counter-clockwise of <paramref name="a"/>.</summary>
        public static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        /// <summary>The vector rotated 90° counter-clockwise, i.e. pointing to its left.</summary>
        public static float2 Left(float2 v) => new(-v.y, v.x);

        /// <summary>Signed angle in radians from <paramref name="from"/> to <paramref name="to"/>, in (-π, π].</summary>
        public static float SignedAngle(float2 from, float2 to) => math.atan2(Cross(from, to), math.dot(from, to));

        /// <summary>
        /// Intersects the lines <c>p + s·dp</c> and <c>q + u·dq</c>.
        /// Returns false when they are (nearly) parallel.
        /// </summary>
        public static bool TryIntersectLines(float2 p, float2 dp, float2 q, float2 dq, out float s, out float u)
        {
            var denominator = Cross(dp, dq);
            if (math.abs(denominator) < ParallelEpsilon)
            {
                s = u = 0f;
                return false;
            }

            var offset = q - p;
            s = Cross(offset, dq) / denominator;
            u = Cross(offset, dp) / denominator;
            return true;
        }

        /// <summary>Intersects segments a0–a1 and b0–b1; <paramref name="s"/> and <paramref name="u"/> are fractions along each.</summary>
        public static bool TryIntersectSegments(float2 a0, float2 a1, float2 b0, float2 b1, out float s, out float u) =>
            TryIntersectLines(a0, a1 - a0, b0, b1 - b0, out s, out u) && s >= 0f && s <= 1f && u >= 0f && u <= 1f;

        /// <summary>Closest point to <paramref name="point"/> on segment a–b, as a fraction along it.</summary>
        public static float ClosestFraction(float2 a, float2 b, float2 point)
        {
            var ab = b - a;
            var lengthSquared = math.lengthsq(ab);
            return lengthSquared > 0f ? math.saturate(math.dot(point - a, ab) / lengthSquared) : 0f;
        }
    }
}
