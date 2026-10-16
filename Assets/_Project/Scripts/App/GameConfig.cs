using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Views.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficGame.App
{
    /// <summary>Global tuning values. World units are meters; the map is centered at the origin.</summary>
    [CreateAssetMenu(menuName = "Traffic Game/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("World")]
        [SerializeField] private Vector2 _worldSize = new(2000f, 2000f);
        [SerializeField, Min(1f)] private float _groundCellSize = 50f;

        [Header("Themes")]
        [Tooltip("Map colors in the light theme.")]
        [SerializeField] private MapPalette _lightMap = MapPalette.Light();
        [Tooltip("Map colors in the dark theme.")]
        [SerializeField] private MapPalette _darkMap = MapPalette.Dark();

        [Header("Levels")]
        [Tooltip("Campaign levels in play order; the file name is the level id used in saves.")]
        [SerializeField] private TextAsset[] _campaignLevels = Array.Empty<TextAsset>();
        [Tooltip("Free-build map; also runs behind the title menu.")]
        [SerializeField] private TextAsset _sandboxLevel;

        [Header("Camera")]
        [SerializeField, Min(1f)] private float _minZoomHalfHeight = 15f;
        [SerializeField, Min(1f)] private float _maxZoomHalfHeight = 600f;
        [SerializeField, Min(1f)] private float _initialZoomHalfHeight = 150f;
        [SerializeField, Min(0.0001f)] private float _scrollZoomSensitivity = 0.008f;

        [Header("Roads")]
        [SerializeField, Min(1f)] private float _laneWidth = 3.5f;
        [SerializeField, Min(0f)] private float _shoulderWidth = 0.5f;
        [SerializeField, Min(0f)] private float _junctionPadding = 4f;
        [SerializeField, Min(0f)] private float _minJunctionTrim = 0.5f;
        [SerializeField, Min(1f)] private float _maxJunctionTrim = 60f;
        [SerializeField, Min(0.1f)] private float _curveSampleSpacing = 2f;
        [SerializeField] private RoadProfileConfig[] _roadProfiles =
        {
            new("two_lane", 1, 1, 4f, 35f),
            new("four_lane", 2, 2, 8f, 45f),
            new("one_way_two_lane", 2, 0, 6f, 30f),
            new("one_way_one_lane", 1, 0, 3f, 20f)
        };

        [Header("Construction")]
        [SerializeField, Min(0)] private long _startingBudget = 50_000;
        [SerializeField, Min(1)] private int _undoDepth = 100;
        [SerializeField, Range(0f, 1f)] private float _bulldozeRefundRatio = 0.75f;
        [SerializeField, Min(0)] private long _trafficLightCost = 400;
        [SerializeField, Min(1f)] private float _minRoadLength = 8f;
        [SerializeField, Min(1f)] private float _minCurveRadius = 12f;
        [SerializeField, Range(5f, 90f)] private float _minCrossingAngle = 30f;
        [SerializeField, Range(5f, 90f)] private float _minConnectionAngle = 25f;
        [SerializeField, Min(1f)] private float _minJunctionSpacing = 12f;
        [Tooltip("Steepest ramp: meters of height per meter of road.")]
        [SerializeField, Range(0.02f, 0.5f)] private float _maxGrade = 0.125f;
        [Tooltip("Extra price of elevated road as a share of the normal price.")]
        [SerializeField, Min(0f)] private float _elevatedCostFactor = 1.5f;
        [SerializeField, Min(0f)] private float _snapMargin = 3f;
        [SerializeField, Range(0f, 15f)] private float _guideSnapAngle = 4f;
        [SerializeField, Min(0f)] private float _guideSearchRadius = 60f;
        [SerializeField] private string _roundaboutProfileId = "one_way_one_lane";
        [SerializeField, Min(10f)] private float _roundaboutRadius = 22f;

        [Header("Traffic")]
        [SerializeField, Min(1f)] private float _simulationTickRate = 20f;
        [SerializeField, Min(1)] private int _maxTicksPerFrame = 8;
        [Tooltip("Simulated seconds per real second at the 1× setting; 2 makes traffic look lively and levels short.")]
        [SerializeField, Min(0.1f)] private float _simulationTimeScale = 2f;
        [Tooltip("Seconds without input, after the player changed something, before a level fast-forwards.")]
        [SerializeField, Min(0f)] private float _autoSpeedIdleSeconds = 4f;
        [SerializeField, Min(1f)] private float _autoSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float _truckShare = 0.1f;
        [SerializeField, Min(1f)] private float _criticalGap = 4f;
        [SerializeField, Min(10)] private int _maxVehicles = 4000;

        [Header("Localization")]
        [SerializeField] private TextAsset[] _languageFiles = Array.Empty<TextAsset>();
        [SerializeField] private string _fallbackLanguage = "en";

        public float2 WorldMin => -WorldHalfSize;
        public float2 WorldMax => WorldHalfSize;
        public float GroundCellSize => _groundCellSize;
        public MapPalette LightMap => _lightMap;
        public MapPalette DarkMap => _darkMap;
        public System.Collections.Generic.IReadOnlyList<TextAsset> CampaignLevels => _campaignLevels;
        public TextAsset SandboxLevel => _sandboxLevel;
        public float ScrollZoomSensitivity => _scrollZoomSensitivity;
        public long StartingBudget => _startingBudget;
        public int UndoDepth => _undoDepth;
        public float BulldozeRefundRatio => _bulldozeRefundRatio;
        public long TrafficLightCost => _trafficLightCost;
        public string RoundaboutProfileId => _roundaboutProfileId;
        public float RoundaboutRadius => _roundaboutRadius;
        public string FallbackLanguage => _fallbackLanguage;

        /// <summary>Language file contents by language code; files are named after their code (en.json, pt-BR.json).</summary>
        public IReadOnlyDictionary<string, string> LanguageFiles => _languageFiles.ToDictionary(file => file.name, file => file.text);

        public RoadPlanningRules RoadPlanningRules =>
            new(_minRoadLength, _minCurveRadius, _minCrossingAngle, _minConnectionAngle, _minJunctionSpacing,
                _curveSampleSpacing, WorldMin, WorldMax, _maxGrade, _elevatedCostFactor);

        public float SimulationTimeScale => _simulationTimeScale;
        public float AutoSpeedIdleSeconds => _autoSpeedIdleSeconds;
        public float AutoSpeed => _autoSpeed;

        public SimulationClock CreateSimulationClock() => new(_simulationTickRate, _maxTicksPerFrame);

        public TrafficSettings CreateTrafficSettings() => new()
        {
            TruckShare = _truckShare,
            CriticalGap = _criticalGap,
            MaxVehicles = _maxVehicles
        };

        public RoadSnapper CreateRoadSnapper() => new(_snapMargin, _minJunctionSpacing);

        public RoadGuides CreateRoadGuides() => new(_guideSnapAngle, _guideSearchRadius);

        public CameraSettings CameraSettings =>
            new(_minZoomHalfHeight, _maxZoomHalfHeight, _initialZoomHalfHeight, WorldMin, WorldMax);

        public RoadGeometrySettings RoadGeometrySettings =>
            new(_junctionPadding, _minJunctionTrim, _maxJunctionTrim, _curveSampleSpacing);

        public RoadProfileCatalog CreateRoadProfileCatalog() =>
            new(_roadProfiles.Select(profile => profile.ToProfile(_laneWidth, _shoulderWidth)));

        private const float MetersPerSecondPerMph = 0.44704f;

        private float2 WorldHalfSize => (float2)(Vector2)_worldSize * 0.5f;

        [Serializable]
        private sealed class RoadProfileConfig
        {
            [SerializeField] private string _id;
            [SerializeField, Min(1)] private int _forwardLanes = 1;
            [SerializeField, Min(0)] private int _backwardLanes = 1;
            [SerializeField, Min(0f)] private float _costPerMeter = 4f;
            [Tooltip("Miles per hour, as on US road signs.")]
            [SerializeField, Min(5f)] private float _speedLimitMph = 35f;

            public RoadProfileConfig(string id, int forwardLanes, int backwardLanes, float costPerMeter, float speedLimitMph)
            {
                _id = id;
                _forwardLanes = forwardLanes;
                _backwardLanes = backwardLanes;
                _costPerMeter = costPerMeter;
                _speedLimitMph = speedLimitMph;
            }

            public RoadProfile ToProfile(float laneWidth, float shoulderWidth) =>
                new(_id, _forwardLanes, _backwardLanes, laneWidth, shoulderWidth, _costPerMeter, _speedLimitMph * MetersPerSecondPerMph);
        }
    }
}
