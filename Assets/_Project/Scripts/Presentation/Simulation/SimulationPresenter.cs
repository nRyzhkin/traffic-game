using System;
using System.Collections.Generic;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Tools;

namespace TrafficGame.Presentation.Simulation
{
    /// <summary>Per-frame callback with frame time (zero while the platform pauses the game).</summary>
    public interface IFrameSource
    {
        event Action<float> FrameUpdated;
    }

    /// <summary>Draws the vehicles of a simulation.</summary>
    public interface ITrafficView
    {
        /// <param name="alpha">How far the frame is between the last two steps, 0–1; vehicles are interpolated by it.</param>
        void Render(TrafficSimulation simulation, float alpha);
    }

    /// <summary>Highlights jammed lanes.</summary>
    public interface ICongestionView
    {
        void Render(IReadOnlyList<LaneCongestion> lanes);
    }

    /// <summary>Simulation speed buttons.</summary>
    public interface ISimulationControlsView
    {
        event Action<float> SpeedRequested;

        void SetSpeed(float speed);

        /// <summary>Shows that the game fast-forwards on its own while the player waits.</summary>
        void SetBoosted(bool boosted);
    }

    /// <summary>
    /// Runs the traffic simulation and its trip demand on a fixed tick driven by frame time and the player's speed
    /// choice, rebuilds it when roads change and keeps the views and statistics up to date.
    /// </summary>
    public sealed class SimulationPresenter : IDisposable
    {
        private const float RefreshInterval = 0.5f;

        /// <summary>Time constant of speed changes, seconds: pausing or fast-forwarding eases in instead of jumping.</summary>
        private const float SpeedChangeTime = 0.15f;

        private readonly RoadNetwork _network;
        private readonly IRoadLayoutSource _layout;
        private readonly TrafficSettings _settings;
        private readonly SimulationClock _clock;
        private readonly TripGenerator _trips;
        private readonly IFrameSource _frames;
        private readonly ITrafficView _view;
        private readonly ICongestionView _congestion;
        private readonly ISimulationControlsView _controls;
        private readonly List<LaneCongestion> _congestedLanes = new();
        private readonly float _timeScale;
        private float _clockTarget;
        private float _refreshTimer;

        /// <param name="timeScale">Simulated seconds per real second at the "1×" setting.</param>
        /// <param name="initialSpeed">Speed setting to start with (0 = paused).</param>
        public SimulationPresenter(RoadNetwork network, IRoadLayoutSource layout, TrafficSettings settings, SimulationClock clock,
            float timeScale, float initialSpeed, TripGenerator trips, IFrameSource frames, ITrafficView view, ICongestionView congestion,
            ISimulationControlsView controls)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _trips = trips ?? throw new ArgumentNullException(nameof(trips));
            _frames = frames ?? throw new ArgumentNullException(nameof(frames));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _congestion = congestion ?? throw new ArgumentNullException(nameof(congestion));
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _timeScale = timeScale > 0f ? timeScale : throw new ArgumentOutOfRangeException(nameof(timeScale));

            Simulation = new TrafficSimulation(BuildTrafficNetwork(), settings);

            _layout.LayoutChanged += OnLayoutChanged;
            _frames.FrameUpdated += OnFrame;
            _controls.SpeedRequested += SetSpeed;
            SetSpeed(initialSpeed);
            _clock.Speed = _clockTarget;
            Refresh();
        }

        /// <summary>Raised after the simulation switched to a network rebuilt for edited roads.</summary>
        public event Action NetworkRebuilt;

        public TrafficSimulation Simulation { get; }

        /// <summary>The current speed setting (0 = paused, 1 = normal).</summary>
        public float Speed { get; private set; }

        /// <summary>
        /// Sets the player's speed choice (0, 1, 2, 4); the clock runs at that times the time scale. The traffic
        /// speeds up or slows down to it over a few frames.
        /// </summary>
        public void SetSpeed(float speed)
        {
            Speed = speed;
            _clockTarget = speed * _timeScale;
            _controls.SetSpeed(speed);
        }

        /// <summary>Simulates traffic ahead of time (the level opens with it), then starts the statistics afresh.</summary>
        public void WarmUp(float seconds)
        {
            if (seconds <= 0f)
                return;

            TrafficWarmup.Run(Simulation, _trips, seconds, _clock.TickDuration);
            Refresh();
        }

        public void Dispose()
        {
            _layout.LayoutChanged -= OnLayoutChanged;
            _frames.FrameUpdated -= OnFrame;
            _controls.SpeedRequested -= SetSpeed;
        }

        private void OnFrame(float deltaTime)
        {
            EaseClockSpeed(deltaTime);
            var ticks = _clock.Advance(deltaTime);
            for (var i = 0; i < ticks; i++)
            {
                _trips.Update(Simulation, _clock.TickDuration);
                Simulation.Step(_clock.TickDuration);
            }

            _view.Render(Simulation, _clock.Alpha);

            _refreshTimer -= deltaTime;
            if (_refreshTimer <= 0f)
                Refresh();
        }

        private void EaseClockSpeed(float deltaTime)
        {
            if (_clock.Speed == _clockTarget || deltaTime <= 0f)
                return;

            var speed = Easing.Approach(_clock.Speed, _clockTarget, SpeedChangeTime, deltaTime);
            _clock.Speed = Math.Abs(speed - _clockTarget) < 0.02f * _timeScale ? _clockTarget : speed;
        }

        private void OnLayoutChanged()
        {
            Simulation.Rebuild(BuildTrafficNetwork());
            NetworkRebuilt?.Invoke();
        }

        private TrafficNetwork BuildTrafficNetwork() => TrafficNetwork.Build(_network, _layout.Layout, _settings);

        private void Refresh()
        {
            _refreshTimer = RefreshInterval;

            Simulation.GetCongestion(_congestedLanes);
            _congestion.Render(_congestedLanes);

        }
    }
}
