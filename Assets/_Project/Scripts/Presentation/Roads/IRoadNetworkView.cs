using TrafficGame.Core.Lanes;
using TrafficGame.Core.Roads;

namespace TrafficGame.Presentation.Roads
{
    /// <summary>Draws road surfaces, curbs and markings.</summary>
    public interface IRoadNetworkView
    {
        void Render(RoadLayout layout);
    }

    /// <summary>Debug overlay of lanes and junction connections.</summary>
    public interface ILaneGraphView
    {
        void Render(LaneGraph graph);
    }
}
