using System;

namespace TrafficGame.Core.Roads
{
    /// <summary>Tuning for turning the road graph into drivable and drawable geometry. Distances in meters.</summary>
    public sealed class RoadGeometrySettings
    {
        public RoadGeometrySettings(float junctionPadding, float minJunctionTrim, float maxJunctionTrim, float sampleSpacing)
        {
            if (junctionPadding < 0f || minJunctionTrim < 0f)
                throw new ArgumentOutOfRangeException(nameof(junctionPadding), "Junction distances cannot be negative.");
            if (maxJunctionTrim < minJunctionTrim)
                throw new ArgumentOutOfRangeException(nameof(maxJunctionTrim), "Max trim must not be below min trim.");
            if (sampleSpacing <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sampleSpacing), "Sample spacing must be positive.");

            JunctionPadding = junctionPadding;
            MinJunctionTrim = minJunctionTrim;
            MaxJunctionTrim = maxJunctionTrim;
            SampleSpacing = sampleSpacing;
        }

        /// <summary>Extra distance beyond the point where road edges meet; rounds the junction corners.</summary>
        public float JunctionPadding { get; }

        /// <summary>Smallest gap left between a road and its node when the node joins two or more roads.</summary>
        public float MinJunctionTrim { get; }

        /// <summary>Upper bound for the gap, so that very acute junctions stay compact.</summary>
        public float MaxJunctionTrim { get; }

        /// <summary>Maximum length of a straight piece when curves are sampled.</summary>
        public float SampleSpacing { get; }

        /// <summary>Length of a segment that always remains drivable after trimming both ends.</summary>
        public float MinSegmentLength => 1f;
    }
}
