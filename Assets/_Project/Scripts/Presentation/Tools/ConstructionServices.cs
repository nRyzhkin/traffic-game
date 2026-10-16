using System;
using TrafficGame.Core.Commands;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Roads;

namespace TrafficGame.Presentation.Tools
{
    /// <summary>Model services shared by the construction tools.</summary>
    public sealed class ConstructionServices
    {
        public ConstructionServices(RoadNetwork network, IRoadLayoutSource layout, Budget budget, CommandHistory history,
            RoadSnapper snapper, RoadGuides guides, RoadPlanner planner, RoadProfileSelection profiles, ElevationSelection elevation = null)
        {
            Elevation = elevation ?? new ElevationSelection();
            Network = network ?? throw new ArgumentNullException(nameof(network));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            History = history ?? throw new ArgumentNullException(nameof(history));
            Snapper = snapper ?? throw new ArgumentNullException(nameof(snapper));
            Guides = guides ?? throw new ArgumentNullException(nameof(guides));
            Planner = planner ?? throw new ArgumentNullException(nameof(planner));
            Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        }

        public RoadNetwork Network { get; }
        public IRoadLayoutSource Layout { get; }
        public Budget Budget { get; }
        public CommandHistory History { get; }
        public RoadSnapper Snapper { get; }
        public RoadGuides Guides { get; }
        public RoadPlanner Planner { get; }
        public RoadProfileSelection Profiles { get; }

        /// <summary>The level new road ends are built on.</summary>
        public ElevationSelection Elevation { get; }
    }
}
