using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Commands;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Levels;
using TrafficGame.Core.Roads;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Presentation.City;
using TrafficGame.Presentation.Hud;
using TrafficGame.Presentation.Levels;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Roads;
using TrafficGame.Presentation.Simulation;
using TrafficGame.Presentation.Toolbar;
using TrafficGame.Presentation.Tools;
using TrafficGame.Views.City;
using TrafficGame.Views.Debugging;
using TrafficGame.Views.Roads;
using TrafficGame.Views.Simulation;
using TrafficGame.Views.Tools;
using TrafficGame.Views.UI;
using UnityEngine;

namespace TrafficGame.App
{
    public enum SessionMode
    {
        /// <summary>Behind the title menu: the map lives, the player cannot build.</summary>
        Showcase,

        /// <summary>A playable level or sandbox.</summary>
        Play
    }

    /// <summary>Scene views shared by all sessions; each session draws into them.</summary>
    [Serializable]
    public sealed class SessionViews
    {
        [SerializeField] private RoadNetworkView _roads;
        [SerializeField] private LaneGraphDebugView _laneGraph;
        [SerializeField] private RoadPreviewView _preview;
        [SerializeField] private BuildingsView _buildings;
        [SerializeField] private CongestionView _congestion;
        [SerializeField] private JunctionControlsView _junctionControls;
        [SerializeField] private TrafficView _traffic;
        [SerializeField] private FrameTicker _frames;
        [SerializeField] private MapPointerInputAdapter _pointer;
        [SerializeField] private ToolHotkeysAdapter _hotkeys;
        [SerializeField] private GameUi _ui;

        public RoadNetworkView Roads => _roads;
        public LaneGraphDebugView LaneGraph => _laneGraph;
        public RoadPreviewView Preview => _preview;
        public BuildingsView Buildings => _buildings;
        public CongestionView Congestion => _congestion;
        public JunctionControlsView JunctionControls => _junctionControls;
        public TrafficView Traffic => _traffic;
        public FrameTicker Frames => _frames;
        public MapPointerInputAdapter Pointer => _pointer;
        public ToolHotkeysAdapter Hotkeys => _hotkeys;
        public GameUi Ui => _ui;
    }

    /// <summary>
    /// Everything that lives while one level is open: its road network, traffic and demand, construction tools,
    /// goal and the presenters binding them to the shared views. Disposing it tears the level down completely.
    /// </summary>
    public sealed class LevelSession : IDisposable
    {
        /// <summary>Room left around the buildings when a level opens, meters.</summary>
        private const float FocusMargin = 15f;

        private readonly List<IDisposable> _disposables = new();
        private readonly Level _level;
        private readonly GameConfig _config;
        private readonly SessionViews _views;
        private readonly SimulationPresenter _simulation;
        private readonly HudView _hud;
        private IntroPresenter _intro;

        /// <param name="levelNumber">Position in the campaign, 1-based; 0 for the sandbox.</param>
        public LevelSession(Level level, int levelNumber, SessionMode mode, bool hasNextLevel, GameConfig config, RoadProfileCatalog profiles,
            SessionViews views, CameraPresenter camera)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _views = views ?? throw new ArgumentNullException(nameof(views));

            // A playable level opens with every building in view, wherever the HUD leaves room; the sandbox and the menu
            // backdrop use the camera the level file asks for. The camera settles into the view as the level fades in.
            if (mode == SessionMode.Play && level.TryGetFocusBounds(FocusMargin, out var focusMin, out var focusMax))
                camera.Frame(focusMin, focusMax, views.Ui.MapInsets, CameraMove.Arrive);
            else
                camera.Focus(level.CameraCenter ?? Unity.Mathematics.float2.zero, level.CameraHalfHeight ?? config.CameraSettings.InitialHalfHeight,
                    CameraMove.Arrive);

            var roads = Track(new RoadNetworkPresenter(level.Network, config.RoadGeometrySettings, views.Roads, views.LaneGraph));
            var settings = config.CreateTrafficSettings();
            var waitsForIntro = mode == SessionMode.Play && level.IntroKey != null;

            var trips = new TripGenerator(level.CreateTripSources(), settings.TruckShare, settings.RandomSeed);
            _simulation = Track(new SimulationPresenter(level.Network, roads, settings, config.CreateSimulationClock(),
                config.SimulationTimeScale, waitsForIntro ? 0f : 1f, trips,
                views.Frames, views.Traffic, views.Congestion, views.Ui.SimulationControls));
            _simulation.WarmUp(level.WarmupSeconds);
            Track(new BuildingsPresenter(level.Buildings, _simulation, views.Buildings));
            Track(new JunctionControlsPresenter(_simulation, views.Frames, views.JunctionControls));

            views.Ui.SetHudVisible(mode == SessionMode.Play);
            if (mode == SessionMode.Showcase)
                return;

            var budget = new Budget(level.Budget > 0 ? level.Budget : config.StartingBudget);
            var (tools, history) = CreateConstruction(level.Network, roads, budget, profiles, camera);

            _hud = views.Ui.Hud;
            _hud.MenuRequested += OnMenuRequested;

