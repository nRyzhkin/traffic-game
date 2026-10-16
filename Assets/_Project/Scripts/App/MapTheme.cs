using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Presentation;
using TrafficGame.Presentation.Settings;
using TrafficGame.Views.Debugging;
using TrafficGame.Views.Rendering;
using UnityEngine;

namespace TrafficGame.App
{
    /// <summary>
    /// Repaints the map in the palette of the current theme: ground, camera background and every map view.
    /// A theme change cross-fades the colors over a fraction of a second (<see cref="Update"/> drives it).
    /// </summary>
    public sealed class MapTheme : IThemeView
    {
        private const float FadeSeconds = 0.35f;

        private readonly GameConfig _config;
        private readonly CheckerGroundView _ground;
        private readonly Camera _camera;
        private readonly IReadOnlyList<IMapPaletteView> _views;
        private MapPalette _shown;
        private MapPalette _from;
        private MapPalette _to;
        private float _elapsed;

        public MapTheme(GameConfig config, CheckerGroundView ground, Camera camera, IEnumerable<IMapPaletteView> views)
        {
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _ground = ground;
            _camera = camera;
            _views = views.Where(view => view != null).ToArray();
        }

        /// <summary>The first theme is painted at once; later changes fade.</summary>
        public void ApplyTheme(bool dark)
        {
            var palette = dark ? _config.DarkMap : _config.LightMap;
            if (_shown == null)
            {
                _to = palette;
                Paint(palette);
                return;
            }

            if (palette == _to)
                return;

            _from = _shown;
            _to = palette;
            _elapsed = 0f;
        }

        public void Update(float deltaTime)
        {
            if (_from == null)
                return;

            _elapsed += deltaTime;
            var t = Mathf.Clamp01(_elapsed / FadeSeconds);
            Paint(t >= 1f ? _to : MapPalette.Lerp(_from, _to, Easing.InOutCubic(t)));
            if (t >= 1f)
                _from = null;
        }

        private void Paint(MapPalette palette)
        {
            _shown = palette;
            _ground.Build(_config.WorldMin, _config.WorldMax, _config.GroundCellSize, palette.GroundA, palette.GroundB);
            _camera.backgroundColor = palette.Background;
            foreach (var view in _views)
                view.ApplyPalette(palette);
        }
    }
}
