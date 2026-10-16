using System;
using NUnit.Framework;
using TrafficGame.Core.Simulation;

namespace TrafficGame.Tests.Simulation
{
    public sealed class SimulationClockTests
    {
        private const float TickRate = 20f; // 0.05 s per tick
        private const int MaxTicks = 8;

        [Test]
        public void Advance_AccumulatesFractionalTicks()
        {
            var clock = new SimulationClock(TickRate, MaxTicks);

            Assert.That(clock.Advance(0.04f), Is.EqualTo(0));
            Assert.That(clock.Advance(0.02f), Is.EqualTo(1));
            Assert.That(clock.Alpha, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(clock.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void Advance_MultipliesTimeBySpeed()
        {
            var clock = new SimulationClock(TickRate, MaxTicks) { Speed = 4f };

            Assert.That(clock.Advance(0.051f), Is.EqualTo(4));
        }

        [Test]
        public void Advance_WhilePaused_NeitherTicksNorAccumulates()
        {
            var clock = new SimulationClock(TickRate, MaxTicks) { Speed = 0f };

            Assert.That(clock.IsPaused, Is.True);
            Assert.That(clock.Advance(1f), Is.EqualTo(0));

            clock.Speed = 1f;
            Assert.That(clock.Advance(0.01f), Is.EqualTo(0));
        }

        [Test]
        public void Advance_AfterLongFrame_ClampsTicksAndDropsBacklog()
        {
            var clock = new SimulationClock(TickRate, MaxTicks);

            Assert.That(clock.Advance(10f), Is.EqualTo(MaxTicks));
            Assert.That(clock.Advance(0.01f), Is.EqualTo(0));
        }

        [Test]
        public void Speed_RejectsNegativeValues()
        {
            var clock = new SimulationClock(TickRate, MaxTicks);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Speed = -1f);
        }
    }
}
