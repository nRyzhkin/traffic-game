using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Commands;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Tools;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    public sealed class JunctionToolPresenterTests
    {
        private const long LightCost = 400;
        private static readonly NodeId Center = new(1);

        private ConstructionTestBed _bed;
        private CommandHistory _history;
        private FakePreview _preview;
        private FakeTooltip _tooltip;
        private TapInput _input;
        private ToolController _controller;

        private void CreateTools(long balance)
        {
            _bed = new ConstructionTestBed(TestRoads.Crossing(TestRoads.TwoLane), balance);
            _history = new CommandHistory(capacity: 10);
            _preview = new FakePreview();
            _tooltip = new FakeTooltip();
            _input = new TapInput();

            var profiles = new RoadProfileSelection(new[] { TestRoads.TwoLane });
            var services = new ConstructionServices(_bed.Network, new LiveLayout(_bed.Network), _bed.Budget, _history, _bed.Snapper,
                new RoadGuides(4f, 60f), _bed.Planner, profiles);
            var tools = new Dictionary<ToolKind, IMapTool>
            {
                [ToolKind.TrafficLights] = new TrafficLightToolPresenter(services, LightCost, 0.75f, _preview, _tooltip),
                [ToolKind.LaneArrows] = new LaneArrowsToolPresenter(services, _preview, _tooltip)
            };
            _controller = new ToolController(_input, new IToolInput[] { _input }, new IdentityCamera(), _history, profiles, tools);
        }

        [TearDown]
        public void TearDown() => _controller?.Dispose();

        [Test]
        public void TrafficLightTool_StepsThroughLightsProtectedLeftAndRemoval()
        {
            CreateTools(balance: 1000);
            _input.RequestTool(ToolKind.TrafficLights);

            _input.Click(float2.zero);
            Assert.That(Settings.Control, Is.EqualTo(JunctionControl.TrafficLights));
            Assert.That(_bed.Budget.Balance, Is.EqualTo(600));

            _input.Click(float2.zero);
            Assert.That(Settings.ProtectedLeft, Is.True);
            Assert.That(_bed.Budget.Balance, Is.EqualTo(600), "Protected left turns are free.");

            _input.Click(float2.zero);
            Assert.That(Settings.IsDefault, Is.True);
            Assert.That(_bed.Budget.Balance, Is.EqualTo(900), "Removing lights refunds part of their price.");

            _history.Undo();
            Assert.That(Settings.ProtectedLeft, Is.True);
            Assert.That(_bed.Budget.Balance, Is.EqualTo(600));
        }

        [Test]
        public void TrafficLightTool_RefusesWithoutMoney()
        {
            CreateTools(balance: 100);
            _input.RequestTool(ToolKind.TrafficLights);

            _input.Move(float2.zero);
            Assert.That(_controller.CanConfirm, Is.False);
            Assert.That(_tooltip.Lines.Any(line => line.text.Key == TextKeys.Cost && line.tone == TooltipTone.Error));

            _input.Click(float2.zero);
            Assert.That(Settings.IsDefault, Is.True);
        }

        [Test]
        public void TrafficLightTool_IgnoresPlainRoads()
        {
            CreateTools(balance: 1000);
            _input.RequestTool(ToolKind.TrafficLights);

            _input.Move(new float2(60f, 0f));

            Assert.That(_controller.CanConfirm, Is.False);
            Assert.That(_preview.Junction, Is.Null);
        }

        [Test]
        public void LaneArrowsTool_TapSelectsTheJunctionAndTapsOnAMarkerCycleItsArrows()
        {
            CreateTools(balance: 1000);
            _input.RequestTool(ToolKind.LaneArrows);

            _input.Tap(float2.zero);
            Assert.That(_preview.Markers.Count, Is.EqualTo(4), "One marker per lane arriving at the crossing.");
            var marker = _preview.Markers.First(m => m.Position.x > 5f);
            Assert.That(marker.Arrows, Is.EqualTo(LaneArrows.Left | LaneArrows.Straight | LaneArrows.Right));

            _input.Tap(marker.Position);
            Assert.That(ArrivingLaneFromEast().Arrows, Is.EqualTo(LaneArrows.Left | LaneArrows.Right));

            _input.Tap(marker.Position);
            Assert.That(ArrivingLaneFromEast().Arrows, Is.EqualTo(LaneArrows.Straight));
            Assert.That(_bed.Budget.Balance, Is.EqualTo(1000), "Painting arrows is free.");

            for (var i = 0; i < 5; i++)
                _input.Tap(marker.Position);
            Assert.That(ArrivingLaneFromEast().Arrows, Is.EqualTo(LaneArrows.Left | LaneArrows.Straight | LaneArrows.Right));
            Assert.That(Settings.IsDefault, Is.True, "Coming back to the default arrows stores nothing.");
        }

        [Test]
        public void LaneArrowsTool_CancelLeavesTheJunctionFirst()
        {
            CreateTools(balance: 1000);
            _input.RequestTool(ToolKind.LaneArrows);
            _input.Tap(float2.zero);

            _input.Cancel();
            Assert.That(_controller.Active, Is.EqualTo(ToolKind.LaneArrows));
            Assert.That(_preview.Hidden, Is.True);

            _input.Cancel();
            Assert.That(_controller.Active, Is.EqualTo(ToolKind.None));
        }

        private JunctionSettings Settings => _bed.Network.GetJunctionSettings(Center);

        private Core.Lanes.Lane ArrivingLaneFromEast() =>
            _bed.Layout.Lanes.Lanes.Single(lane => lane.To == Center && _bed.Network.GetNode(lane.From).Position.x > 50f);

        private sealed class LiveLayout : IRoadLayoutSource
        {
            private readonly RoadNetwork _network;

            public LiveLayout(RoadNetwork network) => _network = network;

            public RoadLayout Layout => RoadLayout.Build(_network, TestRoads.Settings);

            public event Action LayoutChanged { add { } remove { } }
        }

        private sealed class IdentityCamera : IScreenToWorld
        {
            public float2 ScreenToWorld(float2 screenPoint) => screenPoint;
        }

        private sealed class TapInput : IMapPointerInput, IToolInput
        {
            public event Action<float2> PointerMoved;
            public event Action<float2> Tapped;
            public event Action<float2> PrimaryClicked;
            public event Action SecondaryClicked;
            public event Action<ToolKind> ToolRequested;
            public event Action ConfirmRequested { add { } remove { } }
            public event Action UndoRequested { add { } remove { } }
            public event Action RedoRequested { add { } remove { } }
            public event Action CancelRequested { add { } remove { } }
            public event Action NextProfileRequested { add { } remove { } }

            public void Move(float2 point) => PointerMoved?.Invoke(point);
            public void Click(float2 point) => PrimaryClicked?.Invoke(point);
            public void Cancel() => SecondaryClicked?.Invoke();
            public void RequestTool(ToolKind tool) => ToolRequested?.Invoke(tool);

            /// <summary>A touch tap, as the pointer adapter reports it.</summary>
            public void Tap(float2 point)
            {
                PointerMoved?.Invoke(point);
                Tapped?.Invoke(point);
            }
        }

        private sealed class FakePreview : IJunctionPreviewView
        {
            public JunctionGeometry Junction { get; private set; }
            public IReadOnlyList<LaneArrowMarker> Markers { get; private set; } = Array.Empty<LaneArrowMarker>();
            public bool Hidden { get; private set; }

            public void ShowJunction(JunctionGeometry junction, bool valid)
            {
                Junction = junction;
                Hidden = false;
            }

            public void ShowLaneArrows(JunctionGeometry junction, IReadOnlyList<LaneArrowMarker> markers, int highlighted)
            {
                Junction = junction;
                Markers = markers.ToArray();
                Hidden = false;
            }

            public void Hide()
            {
                Junction = null;
                Hidden = true;
            }
        }

        private sealed class FakeTooltip : ITooltipView
        {
            public IReadOnlyList<(LocalizedText text, TooltipTone tone)> Lines { get; private set; } =
                Array.Empty<(LocalizedText, TooltipTone)>();

            public void Show(float2 world, IReadOnlyList<(LocalizedText text, TooltipTone tone)> lines) => Lines = lines;
            public void Hide() => Lines = Array.Empty<(LocalizedText, TooltipTone)>();
        }
    }
}
