using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Roads;
using TrafficGame.Views.Rendering;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrafficGame.Views.Roads
{
    /// <summary>
    /// Draws the road network in three passes per elevation level: surfaces, markings (lines and lane arrows before
    /// junctions), then curbs, so that later passes cover earlier ones. Roads above the ground cast a shadow that falls
    /// on whatever lies below; ramps switch level halfway between two levels. Where a road just continues through a node
    /// (a ramp meeting a bridge, a bend) it is drawn without a break.
    /// </summary>
    public sealed class RoadNetworkView : ProceduralMeshView, IRoadNetworkView, IMapPaletteView
    {
        private const float ArrowLength = 4.5f;
        private const float ArrowSetback = 7f;

        /// <summary>
        /// Shadow shift per meter of height (the sun stands up and to the left, like for buildings). Roads shift only
        /// sideways by its part across them, so a ramp never shades its own lower part.
        /// </summary>
        internal static readonly float2 ShadowPerMeter = new(0.6f, -0.6f);

        /// <summary>Nodes where two roads meet at least this straight are drawn as one continuous road.</summary>
        private const float ContinuationCos = -0.866f;

        /// <summary>Lower roads cast no visible shadow.</summary>
        private const float MinShadowHeight = 0.3f;

        /// <summary>From this height a road gets railings.</summary>
        private const float BridgeFrom = 1.5f;

        private const int SurfacePass = 0;
        private const int MarkingPass = 1;
        private const int CurbPass = 2;

        private readonly List<float2> _shadowPoints = new();
        private readonly HashSet<NodeId> _continuations = new();
        private readonly Dictionary<int, Material> _shadowMaterials = new();

        /// <summary>Shadows reach this far past the ends of their road at nodes, so neighbours overlap without a gap.</summary>
        private const float ShadowOverlap = 0.4f;

        /// <summary>…and start this far into the road where a ramp moves up a level, so they never touch the part below.</summary>
        private const float ShadowInset = 0.3f;
        private RoadStyle _style = new();
        private RoadLayout _layout;

        protected override int SortingOrder => MapLayers.Roads;

        public void ApplyPalette(MapPalette palette)
        {
            _style = palette.Roads;
            if (_layout != null)
                Render(_layout);
        }

        public void Render(RoadLayout layout)
        {
            _layout = layout;
            Layers.Clear();
            FindContinuations(layout);

            foreach (var segment in layout.Segments.Values)
            {
                foreach (var piece in Pieces(segment))
                    AddPiece(segment, piece);
            }

            foreach (var junction in layout.Junctions.Values)
                AddJunction(junction);

            AddArrows(layout);
            Layers.Apply();
        }

        private MeshBuilder Pass(int pass, int level) => Layers[MapLayers.OnLevel(SortingOrder + pass, level)];

        /// <summary>
        /// A level's shadows: overlapping pieces (at nodes, junctions, where a ramp changes level) must not darken twice,
        /// so the shadow materials mark the stencil and skip pixels some road shadow already darkened.
        /// </summary>
        private MeshBuilder Shadows(int level)
        {
            var order = MapLayers.OnLevel(SortingOrder + MapLayers.Shadows, level);
            if (!_shadowMaterials.ContainsKey(level))
            {
                var material = new Material(Renderer.sharedMaterial) { name = $"Road shadows {level}" };
                material.SetFloat("_StencilRef", 1f);
                material.SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
                material.SetFloat("_StencilPass", (float)StencilOp.Replace);
                _shadowMaterials.Add(level, material);
                Layers.UseMaterial(order, material);
            }

            return Layers[order];
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var material in _shadowMaterials.Values)
                Destroy(material);
        }

        /// <summary>Two roads of the same type meeting almost straight: nothing to show at the node.</summary>
        private void FindContinuations(RoadLayout layout)
        {
            _continuations.Clear();
            foreach (var junction in layout.Junctions.Values)
            {
                if (junction.Approaches.Count != 2)
                    continue;

                var a = junction.Approaches[0];
                var b = junction.Approaches[1];
                if (math.dot(a.Direction, b.Direction) <= ContinuationCos &&
                    layout.Segments[a.Segment].Segment.Profile == layout.Segments[b.Segment].Segment.Profile)
                    _continuations.Add(junction.Node);
            }
        }

        /// <summary>
        /// Splits the drawn part of a segment where a ramp crosses from one level's layer to the next. Heights are
        /// taken as linear along the drawn centerline, which reaches the node at continuations.
        /// </summary>
        private IEnumerable<Piece> Pieces(SegmentGeometry geometry)
        {
            var start = _continuations.Contains(geometry.Segment.Start) ? 0f : geometry.TrimStart;
            var end = geometry.Centerline.Length - (_continuations.Contains(geometry.Segment.End) ? 0f : geometry.TrimEnd);
            var line = start == geometry.TrimStart && end == geometry.Centerline.Length - geometry.TrimEnd
                ? geometry.Trimmed
                : geometry.Centerline.Slice(start, math.max(end, start + 0.01f));
            var from = geometry.HeightAt(start);
            var to = geometry.HeightAt(end);
            var startLevel = RoadElevation.LayerOf(from);
            var endLevel = RoadElevation.LayerOf(to);
            if (startLevel == endLevel || line.Length < 0.01f)
            {
                yield return new Piece(line, from, to, startLevel, startLevel, startLevel);
                yield break;
            }

            var step = endLevel > startLevel ? 1 : -1;
            var along = 0f;
            var level = startLevel;
            while (level != endLevel)
            {
                // The layer changes where the height passes halfway to the next level.
                var boundary = (level + (step > 0 ? 0.5f : -0.5f)) * RoadElevation.LevelHeight;
                var next = math.clamp((boundary - from) / (to - from) * line.Length, along, line.Length);
                if (next > along + 0.01f)
                    yield return new Piece(line.Slice(along, next), HeightAt(along), HeightAt(next), level, along > 0f ? level - step : level,
                        level + step);
                along = next;
                level += step;
            }

            if (line.Length > along + 0.01f)
                yield return new Piece(line.Slice(along, line.Length), HeightAt(along), to, endLevel, endLevel - step, endLevel);

            float HeightAt(float distance) => math.lerp(from, to, distance / line.Length);
        }

        private void AddPiece(SegmentGeometry geometry, Piece piece)
        {
            var profile = geometry.Segment.Profile;
            var halfWidth = profile.HalfWidth;
            AddShadow(piece, halfWidth + _style.BridgeEdgeWidth);

            Pass(SurfacePass, piece.Level).AddStrip(piece.Line, halfWidth * 2f, _style.Surface);

            var markings = Pass(MarkingPass, piece.Level);
            if (!profile.IsOneWay)
                markings.AddStrip(piece.Line.Offset(profile.DividerOffset), _style.DividerWidth, _style.Divider);
            AddLaneLines(markings, piece.Line, profile, forward: true);
            AddLaneLines(markings, piece.Line, profile, forward: false);

            // Above the ground the curbs become railings, which tells a bridge from a road at a glance.
            var curbs = Pass(CurbPass, piece.Level);
            var bridge = math.max(piece.From, piece.To) >= BridgeFrom;
            var edgeWidth = bridge ? _style.BridgeEdgeWidth : _style.CurbWidth;
            var edgeColor = bridge ? _style.BridgeEdge : _style.Curb;
            var edgeOffset = halfWidth + (edgeWidth - _style.CurbWidth) * 0.5f;
            curbs.AddStrip(piece.Line.Offset(edgeOffset), edgeWidth, edgeColor);
            curbs.AddStrip(piece.Line.Offset(-edgeOffset), edgeWidth, edgeColor);
        }

        private void AddJunction(JunctionGeometry junction)
        {
            if (_continuations.Contains(junction.Node))
                return;

            var level = RoadElevation.LayerOf(junction.Height);
            if (junction.Outline.Count >= 3)
            {
                if (junction.Height >= MinShadowHeight)
                {
                    var offset = ShadowPerMeter * junction.Height;
                    _shadowPoints.Clear();
                    foreach (var point in junction.Outline)
                        _shadowPoints.Add(point + offset);
                    Shadows(level).AddFan(junction.Center + offset, _shadowPoints, _style.BridgeShadow);
                }

                Pass(SurfacePass, level).AddFan(junction.Center, junction.Outline, _style.Surface);
            }

            var bridge = junction.Height >= BridgeFrom;
            foreach (var curb in junction.Curbs)
                Pass(CurbPass, level).AddStrip(curb, bridge ? _style.BridgeEdgeWidth : _style.CurbWidth, bridge ? _style.BridgeEdge : _style.Curb);
        }

        /// <summary>The road's outline shifted away from the sun by its height, under the road's own level.</summary>
        private void AddShadow(Piece piece, float halfWidth)
        {
            if (math.max(piece.From, piece.To) < MinShadowHeight)
                return;

            var line = piece.Line;
            // Next to a lower part of the same ramp the shadow keeps off it; next to a higher part (or a node) it reaches
            // under it, where the higher part covers it.
            var first = piece.LevelBefore < piece.Level ? math.min(ShadowInset, line.Length * 0.5f) : 0f;
            var last = piece.LevelAfter < piece.Level ? math.max(line.Length - ShadowInset, line.Length * 0.5f) : line.Length;
            _shadowPoints.Clear();
            if (piece.LevelBefore >= piece.Level)
                _shadowPoints.Add(ShadowPoint(piece, 0f) - line.StartDirection * (ShadowOverlap + ShadowInset));
            for (var i = 0; i < line.Count; i++)
            {
                var distance = math.clamp(line.DistanceAt(i), first, last);
                if (i > 0 && distance <= first || i < line.Count - 1 && distance >= last)
                    continue;
                _shadowPoints.Add(ShadowPoint(piece, distance));
            }

            if (piece.LevelAfter >= piece.Level)
                _shadowPoints.Add(ShadowPoint(piece, line.Length) + line.EndDirection * (ShadowOverlap + ShadowInset));

            Shadows(piece.Level).AddStrip(new Polyline(_shadowPoints), halfWidth * 2f, _style.BridgeShadow);
        }

        private static float2 ShadowPoint(Piece piece, float distance)
        {
            var line = piece.Line;
            var height = math.lerp(piece.From, piece.To, line.Length > 0f ? distance / line.Length : 0f);
            return line.PointAt(distance) + SideShadow(line.DirectionAt(distance), height);
        }

        /// <summary>The sideways part of the shadow shift of a road running along <paramref name="direction"/>.</summary>
        internal static float2 SideShadow(float2 direction, float height)
        {
            var across = Geometry2D.Left(direction);
            return across * math.dot(across, ShadowPerMeter) * height;
        }

        /// <summary>Arrows go where they tell something: on roads with several lanes in a direction or on restricted lanes.</summary>
        private void AddArrows(RoadLayout layout)
        {
            foreach (var lane in layout.Lanes.Lanes)
            {
                if (lane.Arrows == LaneArrows.None || lane.Path.Length < ArrowSetback * 2f)
                    continue;

                var segment = layout.Segments[lane.Segment].Segment;
                if (lane.Arrows == lane.AvailableArrows && segment.Profile.LaneCountIn(lane.IsForward) == 1)
                    continue;

                var along = lane.Path.Length - ArrowSetback;
                var level = RoadElevation.LayerOf(lane.IsForward ? segment.EndHeight : segment.StartHeight);
                ArrowGlyph.Add(Pass(MarkingPass, level), lane.Path.PointAt(along), lane.Path.DirectionAt(along), ArrowLength, lane.Arrows,
                    _style.LaneLine);
            }
        }

        private void AddLaneLines(MeshBuilder builder, Polyline centerline, RoadProfile profile, bool forward)
        {
            for (var index = 1; index < profile.LaneCountIn(forward); index++)
            {
                var line = centerline.Offset(profile.LaneBoundaryOffset(forward, index));
                builder.AddDashes(line, _style.LaneLineWidth, _style.DashLength, _style.DashGap, _style.LaneLine);
            }
        }

        /// <summary>A stretch of road drawn on one level; its height runs from <see cref="From"/> to <see cref="To"/>.</summary>
        private readonly struct Piece
        {
            public Piece(Polyline line, float from, float to, int level, int levelBefore, int levelAfter)
            {
                Line = line;
                From = from;
                To = to;
                Level = level;
                LevelBefore = levelBefore;
                LevelAfter = levelAfter;
            }

            public Polyline Line { get; }
            public float From { get; }
            public float To { get; }
            public int Level { get; }

            /// <summary>Layer of the same road just before the piece (its own level at a node).</summary>
            public int LevelBefore { get; }

            public int LevelAfter { get; }
        }
    }
}
