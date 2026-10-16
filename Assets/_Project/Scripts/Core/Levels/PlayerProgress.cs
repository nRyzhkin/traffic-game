using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TrafficGame.Core.Levels
{
    /// <summary>Best result per campaign level; a level opens once the previous one has at least one star.</summary>
    public sealed class PlayerProgress
    {
        public const int CurrentSchemaVersion = 1;

        private readonly Dictionary<string, int> _bestStars;

        public PlayerProgress() : this(new Dictionary<string, int>()) { }

        private PlayerProgress(Dictionary<string, int> bestStars) => _bestStars = bestStars;

        public int BestStars(string levelId) => _bestStars.TryGetValue(levelId, out var stars) ? stars : 0;

        /// <summary>Keeps the better of the stored and the new result; returns true when it improved.</summary>
        public bool Record(string levelId, int stars)
        {
            if (stars <= BestStars(levelId))
                return false;

            _bestStars[levelId] = stars;
            return true;
        }

        public bool IsUnlocked(IReadOnlyList<string> campaignOrder, string levelId)
        {
            var index = IndexOf(campaignOrder, levelId);
            return index == 0 || (index > 0 && BestStars(campaignOrder[index - 1]) > 0);
        }

        public string ToJson() =>
            JsonConvert.SerializeObject(new SaveData { SchemaVersion = CurrentSchemaVersion, BestStars = _bestStars });

        /// <summary>Reads saved progress; anything unreadable or from an unknown version starts fresh rather than failing.</summary>
        public static PlayerProgress FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new PlayerProgress();

            try
            {
                var data = JsonConvert.DeserializeObject<SaveData>(json);
                return data is { SchemaVersion: CurrentSchemaVersion, BestStars: not null }
                    ? new PlayerProgress(new Dictionary<string, int>(data.BestStars))
                    : new PlayerProgress();
            }
            catch (JsonException)
            {
                return new PlayerProgress();
            }
        }

        private static int IndexOf(IReadOnlyList<string> order, string levelId)
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (order[i] == levelId)
                    return i;
            }

            return -1;
        }

        private sealed class SaveData
        {
            [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
            [JsonProperty("bestStars")] public Dictionary<string, int> BestStars { get; set; }
        }
    }
}
