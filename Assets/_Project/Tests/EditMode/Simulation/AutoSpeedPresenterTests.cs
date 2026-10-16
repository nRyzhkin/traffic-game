using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrafficGame.Core.Commands;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Simulation;
using TrafficGame.Presentation.Tools;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Simulation
{
    public sealed class AutoSpeedPresenterTests
    {
        private const float IdleSeconds = 4f;
        private const float FastSpeed = 4f;

        private Frames _frames;
        private Controls _controls;
        private Pointer _pointer;
        private CommandHistory _history;
        private ToolController _tools;
        private SimulationPresenter _simulation;
        private AutoSpeedPresenter _autoSpeed;

        [SetUp]
        public void SetUp()
        {
            var network = TestRoads.Crossing(TestRoads.TwoLane);
            var layout = new FixedLayout(RoadLayout.Build(network, TestRoads.Settings));
            var settings = new TrafficSettings();
            _frames = new Frames();
            _controls = new Controls();
            _pointer = new Pointer();
            _history = new CommandHistory(capacity: 10);
            _simulation = new SimulationPresenter(network, layout, settings, new SimulationClock(20f, 8), timeScale: 2f, initialSpeed: 1f,
                new TripGenerator(Array.Empty<TripSource>(), 0f, 1), _frames, new NoTraffic(), new NoCongestion(), _controls);
            _tools = new ToolController(_pointer, Array.Empty<IToolInput>(), new IdentityCamera(), _history,
                new RoadProfileSelection(new[] { TestRoads.TwoLane }), new Dictionary<ToolKind, IMapTool> { [ToolKind.Road] = new IdleTool() });
            _autoSpeed = new AutoSpeedPresenter(_simulation, _controls, _tools, _history, _pointer, _frames, IdleSeconds, FastSpeed);
        }

        [TearDown]
        public void TearDown()
        {
            _autoSpeed.Dispose();
            _tools.Dispose();
            _simulation.Dispose();
        }

        [Test]
        public void BeforeThePlayerChangesAnything_TimeRunsNormally()
        {
            _frames.Run(IdleSeconds * 3f);

            Assert.That(_simulation.Speed, Is.EqualTo(1f));
        }

        [Test]
        public void AfterAChange_IdleTimeFastForwardsAndAMapTapEndsIt()
        {
            _history.Execute(new NoOp());

            _frames.Run(IdleSeconds - 1f);
            Assert.That(_simulation.Speed, Is.EqualTo(1f));
            _frames.Run(2f);
            Assert.That(_simulation.Speed, Is.EqualTo(FastSpeed));
            Assert.That(_controls.Shown, Is.EqualTo(FastSpeed), "The speed buttons show the fast-forward.");
            Assert.That(_controls.Boosted, Is.True, "A chip says the game fast-forwards on its own.");

            _pointer.Tap(float2.zero);
            Assert.That(_simulation.Speed, Is.EqualTo(1f));
            Assert.That(_controls.Boosted, Is.False);
        }

        [Test]
        public void AimingATool_KeepsNormalSpeedButHoveringDoesNot()
        {
            _history.Execute(new NoOp());
            _tools.Select(ToolKind.Road);
            for (var i = 0; i < 10; i++)
            {
                _pointer.Move(new float2(i, 0f));
                _frames.Run(1f);
            }

            Assert.That(_simulation.Speed, Is.EqualTo(1f));

            _tools.Select(ToolKind.None);
            for (var i = 0; i < 6; i++)
            {
                _pointer.Move(new float2(i, 0f));
                _frames.Run(1f);
            }

            Assert.That(_simulation.Speed, Is.EqualTo(FastSpeed));
        }

        [Test]
        public void ChoosingASpeedWhileFastForwarded_TurnsTheAutomaticModeOff()
        {
            _history.Execute(new NoOp());
            _frames.Run(IdleSeconds + 1f);

            _controls.Request(1f);
            _frames.Run(IdleSeconds * 3f);

            Assert.That(_simulation.Speed, Is.EqualTo(1f));
        }

        [Test]
        public void PausedOrFinished_IsNeverFastForwarded()
        {
            _history.Execute(new NoOp());
            _simulation.SetSpeed(0f);

            _frames.Run(IdleSeconds * 3f);

            Assert.That(_simulation.Speed, Is.Zero);
        }

        private sealed class Frames : IFrameSource
        {
            public event Action<float> FrameUpdated;

            public void Run(float seconds)
            {
                for (var time = 0f; time < seconds; time += 0.1f)
                    FrameUpdated?.Invoke(0.1f);
            }
        }

        private sealed class Controls : ISimulationControlsView
        {
            public event Action<float> SpeedRequested;
            public float Shown { get; private set; }
            public void SetSpeed(float speed) => Shown = speed;
            public bool Boosted { get; private set; }
            public void SetBoosted(bool boosted) => Boosted = boosted;
            public void Request(float speed) => SpeedRequested?.Invoke(speed);
        }

        private sealed class Pointer : IMapPointerInput
        {
            public event Action<float2> PointerMoved;
            public event Action<float2> Tapped;
            public event Action<float2> PrimaryClicked { add { } remove { } }
            public event Action SecondaryClicked { add { } remove { } }
            public void Move(float2 point) => PointerMoved?.Invoke(point);

            public void Tap(float2 point)
            {
                PointerMoved?.Invoke(point);
                Tapped?.Invoke(point);
            }
        }

        private sealed class FixedLayout : IRoadLayoutSource
        {
            public FixedLayout(RoadLayout layout) => Layout = layout;
            public RoadLayout Layout { get; }
            public event Action LayoutChanged { add { } remove { } }
        }

        private sealed class IdleTool : IMapTool
        {
            public bool CanConfirm => false;
            public bool ActsOnTap => false;
            public void Activate() { }
            public void Deactivate() { }
            public void PointerMoved(float2 world) { }
            public void PrimaryClick(float2 world) { }
            public bool Cancel() => false;
        }

        private sealed class NoOp : ICommand
        {
            public void Execute() { }
            public void Undo() { }
        }

        private sealed class NoTraffic : ITrafficView
        {
            public void Render(TrafficSimulation simulation, float alpha) { }
        }

        private sealed class NoCongestion : ICongestionView
        {
            public void Render(IReadOnlyList<LaneCongestion> lanes) { }
        }

        private sealed class IdentityCamera : IScreenToWorld
        {
            public float2 ScreenToWorld(float2 screenPoint) => screenPoint;
        }
    }
}
