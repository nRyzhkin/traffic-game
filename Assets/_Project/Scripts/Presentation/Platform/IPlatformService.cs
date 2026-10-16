using System;

namespace TrafficGame.Presentation.Platform
{
    /// <summary>
    /// Web-portal integration (lifecycle events, language, saves). The only place that talks
    /// to a platform SDK is its implementation in the App layer.
    /// </summary>
    public interface IPlatformService
    {
        bool IsReady { get; }

        /// <summary>Starts the platform SDK; <paramref name="onReady"/> runs once it can be used.</summary>
        void Initialize(Action onReady);

        /// <summary>Locale code chosen by the platform, e.g. "ru", "pt-BR"; falls back to "en".</summary>
        string LanguageCode { get; }

        /// <summary>Loading has finished and the player can interact with the game.</summary>
        void NotifyGameReady();

        /// <summary>The player is actively playing (in a level and not paused).</summary>
        void NotifyGameplayStarted();

        /// <summary>The player left active play: menu, pause or result screen.</summary>
        void NotifyGameplayStopped();

        void NotifyGameplayRestarted();

        /// <summary>A moment of success worth celebrating, e.g. a level is won.</summary>
        void Celebrate();

        string LoadString(string key, string defaultValue);

        /// <summary>Stores a value in memory; call <see cref="Flush"/> to persist a batch.</summary>
        void SaveString(string key, string value);

        void Flush();
    }
}
