using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using TrafficGame.Presentation.Simulation;
using TrafficGame.Views.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.Views.Simulation
{
    /// <summary>
    /// Markings at lane ends: a solid bar at stop signs, a row of triangles where traffic yields and a colored bar
    /// showing what the traffic light shows to the lane. Each is drawn on the elevation level of its lane.
    /// </summary>
    public sealed class JunctionControlsView : ProceduralMeshView, IJunctionControlsView, IMapPaletteView
    {
        private const float StopBarThickness = 0.6f;
        private const float SignalBarThickness = 1.1f;
        private const float LaneShare = 0.9f;
        private const float TriangleDepth = 1.1f;
        private const int TrianglesPerLane = 3;

        private MeshBuilder _builder;
        private MapPalette _palette = MapPalette.Light();
        private IReadOnlyList<StopLine> _lines;

        protected override int SortingOrder => MapLayers.JunctionControls;

        public void ApplyPalette(MapPalette palette)
        {
            _palette = palette;
            if (_lines != null)
                Render(_lines);
        }

        public void Render(IReadOnlyList<StopLine> lines)
        {
            _lines = lines;
            Layers.Clear();
            foreach (var line in lines)
            {
                _builder = Layers[MapLayers.At(SortingOrder, line.Height)];
                switch (line.Kind)
                {
                    case StopLineKind.Stop:
                        AddBar(line, StopBarThickness, _palette.StopLine);
                        break;
                    case StopLineKind.Yield:
                        AddTriangles(line);
                        break;
                    default:
                        AddBar(line, SignalBarThickness, line.Kind switch
                        {
                            StopLineKind.Green => _palette.SignalGreen,
                            StopLineKind.Yellow => _palette.SignalYellow,
                            _ => _palette.SignalRed
                        });
                        break;
                }
            }

            Layers.Apply();
        }

        private void AddBar(StopLine line, float thickness, Color32 color)
        {
            var across = Geometry2D.Left(line.Direction) * line.Width * LaneShare * 0.5f;
            var back = line.Center - line.Direction * thickness;
            _builder.AddQuad(back - across, line.Center - across, line.Center + across, back + across, color);
        }

        /// <summary>Yield "shark teeth": triangles pointing against the traffic that must give way.</summary>
        private void AddTriangles(StopLine line)
        {
            var left = Geometry2D.Left(line.Direction);
            var size = line.Width * LaneShare / TrianglesPerLane;
            var start = line.Center - left * line.Width * LaneShare * 0.5f;
            for (var i = 0; i < TrianglesPerLane; i++)
            {
                var a = start + left * size * i;
                var b = a + left * size;
                var tip = (a + b) * 0.5f - line.Direction * TriangleDepth;
                _builder.AddTriangle(a, b, tip, _palette.StopLine);
            }
        }
    }
}
