using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Simulation;
using TrafficGame.Views.Rendering;
using UnityEngine;

namespace TrafficGame.Views.Simulation
{
    /// <summary>
    /// Red glow over jammed lanes, stronger where traffic stands still. Jam levels arrive a few times a second;
    /// the glow eases towards them every frame, so it swells and fades instead of flicking on and off.
    /// </summary>
    public sealed class CongestionView : ProceduralMeshView, ICongestionView, IMapPaletteView
    {
        private const float GlowWidth = 7f;
        private const byte MinAlpha = 40;
        private const byte MaxAlpha = 150;

        /// <summary>Time constant of the glow following the jam level, seconds.</summary>
        private const float FadeTime = 0.35f;

        private readonly Dictionary<Polyline, float> _targets = new();
        private readonly Dictionary<Polyline, float> _heights = new();
        private readonly Dictionary<Polyline, float> _shown = new();
        private readonly List<Polyline> _paths = new();
        private Color32 _color = MapPalette.Light().Congestion;
        private bool _dirty;

        protected override int SortingOrder => MapLayers.Congestion;

        public void ApplyPalette(MapPalette palette)
        {
            _color = palette.Congestion;
            _dirty = true;
        }

        public void Render(IReadOnlyList<LaneCongestion> lanes)
        {
            _targets.Clear();
            foreach (var lane in lanes)
            {
                _targets[lane.Path] = Mathf.Lerp(MinAlpha, MaxAlpha, lane.Level);
                _heights[lane.Path] = lane.Height;
            }
            _dirty = true;
        }

        private void Update()
        {
            if (!_dirty)
                return;

            // Opacities start from nothing, fade back to nothing and otherwise move towards their targets.
            _paths.Clear();
            _paths.AddRange(_shown.Keys);
            foreach (var path in _targets.Keys)
            {
                if (!_shown.ContainsKey(path))
                    _paths.Add(path);
            }

            var k = 1f - Mathf.Exp(-Time.deltaTime / FadeTime);
            var settled = true;
            Layers.Clear();
            foreach (var path in _paths)
            {
                _targets.TryGetValue(path, out var target);
                _shown.TryGetValue(path, out var shown);
                shown = Mathf.Lerp(shown, target, k);
                if (Mathf.Abs(shown - target) < 1f)
                    shown = target;
                else
                    settled = false;

                if (shown <= 0f)
                {
                    _shown.Remove(path);
                    _heights.Remove(path);
                    continue;
                }

                _shown[path] = shown;
                var color = _color;
                // The palette's alpha scales the glow: a dark map needs less of it to read as red.
                color.a = (byte)(shown * _color.a / 255f);
                Layers[MapLayers.At(SortingOrder, _heights.TryGetValue(path, out var height) ? height : 0f)].AddStrip(path, GlowWidth, color);
            }

            Layers.Apply();
            _dirty = !settled;
        }
    }
}
