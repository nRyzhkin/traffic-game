using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrafficGame.Views.Rendering
{
    /// <summary>Accumulates flat vertex-colored geometry in the XY plane and writes it into a <see cref="Mesh"/>.</summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Color32> _colors = new();
        private readonly List<int> _indices = new();

        public int VertexCount => _vertices.Count;

        public void Clear()
        {
            _vertices.Clear();
            _colors.Clear();
            _indices.Clear();
        }

        /// <summary>Adds an axis-aligned rectangle.</summary>
        public void AddRect(float2 min, float2 max, Color32 color, float z = 0f) =>
            AddQuad(min, new float2(max.x, min.y), max, new float2(min.x, max.y), color, z);

        /// <summary>Adds a quad from four corners given counter-clockwise.</summary>
        public void AddQuad(float2 a, float2 b, float2 c, float2 d, Color32 color, float z = 0f)
        {
            var first = _vertices.Count;
            AddVertex(a, color, z);
            AddVertex(b, color, z);
            AddVertex(c, color, z);
            AddVertex(d, color, z);

            _indices.Add(first);
            _indices.Add(first + 2);
            _indices.Add(first + 1);
            _indices.Add(first);
            _indices.Add(first + 3);
            _indices.Add(first + 2);
        }

        public void AddTriangle(float2 a, float2 b, float2 c, Color32 color, float z = 0f)
        {
            var first = _vertices.Count;
            AddVertex(a, color, z);
            AddVertex(b, color, z);
            AddVertex(c, color, z);
            _indices.Add(first);
            _indices.Add(first + 1);
            _indices.Add(first + 2);
        }

        /// <summary>Adds a band of constant <paramref name="width"/> centered on <paramref name="line"/>.</summary>
        public void AddStrip(Polyline line, float width, Color32 color, float z = 0f)
        {
            var halfWidth = width * 0.5f;
            var previousLeft = line[0] + line.VertexNormal(0) * halfWidth;
            var previousRight = line[0] - line.VertexNormal(0) * halfWidth;

            for (var i = 1; i < line.Count; i++)
            {
                var normal = line.VertexNormal(i) * halfWidth;
                var left = line[i] + normal;
                var right = line[i] - normal;
                AddQuad(previousRight, right, left, previousLeft, color, z);
                previousLeft = left;
                previousRight = right;
            }
        }

        /// <summary>Adds a dashed band; the pattern starts half a gap in so dashes sit away from the ends.</summary>
        public void AddDashes(Polyline line, float width, float dashLength, float gapLength, Color32 color, float z = 0f)
        {
            for (var start = gapLength * 0.5f; start < line.Length; start += dashLength + gapLength)
            {
                var end = math.min(start + dashLength, line.Length);
                if (end - start > 0.01f)
                    AddStrip(line.Slice(start, end), width, color, z);
            }
        }

        /// <summary>Fills a polygon that is star-shaped around <paramref name="center"/>.</summary>
        public void AddFan(float2 center, IReadOnlyList<float2> ring, Color32 color, float z = 0f)
        {
            for (var i = 0; i < ring.Count; i++)
                AddTriangle(center, ring[i], ring[(i + 1) % ring.Count], color, z);
        }

        public void AddCircle(float2 center, float radius, Color32 color, int segments = 24, float z = 0f)
        {
            var ring = new float2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = 2f * math.PI * i / segments;
                ring[i] = center + new float2(math.cos(angle), math.sin(angle)) * radius;
            }

            AddFan(center, ring, color, z);
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = _vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_indices, 0);
            mesh.RecalculateBounds();
        }

        private void AddVertex(float2 position, Color32 color, float z)
        {
            _vertices.Add(new Vector3(position.x, position.y, z));
            _colors.Add(color);
        }
    }
}
