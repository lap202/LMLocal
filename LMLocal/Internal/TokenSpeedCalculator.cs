using System;
using System.Collections.Generic;

namespace LMLocal.Internal
{
    /// <summary>
    /// Calculates token generation speed using a sliding time window.
    /// </summary>
    internal class TokenSpeedCalculator : ITokenSpeedCalculator
    {
        private readonly Queue<(long ticksUtc, int tokens)> _window = new Queue<(long, int)>();
        private readonly long _windowTicks;
        private int _currentTokens;
        private readonly ITimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenSpeedCalculator"/> class.
        /// </summary>
        /// <param name="windowSeconds">The sliding window duration in seconds.</param>
        /// <param name="timeProvider">Optional time provider for deterministic tests.</param>
        public TokenSpeedCalculator(int windowSeconds = 5, ITimeProvider timeProvider = null)
        {
            _timeProvider = timeProvider ?? new SystemTimeProvider();
            _windowTicks = TimeSpan.FromSeconds(windowSeconds).Ticks;
            _window.Enqueue((_timeProvider.UtcNowTicks(), 0));
        }

        /// <summary>
        /// Updates the speed calculator with the current total tokens count.
        /// </summary>
        /// <param name="totalTokens">The total tokens generated so far.</param>
        public void Update(int totalTokens)
        {
            if (totalTokens != _currentTokens)
            {
                _currentTokens = totalTokens;
                _window.Enqueue((_timeProvider.UtcNowTicks(), totalTokens));
            }
        }

        /// <summary>
        /// Gets the generation speed in tokens per second over the sliding window.
        /// </summary>
        /// <returns>Tokens per second.</returns>
        public double GetTokensPerSecond()
        {
            long now = _timeProvider.UtcNowTicks();
            long cutoff = now - _windowTicks;

            // Remove outdated entries but keep at least one for delta calculation
            while (_window.Count > 1 && _window.Peek().ticksUtc < cutoff)
            {
                _window.Dequeue();
            }

            var (ticksUtc, tokens) = _window.Peek();
            double spanSeconds = TimeSpan.FromTicks(now - ticksUtc).TotalSeconds;

            if (spanSeconds <= 0) return 0.0;

            int tokensInWindow = _currentTokens - tokens;
            return tokensInWindow / spanSeconds;
        }
    }
}
