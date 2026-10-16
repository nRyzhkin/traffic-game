using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrafficGame.Core.Commands;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Presentation.Toolbar;
using TrafficGame.Presentation.Tools;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    public sealed class ToolbarPresenterTests
    {
        private FakeToolbar _toolbar;
        private RoadProfileSelection _profiles;
        private CommandHistory _history;
        private ToolController _tools;

        [SetUp]
        public void SetUp()
        {
            _toolbar = new FakeToolbar();
            _profiles = new RoadProfileSelection(new[] { TestRoads.TwoLane, TestRoads.FourLane });
            _history = new CommandHistory(10);
            var tools = new Dictionary<ToolKind, IMapTool>
            {
                [ToolKind.Road] = new IdleTool(),
                [ToolKind.Bulldoze] = new IdleTool()
            };
            _tools = new ToolController(new NoPointer(), new IToolInput[] { _toolbar }, new IdentityCamera(), _history, _profiles, tools);
        }

        [Test]
        public void ReflectsActiveToolAndHidesActionsWithoutOne()
        {
            using var presenter = new ToolbarPresenter(_toolbar, _tools, _history);
            Assert.That(_toolbar.ActionsVisible, Is.False);

            _toolbar.Request(ToolKind.Bulldoze);

            Assert.That(_toolbar.ActiveTool, Is.EqualTo(ToolKind.Bulldoze));
            Assert.That(_toolbar.ActionsVisible, Is.True);
            Assert.That(_toolbar.CanUndo, Is.False);
        }

        [Test]
        public void ProfileBar_IsShownForRoadToolsAndAppliesTheChoice()
        {
            var bar = new FakeProfileBar();
            using var presenter = new RoadProfileBarPresenter(bar, _tools, _profiles);
            Assert.That(bar.Visible, Is.False);

            _toolbar.Request(ToolKind.Road);
            Assert.That(bar.Visible, Is.True);
            Assert.That(bar.Options.Count, Is.EqualTo(2));
            Assert.That(bar.Selected, Is.EqualTo(TestRoads.TwoLane.Id));

            bar.Choose(TestRoads.FourLane.Id);
            Assert.That(_profiles.Current, Is.SameAs(TestRoads.FourLane));
            Assert.That(bar.Selected, Is.EqualTo(TestRoads.FourLane.Id));

            _toolbar.Request(ToolKind.Bulldoze);
            Assert.That(bar.Visible, Is.False);
        }

        private sealed class FakeToolbar : IToolbarView
        {
            public event Action<ToolKind> ToolRequested;
            public event Action ConfirmRequested { add { } remove { } }
            public event Action UndoRequested { add { } remove { } }
            public event Action RedoRequested { add { } remove { } }
            public event Action CancelRequested { add { } remove { } }
            public event Action NextProfileRequested { add { } remove { } }

            public ToolKind ActiveTool { get; private set; }
            public bool ActionsVisible { get; private set; }
            public bool CanUndo { get; private set; }

            public void Request(ToolKind tool) => ToolRequested?.Invoke(tool);

            public void SetActiveTool(ToolKind tool) => ActiveTool = tool;
            public void SetHistory(bool canUndo, bool canRedo) => CanUndo = canUndo;
            public void SetActions(bool toolActive, bool canConfirm) => ActionsVisible = toolActive;
        }

        private sealed class FakeProfileBar : IRoadProfileBarView
        {
            public event Action<string> ProfileChosen;

            public bool Visible { get; private set; }
            public IReadOnlyList<RoadProfileOption> Options { get; private set; }
            public string Selected { get; private set; }

            public void Show(IReadOnlyList<RoadProfileOption> options, string selectedId)
            {
                Options = options;
                Selected = selectedId;
                Visible = true;
            }

            public void Hide() => Visible = false;
            public void Choose(string id) => ProfileChosen?.Invoke(id);
        }

        private sealed class IdleTool : IMapTool
        {
            public bool CanConfirm => false;
            public bool ActsOnTap => false;
            public void Activate() { }
            public void Deactivate() { }
            public void PointerMoved(float2 world) { }
            public void PrimaryClick(float2 world) { }
            public bool Cancel() => false;
        }

        private sealed class NoPointer : IMapPointerInput
        {
            public event Action<float2> PointerMoved { add { } remove { } }
            public event Action<float2> Tapped { add { } remove { } }
            public event Action<float2> PrimaryClicked { add { } remove { } }
            public event Action SecondaryClicked { add { } remove { } }
        }

        private sealed class IdentityCamera : IScreenToWorld
        {
            public float2 ScreenToWorld(float2 screenPoint) => screenPoint;
        }
    }
}
