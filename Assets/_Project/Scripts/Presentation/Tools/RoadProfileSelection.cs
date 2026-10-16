using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Roads;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>The road profile new roads are built with.</summary>
    public sealed class RoadProfileSelection
    {
        private readonly IReadOnlyList<RoadProfile> _profiles;
        private int _index;

        public RoadProfileSelection(IEnumerable<RoadProfile> profiles)
        {
            _profiles = profiles?.ToArray() ?? throw new ArgumentNullException(nameof(profiles));
            if (_profiles.Count == 0)
                throw new ArgumentException("At least one road profile is required.", nameof(profiles));
        }

        public event Action Changed;

        public RoadProfile Current => _profiles[_index];

        public IReadOnlyList<RoadProfile> All => _profiles;

        public void Select(string profileId)
        {
            var index = _profiles.ToList().FindIndex(profile => profile.Id == profileId);
            if (index < 0)
                throw new ArgumentException($"Unknown road profile '{profileId}'.", nameof(profileId));
            if (index == _index)
                return;

            _index = index;
            Changed?.Invoke();
        }

        public void SelectNext()
        {
            _index = (_index + 1) % _profiles.Count;
            Changed?.Invoke();
        }
    }
}
