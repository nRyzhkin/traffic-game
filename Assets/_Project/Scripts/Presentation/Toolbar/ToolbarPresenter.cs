using System;
using TrafficGame.Core.Commands;
using TrafficGame.Presentation.Tools;

namespace TrafficGame.Presentation.Toolbar
{
    /// <summary>
    /// On-screen controls: tools, undo/redo, back and ✓. Its requests go straight to the
    /// <see cref="ToolController"/> as an <see cref="IToolInput"/>; this presenter keeps its state in sync.
    /// </summary>
    public interface IToolbarView : IToolInput
    {
        void SetActiveTool(ToolKind tool);
        void SetHistory(bool canUndo, bool canRedo);

        /// <param name="toolActive">Shows back and ✓ only while a tool is in use.</param>
        void SetActions(bool toolActive, bool canConfirm);
    }

    public sealed class ToolbarPresenter : IDisposable
    {
        private readonly IToolbarView _view;
        private readonly ToolController _tools;
        private readonly CommandHistory _history;

        public ToolbarPresenter(IToolbarView view, ToolController tools, CommandHistory history)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _history = history ?? throw new ArgumentNullException(nameof(history));

            _tools.Changed += ShowTools;
            _history.Changed += ShowHistory;
            ShowTools();
            ShowHistory();
        }

        public void Dispose()
        {
            _tools.Changed -= ShowTools;
            _history.Changed -= ShowHistory;
        }

        private void ShowTools()
        {
            _view.SetActiveTool(_tools.Active);
            _view.SetActions(_tools.Active != ToolKind.None, _tools.CanConfirm);
        }

        private void ShowHistory() => _view.SetHistory(_history.CanUndo, _history.CanRedo);
    }
}
