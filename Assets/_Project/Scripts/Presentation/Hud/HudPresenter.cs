using System;
using TrafficGame.Core.Economy;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Tools;

namespace TrafficGame.Presentation.Hud
{
    /// <summary>Always-visible status: money and a hint for the active tool.</summary>
    public interface IHudView
    {
        void SetBalance(LocalizedText balance);
        void SetHint(LocalizedText hint);
    }

    public sealed class HudPresenter : IDisposable
    {
        private readonly IHudView _view;
        private readonly Budget _budget;
        private readonly ToolController _tools;

        public HudPresenter(IHudView view, Budget budget, ToolController tools)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));

            _budget.Changed += ShowBalance;
            _tools.Changed += ShowHint;
            ShowBalance();
            ShowHint();
        }

        /// <summary>Clears the money, so the next level's budget appears at once instead of counting from this one.</summary>
        public void Dispose()
        {
            _budget.Changed -= ShowBalance;
            _tools.Changed -= ShowHint;
            _view.SetBalance(LocalizedText.None);
        }

        private void ShowBalance() => _view.SetBalance(new LocalizedText(TextKeys.Balance, _budget.Balance));

        private void ShowHint() => _view.SetHint(new LocalizedText(TextKeys.Hint(_tools.Active)));
    }
}
