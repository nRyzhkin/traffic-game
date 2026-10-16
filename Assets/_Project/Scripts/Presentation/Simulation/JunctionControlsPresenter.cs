using System;
using System.Collections.Generic;
using TrafficGame.Core.Simulation;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Simulation
{
    public enum StopLineKind
    {
        Stop,
        Yield,
        Red,
        Yellow,
        Green
    }

    /// <summary>The control a lane ends at, drawn across the lane end.</summary>
    public readonly struct StopLine
    {
        public StopLine(float2 center, float2 direction, float width, StopLineKind kind, float height = 0f)
        {
            Height = height;
            Center = center;
            Direction = direction;
            Width = width;
            Kind = kind;
        }

        public float2 Center { get; }

        /// <summary>Direction of travel of the lane.</summary>
        public float2 Direction { get; }

        public float Width { get; }
        public StopLineKind Kind { get; }

        /// <summary>Meters above the ground.</summary>
        public float Height { get; }
    }

    /// <summary>Stop lines, yield markings and traffic light colors at the ends of lanes.</summary>
    public interface IJunctionControlsView
    {
        void Render(IReadOnlyList<StopLine> lines);
    }

    /// <summary>Keeps the junction control markings in sync with the traffic network and the traffic lights.</summary>
    public sealed class JunctionControlsPresenter : IDisposable
    {
        private readonly SimulationPresenter _simulation;
        private readonly IFrameSource _frames;
        private readonly IJunctionControlsView _view;
        private readonly List<StopLine> _lines = new();
        private int _signature;
        private bool _dirty = true;

        public JunctionControlsPresenter(SimulationPresenter simulation, IFrameSource frames, IJunctionControlsView view)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _frames = frames ?? throw new ArgumentNullException(nameof(frames));
            _view = view ?? throw new ArgumentNullException(nameof(view));

            _simulation.NetworkRebuilt += MarkDirty;
            _frames.FrameUpdated += OnFrame;
            OnFrame(0f);
        }

        public void Dispose()
        {
            _simulation.NetworkRebuilt -= MarkDirty;
            _frames.FrameUpdated -= OnFrame;
        }

        private void MarkDirty() => _dirty = true;

        private void OnFrame(float deltaTime)
        {
            var network = _simulation.Simulation.Network;
            var signature = SignalSignature(network);
            if (!_dirty && signature == _signature)
                return;

            _dirty = false;
            _signature = signature;
            _lines.Clear();
            foreach (var link in network.Links)
            {
                if (link.Kind != LinkKind.Lane || link.Control == ApproachControl.None)
                    continue;

                var kind = link.Control switch
                {
                    ApproachControl.Stop => StopLineKind.Stop,
                    ApproachControl.Yield => StopLineKind.Yield,
                    _ => link.Signal.StateOfLane(link) switch
                    {
                        SignalState.Green => StopLineKind.Green,
                        SignalState.Yellow => StopLineKind.Yellow,
                        _ => StopLineKind.Red
                    }
                };
                _lines.Add(new StopLine(link.Path.End, link.Path.EndDirection, link.LaneWidth, kind, link.EndHeight));
            }

            _view.Render(_lines);
        }

        /// <summary>Changes whenever any traffic light changes what it shows.</summary>
        private static int SignalSignature(TrafficNetwork network)
        {
            var signature = 17;
            foreach (var signal in network.Signals)
                signature = signature * 31 + signal.Phase * 3 + (int)signal.Interval;
            return signature;
        }
    }
}
