using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Commands;
using TrafficGame.Core.Construction;
using Unity.Mathematics;

namespace TrafficGame.Tests.Construction
{
    public sealed class RoadCommandTests
    {
        [Test]
        public void BuildingAcrossARoad_SplitsBothIntoACrossing()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();
            var plan = bed.Plan(new float2(0f, -50f), new float2(0f, 50f));

            new BuildRoadCommand(bed.Network, bed.Budget, plan).Execute();

            Assert.That(bed.Network.Nodes.Count, Is.EqualTo(5));
            Assert.That(bed.Network.Segments.Count, Is.EqualTo(4));
            var crossing = bed.Network.Nodes.Single(node => node.Degree == 4);
            Assert.That(math.distance(crossing.Position, float2.zero), Is.LessThan(0.05f));
            Assert.That(bed.Budget.Balance, Is.EqualTo(1_000_000 - plan.Cost));
        }

        [Test]
        public void BuildingFromTheMiddleOfARoad_CreatesTJunction()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();
            var plan = bed.Plan(new float2(0f, 2f), new float2(0f, 80f));
            Assert.That(plan.Request.Start.Kind, Is.EqualTo(AnchorKind.Segment));

            var command = new BuildRoadCommand(bed.Network, bed.Budget, plan);
            command.Execute();

            Assert.That(bed.Network.Segments.Count, Is.EqualTo(3));
            Assert.That(bed.Network.Nodes.Single(node => node.Degree == 3).Position.x, Is.EqualTo(0f).Within(0.05f));
            Assert.That(bed.Network.GetNode(command.EndNode).Position, Is.EqualTo(new float2(0f, 80f)));
        }

        [Test]
        public void UndoAndRedo_RestoreNetworkBudgetAndIds()
        {
            var bed = ConstructionTestBed.WithEastWestRoad();
            var history = new CommandHistory(capacity: 10);
            history.Execute(new BuildRoadCommand(bed.Network, bed.Budget, bed.Plan(new float2(0f, -50f), new float2(0f, 50f))));
            var builtIds = bed.Network.Segments.Select(segment => segment.Id).OrderBy(id => id.Value).ToArray();
            var builtBalance = bed.Budget.Balance;

            history.Undo();
            Assert.That(bed.Network.Segments.Count, Is.EqualTo(1));
            Assert.That(bed.Budget.Balance, Is.EqualTo(1_000_000));

            history.Redo();
            Assert.That(bed.Network.Segments.Select(segment => segment.Id).OrderBy(id => id.Value), Is.EqualTo(builtIds));
            Assert.That(bed.Budget.Balance, Is.EqualTo(builtBalance));
        }

        [Test]
        public void Bulldozing_RemovesTheRoadAndRefunds()
        {
            var bed = ConstructionTestBed.WithEastWestRoad(balance: 0);
            var segment = bed.Network.Segments.Single().Id;
            var command = new BulldozeCommand(bed.Network, bed.Budget, segment, refund: 300);

            command.Execute();
            Assert.That(bed.Network.Segments, Is.Empty);
            Assert.That(bed.Network.Nodes, Is.Empty);
            Assert.That(bed.Budget.Balance, Is.EqualTo(300));

            command.Undo();
            Assert.That(bed.Network.Segments.Count, Is.EqualTo(1));
            Assert.That(bed.Budget.Balance, Is.EqualTo(0));
        }

        [Test]
        public void History_DropsOldestCommandsBeyondCapacity()
        {
            var bed = new ConstructionTestBed();
            var history = new CommandHistory(capacity: 2);

            for (var i = 0; i < 3; i++)
                history.Execute(new BuildRoadCommand(bed.Network, bed.Budget, bed.Plan(new float2(0f, i * 50f), new float2(100f, i * 50f))));

            Assert.That(history.Undo(), Is.True);
            Assert.That(history.Undo(), Is.True);
            Assert.That(history.Undo(), Is.False);
            Assert.That(bed.Network.Segments.Count, Is.EqualTo(1));
        }
    }
}
