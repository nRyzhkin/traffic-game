using System;
using System.Collections.Generic;
using System.Linq;

namespace TrafficGame.Presentation.Settings
{
    /// <summary>The player's color scheme choice.</summary>
    public enum ThemeMode
    {
        /// <summary>Follow the device or browser setting.</summary>
        System,
        Light,
        Dark
    }

    /// <summary>The color scheme the device or browser asks for.</summary>
    public interface ISystemTheme
    {
        bool PrefersDark { get; }

        /// <summary>Raised when the system setting changes while the game runs.</summary>
        event Action Changed;
    }

    /// <summary>Anything drawn in theme colors: the UI and the map.</summary>
    public interface IThemeView
    {
        void ApplyTheme(bool dark);
    }

    /// <summary>The control that lets the player pick a <see cref="ThemeMode"/>.</summary>
    public interface IThemeControlView
    {
        event Action<ThemeMode> ModeRequested;

        void SetMode(ThemeMode mode);
    }

    /// <summary>Resolves the player's choice against the system setting and repaints every themed view when either changes.</summary>
    public sealed class ThemePresenter : IDisposable
    {
        private readonly ISystemTheme _system;
        private readonly IThemeControlView _control;
        private readonly IReadOnlyList<IThemeView> _views;
        private bool? _appliedDark;

        public ThemePresenter(ISystemTheme system, IThemeControlView control, IEnumerable<IThemeView> views, ThemeMode mode)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            _control = control ?? throw new ArgumentNullException(nameof(control));
            _views = views?.ToArray() ?? throw new ArgumentNullException(nameof(views));
            Mode = mode;

            _system.Changed += Apply;
            _control.ModeRequested += SetMode;
            _control.SetMode(mode);
            Apply();
        }

        /// <summary>Raised after the player picked another mode (to save it).</summary>
        public event Action<ThemeMode> ModeChanged;

        public ThemeMode Mode { get; private set; }

        public bool IsDark => Mode == ThemeMode.Dark || (Mode == ThemeMode.System && _system.PrefersDark);

        public void SetMode(ThemeMode mode)
        {
            if (mode == Mode)
                return;

            Mode = mode;
            _control.SetMode(mode);
            Apply();
            ModeChanged?.Invoke(mode);
        }

        public void Dispose()
        {
            _system.Changed -= Apply;
            _control.ModeRequested -= SetMode;
        }

        private void Apply()
        {
            var dark = IsDark;
            if (_appliedDark == dark)
                return;

            _appliedDark = dark;
            foreach (var view in _views)
                view.ApplyTheme(dark);
        }

        /// <summary>Reads a saved mode; anything unknown means <see cref="ThemeMode.System"/>.</summary>
        public static ThemeMode Parse(string value) =>
            Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode) && Enum.IsDefined(typeof(ThemeMode), mode) ? mode : ThemeMode.System;
    }
}
