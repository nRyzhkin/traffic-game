using System;

namespace TrafficGame.Core.Simulation
{
    /// <summary>
    /// Turns variable frame time into a whole number of fixed simulation ticks.
    /// <see cref="Speed"/> is the game's own multiplier (pause, x1, x2, x4); platform pauses
    /// arrive upstream as zero frame time, so the clock never touches engine time scale.
    /// </summary>
    public sealed class SimulationClock
    {
        private readonly int _maxTicksPerAdvance;
        private double _accumulator;
        private float _speed = 1f;

        public SimulationClock(float tickRate, int maxTicksPerAdvance)
        {
            if (tickRate <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "Tick rate must be positive.");
            if (maxTicksPerAdvance < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance), maxTicksPerAdvance, "At least one tick per advance is required.");

            TickDuration = 1f / tickRate;
            _maxTicksPerAdvance = maxTicksPerAdvance;
        }

        /// <summary>Simulated seconds per tick.</summary>
        public float TickDuration { get; }

        /// <summary>Ticks executed since creation.</summary>
        public long TickCount { get; private set; }

        /// <summary>Fraction of the next tick already elapsed, in [0, 1); used to interpolate rendering.</summary>
        public float Alpha => (float)(_accumulator / TickDuration);

        /// <summary>Simulation speed multiplier; zero pauses the simulation.</summary>
        public float Speed
        {
            get => _speed;
            set
            {
                if (value < 0f || float.IsNaN(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Speed must be zero or positive.");
                _speed = value;
            }
        }

        public bool IsPaused => _speed == 0f;

        /// <summary>
        /// Accumulates <paramref name="deltaTime"/> real seconds and returns how many ticks to run now.
        /// When the frame was too long the backlog is dropped instead of spiralling into ever longer frames.
        /// </summary>
        public int Advance(float deltaTime)
        {
            if (deltaTime <= 0f || IsPaused)
                return 0;

            _accumulator += (double)deltaTime * _speed;
            var ticks = (int)(_accumulator / TickDuration);

            if (ticks > _maxTicksPerAdvance)
            {
                ticks = _maxTicksPerAdvance;
                _accumulator = 0d;
            }
            else
            {
                _accumulator -= ticks * (double)TickDuration;
            }

            TickCount += ticks;
            return ticks;
        }
    }
}
