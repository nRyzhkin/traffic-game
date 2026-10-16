using System;
using NUnit.Framework;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Presentation.Simulation;
using Unity.Mathematics;

namespace TrafficGame.Tests.Cameras
{
    public sealed class CameraPresenterTests
    {
        private static readonly CameraSettings Settings =
            new(minHalfHeight: 10f, maxHalfHeight: 100f, initialHalfHeight: 50f, new float2(-1000f), new float2(1000f));

        private FakeCameraView _view;
        private FakeCameraInput _input;
        private FakeFrames _frames;
        private CameraPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeCameraView { ViewportSize = new float2(800f, 600f) };
            _input = new FakeCameraInput();
            _frames = new FakeFrames();
            _presenter = new CameraPresenter(_view, _input, _frames, Settings, float2.zero);
        }

        [Test]
        public void Constructor_ShowsInitialState()
        {
            Assert.That(_view.Center, Is.EqualTo(float2.zero));
            Assert.That(_view.HalfHeight, Is.EqualTo(50f));
        }

        [Test]
        public void Frame_FitsTheRectangleIntoTheScreenPartTheUiLeavesFree()
        {
            // Portrait screen; the HUD covers 100 px at the top and 200 px at the bottom.
            _view.ViewportSize = new float2(400f, 800f);
            var insets = new ScreenInsets(top: 100f, bottom: 200f, left: 0f, right: 0f);

            _presenter.Frame(new float2(-30f, -10f), new float2(30f, 10f), insets);

            var min = _presenter.ScreenToWorld(new float2(0f, 200f));
            var max = _presenter.ScreenToWorld(new float2(400f, 700f));
            Assert.That(min.x, Is.EqualTo(-30f).Within(0.01f), "The wide rectangle fills the screen width.");
            Assert.That(max.x, Is.EqualTo(30f).Within(0.01f));
            Assert.That((min.y + max.y) * 0.5f, Is.EqualTo(0f).Within(0.01f), "It sits in the middle of the free band.");
            Assert.That(min.y, Is.LessThanOrEqualTo(-10f));
            Assert.That(max.y, Is.GreaterThanOrEqualTo(10f));
        }

