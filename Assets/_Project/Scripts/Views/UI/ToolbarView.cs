using System;
using System.Collections.Generic;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Toolbar;
using TrafficGame.Presentation.Tools;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>
    /// The tool bar (a pill indicator marks the active tool), undo/redo and the contextual back and ✓ buttons above it.
    /// On phones held upright undo/redo move into the row with back and ✓.
    /// </summary>
    public sealed class ToolbarView : IToolbarView, IDisposable
    {
        private const string SelectedClass = "is-selected";
        private const string IdleClass = "actions--idle";

        private readonly ILocalizer _localizer;
        private readonly Dictionary<ToolKind, (Button button, Label label, string key)> _tools = new();
        private readonly List<Button> _undo = new();
        private readonly List<Button> _redo = new();
        private readonly VisualElement _actions;
        private readonly Button _back;
        private readonly Button _confirm;
        private readonly Label _confirmLabel;
        private ToolKind _active;

        public ToolbarView(VisualElement root, ILocalizer localizer)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

            BindTool(root, "tool-road", ToolKind.Road, IconKind.Road, TextKeys.ButtonRoad);
            BindTool(root, "tool-curve", ToolKind.CurvedRoad, IconKind.CurvedRoad, TextKeys.ButtonCurvedRoad);
            BindTool(root, "tool-roundabout", ToolKind.Roundabout, IconKind.Roundabout, TextKeys.ButtonRoundabout);
            BindTool(root, "tool-lights", ToolKind.TrafficLights, IconKind.TrafficLight, TextKeys.ButtonTrafficLights);
            BindTool(root, "tool-lanes", ToolKind.LaneArrows, IconKind.LaneArrows, TextKeys.ButtonLaneArrows);
            BindTool(root, "tool-bulldoze", ToolKind.Bulldoze, IconKind.Bulldoze, TextKeys.ButtonBulldoze);

            BindHistory(root.Q<Button>("undo"), IconKind.Undo, _undo, () => UndoRequested?.Invoke());
            BindHistory(root.Q<Button>("undo-floating"), IconKind.Undo, _undo, () => UndoRequested?.Invoke());
            BindHistory(root.Q<Button>("redo"), IconKind.Redo, _redo, () => RedoRequested?.Invoke());
            BindHistory(root.Q<Button>("redo-floating"), IconKind.Redo, _redo, () => RedoRequested?.Invoke());

            _actions = root.Q("actions");
            _back = root.Q<Button>("back");
            _back.Add(new IconElement(IconKind.Back));
            _back.clicked += () => CancelRequested?.Invoke();

            _confirm = root.Q<Button>("confirm");
            _confirmLabel = _confirm.Q<Label>("label");
            _confirm.Insert(0, new IconElement(IconKind.Check));
            _confirm.clicked += () => ConfirmRequested?.Invoke();

            _localizer.LanguageChanged += RenderTexts;
            RenderTexts();
            SetActiveTool(ToolKind.None);
        }

        public event Action<ToolKind> ToolRequested;
        public event Action ConfirmRequested;
        public event Action UndoRequested;
        public event Action RedoRequested;
        public event Action CancelRequested;
        // Road types are chosen in the profile bar, not cycled from the toolbar.
        public event Action NextProfileRequested { add { } remove { } }

        public void SetActiveTool(ToolKind tool)
        {
            _active = tool;
            foreach (var (kind, (button, _, _)) in _tools)
                button.EnableInClassList(SelectedClass, kind == tool);
        }

        public void SetHistory(bool canUndo, bool canRedo)
        {
            foreach (var button in _undo)
                button.SetEnabled(canUndo);
            foreach (var button in _redo)
                button.SetEnabled(canRedo);
        }

        /// <summary>Back and ✓ fade and shrink away without a tool (the row folds up in landscape); hidden, they take no taps.</summary>
        public void SetActions(bool toolActive, bool canConfirm)
        {
            _actions.EnableInClassList(IdleClass, !toolActive);
            _back.pickingMode = toolActive ? PickingMode.Position : PickingMode.Ignore;
            _confirm.pickingMode = toolActive ? PickingMode.Position : PickingMode.Ignore;
            _confirm.SetEnabled(canConfirm);
        }

        public void Dispose() => _localizer.LanguageChanged -= RenderTexts;

        private void BindTool(VisualElement root, string name, ToolKind kind, IconKind iconKind, string key)
        {
            var button = root.Q<Button>(name);
            var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
            indicator.AddToClassList("tool__indicator");
            indicator.Add(new IconElement(iconKind));
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("tool__label");
            button.Add(indicator);
            button.Add(label);

            // Pressing the active tool again puts it away.
            button.clicked += () => ToolRequested?.Invoke(_active == kind ? ToolKind.None : kind);
            _tools.Add(kind, (button, label, key));
        }

        private static void BindHistory(Button button, IconKind icon, List<Button> group, Action action)
        {
            button.Add(new IconElement(icon));
            button.clicked += action;
            group.Add(button);
        }

        private void RenderTexts()
        {
            foreach (var (_, (_, label, key)) in _tools)
                label.text = _localizer.Get(new LocalizedText(key));
            _confirmLabel.text = _localizer.Get(new LocalizedText(TextKeys.ButtonConfirm));
        }
    }
}
