using TrafficGame.Views.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.Views.Debugging
{
    /// <summary>
    /// Placeholder ground: a subtle checkerboard that makes camera movement visible
    /// until real terrain rendering exists.
    /// </summary>
    public sealed class CheckerGroundView : ProceduralMeshView
    {
        protected override int SortingOrder => MapLayers.Ground;

        public void Build(float2 min, float2 max, float cellSize, Color32 colorA, Color32 colorB)
        {
            var builder = new MeshBuilder();
            var cells = (int2)math.ceil((max - min) / cellSize);

            for (var y = 0; y < cells.y; y++)
            for (var x = 0; x < cells.x; x++)
            {
                var cellMin = min + new float2(x, y) * cellSize;
                var cellMax = math.min(cellMin + cellSize, max);
                builder.AddRect(cellMin, cellMax, (x + y) % 2 == 0 ? colorA : colorB);
            }

            Apply(builder);
        }
    }
}
