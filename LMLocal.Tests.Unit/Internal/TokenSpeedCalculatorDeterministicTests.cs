using System;
using LMLocal.Internal;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    internal class FakeTimeProvider : ITimeProvider
    {
        private long _ticks;
        public FakeTimeProvider(long initialTicks)
        {
            _ticks = initialTicks;
        }

        public long UtcNowTicks() => _ticks;

        public void Advance(TimeSpan ts) => _ticks += ts.Ticks;
    }

    [TestFixture]
    public class TokenSpeedCalculatorDeterministicTests
    {
        [Test]
        public void GetTokensPerSecond_ReturnsDeterministicValue_WithFakeTimeProvider()
        {
            var start = DateTime.UtcNow.Ticks;
            var fake = new FakeTimeProvider(start);
            var calculator = new TokenSpeedCalculator(windowSeconds: 5, timeProvider: fake);

            // initial state
            calculator.Update(0);

            // advance 1 second and add 10 tokens
            fake.Advance(TimeSpan.FromSeconds(1));
            calculator.Update(10);

            var speed = calculator.GetTokensPerSecond();
            Assert.That(speed, Is.EqualTo(10.0).Within(0.0001), "Expected 10 tokens/sec after 1 second and +10 tokens");
        }

        [Test]
        public void SlidingWindow_EvictsOldEntries()
        {
            var start = DateTime.UtcNow.Ticks;
            var fake = new FakeTimeProvider(start);
            var calculator = new TokenSpeedCalculator(windowSeconds: 2, timeProvider: fake);

            calculator.Update(0);
            // after 1 second, +4 tokens
            fake.Advance(TimeSpan.FromSeconds(1));
            calculator.Update(4);

            // after another 1 second, +6 tokens (total 10)
            fake.Advance(TimeSpan.FromSeconds(1));
            calculator.Update(10);

            // windowSeconds=2 covers whole period, speed = (10-0)/2 = 5
            var speed = calculator.GetTokensPerSecond();
            Assert.That(speed, Is.EqualTo(5.0).Within(0.0001));

            // advance beyond window to evict initial entry
            fake.Advance(TimeSpan.FromSeconds(3));
            // No new update, but GetTokensPerSecond should handle window eviction; span between last entry and now > window -> uses last remaining entry
            var speed2 = calculator.GetTokensPerSecond();
            Assert.That(speed2, Is.GreaterThanOrEqualTo(0.0));
        }
    }
}
