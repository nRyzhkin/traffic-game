using Unity.Mathematics;

namespace TrafficGame.Core.Construction
{
    /// <summary>Limits that keep roads drivable and junctions readable. Distances in meters, angles in degrees.</summary>
    public sealed class RoadPlanningRules
    {
        public RoadPlanningRules(float minLength, float minCurveRadius, float minCrossingAngle, float minConnectionAngle,
            float minJunctionSpacing, float sampleSpacing, float2 boundsMin, float2 boundsMax, float maxGrade = 0.125f,
            float elevatedCostFactor = 1.5f)
        {
            MaxGrade = maxGrade;
            ElevatedCostFactor = elevatedCostFactor;
            MinLength = minLength;
            MinCurveRadius = minCurveRadius;
            MinCrossingAngle = minCrossingAngle;
            MinConnectionAngle = minConnectionAngle;
            MinJunctionSpacing = minJunctionSpacing;
            SampleSpacing = sampleSpacing;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
        }

        public float MinLength { get; }
        public float MinCurveRadius { get; }

        /// <summary>Smallest angle between a new road and a road it crosses.</summary>
        public float MinCrossingAngle { get; }

        /// <summary>Smallest angle between a new road and the roads at a node or segment it connects to.</summary>
        public float MinConnectionAngle { get; }

        /// <summary>Smallest distance between two junctions along a road.</summary>
        public float MinJunctionSpacing { get; }

        public float SampleSpacing { get; }

        /// <summary>Steepest ramp: height change per meter of road.</summary>
        public float MaxGrade { get; }

        /// <summary>Extra price of elevated road as a share of the normal price (1.5 = elevated meters cost 2.5×).</summary>
        public float ElevatedCostFactor { get; }
        public float2 BoundsMin { get; }
        public float2 BoundsMax { get; }
    }
}
