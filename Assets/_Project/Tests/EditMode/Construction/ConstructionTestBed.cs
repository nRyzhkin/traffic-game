using TrafficGame.Core.Construction;
using TrafficGame.Core.Economy;
using TrafficGame.Core.Roads;
using TrafficGame.Tests.Roads;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    /// <summary>A network with planning services wired the way the game wires them.</summary>
    internal sealed class ConstructionTestBed
    {
        public static readonly RoadPlanningRules Rules = new(
            minLength: 8f, minCurveRadius: 10f, minCrossingAngle: 30f, minConnectionAngle: 25f,
            minJunctionSpacing: 12f, sampleSpacing: 2f, boundsMin: new float2(-1000f), boundsMax: new float2(1000f));

        public ConstructionTestBed(RoadNetwork network = null, long balance = 1_000_000)
        {
            Network = network ?? new RoadNetwork();
            Budget = new Budget(balance);
            Planner = new RoadPlanner(Network, Rules);
            Snapper = new RoadSnapper(snapMargin: 3f, endExclusion: Rules.MinJunctionSpacing);
        }

        public RoadNetwork Network { get; }
        public Budget Budget { get; }
        public RoadPlanner Planner { get; }
        public RoadSnapper Snapper { get; }

        public RoadLayout Layout => RoadLayout.Build(Network, TestRoads.Settings);

        /// <summary>A network with one east–west two-lane road from (-100, 0) to (100, 0).</summary>
        public static ConstructionTestBed WithEastWestRoad(long balance = 1_000_000)
        {
            var network = new RoadNetwork();
            network.AddStraightSegment(network.AddNode(new float2(-100f, 0f)), network.AddNode(new float2(100f, 0f)), TestRoads.TwoLane);
            return new ConstructionTestBed(network, balance);
        }

        public RoadPlan Plan(float2 from, float2 to, float2? control = null) => Plan(from, 0, to, 0, control);

        /// <summary>Plans a road whose ends are built on the given elevation levels.</summary>
        public RoadPlan Plan(float2 from, int fromLevel, float2 to, int toLevel, float2? control = null)
        {
            var layout = Layout;
            var request = new RoadBuildRequest(Snapper.Snap(from, Network, layout, RoadElevation.HeightOf(fromLevel)),
                Snapper.Snap(to, Network, layout, RoadElevation.HeightOf(toLevel)), control, TestRoads.TwoLane);
            return Planner.Plan(request, layout, Budget);
        }

        public void Build(RoadPlan plan) => new BuildRoadCommand(Network, Budget, plan).Execute();
    }
}
