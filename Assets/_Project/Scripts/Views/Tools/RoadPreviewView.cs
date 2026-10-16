using System.Collections.Generic;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Tools;
using TrafficGame.Views.Rendering;
using TrafficGame.Views.Roads;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.Views.Tools
{
    /// <summary>
    /// Translucent overlay of the road being drawn, the alignment guide and snap markers; for the junction tools
    /// the junction under the pointer and its lane arrow markers.
    /// </summary>
    public sealed class RoadPreviewView : ProceduralMeshView, IRoadPreviewView, IJunctionPreviewView, IMapPaletteView
    {
        private const float JunctionApproachExtent = 4f;
        private const float LaneMarkerRadius = 1.7f;
        private const float LaneMarkerArrowLength = 2.4f;
        private static readonly Color32 LaneMarkerColor = new(255, 255, 255, 235);
        private static readonly Color32 LaneMarkerHighlight = new(255, 214, 90, 255);
        private static readonly Color32 LaneMarkerArrow = new(50, 50, 50, 255);

        private const float GuideHalfLength = 400f;
        private const float GuideWidth = 0.5f;
        private const float GuideDash = 3f;
        private const float MarkerRadius = 2.5f;
        private const float ControlMarkerRadius = 1.8f;
        private const float RemovalMargin = 1f;

        private readonly MeshBuilder _builder = new();
        private readonly List<float2> _shadowPoints = new();
        private RoadStyle _style = new();

        protected override int SortingOrder => MapLayers.Preview;

        public void ApplyPalette(MapPalette palette)
        {
            _style = palette.Roads;
        }

        public void ShowAnchor(RoadAnchor anchor)
        {
            _builder.Clear();
            AddMarker(anchor);
            Apply(_builder);
        }

        public void ShowPlan(RoadPlan plan, RoadGuide guide, float2? control)
        {
            _builder.Clear();

            if (guide.Kind != GuideKind.None)
            {
                var from = guide.Origin - guide.Direction * GuideHalfLength;
                var to = guide.Origin + guide.Direction * GuideHalfLength;
                _builder.AddDashes(new Polyline(new[] { from, to }), GuideWidth, GuideDash, GuideDash, _style.PreviewGuide);
            }

            if (plan.Path != null)
            {
                AddShadow(plan);
                var color = plan.IsValid ? _style.PreviewValid : _style.PreviewInvalid;
                _builder.AddStrip(plan.Path, plan.Request.Profile.HalfWidth * 2f, color);
            }

            AddMarker(plan.Request.Start);
            AddMarker(plan.Request.End);
            if (control.HasValue)
                _builder.AddCircle(control.Value, ControlMarkerRadius, _style.PreviewGuide);

            Apply(_builder);
        }

        public void ShowRoundabout(RoundaboutPlan plan)
        {
            _builder.Clear();
            var color = plan.IsValid ? _style.PreviewValid : _style.PreviewInvalid;
            _builder.AddStrip(plan.Ring, plan.Profile.HalfWidth * 2f, color);
            _builder.AddCircle(plan.Center, ControlMarkerRadius, _style.PreviewGuide);
            Apply(_builder);
        }

        public void ShowRemoval(SegmentGeometry segment)
        {
            _builder.Clear();
            _builder.AddStrip(segment.Centerline, segment.Segment.Profile.HalfWidth * 2f + RemovalMargin, _style.PreviewInvalid);
            Apply(_builder);
        }

        public void ShowJunction(JunctionGeometry junction, bool valid)
        {
            _builder.Clear();
            AddJunctionArea(junction, valid ? _style.PreviewValid : _style.PreviewInvalid);
            Apply(_builder);
        }

        public void ShowLaneArrows(JunctionGeometry junction, IReadOnlyList<LaneArrowMarker> markers, int highlighted)
        {
            _builder.Clear();
            AddJunctionArea(junction, _style.PreviewValid);
            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                _builder.AddCircle(marker.Position, LaneMarkerRadius + 0.25f, _style.PreviewAnchor);
                _builder.AddCircle(marker.Position, LaneMarkerRadius, i == highlighted ? LaneMarkerHighlight : LaneMarkerColor);
                ArrowGlyph.Add(_builder, marker.Position, marker.Direction, LaneMarkerArrowLength, marker.Arrows, LaneMarkerArrow);
            }

            Apply(_builder);
        }

        public void Hide()
        {
            _builder.Clear();
            Apply(_builder);
        }

        /// <summary>An elevated road casts its shadow already while it is drawn, which shows how high it runs.</summary>
        private void AddShadow(RoadPlan plan)
        {
            if (math.max(plan.StartHeight, plan.EndHeight) < 0.3f)
                return;

            var path = plan.Path;
            _shadowPoints.Clear();
            for (var i = 0; i < path.Count; i++)
            {
                var distance = path.DistanceAt(i);
                _shadowPoints.Add(path[i] + RoadNetworkView.SideShadow(path.DirectionAt(distance), plan.HeightAt(path.ParameterAt(distance))));
            }
            _builder.AddStrip(new Polyline(_shadowPoints), plan.Request.Profile.HalfWidth * 2f, _style.BridgeShadow);
        }

        /// <summary>The junction surface plus the ends of its roads.</summary>
        private void AddJunctionArea(JunctionGeometry junction, Color32 color)
        {
            if (junction.Outline.Count >= 3)
                _builder.AddFan(junction.Center, junction.Outline, color);
            foreach (var approach in junction.Approaches)
            {
                var extent = approach.Direction * JunctionApproachExtent;
                _builder.AddQuad(approach.Left, approach.Right, approach.Right + extent, approach.Left + extent, color);
            }
        }

        private void AddMarker(RoadAnchor anchor)
        {
            Color32 color = anchor.IsAttached ? _style.PreviewAnchor : _style.PreviewFreeAnchor;
            _builder.AddCircle(anchor.Position, MarkerRadius, color);
        }
    }
}
