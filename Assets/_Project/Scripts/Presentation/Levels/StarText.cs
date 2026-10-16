using TrafficGame.Core.Levels;
using TrafficGame.Presentation.Localization;

namespace TrafficGame.Presentation.Levels
{
    /// <summary>How a star condition reads on the task and result cards.</summary>
    public static class StarText
    {
        public static LocalizedText Describe(StarCondition star)
        {
            if (star.MaxSpent.HasValue && star.MaxAverageTripSeconds.HasValue)
                return new LocalizedText(TextKeys.StarSpentAndTrip, star.MaxSpent.Value, star.MaxAverageTripSeconds.Value);
            return star.MaxSpent.HasValue
                ? new LocalizedText(TextKeys.StarSpent, star.MaxSpent.Value)
                : new LocalizedText(TextKeys.StarTrip, star.MaxAverageTripSeconds ?? 0f);
        }
    }
}
