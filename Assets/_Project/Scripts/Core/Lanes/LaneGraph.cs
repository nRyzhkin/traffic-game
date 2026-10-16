using System.Collections.Generic;

namespace TrafficGame.Core.Lanes
{
    /// <summary>Directed graph of lanes linked by connections through junctions; what vehicles drive on.</summary>
    public sealed class LaneGraph
    {
        private readonly Lane[] _lanes;
        private readonly LaneConnection[] _connections;
        private readonly List<LaneConnection>[] _outgoing;
        private readonly List<LaneConnection>[] _incoming;

        internal LaneGraph(IReadOnlyList<Lane> lanes, IReadOnlyList<LaneConnection> connections)
        {
            _lanes = new Lane[lanes.Count];
            _outgoing = new List<LaneConnection>[lanes.Count];
            _incoming = new List<LaneConnection>[lanes.Count];
            for (var i = 0; i < lanes.Count; i++)
            {
                _lanes[i] = lanes[i];
                _outgoing[i] = new List<LaneConnection>();
                _incoming[i] = new List<LaneConnection>();
            }

            _connections = new LaneConnection[connections.Count];
            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                _connections[i] = connection;
                _outgoing[connection.From.Id].Add(connection);
                _incoming[connection.To.Id].Add(connection);
            }
        }

        public IReadOnlyList<Lane> Lanes => _lanes;
        public IReadOnlyList<LaneConnection> Connections => _connections;

        public IReadOnlyList<LaneConnection> OutgoingFrom(Lane lane) => _outgoing[lane.Id];
        public IReadOnlyList<LaneConnection> IncomingTo(Lane lane) => _incoming[lane.Id];
    }
}
