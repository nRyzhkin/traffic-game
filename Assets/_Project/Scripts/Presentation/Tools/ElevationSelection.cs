using System;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>The elevation level new roads are drawn on (0 = ground … <see cref="RoadElevation.MaxLevel"/>).</summary>
    public sealed class ElevationSelection
    {
        public event Action Changed;

        public int Level { get; private set; }

        public float Height => RoadElevation.HeightOf(Level);

        public bool CanRaise => Level < RoadElevation.MaxLevel;
        public bool CanLower => Level > 0;

        public void Select(int level)
        {
            level = math.clamp(level, 0, RoadElevation.MaxLevel);
            if (level == Level)
                return;

            Level = level;
            Changed?.Invoke();
        }

        public void Raise() => Select(Level + 1);
        public void Lower() => Select(Level - 1);
    }
}
