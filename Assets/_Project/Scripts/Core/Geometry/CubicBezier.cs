using Unity.Mathematics;

namespace TrafficGame.Core.Geometry
{
    /// <summary>Planar cubic Bézier curve.</summary>
    public readonly struct CubicBezier
    {
        public CubicBezier(float2 p0, float2 p1, float2 p2, float2 p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public float2 P0 { get; }
        public float2 P1 { get; }
        public float2 P2 { get; }
        public float2 P3 { get; }

        /// <summary>Upper/lower bound average of the arc length; good enough to choose a sampling density.</summary>
        public float ApproximateLength =>
            (math.distance(P0, P3) + math.distance(P0, P1) + math.distance(P1, P2) + math.distance(P2, P3)) * 0.5f;

        /// <summary>A straight line expressed as a Bézier, with evenly spaced control points.</summary>
        public static CubicBezier Line(float2 from, float2 to) =>
            new(from, math.lerp(from, to, 1f / 3f), math.lerp(from, to, 2f / 3f), to);

        /// <summary>A curve leaving <paramref name="from"/> along <paramref name="startDirection"/> and arriving at <paramref name="to"/> along <paramref name="endDirection"/>.</summary>
        public static CubicBezier Hermite(float2 from, float2 startDirection, float2 to, float2 endDirection, float handleLength) =>
            new(from, from + startDirection * handleLength, to - endDirection * handleLength, to);

        /// <summary>A quadratic curve through a single control point, expressed as a cubic.</summary>
        public static CubicBezier Quadratic(float2 from, float2 control, float2 to) =>
            new(from, from + (control - from) * (2f / 3f), to + (control - to) * (2f / 3f), to);

        /// <summary>Splits the curve at parameter <paramref name="t"/> into two curves that together trace it exactly.</summary>
        public void Split(float t, out CubicBezier first, out CubicBezier second)
        {
            var p01 = math.lerp(P0, P1, t);
            var p12 = math.lerp(P1, P2, t);
            var p23 = math.lerp(P2, P3, t);
            var p012 = math.lerp(p01, p12, t);
            var p123 = math.lerp(p12, p23, t);
            var point = math.lerp(p012, p123, t);

            first = new CubicBezier(P0, p01, p012, point);
            second = new CubicBezier(point, p123, p23, P3);
        }

        public float2 Evaluate(float t)
        {
            var u = 1f - t;
            return u * u * u * P0 + 3f * u * u * t * P1 + 3f * u * t * t * P2 + t * t * t * P3;
        }

        public float2 Derivative(float t)
        {
            var u = 1f - t;
            return 3f * u * u * (P1 - P0) + 6f * u * t * (P2 - P1) + 3f * t * t * (P3 - P2);
        }
    }
}
