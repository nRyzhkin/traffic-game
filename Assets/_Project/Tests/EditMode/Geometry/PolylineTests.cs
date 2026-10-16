using System;
using NUnit.Framework;
using TrafficGame.Core.Geometry;
using Unity.Mathematics;

namespace TrafficGame.Tests.Geometry
{
    public sealed class PolylineTests
    {
        private const float Tolerance = 1e-4f;

        // (0,0) → (3,0) → (3,4): length 7 with a left turn at (3,0).
        private static Polyline LShape() => new(new[] { new float2(0f, 0f), new float2(3f, 0f), new float2(3f, 4f) });

        [Test]
        public void Length_IsSumOfSegments()
        {
            Assert.That(LShape().Length, Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void PointAt_InterpolatesAcrossVertices()
        {
            var point = LShape().PointAt(5f);

            AssertClose(point, new float2(3f, 2f));
        }

        [Test]
        public void Slice_KeepsInteriorVertices()
        {
            var slice = LShape().Slice(1f, 6f);

            Assert.That(slice.Count, Is.EqualTo(3));
            AssertClose(slice.Start, new float2(1f, 0f));
            AssertClose(slice[1], new float2(3f, 0f));
            AssertClose(slice.End, new float2(3f, 3f));
            Assert.That(slice.Length, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void Offset_PositiveDistanceGoesLeft()
        {
            var line = new Polyline(new[] { new float2(0f, 0f), new float2(10f, 0f) });

            var offset = line.Offset(2f);

            AssertClose(offset.Start, new float2(0f, 2f));
            AssertClose(offset.End, new float2(10f, 2f));
        }

        [Test]
        public void Offset_KeepsDistanceFromBothSegmentsAtCorner()
        {
            var line = new Polyline(new[] { new float2(0f, 0f), new float2(10f, 0f), new float2(10f, 10f) });

            var offset = line.Offset(1f);

            AssertClose(offset[1], new float2(9f, 1f));
        }

        [Test]
        public void Reversed_SwapsEndsAndDirections()
        {
            var reversed = LShape().Reversed();

            AssertClose(reversed.Start, new float2(3f, 4f));
            AssertClose(reversed.StartDirection, new float2(0f, -1f));
        }

        [Test]
        public void Constructor_RejectsFewerThanTwoDistinctPoints()
        {
            Assert.Throws<ArgumentException>(() => new Polyline(new[] { new float2(1f, 1f), new float2(1f, 1f) }));
        }

        [Test]
        public void FromBezier_StraightLineHasExactLength()
        {
            var line = Polyline.FromBezier(CubicBezier.Line(float2.zero, new float2(30f, 40f)), maxSegmentLength: 2f);

            Assert.That(line.Length, Is.EqualTo(50f).Within(1e-3f));
        }

        private static void AssertClose(float2 actual, float2 expected) =>
            Assert.That(math.distance(actual, expected), Is.LessThan(Tolerance), $"Expected {expected}, got {actual}.");
    }
}
