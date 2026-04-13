using System;

namespace LMLocal.Internal
{
    internal class SystemTimeProvider : ITimeProvider
    {
        public long UtcNowTicks()
        {
            return DateTime.UtcNow.Ticks;
        }
    }
}