        [Test]
        public void Pan_MovesCenterAgainstDragInWorldUnits()
        {
            // 100 world units over 600 pixels: 60 px = 10 m.
            _input.Pan(new float2(60f, 0f));

            Assert.That(_view.Center.x, Is.EqualTo(-10f).Within(1e-4f));
            Assert.That(_view.Center.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Zoom_KeepsWorldPointUnderCursor()
        {
            var cursor = new float2(700f, 500f);
            var before = _presenter.ScreenToWorld(cursor);

            _input.Zoom(cursor, 2f);

            Assert.That(_view.HalfHeight, Is.EqualTo(25f).Within(1e-4f));
            var after = _presenter.ScreenToWorld(cursor);
            Assert.That(math.distance(before, after), Is.LessThan(1e-3f));
        }

        [Test]
        public void Zoom_IsClampedToLimits()
        {
            _input.Zoom(float2.zero, 100f);
            Assert.That(_view.HalfHeight, Is.EqualTo(Settings.MinHalfHeight));

            _input.Zoom(float2.zero, 0.001f);
            Assert.That(_view.HalfHeight, Is.EqualTo(Settings.MaxHalfHeight));
        }

        [Test]
        public void Pan_IsClampedToBounds()
        {
            _input.Pan(new float2(-1e6f, 1e6f));

            Assert.That(_view.Center, Is.EqualTo(new float2(1000f, -1000f)));
        }

        [Test]
        public void ReleasedDrag_CoastsAndSlowsDown()
        {
            for (var i = 0; i < 10; i++)
            {
                _input.Pan(new float2(-12f, 0f));
                _frames.Tick(1f / 60f);
            }

            var released = _view.Center.x;
            _input.EndPan();
            _frames.Tick(1f / 60f);
            var firstStep = _view.Center.x - released;
            _frames.Run(0.2f);
            var later = _view.Center.x;
            _frames.Tick(1f / 60f);
            var laterStep = _view.Center.x - later;

            Assert.That(firstStep, Is.GreaterThan(0f), "The map keeps moving the way it was thrown.");
            Assert.That(laterStep, Is.LessThan(firstStep * 0.75f), "…and slows down.");
            _frames.Run(5f);
            Assert.That(_presenter.IsMoving, Is.False, "It comes to rest.");
        }

        [Test]
        public void DragHeldStillBeforeRelease_DoesNotCoast()
        {
            for (var i = 0; i < 10; i++)
            {
                _input.Pan(new float2(-12f, 0f));
                _frames.Tick(1f / 60f);
            }

            _frames.Run(0.3f);
            var released = _view.Center;
            _input.EndPan();
            _frames.Run(1f);

            Assert.That(_view.Center, Is.EqualTo(released));
        }

        [Test]
        public void WheelStep_EasesIntoTheZoomAroundTheCursor()
        {
            var cursor = new float2(700f, 500f);
            var before = _presenter.ScreenToWorld(cursor);

            _input.Step(cursor, 2f);
            _frames.Tick(1f / 60f);
            Assert.That(_view.HalfHeight, Is.LessThan(50f).And.GreaterThan(25f), "The zoom is on its way, not there at once.");

            _frames.Run(1f);
            Assert.That(_view.HalfHeight, Is.EqualTo(25f).Within(1e-3f));
            Assert.That(math.distance(before, _presenter.ScreenToWorld(cursor)), Is.LessThan(1e-2f));
        }

        [Test]
        public void Glide_EasesToTheTargetAndStops()
        {
            _presenter.Focus(new float2(200f, 100f), 20f, CameraMove.Glide);
            _frames.Tick(0.1f);
            Assert.That(_view.Center.x, Is.GreaterThan(0f).And.LessThan(200f));

            _frames.Run(1f);
            Assert.That(_view.Center.x, Is.EqualTo(200f).Within(1e-3f));
            Assert.That(_view.HalfHeight, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(_presenter.IsMoving, Is.False);
        }

        [Test]
        public void Arrive_StartsWiderAndSettlesIn()
        {
            _presenter.Focus(new float2(200f, 100f), 20f, CameraMove.Arrive);
            Assert.That(_view.Center, Is.EqualTo(new float2(200f, 100f)));
            Assert.That(_view.HalfHeight, Is.GreaterThan(20f));

            _frames.Run(2f);
            Assert.That(_view.HalfHeight, Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void Drag_StopsAGlide()
        {
            _presenter.Focus(new float2(200f, 100f), 20f, CameraMove.Glide);
            _frames.Tick(0.1f);
            _input.Pan(new float2(1f, 0f));
            var center = _view.Center;

            _frames.Run(1f);

            Assert.That(_view.Center, Is.EqualTo(center));
        }

        [Test]
        public void Dispose_StopsReactingToInput()
        {
            _presenter.Dispose();

            _input.Pan(new float2(60f, 0f));

            Assert.That(_view.Center, Is.EqualTo(float2.zero));
        }

        private sealed class FakeCameraView : ICameraView
        {
            public float2 ViewportSize { get; set; }
            public float2 Center { get; private set; }
            public float HalfHeight { get; private set; }

            public void Show(float2 center, float halfHeight)
            {
                Center = center;
                HalfHeight = halfHeight;
            }
        }

        private sealed class FakeCameraInput : ICameraInput
        {
            public event Action<float2> Panned;
            public event Action PanEnded;
            public event Action<float2, float> Zoomed;
            public event Action<float2, float> ZoomStepped;

            public void Pan(float2 delta) => Panned?.Invoke(delta);
            public void EndPan() => PanEnded?.Invoke();
            public void Zoom(float2 point, float factor) => Zoomed?.Invoke(point, factor);
            public void Step(float2 point, float factor) => ZoomStepped?.Invoke(point, factor);
        }

        private sealed class FakeFrames : IFrameSource
        {
            public event Action<float> FrameUpdated;

            public void Tick(float deltaTime) => FrameUpdated?.Invoke(deltaTime);

            public void Run(float seconds)
            {
                for (var t = 0f; t < seconds; t += 1f / 60f)
                    Tick(1f / 60f);
            }
        }
    }
}
