using System;
using LMLocal.Infrastructure.Time;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Infrastructure
{
    [TestFixture]
    public class SystemTimeProviderTests
    {
        [Test]
        public void UtcNowTicks_ReturnsCurrentTimeApprox()
        {
            var provider = new SystemTimeProvider();
            long before = DateTime.UtcNow.Ticks;
            long ticks = provider.UtcNowTicks();
            long after = DateTime.UtcNow.Ticks;

            Assert.That(ticks, Is.GreaterThanOrEqualTo(before));
            Assert.That(ticks, Is.LessThanOrEqualTo(after));
        }
    }
}
