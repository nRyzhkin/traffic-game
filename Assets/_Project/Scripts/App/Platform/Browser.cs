using System;
using System.Runtime.InteropServices;
using TrafficGame.Presentation.Settings;
using UnityEngine;

namespace TrafficGame.App.Platform
{
    /// <summary>
    /// The color scheme the browser asks for (<c>prefers-color-scheme</c>), checked now and then so switching the OS to
    /// dark mode while playing follows through. Outside WebGL builds there is no browser: always light.
    /// </summary>
    public sealed class Browser : ISystemTheme
    {
        private const float PollInterval = 1f;
        private float _sinceLastPoll;

        public Browser() => PrefersDark = QueryPrefersDark();

        public bool PrefersDark { get; private set; }

        public event Action Changed;

        /// <summary>Checks now and then whether the system scheme changed (the player switched the OS to dark mode).</summary>
        public void Update(float deltaTime)
        {
            _sinceLastPoll += deltaTime;
            if (_sinceLastPoll < PollInterval)
                return;

            _sinceLastPoll = 0f;
            var dark = QueryPrefersDark();
            if (dark == PrefersDark)
                return;

            PrefersDark = dark;
            Changed?.Invoke();
        }

        private static bool QueryPrefersDark()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TrafficGame_PrefersDarkScheme() != 0;
#else
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int TrafficGame_PrefersDarkScheme();
#endif
    }
}