            if (level.Objective != null)
            {
                CreateObjective(level, budget, hasNextLevel);
                Track(new AutoSpeedPresenter(_simulation, views.Ui.SimulationControls, tools, history, views.Pointer, views.Frames,
                    config.AutoSpeedIdleSeconds, config.AutoSpeed));
            }

            if (waitsForIntro)
            {
                _intro = Track(new IntroPresenter(views.Ui.Intro, level, levelNumber));
                _intro.Started += OnIntroFinished;
                _intro.Declined += OnMenuRequested;
            }
        }

        /// <summary>The player is now actively playing (the intro card was dismissed, or there was none).</summary>
        public event Action GameplayStarted;

        /// <summary>The goal was reached; carries the stars earned.</summary>
        public event Action<int> Completed;

        public event Action RetryRequested;
        public event Action NextRequested;
        public event Action MenuRequested;

        public Level Level => _level;
        public bool StartsImmediately => _intro == null;

        public void Dispose()
        {
            if (_hud != null)
                _hud.MenuRequested -= OnMenuRequested;
            if (_intro != null)
            {
                _intro.Started -= OnIntroFinished;
                _intro.Declined -= OnMenuRequested;
            }

            var result = _views.Ui.Result;
            result.RetryRequested -= OnRetry;
            result.NextRequested -= OnNext;
            result.LevelsRequested -= OnMenuRequested;

            for (var i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Dispose();
            _disposables.Clear();

            _views.Preview.Hide();
            _views.Ui.Tooltip.Hide();
        }

        private (ToolController tools, CommandHistory history) CreateConstruction(RoadNetwork network, RoadNetworkPresenter roads, Budget budget,
            RoadProfileCatalog profiles, CameraPresenter camera)
        {
            var history = new CommandHistory(_config.UndoDepth);
            var selection = new RoadProfileSelection(profiles.All);
            var elevation = new ElevationSelection();
            var services = new ConstructionServices(network, roads, budget, history,
                _config.CreateRoadSnapper(), _config.CreateRoadGuides(), new RoadPlanner(network, _config.RoadPlanningRules), selection, elevation);

            var ui = _views.Ui;
            if (!profiles.TryGet(_config.RoundaboutProfileId, out var ringProfile))
                throw new InvalidOperationException($"Roundabout profile '{_config.RoundaboutProfileId}' is not in the profile list.");

            var tools = new Dictionary<ToolKind, IMapTool>
            {
                [ToolKind.Road] = new RoadToolPresenter(false, services, _views.Preview, ui.Tooltip),
                [ToolKind.CurvedRoad] = new RoadToolPresenter(true, services, _views.Preview, ui.Tooltip),
                [ToolKind.Roundabout] = new RoundaboutToolPresenter(services, new RoundaboutPlanner(_config.RoadPlanningRules),
                    ringProfile, _config.RoundaboutRadius, _views.Preview, ui.Tooltip),
                [ToolKind.Bulldoze] = new BulldozeToolPresenter(services, _config.BulldozeRefundRatio, _views.Preview, ui.Tooltip),
                [ToolKind.TrafficLights] = new TrafficLightToolPresenter(services, _config.TrafficLightCost, _config.BulldozeRefundRatio,
                    _views.Preview, ui.Tooltip),
                [ToolKind.LaneArrows] = new LaneArrowsToolPresenter(services, _views.Preview, ui.Tooltip)
            };

            var inputs = new IToolInput[] { ui.Toolbar, _views.Hotkeys };
            var toolController = Track(new ToolController(_views.Pointer, inputs, camera, history, selection, tools));
            Track(new HudPresenter(ui.Hud, budget, toolController));
            Track(new ToolbarPresenter(ui.Toolbar, toolController, history));
            Track(new RoadProfileBarPresenter(ui.ProfileBar, toolController, selection));
            Track(new ElevationPresenter(ui.Elevation, toolController, elevation));
            return (toolController, history);
        }

        private void CreateObjective(Level level, Budget budget, bool hasNextLevel)
        {
            var doors = new HashSet<NodeId>(level.DestinationDoors);
            var progress = new LevelProgress(level.Objective, budget);
            var objective = Track(new ObjectivePresenter(progress, _simulation.Simulation, doors, _views.Ui.Objective, _views.Ui.Result,
                hasNextLevel, new LocalizedText(level.TitleKey ?? level.Id)));
            objective.Completed += stars =>
            {
                _simulation.SetSpeed(0f);
                Completed?.Invoke(stars);
            };

            var result = _views.Ui.Result;
            result.RetryRequested += OnRetry;
            result.NextRequested += OnNext;
            result.LevelsRequested += OnMenuRequested;
        }

        private void OnIntroFinished()
        {
            _simulation.SetSpeed(1f);
            GameplayStarted?.Invoke();
        }

        private void OnRetry() => RetryRequested?.Invoke();
        private void OnNext() => NextRequested?.Invoke();
        private void OnMenuRequested() => MenuRequested?.Invoke();

        private T Track<T>(T disposable) where T : IDisposable
        {
            _disposables.Add(disposable);
            return disposable;
        }
    }
}
