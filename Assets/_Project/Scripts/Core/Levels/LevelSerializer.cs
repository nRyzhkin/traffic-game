using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TrafficGame.Core.City;
using Newtonsoft.Json;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Levels
{
    /// <summary>Reads and writes level JSON and turns <see cref="LevelData"/> into a playable <see cref="Level"/>.</summary>
    public static class LevelSerializer
    {
        public const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializerSettings Settings = new()
        {
            Culture = CultureInfo.InvariantCulture,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static LevelData Deserialize(string json)
        {
            LevelData data;
            try
            {
                data = JsonConvert.DeserializeObject<LevelData>(json, Settings);
            }
            catch (JsonException exception)
            {
                throw new LevelFormatException($"Level JSON is invalid: {exception.Message}", exception);
            }

            if (data == null)
                throw new LevelFormatException("Level JSON is empty.");
            if (data.SchemaVersion != CurrentSchemaVersion)
                throw new LevelFormatException($"Unsupported level schema version {data.SchemaVersion}; expected {CurrentSchemaVersion}.");
            return data;
        }

        public static string Serialize(LevelData data) => JsonConvert.SerializeObject(data, Formatting.Indented, Settings);

        /// <summary>Builds the whole level: roads, buildings, through traffic and goal.</summary>
        public static Level CreateLevel(string id, LevelData data, RoadProfileCatalog profiles)
        {
            var network = CreateNetwork(data, profiles, out var nodes);

            var buildings = data.Buildings.Select(building =>
            {
                var kind = ParseKind(building.Type);
                var door = ResolveNode(nodes, building.Door);
                if (network.GetNode(door).Degree != 1)
                    throw new LevelFormatException($"Door of building {building.Id} must be the dead end of its driveway.");
                return new Building(building.Id, kind, new float2(building.X, building.Y), math.radians(building.Rotation), door,
                    building.PerMinute ?? Building.DefaultTripsPerMinute(kind));
            }).ToArray();

            var edges = data.Edges.Select(edge => (ResolveNode(nodes, edge.Node), edge.PerMinute)).ToArray();

            var objective = data.Objective == null
                ? null
                : new LevelObjective(data.Objective.Trips, data.Objective.Stars.Select(star => new StarCondition(star.MaxSpent, star.MaxAverageTrip)));

            var camera = data.Camera;
            return new Level(id, data.TitleKey, data.IntroKey, data.HintKey, network, buildings, edges, objective, data.Budget,
                camera != null ? new float2(camera.X, camera.Y) : null, camera?.HalfHeight, math.max(data.Warmup, 0f));
        }

        public static RoadNetwork CreateNetwork(LevelData data, RoadProfileCatalog profiles) => CreateNetwork(data, profiles, out _);

        private static RoadNetwork CreateNetwork(LevelData data, RoadProfileCatalog profiles, out Dictionary<int, NodeId> nodes)
        {
            var network = new RoadNetwork();
            nodes = new Dictionary<int, NodeId>();

            foreach (var node in data.Nodes)
            {
                if (nodes.ContainsKey(node.Id))
                    throw new LevelFormatException($"Duplicate node id {node.Id}.");
                nodes.Add(node.Id, network.AddNode(new float2(node.X, node.Y), RoadElevation.HeightOf(node.Level)));
            }

            foreach (var segment in data.Segments)
            {
                if (!profiles.TryGet(segment.Profile, out var profile))
                    throw new LevelFormatException($"Unknown road profile '{segment.Profile}'.");

                var start = ResolveNode(nodes, segment.Start);
                var end = ResolveNode(nodes, segment.End);

                if (segment.Control1 == null && segment.Control2 == null)
                {
                    var from = network.GetNode(start).Position;
                    var to = network.GetNode(end).Position;
                    network.AddSegment(start, end, math.lerp(from, to, 1f / 3f), math.lerp(from, to, 2f / 3f), profile, Traits(segment));
                }
                else
                {
                    network.AddSegment(start, end, ToPoint(segment.Control1), ToPoint(segment.Control2), profile, Traits(segment));
                }
            }

            foreach (var node in data.Nodes)
            {
                if (node.Lights)
                    network.SetJunctionSettings(nodes[node.Id],
                        JunctionSettings.Default.WithControl(JunctionControl.TrafficLights, node.ProtectedLeft));
            }

            return network;
        }

        private static BuildingKind ParseKind(string type) => type switch
        {
            "house" => BuildingKind.House,
            "shop" => BuildingKind.Shop,
            "office" => BuildingKind.Office,
            _ => throw new LevelFormatException($"Unknown building type '{type}'.")
        };

        private static SegmentTraits Traits(SegmentData segment) =>
            (segment.Roundabout ? SegmentTraits.Roundabout : SegmentTraits.None) | (segment.Locked ? SegmentTraits.Locked : SegmentTraits.None);

        private static NodeId ResolveNode(IReadOnlyDictionary<int, NodeId> nodes, int id) =>
            nodes.TryGetValue(id, out var node) ? node : throw new LevelFormatException($"Segment refers to missing node {id}.");

        private static float2 ToPoint(float[] values)
        {
            if (values == null || values.Length != 2)
                throw new LevelFormatException("A control point must be an array [x, y]; curved segments need both control points.");
            return new float2(values[0], values[1]);
        }
    }
}
