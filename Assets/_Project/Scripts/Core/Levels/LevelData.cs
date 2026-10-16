using System.Collections.Generic;
using Newtonsoft.Json;

namespace TrafficGame.Core.Levels
{
    /// <summary>Serialized form of a level. Built-in and player-made levels share it.</summary>
    public sealed class LevelData
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("name")] public string Name { get; set; }

        /// <summary>Localization keys of the level title and of the short task shown when it starts.</summary>
        [JsonProperty("titleKey", NullValueHandling = NullValueHandling.Ignore)] public string TitleKey { get; set; }
        [JsonProperty("introKey", NullValueHandling = NullValueHandling.Ignore)] public string IntroKey { get; set; }
        [JsonProperty("hintKey", NullValueHandling = NullValueHandling.Ignore)] public string HintKey { get; set; }

        /// <summary>Starting money; 0 uses the game default.</summary>
        [JsonProperty("budget", DefaultValueHandling = DefaultValueHandling.Ignore)] public long Budget { get; set; }

        /// <summary>Seconds of traffic simulated before the level opens, so it starts with traffic (and jams) already there.</summary>
        [JsonProperty("warmup", DefaultValueHandling = DefaultValueHandling.Ignore)] public float Warmup { get; set; }

        [JsonProperty("camera", NullValueHandling = NullValueHandling.Ignore)] public CameraData Camera { get; set; }
        [JsonProperty("nodes")] public List<NodeData> Nodes { get; set; } = new();
        [JsonProperty("segments")] public List<SegmentData> Segments { get; set; } = new();
        [JsonProperty("buildings")] public List<BuildingData> Buildings { get; set; } = new();

        /// <summary>Dead ends at the map border that send through traffic to each other.</summary>
        [JsonProperty("edges")] public List<EdgeData> Edges { get; set; } = new();

        /// <summary>Goal of a puzzle level; null for a sandbox.</summary>
        [JsonProperty("objective", NullValueHandling = NullValueHandling.Ignore)] public ObjectiveData Objective { get; set; }
    }

    public sealed class CameraData
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("halfHeight")] public float HalfHeight { get; set; }
    }

    public sealed class BuildingData
    {
        [JsonProperty("id")] public int Id { get; set; }

        /// <summary>"house", "shop" or "office".</summary>
        [JsonProperty("type")] public string Type { get; set; }

        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }

        /// <summary>Counter-clockwise rotation in degrees.</summary>
        [JsonProperty("rotation", DefaultValueHandling = DefaultValueHandling.Ignore)] public float Rotation { get; set; }

        /// <summary>Node id of the dead end of the building's driveway.</summary>
        [JsonProperty("door")] public int Door { get; set; }

        /// <summary>Trips per minute; omitted for the default of the building type.</summary>
        [JsonProperty("perMinute", NullValueHandling = NullValueHandling.Ignore)] public float? PerMinute { get; set; }
    }

    public sealed class EdgeData
    {
        [JsonProperty("node")] public int Node { get; set; }
        [JsonProperty("perMinute")] public float PerMinute { get; set; }
    }

    public sealed class ObjectiveData
    {
        [JsonProperty("trips")] public int Trips { get; set; }
        [JsonProperty("stars")] public List<StarData> Stars { get; set; } = new();
    }

    public sealed class StarData
    {
        [JsonProperty("maxSpent", NullValueHandling = NullValueHandling.Ignore)] public long? MaxSpent { get; set; }
        [JsonProperty("maxAverageTrip", NullValueHandling = NullValueHandling.Ignore)] public float? MaxAverageTrip { get; set; }
    }

    public sealed class NodeData
    {
        /// <summary>Id local to the file; segments refer to nodes by it.</summary>
        [JsonProperty("id")] public int Id { get; set; }

        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }

        /// <summary>Elevation level, 0 = ground (<see cref="Roads.RoadElevation"/>).</summary>
        [JsonProperty("level", DefaultValueHandling = DefaultValueHandling.Ignore)] public int Level { get; set; }

        /// <summary>The junction starts with traffic lights.</summary>
        [JsonProperty("lights", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Lights { get; set; }

        /// <summary>With <see cref="Lights"/>: left turns get protected phases.</summary>
        [JsonProperty("protectedLeft", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool ProtectedLeft { get; set; }
    }

    public sealed class SegmentData
    {
        [JsonProperty("start")] public int Start { get; set; }
        [JsonProperty("end")] public int End { get; set; }
        [JsonProperty("profile")] public string Profile { get; set; }

        /// <summary>Bézier control points as [x, y]; both omitted for a straight segment.</summary>
        [JsonProperty("control1", NullValueHandling = NullValueHandling.Ignore)] public float[] Control1 { get; set; }

        [JsonProperty("control2", NullValueHandling = NullValueHandling.Ignore)] public float[] Control2 { get; set; }

        /// <summary>Part of a roundabout ring.</summary>
        [JsonProperty("roundabout", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Roundabout { get; set; }

        /// <summary>Placed by the level designer; cannot be bulldozed.</summary>
        [JsonProperty("locked", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Locked { get; set; }
    }
}
