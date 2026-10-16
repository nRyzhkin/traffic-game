using System;
using TrafficGame.Presentation.Hud;
using TrafficGame.Presentation.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>
    /// Menu button and money at the top, a hint for the active tool above the toolbar. The money counts to a new
    /// amount instead of jumping; hints fade in, out and into each other.
    /// </summary>
    public sealed class HudView : IHudView, IDisposable
    {
        private const float CountSeconds = 0.45f;

        private readonly ILocalizer _localizer;
        private readonly Label _balance;
        private readonly Label _hint;
        private LocalizedText _balanceText;
        private LocalizedText _hintText;
        private double _countFrom;
        private double _countTo;
        private double _counted;
        private float _countStarted;
        private IVisualElementScheduledItem _counting;

        public HudView(VisualElement root, ILocalizer localizer)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _balance = root.Q<Label>("balance");
            _hint = root.Q<Label>("hint");
            root.Q("balance-chip").Insert(0, new IconElement(IconKind.Wallet));
            var menu = root.Q<Button>("menu-button");
            menu.Add(new IconElement(IconKind.Menu));
            menu.clicked += () => MenuRequested?.Invoke();
            Motion.Hide(_hint, instantly: true);
            _localizer.LanguageChanged += Render;
        }

        /// <summary>The player wants to leave the level for the menu.</summary>
        public event Action MenuRequested;

        public void SetBalance(LocalizedText balance)
        {
            var counts = TryGetAmount(balance, out var amount) && TryGetAmount(_balanceText, out _) && balance.Key == _balanceText.Key
                && _balance.panel != null;
            _balanceText = balance;
            if (!counts)
            {
                _counting?.Pause();
                _counted = _countTo = amount;
                RenderBalance();
                return;
            }

            _countFrom = _counted;
            _countTo = amount;
            _countStarted = Time.realtimeSinceStartup;
            if (_counting == null)
                _counting = _balance.schedule.Execute(CountStep).Every(0);
            else
                _counting.Resume();
        }

        public void SetHint(LocalizedText hint)
        {
            _hintText = hint;
            Render();
        }

        public void Dispose() => _localizer.LanguageChanged -= Render;

        private void Render()
        {
            RenderBalance();
            if (_hintText.IsEmpty)
            {
                // Keep the old text while it fades out.
                Motion.Hide(_hint);
                return;
            }

            var text = _localizer.Get(_hintText);
            if (Motion.IsShown(_hint))
            {
                Motion.SetText(_hint, text);
                return;
            }

            _hint.text = text;
            Motion.Show(_hint);
        }

        private void CountStep()
        {
            var t = Mathf.Clamp01((Time.realtimeSinceStartup - _countStarted) / CountSeconds);
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            _counted = _countFrom + (_countTo - _countFrom) * eased;
            if (t >= 1f)
            {
                _counted = _countTo;
                _counting.Pause();
            }

            RenderBalance();
        }

        private void RenderBalance()
        {
            if (_balanceText.IsEmpty)
                return;

            _balance.text = TryGetAmount(_balanceText, out _)
                ? _localizer.Get(new LocalizedText(_balanceText.Key, (long)Math.Round(_counted)))
                : _localizer.Get(_balanceText);
        }

        /// <summary>Texts with a single number (the balance) can be counted.</summary>
        private static bool TryGetAmount(LocalizedText text, out double amount)
        {
            amount = 0d;
            if (text.IsEmpty || text.Args.Length != 1)
                return false;

            switch (text.Args[0])
            {
                case long value:
                    amount = value;
                    return true;
                case int value:
                    amount = value;
                    return true;
                default:
                    return false;
            }
        }
    }
}
