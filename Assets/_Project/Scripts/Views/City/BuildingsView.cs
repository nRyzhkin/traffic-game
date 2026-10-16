using System.Collections.Generic;
using TrafficGame.Core.City;
using TrafficGame.Presentation.City;
using TrafficGame.Views.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.Views.City
{
    /// <summary>
    /// Flat buildings in the trailer's style: a soft drop shadow, a darker walls outline, a lighter roof with a ridge,
    /// colour by type. Buildings no trip can use yet carry a red "!" badge.
    /// </summary>
    public sealed class BuildingsView : ProceduralMeshView, IBuildingsView, IMapPaletteView
    {
        private const float WallInset = 1.2f;
        private const float RidgeWidth = 0.5f;
        private const float BadgeRadius = 4f;
        private static readonly float2 ShadowOffset = new(1.8f, -1.8f);

        private readonly MeshBuilder _builder = new();
        private MapPalette _palette = MapPalette.Light();
        private IReadOnlyList<Building> _buildings;
        private IReadOnlyCollection<int> _disconnected;

        protected override int SortingOrder => MapLayers.Buildings;

        public void ApplyPalette(MapPalette palette)
        {
            _palette = palette;
            if (_buildings != null)
                Render(_buildings, _disconnected);
        }

        public void Render(IReadOnlyList<Building> buildings, IReadOnlyCollection<int> disconnected)
        {
            _buildings = buildings;
            _disconnected = disconnected;
            _builder.Clear();
            foreach (var building in buildings)
                AddShape(building, ShadowOffset, 0f, _palette.Shadow);

            var badges = new HashSet<int>(disconnected);
            foreach (var building in buildings)
            {
                var (walls, roof) = ColorsOf(building.Kind);
                AddShape(building, float2.zero, 0f, walls);
                AddShape(building, float2.zero, WallInset, roof);
                AddRidge(building, walls);
                if (badges.Contains(building.Id))
                    AddBadge(building.Position + new float2(0f, building.Size.y * 0.5f + BadgeRadius));
            }

            Apply(_builder);
        }

        private void AddShape(Building building, float2 offset, float inset, Color32 color)
        {
            var half = building.Size * 0.5f - inset;
            _builder.AddQuad(
                Corner(building, new float2(-half.x, -half.y)) + offset,
                Corner(building, new float2(half.x, -half.y)) + offset,
                Corner(building, new float2(half.x, half.y)) + offset,
                Corner(building, new float2(-half.x, half.y)) + offset,
                color);
        }

        /// <summary>A roof ridge along the longer side.</summary>
        private void AddRidge(Building building, Color32 color)
        {
            var half = building.Size * 0.5f - WallInset * 2f;
            var alongX = building.Size.x >= building.Size.y;
            var a = alongX ? new float2(-half.x, -RidgeWidth) : new float2(-RidgeWidth, -half.y);
            var b = alongX ? new float2(half.x, RidgeWidth) : new float2(RidgeWidth, half.y);
            _builder.AddQuad(Corner(building, a), Corner(building, new float2(b.x, a.y)), Corner(building, b), Corner(building, new float2(a.x, b.y)), color);
        }

        private void AddBadge(float2 center)
        {
            _builder.AddCircle(center, BadgeRadius, _palette.Badge);
            _builder.AddRect(center + new float2(-0.45f, -0.6f), center + new float2(0.45f, 2.6f), _palette.BadgeMark);
            _builder.AddCircle(center + new float2(0f, -1.8f), 0.55f, _palette.BadgeMark, 12);
        }

        private static float2 Corner(Building building, float2 local)
        {
            var cos = math.cos(building.Rotation);
            var sin = math.sin(building.Rotation);
            return building.Position + new float2(local.x * cos - local.y * sin, local.x * sin + local.y * cos);
        }

        private (Color32 walls, Color32 roof) ColorsOf(BuildingKind kind) => kind switch
        {
            BuildingKind.House => (_palette.HouseWalls, _palette.HouseRoof),
            BuildingKind.Shop => (_palette.ShopWalls, _palette.ShopRoof),
            _ => (_palette.OfficeWalls, _palette.OfficeRoof)
        };
    }
}
