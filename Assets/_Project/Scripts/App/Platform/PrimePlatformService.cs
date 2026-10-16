using System;
using PrimeGames.SDK;
using PrimeGames.SDK.Common;
using TrafficGame.Presentation.Platform;

namespace TrafficGame.App.Platform
{
    /// <summary>
    /// <see cref="IPlatformService"/> backed by PrimeSDK. PrimeSDK creates its own instance before the
    /// first scene loads; this service only waits for its providers and guards every call until then.
    /// </summary>
    public sealed class PrimePlatformService : IPlatformService
    {
        private const string FallbackLanguageCode = "en";

        public bool IsReady { get; private set; }

        public string LanguageCode => IsReady ? ToLocaleCode(PrimeSDK.Language.Current) : FallbackLanguageCode;

        public void Initialize(Action onReady)
        {
            if (onReady == null)
                throw new ArgumentNullException(nameof(onReady));

            if (IsReady)
            {
                onReady();
                return;
            }

            PrimeSDK.WaitForProviders(() =>
            {
                IsReady = true;
                onReady();
            });
        }

        public void NotifyGameReady()
        {
            ThrowIfNotReady();
            PrimeSDK.Analytics.GameIsReady();
        }

        public void NotifyGameplayStarted()
        {
            ThrowIfNotReady();
            PrimeSDK.Analytics.GameplayStart();
        }

        public void NotifyGameplayStopped()
        {
            ThrowIfNotReady();
            PrimeSDK.Analytics.GameplayStop();
        }

        public void NotifyGameplayRestarted()
        {
            ThrowIfNotReady();
            PrimeSDK.Analytics.GameplayRestart();
        }

        public void Celebrate()
        {
            ThrowIfNotReady();
            PrimeSDK.Achievements.HappyTime();
        }

        public string LoadString(string key, string defaultValue)
        {
            ThrowIfNotReady();
            return PrimeSDK.Data.GetString(key, defaultValue);
        }

        public void SaveString(string key, string value)
        {
            ThrowIfNotReady();
            PrimeSDK.Data.SetString(key, value, important: false);
        }

        public void Flush()
        {
            ThrowIfNotReady();
            PrimeSDK.Data.Save();
        }

        /// <summary>Maps platform languages to the locales the game ships; anything else gets English.</summary>
        private static string ToLocaleCode(LanguageType language) => language switch
        {
            LanguageType.Russian => "ru",
            LanguageType.Portuguese => "pt-BR",
            LanguageType.German => "de",
            LanguageType.Spanish => "es",
            LanguageType.Turkish => "tr",
            _ => FallbackLanguageCode
        };

        private void ThrowIfNotReady()
        {
            if (!IsReady)
                throw new InvalidOperationException("PrimeSDK is used before its providers are ready.");
        }
    }
}
