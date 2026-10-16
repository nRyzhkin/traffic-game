using System;
using System.Collections.Generic;

namespace TrafficGame.Core.Roads
{
    /// <summary>The road profiles available in the game, looked up by id.</summary>
    public sealed class RoadProfileCatalog
    {
        private readonly Dictionary<string, RoadProfile> _profiles = new(StringComparer.Ordinal);

        public RoadProfileCatalog(IEnumerable<RoadProfile> profiles)
        {
            foreach (var profile in profiles)
            {
                if (!_profiles.TryAdd(profile.Id, profile))
                    throw new ArgumentException($"Duplicate road profile id '{profile.Id}'.", nameof(profiles));
            }
        }

        public IEnumerable<RoadProfile> All => _profiles.Values;

        public bool TryGet(string id, out RoadProfile profile) => _profiles.TryGetValue(id ?? string.Empty, out profile);
    }
}
