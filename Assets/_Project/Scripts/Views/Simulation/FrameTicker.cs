using System;
using TrafficGame.Presentation.Simulation;
using UnityEngine;

namespace TrafficGame.Views.Simulation
{
    /// <summary>
    /// Forwards frame time to presenters. Uses the engine's scaled delta time, which the platform SDK sets to zero
    /// while the game is paused (ads, lost focus); the game's own speed control lives in the simulation clock.
    /// </summary>
    public sealed class FrameTicker : MonoBehaviour, IFrameSource
    {
        public event Action<float> FrameUpdated;

        private void Update() => FrameUpdated?.Invoke(Time.deltaTime);
    }
}
