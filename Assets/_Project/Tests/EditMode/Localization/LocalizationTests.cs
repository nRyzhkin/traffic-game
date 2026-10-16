using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using TrafficGame.Presentation.Localization;

namespace TrafficGame.Tests.Localization
{
    public sealed class LocalizationTests
    {
        private const string LanguageFolder = "Assets/_Project/Localization";
        private const string FallbackLanguage = "en";

        private static Dictionary<string, Dictionary<string, string>> LoadLanguageFiles() =>
            Directory.GetFiles(LanguageFolder, "*.json")
                .ToDictionary(Path.GetFileNameWithoutExtension,
                    path => JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)));

        [Test]
        public void EveryLanguageHasExactlyTheFallbackKeys()
        {
            var files = LoadLanguageFiles();
            var expected = files[FallbackLanguage].Keys.OrderBy(key => key).ToArray();

            foreach (var (language, entries) in files)
                Assert.That(entries.Keys.OrderBy(key => key), Is.EqualTo(expected), $"Keys of {language}.json differ from {FallbackLanguage}.json.");
        }

        [Test]
        public void EveryKeyUsedByCodeIsTranslated()
        {
            var keys = typeof(TextKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (string)field.GetRawConstantValue());
            var fallback = LoadLanguageFiles()[FallbackLanguage];

            Assert.That(keys.Where(key => !fallback.ContainsKey(key)), Is.Empty);
        }

        [Test]
        public void EveryTextIsAValidFormatTemplate()
        {
            foreach (var (language, entries) in LoadLanguageFiles())
            foreach (var (key, template) in entries)
                Assert.DoesNotThrow(() => string.Format(CultureInfo.InvariantCulture, template, 1234.5f, 2, 3), $"{language}: {key}");
        }

        [Test]
        public void Localizer_FormatsArgumentsAndFallsBack()
        {
            var localizer = new JsonLocalizer(new Dictionary<string, string>
            {
                ["en"] = "{ \"cost\": \"${0:N0}\", \"only_en\": \"English\" }",
                ["pt-BR"] = "{ \"cost\": \"R${0:N0}\" }"
            }, "en");

            localizer.SetLanguage("pt");

            Assert.That(localizer.LanguageCode, Is.EqualTo("pt-BR"));
            Assert.That(localizer.Get(new LocalizedText("cost", 12345)), Is.EqualTo("R$12,345"));
            Assert.That(localizer.Get(new LocalizedText("only_en")), Is.EqualTo("English"));
            Assert.That(localizer.Get(new LocalizedText("missing")), Is.EqualTo("missing"));
        }

        [Test]
        public void Localizer_UnknownLanguageFallsBack()
        {
            var localizer = new JsonLocalizer(new Dictionary<string, string> { ["en"] = "{}", ["ru"] = "{}" }, "en");

            localizer.SetLanguage("ja");

            Assert.That(localizer.LanguageCode, Is.EqualTo("en"));
        }
    }
}
