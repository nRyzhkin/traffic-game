using TrafficGame.Core.Geometry;
using TrafficGame.Core.Lanes;
using TrafficGame.Presentation.Roads;
using TrafficGame.Views.Rendering;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrafficGame.Views.Debugging
{
    /// <summary>
    /// Overlay of lane centerlines (blue, with an arrowhead in the direction of travel) and junction
    /// connections (orange). Toggle with the L key.
    /// </summary>
    public sealed class LaneGraphDebugView : ProceduralMeshView, ILaneGraphView
    {
        private const float LineWidth = 0.3f;
        private const float ArrowLength = 2.5f;
        private const float ArrowHalfWidth = 1f;

        private static readonly Color32 LaneColor = new(50, 120, 230, 220);
        private static readonly Color32 ConnectionColor = new(240, 140, 30, 200);

        private readonly MeshBuilder _builder = new();

        protected override int SortingOrder => MapLayers.Debug;

        public void Render(LaneGraph graph)
        {
            _builder.Clear();

            foreach (var connection in graph.Connections)
                _builder.AddStrip(connection.Path, LineWidth, ConnectionColor);

            foreach (var lane in graph.Lanes)
            {
                _builder.AddStrip(lane.Path, LineWidth, LaneColor);
                AddArrowhead(lane.Path);
            }

            Apply(_builder);
        }

        private void Awake() => Renderer.enabled = false;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.lKey.wasPressedThisFrame)
                Renderer.enabled = !Renderer.enabled;
        }

        private void AddArrowhead(Polyline path)
        {
            var tip = path.End;
            var direction = path.EndDirection;
            var side = Geometry2D.Left(direction) * ArrowHalfWidth;
            var baseCenter = tip - direction * math.min(ArrowLength, path.Length);
            _builder.AddTriangle(baseCenter + side, baseCenter - side, tip, LaneColor);
        }
    }
}
