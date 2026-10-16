using System;
using TrafficGame.Presentation.Simulation;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Cameras
{
    /// <summary>How the camera gets to a new view.</summary>
    public enum CameraMove
    {
        /// <summary>Jump there at once.</summary>
        Cut,

        /// <summary>Glide there from the current view.</summary>
        Glide,

        /// <summary>After a scene change: start a little wider and settle into the view.</summary>
        Arrive
    }

    /// <summary>
    /// Pans and zooms the map camera from <see cref="ICameraInput"/> gestures and converts between screen and world
    /// coordinates for tools. Nothing jumps: a released drag coasts and slows down, wheel steps zoom smoothly around the
    /// cursor, and programmatic moves glide with an ease curve. Fingers and drags are followed exactly.
    /// </summary>
    public sealed class CameraPresenter : IScreenToWorld, IDisposable
    {
        /// <summary>Time constant of the coasting after a drag, seconds (about the iOS scroll deceleration).</summary>
        private const float CoastTime = 0.35f;

        /// <summary>Time constant of a wheel zoom step, seconds.</summary>
        private const float ZoomTime = 0.09f;

        /// <summary>How quickly the measured drag velocity follows the finger, seconds.</summary>
        private const float VelocitySmoothing = 0.04f;

        /// <summary>Releases slower than this (screen pixels per second) do not coast.</summary>
        private const float MinCoastSpeed = 80f;

        private const float MaxCoastSpeed = 5000f;
        private const float StopSpeed = 8f;
        private const float GlideDuration = 0.65f;
        private const float ArriveDuration = 0.9f;

        /// <summary>How much wider than the target view an <see cref="CameraMove.Arrive"/> starts.</summary>
        private const float ArriveZoomOut = 1.18f;

        private readonly ICameraView _view;
        private readonly ICameraInput _input;
        private readonly IFrameSource _frames;
        private readonly CameraSettings _settings;

        private bool _dragging;
        private float2 _frameDrag;
        private float2 _velocity;

        private bool _zooming;
        private float _zoomTarget;
        private float2 _zoomAnchorScreen;
        private float2 _zoomAnchorWorld;

        private bool _flying;
        private float _flyElapsed;
        private float _flyDuration;
        private bool _flyEaseIn;
        private float2 _flyFromCenter;
        private float2 _flyToCenter;
        private float _flyFromHalfHeight;
        private float _flyToHalfHeight;

        public CameraPresenter(ICameraView view, ICameraInput input, IFrameSource frames, CameraSettings settings, float2 initialCenter)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _frames = frames ?? throw new ArgumentNullException(nameof(frames));
            _settings = settings;

            HalfHeight = settings.InitialHalfHeight;
            Center = ClampToBounds(initialCenter);

            _input.Panned += OnPanned;
            _input.PanEnded += OnPanEnded;
            _input.Zoomed += OnZoomed;
            _input.ZoomStepped += OnZoomStepped;
            _frames.FrameUpdated += Tick;
            Apply();
        }

        public float2 Center { get; private set; }

        public float HalfHeight { get; private set; }

        /// <summary>Whether the camera is still moving on its own (coasting, zooming or gliding).</summary>
        public bool IsMoving => _flying || _zooming || math.any(_velocity != float2.zero);

        private float WorldUnitsPerPixel => UnitsPerPixel(HalfHeight);

        public float2 ScreenToWorld(float2 screenPoint) =>
            Center + (screenPoint - _view.ViewportSize * 0.5f) * WorldUnitsPerPixel;

        /// <summary>Moves the camera to look at <paramref name="center"/> with the given half height.</summary>
        public void Focus(float2 center, float halfHeight, CameraMove move = CameraMove.Cut) =>
            MoveTo(center, ClampHalfHeight(halfHeight), move);

        /// <summary>
        /// Zooms and moves the camera so the world rectangle from <paramref name="min"/> to <paramref name="max"/> fills
        /// the part of the screen the UI leaves free, as large as it fits.
        /// </summary>
        public void Frame(float2 min, float2 max, ScreenInsets insets, CameraMove move = CameraMove.Cut)
        {
            var viewport = _view.ViewportSize;
            var free = new float2(viewport.x - insets.Left - insets.Right, viewport.y - insets.Top - insets.Bottom);
            free = math.max(free, viewport * 0.2f);

            var size = math.max(max - min, 1f);
            var halfHeight = ClampHalfHeight(math.max(size.x / free.x, size.y / free.y) * viewport.y * 0.5f);
            var unitsPerPixel = UnitsPerPixel(halfHeight);

            // Put the rectangle's center in the middle of the free area (screen Y points up, insets are from the edges).
            var freeCenter = new float2(insets.Left + free.x * 0.5f, insets.Bottom + free.y * 0.5f);
            MoveTo((min + max) * 0.5f - (freeCenter - viewport * 0.5f) * unitsPerPixel, halfHeight, move);
        }

        /// <summary>Advances coasting, zoom steps and glides; the frame source calls it every frame.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (_dragging)
            {
                // The finger's velocity, smoothed over a few frames; a pause before letting go brings it to zero.
                var sample = _frameDrag / deltaTime;
                _velocity = math.lerp(_velocity, sample, 1f - math.exp(-deltaTime / VelocitySmoothing));
                _frameDrag = float2.zero;
                return;
            }

            if (_flying)
            {
                TickFlight(deltaTime);
                return;
            }

            if (!_zooming && math.all(_velocity == float2.zero))
                return;

            if (math.any(_velocity != float2.zero))
                TickCoast(deltaTime);
            if (_zooming)
                TickZoom(deltaTime);
            Apply();
        }

        public void Dispose()
        {
            _input.Panned -= OnPanned;
            _input.PanEnded -= OnPanEnded;
            _input.Zoomed -= OnZoomed;
            _input.ZoomStepped -= OnZoomStepped;
            _frames.FrameUpdated -= Tick;
        }

        private void MoveTo(float2 center, float halfHeight, CameraMove move)
        {
            Stop();
            center = ClampToBounds(center);
            if (move == CameraMove.Cut)
            {
                Center = center;
                HalfHeight = halfHeight;
                Apply();
                return;
            }

            if (move == CameraMove.Arrive)
            {
                Center = center;
                HalfHeight = ClampHalfHeight(halfHeight * ArriveZoomOut);
            }

            _flying = true;
            _flyElapsed = 0f;
            _flyDuration = move == CameraMove.Arrive ? ArriveDuration : GlideDuration;
            _flyEaseIn = move == CameraMove.Glide;
            _flyFromCenter = Center;
            _flyFromHalfHeight = HalfHeight;
            _flyToCenter = center;
            _flyToHalfHeight = halfHeight;
            Apply();
        }

        private void TickFlight(float deltaTime)
        {
            _flyElapsed += deltaTime;
            var t = math.saturate(_flyElapsed / _flyDuration);
            var eased = _flyEaseIn ? Easing.InOutCubic(t) : Easing.OutCubic(t);

            // Zoom in log space so zooming in and out feel equally fast.
            HalfHeight = math.exp(math.lerp(math.log(_flyFromHalfHeight), math.log(_flyToHalfHeight), eased));
            Center = math.lerp(_flyFromCenter, _flyToCenter, eased);
            _flying = t < 1f;
            Apply();
        }

        private void TickCoast(float deltaTime)
        {
            var before = Center;
            Center = ClampToBounds(Center + _velocity * deltaTime);

            // Hitting the edge of the map stops the motion along that axis.
            var moved = Center - before;
            _velocity = math.select(_velocity, float2.zero, math.abs(moved) < math.abs(_velocity * deltaTime) * 0.5f);
            _velocity *= math.exp(-deltaTime / CoastTime);
            if (math.length(_velocity) / WorldUnitsPerPixel < StopSpeed)
                _velocity = float2.zero;

            _zoomAnchorWorld += moved;
        }

        private void TickZoom(float deltaTime)
        {
            var k = 1f - math.exp(-deltaTime / ZoomTime);
            var logHeight = math.lerp(math.log(HalfHeight), math.log(_zoomTarget), k);
            if (math.abs(logHeight - math.log(_zoomTarget)) < 0.002f)
            {
                logHeight = math.log(_zoomTarget);
                _zooming = false;
            }

            HalfHeight = math.exp(logHeight);
            Center = ClampToBounds(_zoomAnchorWorld - (_zoomAnchorScreen - _view.ViewportSize * 0.5f) * WorldUnitsPerPixel);
        }

        private void OnPanned(float2 screenDelta)
        {
            if (!_dragging)
            {
                Stop();
                _dragging = true;
            }

            var before = Center;
            Center = ClampToBounds(Center - screenDelta * WorldUnitsPerPixel);
            _frameDrag += Center - before;
            Apply();
        }

        private void OnPanEnded()
        {
            if (!_dragging)
                return;

            _dragging = false;
            _frameDrag = float2.zero;
            var speed = math.length(_velocity) / WorldUnitsPerPixel;
            if (speed < MinCoastSpeed)
                _velocity = float2.zero;
            else if (speed > MaxCoastSpeed)
                _velocity *= MaxCoastSpeed / speed;
        }

        /// <summary>Pinch: follows the fingers exactly.</summary>
        private void OnZoomed(float2 screenPoint, float factor)
        {
            if (!IsValidFactor(factor))
                return;

            _flying = false;
            _zooming = false;
            ZoomAround(screenPoint, ClampHalfHeight(HalfHeight / factor));
            Apply();
        }

        /// <summary>Wheel: each step moves the zoom target; the view eases towards it around the cursor.</summary>
        private void OnZoomStepped(float2 screenPoint, float factor)
        {
            if (!IsValidFactor(factor))
                return;

            var from = _zooming ? _zoomTarget : HalfHeight;
            _flying = false;
            _zoomTarget = ClampHalfHeight(from / factor);
            _zoomAnchorScreen = screenPoint;
            _zoomAnchorWorld = ScreenToWorld(screenPoint);
            _zooming = !math.isnan(_zoomTarget) && math.abs(_zoomTarget - HalfHeight) > 1e-4f;
        }

        /// <summary>Keeps the world point under <paramref name="screenPoint"/> in place while zooming.</summary>
        private void ZoomAround(float2 screenPoint, float halfHeight)
        {
            var anchorBefore = ScreenToWorld(screenPoint);
            HalfHeight = halfHeight;
            var anchorAfter = ScreenToWorld(screenPoint);
            Center = ClampToBounds(Center + anchorBefore - anchorAfter);
        }

        private void Stop()
        {
            _flying = false;
            _zooming = false;
            _velocity = float2.zero;
            _frameDrag = float2.zero;
        }

        private static bool IsValidFactor(float factor) => factor > 0f && !float.IsNaN(factor) && !float.IsInfinity(factor);

        private float UnitsPerPixel(float halfHeight) => 2f * halfHeight / math.max(1f, _view.ViewportSize.y);

        private float ClampHalfHeight(float halfHeight) => math.clamp(halfHeight, _settings.MinHalfHeight, _settings.MaxHalfHeight);

        private float2 ClampToBounds(float2 point) => math.clamp(point, _settings.BoundsMin, _settings.BoundsMax);

        private void Apply() => _view.Show(Center, HalfHeight);
    }
}
