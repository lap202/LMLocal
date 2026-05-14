using System;
using System.Runtime.CompilerServices;

namespace LMLocal.Infrastructure.Time
{
    internal interface ITimeProvider
    {
        long UtcNowTicks();
    }

    internal class SystemTimeProvider : ITimeProvider
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long UtcNowTicks()
        {
            return DateTime.UtcNow.Ticks;
        }
    }
}
