using System.Collections.Generic;
using TrafficGame.Core.Geometry;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Simulation;
using TrafficGame.Views.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.Views.Simulation
{
    /// <summary>
    /// Draws all vehicles as one mesh rebuilt every frame: a coloured body with a lighter roof for cars,
    /// a cab and a white trailer for trucks. Vehicles fade and grow in when they appear and fade out where they
    /// leave the simulation, so nothing pops.
    /// </summary>
    public sealed class TrafficView : ProceduralMeshView, ITrafficView
    {
        private const float RoofLengthShare = 0.45f;
        private const float RoofWidthShare = 0.7f;
        private const float CabLength = 2.2f;

        private static readonly Color32[] Palette =
        {
            new(64, 120, 212, 255), new(86, 168, 92, 255), new(232, 136, 52, 255), new(226, 196, 60, 255),
            new(214, 96, 150, 255), new(136, 98, 200, 255), new(52, 166, 170, 255), new(206, 72, 64, 255)
        };

        private static readonly Color32 TrailerColor = new(236, 236, 236, 255);

        // A fade lasts this long in real time or in simulated time, whichever ends first (fast-forward stays snappy).
        private const float FadeSeconds = 0.3f;
        private const float FadeSimulatedSeconds = 0.6f;
        private const float StartScale = 0.6f;

        private MeshBuilder _builder;
        private readonly Dictionary<int, Track> _tracks = new();
        private readonly List<int> _gone = new();
        private readonly List<Track> _ghosts = new();
        private TrafficSimulation _simulation;
        private int _frame;

        protected override int SortingOrder => MapLayers.Vehicles;

        public void Render(TrafficSimulation simulation, float alpha)
        {
            if (!ReferenceEquals(simulation, _simulation))
            {
                // Another level: nothing of the old traffic lingers.
                _simulation = simulation;
                _tracks.Clear();
                _ghosts.Clear();
            }

            _frame++;
            var now = Time.time;
            Layers.Clear();

            for (var i = _ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = _ghosts[i];
                var progress = Progress(ghost, now, simulation.Time);
                if (progress >= 1f)
                {
                    _ghosts.RemoveAt(i);
                    continue;
                }

                AddVehicle(ghost.Last, 1f - progress);
            }

            for (var i = 0; i < simulation.VehicleCount; i++)
            {
                var vehicle = simulation.GetVehicle(i, alpha);
                if (!_tracks.TryGetValue(vehicle.Id, out var track))
                    track = new Track { Since = now, SinceSimulated = simulation.Time };

                track.Last = vehicle;
                track.Frame = _frame;
                _tracks[vehicle.Id] = track;
                AddVehicle(vehicle, Progress(track, now, simulation.Time));
            }

            // Vehicles that arrived (or lost their road) fade out where they were last seen.
            foreach (var (id, track) in _tracks)
            {
                if (track.Frame != _frame)
                    _gone.Add(id);
            }

            foreach (var id in _gone)
            {
                var track = _tracks[id];
                track.Since = now;
                track.SinceSimulated = simulation.Time;
                _ghosts.Add(track);
                _tracks.Remove(id);
            }

            _gone.Clear();
            Layers.Apply();
        }

        private static float Progress(Track track, float now, float simulatedNow) =>
            Mathf.Clamp01(Mathf.Max((now - track.Since) / FadeSeconds, (simulatedNow - track.SinceSimulated) / FadeSimulatedSeconds));

        /// <param name="shown">0 = invisible and small, 1 = fully there.</param>
        private void AddVehicle(VehicleSnapshot vehicle, float shown)
        {
            // Vehicles are drawn with the level they drive on: under a bridge, the bridge covers them.
            _builder = Layers[MapLayers.At(SortingOrder, vehicle.Height)];
            var eased = 1f - (1f - shown) * (1f - shown) * (1f - shown);
            var scale = Mathf.Lerp(StartScale, 1f, eased);
            var opacity = (byte)Mathf.RoundToInt(eased * 255f);
            var color = Palette[vehicle.ColorIndex % Palette.Length];
            color.a = opacity;
            if (vehicle.Kind == VehicleKind.Truck)
                AddTruck(vehicle, color, scale, opacity);
            else
                AddCar(vehicle, color, scale);
        }

        private void AddCar(VehicleSnapshot vehicle, Color32 color, float scale)
        {
            var length = vehicle.Length * scale;
            var width = vehicle.Width * scale;
            AddBox(vehicle.Position, vehicle.Direction, length, width, color);
            var roof = Color32.Lerp(color, new Color32(255, 255, 255, color.a), 0.35f);
            AddBox(vehicle.Position - vehicle.Direction * length * 0.05f, vehicle.Direction, length * RoofLengthShare, width * RoofWidthShare, roof);
        }

        private void AddTruck(VehicleSnapshot vehicle, Color32 color, float scale, byte opacity)
        {
            var length = vehicle.Length * scale;
            var cabLength = CabLength * scale;
            var trailerLength = length - cabLength - 0.3f * scale;
            var front = vehicle.Position + vehicle.Direction * length * 0.5f;
            var trailer = TrailerColor;
            trailer.a = opacity;
            AddBox(front - vehicle.Direction * cabLength * 0.5f, vehicle.Direction, cabLength, vehicle.Width * scale * 0.92f, color);
            AddBox(front - vehicle.Direction * (length - trailerLength * 0.5f), vehicle.Direction, trailerLength, vehicle.Width * scale, trailer);
        }

        private void AddBox(float2 center, float2 direction, float length, float width, Color32 color)
        {
            var along = direction * length * 0.5f;
            var across = Geometry2D.Left(direction) * width * 0.5f;
            _builder.AddQuad(center - along - across, center + along - across, center + along + across, center - along + across, color);
        }

        private struct Track
        {
            public VehicleSnapshot Last;
            public float Since;
            public float SinceSimulated;
            public int Frame;
        }
    }
}
