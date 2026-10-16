using System.Collections.Generic;
using TrafficGame.Core.Roads;
using Unity.Mathematics;

namespace TrafficGame.Core.Simulation
{
    /// <summary>
    /// A* over directed roads. The cost of a road is its expected travel time at the currently observed speed
    /// plus the delay of the junction control at its end, so drivers avoid congestion and stop signs.
    /// Buffers are reused between queries; no allocations per search.
    /// </summary>
    internal sealed class Router
    {
        private readonly TrafficNetwork _network;
        private readonly float[] _cost;
        private readonly RoadInfo[] _cameFrom;
        private readonly int[] _visitedStamp;
        private readonly int[] _closedStamp;
        private readonly MinHeap _open;
        private readonly float _maxSpeed;
        private int _stamp;

        public Router(TrafficNetwork network)
        {
            _network = network;
            var count = network.Roads.Count;
            _cost = new float[count];
            _cameFrom = new RoadInfo[count];
            _visitedStamp = new int[count];
            _closedStamp = new int[count];
            _open = new MinHeap(count);

            _maxSpeed = 1f;
            foreach (var road in network.Roads)
                _maxSpeed = math.max(_maxSpeed, road.SpeedLimit);
        }

        /// <summary>Fills <paramref name="route"/> with roads from <paramref name="start"/> to one that ends at <paramref name="destination"/>.</summary>
        public bool TryFindRoute(RoadInfo start, NodeId destination, List<RoadInfo> route)
        {
            route.Clear();
            if (!_network.NodePositions.TryGetValue(destination, out var target))
                return false;

            _stamp++;
            _open.Clear();
            Visit(start, null, TravelCost(start), target);

            while (_open.TryPop(out var index))
            {
                if (_closedStamp[index] == _stamp)
                    continue;
                _closedStamp[index] = _stamp;

                var road = _network.Roads[index];
                if (road.To == destination)
                {
                    for (var step = road; step != null; step = _cameFrom[step.Index])
                        route.Add(step);
                    route.Reverse();
                    return true;
                }

                foreach (var next in road.ConnectionsTo.Keys)
                {
                    if (_closedStamp[next.Index] == _stamp)
                        continue;

                    var cost = _cost[index] + TravelCost(next);
                    if (_visitedStamp[next.Index] != _stamp || cost < _cost[next.Index])
                        Visit(next, road, cost, target);
                }
            }

            return false;
        }

        private void Visit(RoadInfo road, RoadInfo from, float cost, float2 target)
        {
            _visitedStamp[road.Index] = _stamp;
            _cost[road.Index] = cost;
            _cameFrom[road.Index] = from;
            _open.Push(road.Index, cost + math.distance(road.EndPoint, target) / _maxSpeed);
        }

        private static float TravelCost(RoadInfo road) =>
            road.Length / math.max(1f, road.ObservedSpeed) + road.JunctionPenalty;

        /// <summary>Binary min-heap of (index, priority) with a fixed capacity.</summary>
        private sealed class MinHeap
        {
            private readonly List<(int index, float priority)> _items;

            public MinHeap(int capacity) => _items = new List<(int, float)>(capacity);

            public void Clear() => _items.Clear();

            public void Push(int index, float priority)
            {
                _items.Add((index, priority));
                var child = _items.Count - 1;
                while (child > 0)
                {
                    var parent = (child - 1) / 2;
                    if (_items[parent].priority <= _items[child].priority)
                        break;
                    (_items[parent], _items[child]) = (_items[child], _items[parent]);
                    child = parent;
                }
            }

            public bool TryPop(out int index)
            {
                if (_items.Count == 0)
                {
                    index = -1;
                    return false;
                }

                index = _items[0].index;
                var last = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                if (_items.Count == 0)
                    return true;

                _items[0] = last;
                var parent = 0;
                while (true)
                {
                    var left = parent * 2 + 1;
                    var right = left + 1;
                    var smallest = parent;
                    if (left < _items.Count && _items[left].priority < _items[smallest].priority)
                        smallest = left;
                    if (right < _items.Count && _items[right].priority < _items[smallest].priority)
                        smallest = right;
                    if (smallest == parent)
                        break;
                    (_items[parent], _items[smallest]) = (_items[smallest], _items[parent]);
                    parent = smallest;
                }

                return true;
            }
        }
    }
}
