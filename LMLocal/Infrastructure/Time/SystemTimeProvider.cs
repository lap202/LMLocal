using System;

namespace LMLocal.Infrastructure.Time
{
    internal interface ITimeProvider
    {
        long UtcNowTicks();
    }

    internal class SystemTimeProvider : ITimeProvider
    {
        public long UtcNowTicks()
        {
            return DateTime.UtcNow.Ticks;
        }
    }
}
