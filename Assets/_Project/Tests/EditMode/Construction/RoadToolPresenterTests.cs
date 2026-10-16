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
    public sealed class RoadToolPresenterTests
    {
        private ConstructionTestBed _bed;
        private CommandHistory _history;
        private FakePreview _preview;
        private FakeTooltip _tooltip;
        private FakeInput _input;
        private ToolController _controller;
        private ElevationSelection _elevation;

        [SetUp]
        public void SetUp()
        {
            _bed = ConstructionTestBed.WithEastWestRoad();
            _history = new CommandHistory(capacity: 10);
            _preview = new FakePreview();
            _tooltip = new FakeTooltip();
            _input = new FakeInput();

            var layoutSource = new LiveLayout(_bed.Network);
            var profiles = new RoadProfileSelection(new[] { TestRoads.TwoLane, TestRoads.FourLane });
            var services = new ConstructionServices(_bed.Network, layoutSource, _bed.Budget, _history, _bed.Snapper,
                new RoadGuides(4f, 60f), _bed.Planner, profiles, _elevation = new ElevationSelection());
            var tools = new Dictionary<ToolKind, IMapTool>
            {
                [ToolKind.Road] = new RoadToolPresenter(false, services, _preview, _tooltip),
                [ToolKind.CurvedRoad] = new RoadToolPresenter(true, services, _preview, _tooltip),
                [ToolKind.Bulldoze] = new BulldozeToolPresenter(services, 0.5f, _preview, _tooltip)
            };
            _controller = new ToolController(_input, new IToolInput[] { _input }, new IdentityCamera(), _history, profiles, tools);
        }

        [TearDown]
        public void TearDown() => _controller.Dispose();

        [Test]
        public void Confirm_ActsAtTheLastPointerLikeAClick()
        {
            _input.RequestTool(ToolKind.Road);
            Assert.That(_controller.CanConfirm, Is.False, "Nothing to confirm before the pointer is known (touch).");

            _input.Move(new float2(0f, 50f));
            _input.Confirm();
            _input.Move(new float2(0f, 150f));
            Assert.That(_controller.CanConfirm, Is.True);
            _input.Confirm();

            Assert.That(_bed.Network.Segments.Count, Is.EqualTo(2));
        }

        [Test]
        public void RaisedLevel_BuildsABridgeOverTheRoadAndShowsTheHeight()
        {
            _input.RequestTool(ToolKind.Road);
            _elevation.Raise();

            _input.Click(new float2(0f, -60f));
            _input.Move(new float2(0f, 60f));
            Assert.That(_tooltip.Lines.Select(line => line.text.Key), Does.Contain(TextKeys.Elevated));
            _input.Click(new float2(0f, 60f));

            Assert.That(_bed.Network.Segments.Count, Is.EqualTo(2), "The bridge does not split the road below.");
            Assert.That(_bed.Network.Segments.Count(segment => segment.StartHeight == RoadElevation.HeightOf(1)), Is.EqualTo(1));
        }

        [Test]
        public void ChangingTheLevel_MovesTheEndBeingAimed()
        {
            _input.RequestTool(ToolKind.Road);
            _input.Click(new float2(0f, 50f));
            _input.Move(new float2(0f, 150f));

            _elevation.Raise();

            Assert.That(_preview.LastPlan.EndHeight, Is.EqualTo(RoadElevation.HeightOf(1)));
            Assert.That(_preview.LastPlan.StartHeight, Is.Zero, "The start stays where it was placed.");
        }

        [Test]
        public void TwoClicks_BuildARoadAndChainFromItsEnd()
        {
            _input.RequestTool(ToolKind.Road);

            _input.Click(new float2(0f, 50f));
            _input.Click(new float2(0f, 150f));

            Assert.That(_bed.Network.Segments.Count, Is.EqualTo(2));
            Assert.That(_history.CanUndo, Is.True);

            _input.Move(new float2(100f, 150f));
            Assert.That(_preview.LastPlan.Request.Start.Kind, Is.EqualTo(AnchorKind.Node), "The next road starts at the end of the last one.");
            Assert.That(_tooltip.Lines.Select(line => line.text.Key), Does.Contain(TextKeys.Cost));
        }

        [Test]
        public void InvalidPlan_IsNotBuiltAndExplainsWhy()
        {
            _input.RequestTool(ToolKind.Road);

            _input.Click(new float2(0f, 50f));
            _input.Click(new float2(3f, 50f));

            Assert.That(_bed.Network.Segments.Count, Is.EqualTo(1));
            Assert.That(_tooltip.Lines.Any(line => line.text.Key == TextKeys.ErrorTooShort && line.tone == TooltipTone.Error));
        }

        [Test]
        public void CurvedRoad_TakesStartControlAndEnd()
        {
            _input.RequestTool(ToolKind.CurvedRoad);

            _input.Click(new float2(0f, 50f));
            _input.Click(new float2(80f, 50f));
            Assert.That(_bed.Network.Segments.Count, Is.EqualTo(1), "The second click only places the bend.");

            _input.Click(new float2(80f, 130f));

            Assert.That(_bed.Network.Segments.Count, Is.EqualTo(2));
        }

        [Test]
        public void Cancel_StepsBackThenLeavesTheTool()
        {
            _input.RequestTool(ToolKind.Road);
            _input.Click(new float2(0f, 50f));

            _input.Cancel();
            Assert.That(_controller.Active, Is.EqualTo(ToolKind.Road));

            _input.Cancel();
            Assert.That(_controller.Active, Is.EqualTo(ToolKind.None));
            Assert.That(_preview.Hidden, Is.True);
        }

        [Test]
        public void Undo_DropsAChainStartThatNoLongerExists()
        {
            _input.RequestTool(ToolKind.Road);
            _input.Click(new float2(0f, 50f));
            _input.Click(new float2(0f, 150f));

            _input.Undo();
            _input.Move(new float2(50f, 150f));

            Assert.That(_bed.Network.Segments.Count, Is.EqualTo(1));
            Assert.That(_preview.LastAnchor.HasValue, Is.True, "Back to choosing a start point.");
        }

        [Test]
        public void Bulldozer_RemovesRoadUnderPointerAndRefundsHalf()
        {
            var balance = _bed.Budget.Balance;
            _input.RequestTool(ToolKind.Bulldoze);

            _input.Click(new float2(0f, 1f));

            Assert.That(_bed.Network.Segments, Is.Empty);
            Assert.That(_bed.Budget.Balance, Is.EqualTo(balance + TestRoads.TwoLane.CostOf(200f) / 2));
        }

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

        private sealed class FakeInput : IMapPointerInput, IToolInput
        {
            public event Action<float2> PointerMoved;
            public event Action<float2> Tapped { add { } remove { } }
            public event Action<float2> PrimaryClicked;
            public event Action SecondaryClicked;
            public event Action<ToolKind> ToolRequested;
            public event Action UndoRequested;
            public event Action ConfirmRequested;
            public event Action RedoRequested { add { } remove { } }
            public event Action CancelRequested { add { } remove { } }
            public event Action NextProfileRequested { add { } remove { } }

            public void Move(float2 point) => PointerMoved?.Invoke(point);
            public void Click(float2 point) => PrimaryClicked?.Invoke(point);
            public void Cancel() => SecondaryClicked?.Invoke();
            public void RequestTool(ToolKind tool) => ToolRequested?.Invoke(tool);
            public void Undo() => UndoRequested?.Invoke();
            public void Confirm() => ConfirmRequested?.Invoke();
        }

        private sealed class FakePreview : IRoadPreviewView
        {
            public RoadPlan LastPlan { get; private set; }
            public RoadAnchor? LastAnchor { get; private set; }
            public bool Hidden { get; private set; }

            public void ShowAnchor(RoadAnchor anchor)
            {
                LastAnchor = anchor;
                Hidden = false;
            }

            public void ShowPlan(RoadPlan plan, RoadGuide guide, float2? control)
            {
                LastPlan = plan;
                LastAnchor = null;
                Hidden = false;
            }

            public void ShowRoundabout(RoundaboutPlan plan) => Hidden = false;
            public void ShowRemoval(SegmentGeometry segment) => Hidden = false;
            public void Hide() => Hidden = true;
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
