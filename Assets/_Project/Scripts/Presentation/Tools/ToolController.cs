using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Core.Commands;
using TrafficGame.Presentation.Cameras;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>
    /// Owns the active map tool: routes pointer input to it, switches tools and road profiles on request,
    /// confirms the tool's pending action (the ✓ button) and runs undo/redo.
    /// </summary>
    public sealed class ToolController : IDisposable
    {
        private readonly IMapPointerInput _pointer;
        private readonly IReadOnlyList<IToolInput> _inputs;
        private readonly IScreenToWorld _camera;
        private readonly CommandHistory _history;
        private readonly RoadProfileSelection _profiles;
        private readonly IReadOnlyDictionary<ToolKind, IMapTool> _tools;
        private float2 _lastPointer;
        private bool _hasPointer;

        public ToolController(IMapPointerInput pointer, IEnumerable<IToolInput> inputs, IScreenToWorld camera,
            CommandHistory history, RoadProfileSelection profiles, IReadOnlyDictionary<ToolKind, IMapTool> tools)
        {
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            _inputs = inputs?.ToArray() ?? throw new ArgumentNullException(nameof(inputs));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));

            _pointer.PointerMoved += OnPointerMoved;
            _pointer.Tapped += OnTapped;
            _pointer.PrimaryClicked += OnPrimaryClicked;
            _pointer.SecondaryClicked += Cancel;
            foreach (var input in _inputs)
            {
                input.ToolRequested += Select;
                input.ConfirmRequested += Confirm;
                input.UndoRequested += Undo;
                input.RedoRequested += Redo;
                input.CancelRequested += Cancel;
                input.NextProfileRequested += NextProfile;
            }

            _profiles.Changed += OnProfileChanged;
        }

        /// <summary>Raised whenever the active tool or its pending action may have changed.</summary>
        public event Action Changed;

        public ToolKind Active { get; private set; }

        public bool CanConfirm => ActiveTool?.CanConfirm ?? false;

        private IMapTool ActiveTool => _tools.TryGetValue(Active, out var tool) ? tool : null;

        public void Select(ToolKind kind)
        {
            if (kind != Active)
            {
                ActiveTool?.Deactivate();
                Active = kind;
                ActiveTool?.Activate();
                RefreshActiveTool();
            }

            Changed?.Invoke();
        }

        /// <summary>Performs the active tool's action at the last pointer position, as a click there would.</summary>
        public void Confirm()
        {
            if (!_hasPointer || !CanConfirm)
                return;

            ActiveTool.PrimaryClick(_lastPointer);
            Changed?.Invoke();
        }

        /// <summary>Steps the active tool back; leaves the tool when it has nothing to step back from.</summary>
        public void Cancel()
        {
            var tool = ActiveTool;
            if (tool != null && !tool.Cancel())
                Select(ToolKind.None);
            Changed?.Invoke();
        }

        public void Undo() => Replay(_history.Undo);

        public void Redo() => Replay(_history.Redo);

        public void NextProfile() => _profiles.SelectNext();

        public void Dispose()
        {
            _pointer.PointerMoved -= OnPointerMoved;
            _pointer.Tapped -= OnTapped;
            _pointer.PrimaryClicked -= OnPrimaryClicked;
            _pointer.SecondaryClicked -= Cancel;
            foreach (var input in _inputs)
            {
                input.ToolRequested -= Select;
                input.ConfirmRequested -= Confirm;
                input.UndoRequested -= Undo;
                input.RedoRequested -= Redo;
                input.CancelRequested -= Cancel;
                input.NextProfileRequested -= NextProfile;
            }

            _profiles.Changed -= OnProfileChanged;
        }

        private void OnPointerMoved(float2 screen)
        {
            _lastPointer = _camera.ScreenToWorld(screen);
            _hasPointer = true;
            ActiveTool?.PointerMoved(_lastPointer);
            Changed?.Invoke();
        }

        private void OnTapped(float2 screen)
        {
            if (ActiveTool is { ActsOnTap: true })
                OnPrimaryClicked(screen);
        }

        private void OnPrimaryClicked(float2 screen)
        {
            _lastPointer = _camera.ScreenToWorld(screen);
            _hasPointer = true;
            ActiveTool?.PrimaryClick(_lastPointer);
            Changed?.Invoke();
        }

        private void OnProfileChanged()
        {
            RefreshActiveTool();
            Changed?.Invoke();
        }

        /// <summary>Runs a history step and lets the active tool refresh its preview against the new state.</summary>
        private void Replay(Func<bool> step)
        {
            if (!step())
                return;

            RefreshActiveTool();
            Changed?.Invoke();
        }

        private void RefreshActiveTool()
        {
            if (_hasPointer)
                ActiveTool?.PointerMoved(_lastPointer);
        }
    }
}
