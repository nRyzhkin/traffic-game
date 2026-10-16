using System;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Cameras
{
    /// <summary>Limits of the top-down map camera, in world units.</summary>
    public readonly struct CameraSettings
    {
        public CameraSettings(float minHalfHeight, float maxHalfHeight, float initialHalfHeight, float2 boundsMin, float2 boundsMax)
        {
            if (minHalfHeight <= 0f || maxHalfHeight < minHalfHeight)
                throw new ArgumentException("Zoom limits must satisfy 0 < min <= max.");
            if (math.any(boundsMax < boundsMin))
                throw new ArgumentException("Bounds max must not be below bounds min.");

            MinHalfHeight = minHalfHeight;
            MaxHalfHeight = maxHalfHeight;
            InitialHalfHeight = math.clamp(initialHalfHeight, minHalfHeight, maxHalfHeight);
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
        }

        /// <summary>Smallest visible half height (most zoomed in).</summary>
        public float MinHalfHeight { get; }

        /// <summary>Largest visible half height (most zoomed out).</summary>
        public float MaxHalfHeight { get; }

        public float InitialHalfHeight { get; }

        /// <summary>The camera center is kept inside these bounds.</summary>
        public float2 BoundsMin { get; }

        public float2 BoundsMax { get; }
    }
}
