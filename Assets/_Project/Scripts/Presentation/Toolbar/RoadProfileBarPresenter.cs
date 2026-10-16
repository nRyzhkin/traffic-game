using System;
using System.Collections.Generic;
using System.Linq;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Tools;

namespace TrafficGame.Presentation.Toolbar
{
    /// <summary>One road type in the profile bar.</summary>
    public readonly struct RoadProfileOption
    {
        public RoadProfileOption(string id, LocalizedText name, LocalizedText details)
        {
            Id = id;
            Name = name;
            Details = details;
        }

        public string Id { get; }
        public LocalizedText Name { get; }
        public LocalizedText Details { get; }
    }

    /// <summary>Row of road types above the toolbar, shown while a road tool is active.</summary>
    public interface IRoadProfileBarView
    {
        event Action<string> ProfileChosen;

        void Show(IReadOnlyList<RoadProfileOption> options, string selectedId);
        void Hide();
    }

    public sealed class RoadProfileBarPresenter : IDisposable
    {
        private readonly IRoadProfileBarView _view;
        private readonly ToolController _tools;
        private readonly RoadProfileSelection _profiles;
        private readonly RoadProfileOption[] _options;

        public RoadProfileBarPresenter(IRoadProfileBarView view, ToolController tools, RoadProfileSelection profiles)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));

            _options = _profiles.All.Select(profile => new RoadProfileOption(
                profile.Id,
                new LocalizedText(TextKeys.Profile(profile.Id)),
                new LocalizedText(profile.IsOneWay ? TextKeys.ProfileDetailsOneWay : TextKeys.ProfileDetails,
                    profile.LaneCount, profile.CostPerMeter))).ToArray();

            _view.ProfileChosen += _profiles.Select;
            _tools.Changed += Refresh;
            _profiles.Changed += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            _view.ProfileChosen -= _profiles.Select;
            _tools.Changed -= Refresh;
            _profiles.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_tools.Active is ToolKind.Road or ToolKind.CurvedRoad)
                _view.Show(_options, _profiles.Current.Id);
            else
                _view.Hide();
        }
    }
}
