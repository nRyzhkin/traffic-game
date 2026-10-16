using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.Views.Rendering
{
    /// <summary>Lane arrow markings: a stem with a head for straight on and branches bending left or right.</summary>
    public static class ArrowGlyph
    {
        private const float StemWidth = 0.11f;
        private const float HeadHalfWidth = 0.17f;

        /// <param name="length">Size of the glyph along <paramref name="forward"/>, in meters.</param>
        public static void Add(MeshBuilder builder, float2 center, float2 forward, float length, LaneArrows arrows, Color32 color, float z = 0f)
        {
            if (arrows == LaneArrows.None)
                return;

            var right = new float2(forward.y, -forward.x);
            float2 P(float x, float y) => center + (right * x + forward * y) * length;
            var width = StemWidth * length;

            var straight = (arrows & LaneArrows.Straight) != 0;
            builder.AddStrip(new Polyline(new[] { P(0f, -0.5f), P(0f, straight ? 0.24f : 0.02f) }), width, color, z);
            if (straight)
                builder.AddTriangle(P(-HeadHalfWidth, 0.22f), P(HeadHalfWidth, 0.22f), P(0f, 0.5f), color, z);
            if ((arrows & LaneArrows.Left) != 0)
                AddBranch(-1f);
            if ((arrows & LaneArrows.Right) != 0)
                AddBranch(1f);

            void AddBranch(float side)
            {
                builder.AddStrip(new Polyline(new[] { P(0f, -0.12f), P(side * 0.06f, 0.05f), P(side * 0.13f, 0.1f), P(side * 0.24f, 0.1f) }),
                    width, color, z);
                builder.AddTriangle(P(side * 0.22f, 0.1f - HeadHalfWidth), P(side * 0.22f, 0.1f + HeadHalfWidth), P(side * 0.46f, 0.1f), color, z);
            }
        }
    }
}
