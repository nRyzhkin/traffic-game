using System;

namespace TrafficGame.Presentation.Localization
{
    /// <summary>Resolves <see cref="LocalizedText"/> in the current language.</summary>
    public interface ILocalizer
    {
        string LanguageCode { get; }

        event Action LanguageChanged;

        string Get(LocalizedText text);
    }
}
