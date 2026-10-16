using System.Linq;
using NUnit.Framework;
using TrafficGame.Views.UI;
using UnityEngine;

namespace TrafficGame.Tests.UI
{
    public sealed class SvgPathTests
    {
        [Test]
        public void RelativeLinesAndHorizontalVertical_EndWhereTheyShould()
        {
            var commands = SvgPath.Parse("M5 21V4h11l-2 4 2 4H5");

            Assert.That(commands.Select(c => c.Kind), Is.EqualTo(new[]
            {
                PathCommandKind.Move, PathCommandKind.Line, PathCommandKind.Line, PathCommandKind.Line, PathCommandKind.Line, PathCommandKind.Line
            }));
            Assert.That(commands.Select(c => c.P3), Is.EqualTo(new[]
            {
                new Vector2(5f, 21f), new Vector2(5f, 4f), new Vector2(16f, 4f), new Vector2(14f, 8f), new Vector2(16f, 12f), new Vector2(5f, 12f)
            }));
        }

        [Test]
        public void CompactNumbers_AreSplitAtSignsAndSecondDots()
        {
            var commands = SvgPath.Parse("M12 3l2.8 5.9 6.2.7-4.6 4.3");

            Assert.That(commands[1].P3.x, Is.EqualTo(14.8f).Within(1e-4f));
            Assert.That(commands[2].P3, Is.EqualTo(new Vector2(21f, 9.6f)).Using<Vector2>((a, b) => (a - b).magnitude < 1e-4f ? 0 : 1));
            Assert.That(commands[3].P3, Is.EqualTo(new Vector2(16.4f, 13.9f)).Using<Vector2>((a, b) => (a - b).magnitude < 1e-4f ? 0 : 1));
        }

        [Test]
        public void HalfCircleArc_BecomesCurvesThatStayOnTheCircle()
        {
            var commands = SvgPath.Parse("M7 12a5 5 0 1 0 10 0");

            var curves = commands.Skip(1).ToArray();
            Assert.That(curves.Select(c => c.Kind), Is.All.EqualTo(PathCommandKind.Cubic));
            Assert.That(curves.Last().P3, Is.EqualTo(new Vector2(17f, 12f)));
            foreach (var curve in curves)
                Assert.That((curve.P3 - new Vector2(12f, 12f)).magnitude, Is.EqualTo(5f).Within(1e-3f));
        }
    }
}
