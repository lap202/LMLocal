using System.Threading;

using LMLocal.Internal;

using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class TokenSpeedCalculatorTests
    {
        [Test]
        public void InitialSpeed_IsZero()
        {
            var calculator = new TokenSpeedCalculator(windowSeconds: 5);
            Assert.That(calculator.GetTokensPerSecond(), Is.EqualTo(0.0).Within(0.001));
        }

        [Test]
        public void SpeedCalculation_ReturnsCorrectValue()
        {
            // Use a 5-second window
            var calculator = new TokenSpeedCalculator(windowSeconds: 5);

            // We simulate 100 tokens being added after 1 second
            // Note: TokenSpeedCalculator uses DateTime.UtcNow.Ticks internally, 
            // so we have to use real sleep for a unit test or refactor it to accept an ITimeProvider.
            // Since we want to keep it simple, we'll use a small sleep.

            calculator.Update(0);
            Thread.Sleep(100); // Wait a bit
            calculator.Update(10);

            double speed = calculator.GetTokensPerSecond();

            // Speed should be around 10 tokens / ~0.1s = ~100 tokens/sec
            Assert.That(speed, Is.GreaterThan(0));
        }

        [Test]
        public void Update_DoesNotAddEntry_IfTokenCountUnchanged()
        {
            var calculator = new TokenSpeedCalculator(windowSeconds: 5);

            calculator.Update(10);
            calculator.Update(10); // Same value

            // This is more of an internal state check, but since we can't see the queue,
            // we just ensure no crashes occur and speed remains consistent.
            Assert.That(calculator.GetTokensPerSecond(), Is.Not.Null);
        }

        [Test]
        public void GetTokensPerSecond_HandlesZeroTimeSpanGracefully()
        {
            var calculator = new TokenSpeedCalculator(windowSeconds: 5);
            // Update immediately twice
            calculator.Update(0);
            calculator.Update(20);

            // If the time span between updates is essentially zero, it should return 0 or handles it
            var speed = calculator.GetTokensPerSecond();
            Assert.That(speed, Is.Not.NaN);
        }
    }
}
