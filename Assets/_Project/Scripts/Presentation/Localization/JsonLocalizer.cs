using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace TrafficGame.Presentation.Localization
{
    /// <summary>
    /// Localizer over flat JSON dictionaries (one file per language: <c>{ "key": "text" }</c>).
    /// Missing keys fall back to the fallback language, then to the key itself. Texts are
    /// <see cref="string.Format(IFormatProvider, string, object[])"/> templates formatted with the invariant culture.
    /// </summary>
    public sealed class JsonLocalizer : ILocalizer
    {
        private readonly Dictionary<string, Dictionary<string, string>> _languages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _fallback;
        private readonly string _fallbackCode;
        private Dictionary<string, string> _current;

        /// <param name="languageFiles">JSON text per language code, e.g. "en", "pt-BR".</param>
        public JsonLocalizer(IReadOnlyDictionary<string, string> languageFiles, string fallbackLanguage)
        {
            foreach (var (code, json) in languageFiles)
                _languages[code] = Parse(code, json);

            if (!_languages.TryGetValue(fallbackLanguage, out _fallback))
                throw new ArgumentException($"Fallback language '{fallbackLanguage}' is not among the language files.", nameof(fallbackLanguage));

            _fallbackCode = fallbackLanguage;
            LanguageCode = fallbackLanguage;
            _current = _fallback;
        }

        public event Action LanguageChanged;

        public string LanguageCode { get; private set; }

        public IEnumerable<string> Languages => _languages.Keys;

        /// <summary>Switches language; unknown codes fall back to the language part ("pt" for "pt-PT") and then to the fallback.</summary>
        public void SetLanguage(string code)
        {
            var resolved = Resolve(code);
            if (string.Equals(resolved, LanguageCode, StringComparison.OrdinalIgnoreCase))
                return;

            LanguageCode = resolved;
            _current = _languages[resolved];
            LanguageChanged?.Invoke();
        }

        public string Get(LocalizedText text)
        {
            if (text.IsEmpty)
                return string.Empty;

            if (!_current.TryGetValue(text.Key, out var template) && !_fallback.TryGetValue(text.Key, out template))
                return text.Key;

            return text.Args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, text.Args);
        }

        private string Resolve(string code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                if (_languages.ContainsKey(code))
                    return code;

                var dash = code.IndexOf('-');
                var language = dash > 0 ? code.Substring(0, dash) : code;
                foreach (var available in _languages.Keys)
                {
                    if (available.StartsWith(language, StringComparison.OrdinalIgnoreCase))
                        return available;
                }
            }

            return _fallbackCode;
        }

        private static Dictionary<string, string> Parse(string code, string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch (JsonException exception)
            {
                throw new FormatException($"Language file '{code}' is not a flat JSON object: {exception.Message}", exception);
            }
        }
    }
}
