using System;
using TrafficGame.Views.Roads;
using UnityEngine;

namespace TrafficGame.Views.Rendering
{
    /// <summary>Every color the map is drawn with, one set per theme.</summary>
    [Serializable]
    public sealed class MapPalette
    {
        [Header("Ground")]
        [SerializeField] private Color32 _groundA = new(234, 231, 223, 255);
        [SerializeField] private Color32 _groundB = new(228, 224, 214, 255);
        [SerializeField] private Color _background = new(0.82f, 0.81f, 0.78f);

        [Header("Roads")]
        [SerializeField] private RoadStyle _roads = new();

        [Header("Buildings")]
        [SerializeField] private Color32 _houseWalls = new(84, 146, 60, 255);
        [SerializeField] private Color32 _houseRoof = new(124, 196, 88, 255);
        [SerializeField] private Color32 _shopWalls = new(56, 92, 168, 255);
        [SerializeField] private Color32 _shopRoof = new(96, 140, 222, 255);
        [SerializeField] private Color32 _officeWalls = new(98, 70, 156, 255);
        [SerializeField] private Color32 _officeRoof = new(150, 120, 214, 255);
        [SerializeField] private Color32 _shadow = new(40, 36, 28, 45);
        [SerializeField] private Color32 _badge = new(227, 155, 0, 255);
        [SerializeField] private Color32 _badgeMark = new(255, 255, 255, 255);

        [Header("Traffic")]
        [SerializeField] private Color32 _congestion = new(226, 72, 62, 255);
        [SerializeField] private Color32 _stopLine = new(95, 94, 90, 255);
        [SerializeField] private Color32 _signalRed = new(224, 71, 60, 255);
        [SerializeField] private Color32 _signalYellow = new(242, 183, 41, 255);
        [SerializeField] private Color32 _signalGreen = new(47, 166, 90, 255);

        public static MapPalette Light() => new();

        public static MapPalette Dark() => new()
        {
            _groundA = new Color32(22, 24, 27, 255),
            _groundB = new Color32(26, 28, 32, 255),
            _background = new Color(0.07f, 0.08f, 0.09f),
            _roads = RoadStyle.Dark(),
            _houseWalls = new Color32(125, 190, 113, 255),
            _houseRoof = new Color32(78, 140, 70, 255),
            _shopWalls = new Color32(120, 162, 238, 255),
            _shopRoof = new Color32(63, 108, 192, 255),
            _officeWalls = new Color32(164, 140, 230, 255),
            _officeRoof = new Color32(110, 87, 180, 255),
            _shadow = new Color32(0, 0, 0, 110),
            _badge = new Color32(255, 201, 74, 255),
            _badgeMark = new Color32(27, 27, 31, 255),
            _congestion = new Color32(255, 94, 82, 140),
            _stopLine = new Color32(154, 160, 170, 255),
            _signalRed = new Color32(255, 94, 82, 255),
            _signalYellow = new Color32(255, 196, 64, 255),
            _signalGreen = new Color32(59, 209, 110, 255)
        };

        /// <summary>A palette between <paramref name="a"/> and <paramref name="b"/>, for cross-fading themes.</summary>
        public static MapPalette Lerp(MapPalette a, MapPalette b, float t) => new()
        {
            _groundA = Color32.Lerp(a._groundA, b._groundA, t),
            _groundB = Color32.Lerp(a._groundB, b._groundB, t),
            _background = Color.Lerp(a._background, b._background, t),
            _roads = RoadStyle.Lerp(a._roads, b._roads, t),
            _houseWalls = Color32.Lerp(a._houseWalls, b._houseWalls, t),
            _houseRoof = Color32.Lerp(a._houseRoof, b._houseRoof, t),
            _shopWalls = Color32.Lerp(a._shopWalls, b._shopWalls, t),
            _shopRoof = Color32.Lerp(a._shopRoof, b._shopRoof, t),
            _officeWalls = Color32.Lerp(a._officeWalls, b._officeWalls, t),
            _officeRoof = Color32.Lerp(a._officeRoof, b._officeRoof, t),
            _shadow = Color32.Lerp(a._shadow, b._shadow, t),
            _badge = Color32.Lerp(a._badge, b._badge, t),
            _badgeMark = Color32.Lerp(a._badgeMark, b._badgeMark, t),
            _congestion = Color32.Lerp(a._congestion, b._congestion, t),
            _stopLine = Color32.Lerp(a._stopLine, b._stopLine, t),
            _signalRed = Color32.Lerp(a._signalRed, b._signalRed, t),
            _signalYellow = Color32.Lerp(a._signalYellow, b._signalYellow, t),
            _signalGreen = Color32.Lerp(a._signalGreen, b._signalGreen, t)
        };

        public Color32 GroundA => _groundA;
        public Color32 GroundB => _groundB;
        public Color Background => _background;
        public RoadStyle Roads => _roads;
        public Color32 HouseWalls => _houseWalls;
        public Color32 HouseRoof => _houseRoof;
        public Color32 ShopWalls => _shopWalls;
        public Color32 ShopRoof => _shopRoof;
        public Color32 OfficeWalls => _officeWalls;
        public Color32 OfficeRoof => _officeRoof;
        public Color32 Shadow => _shadow;
        public Color32 Badge => _badge;
        public Color32 BadgeMark => _badgeMark;
        public Color32 Congestion => _congestion;
        public Color32 StopLine => _stopLine;
        public Color32 SignalRed => _signalRed;
        public Color32 SignalYellow => _signalYellow;
        public Color32 SignalGreen => _signalGreen;
    }
}
