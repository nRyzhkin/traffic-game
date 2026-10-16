using System;
using TrafficGame.Core.Commands;
using TrafficGame.Presentation.Tools;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Simulation
{
    /// <summary>
    /// Fast-forwards a level while the player waits for the result: once they have changed something and then leave
    /// the map alone for a few seconds at normal speed, time runs at <c>fastSpeed</c>. Switching tools, clicking the map,
    /// aiming a tool or undoing brings normal speed back. Choosing a speed by hand while fast-forwarded turns this off
    /// for the level.
    /// </summary>
    public sealed class AutoSpeedPresenter : IDisposable
    {
        private const float NormalSpeed = 1f;

        private readonly SimulationPresenter _simulation;
        private readonly ISimulationControlsView _controls;
        private readonly ToolController _tools;
        private readonly CommandHistory _history;
        private readonly IMapPointerInput _pointer;
        private readonly IFrameSource _frames;
        private readonly float _idleSeconds;
        private readonly float _fastSpeed;
        private ToolKind _lastTool;
        private float _idle;
        private bool _boosted;
        private bool _disabled;

        public AutoSpeedPresenter(SimulationPresenter simulation, ISimulationControlsView controls, ToolController tools,
            CommandHistory history, IMapPointerInput pointer, IFrameSource frames, float idleSeconds, float fastSpeed)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            _frames = frames ?? throw new ArgumentNullException(nameof(frames));
            _idleSeconds = math.max(idleSeconds, 0f);
            _fastSpeed = fastSpeed;

            _controls.SpeedRequested += OnSpeedRequested;
            _history.Changed += OnActivity;
            _pointer.PointerMoved += OnPointerMoved;
            _pointer.PrimaryClicked += OnMapPressed;
            _pointer.Tapped += OnMapPressed;
            _tools.Changed += OnToolsChanged;
            _frames.FrameUpdated += OnFrame;
        }

        /// <summary>Whether time is currently fast-forwarded by this presenter.</summary>
        public bool IsBoosted => _boosted;

        public void Dispose()
        {
            _controls.SpeedRequested -= OnSpeedRequested;
            _history.Changed -= OnActivity;
            _pointer.PointerMoved -= OnPointerMoved;
            _pointer.PrimaryClicked -= OnMapPressed;
            _pointer.Tapped -= OnMapPressed;
            _tools.Changed -= OnToolsChanged;
            _frames.FrameUpdated -= OnFrame;
            _controls.SetBoosted(false);
        }

        private void OnFrame(float deltaTime)
        {
            if (_disabled || _boosted || !_history.CanUndo || _simulation.Speed != NormalSpeed)
                return;

            _idle += deltaTime;
            if (_idle < _idleSeconds)
                return;

            _boosted = true;
            _simulation.SetSpeed(_fastSpeed);
            _controls.SetBoosted(true);
        }

        private void OnMapPressed(float2 _) => OnActivity();

        /// <summary>Moving the pointer counts only while aiming a tool; hovering over the map is just watching.</summary>
        private void OnPointerMoved(float2 _)
        {
            if (_tools.Active != ToolKind.None)
                OnActivity();
        }

        private void OnToolsChanged()
        {
            if (_tools.Active == _lastTool)
                return;
            _lastTool = _tools.Active;
            OnActivity();
        }

        private void OnActivity()
        {
            _idle = 0f;
            if (!_boosted)
                return;

            _boosted = false;
            _controls.SetBoosted(false);
            if (_simulation.Speed == _fastSpeed)
                _simulation.SetSpeed(NormalSpeed);
        }

        /// <summary>A speed chosen by hand wins: while fast-forwarded it switches the automatic mode off.</summary>
        private void OnSpeedRequested(float speed)
        {
            if (_boosted)
            {
                _disabled = true;
                _controls.SetBoosted(false);
            }

            _boosted = false;
            _idle = 0f;
        }
    }
}
