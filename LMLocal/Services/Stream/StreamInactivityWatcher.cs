using System;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;

namespace LMLocal.Services
{
    /// <summary>
    /// Watches a stream for inactivity and cancels the operation when the configured
    /// timeout (in seconds) is exceeded.
    /// </summary>
    internal interface IStreamInactivityWatcher
    {
        bool IsTimeout { get; }

        void SignalCompletion();
        Task WatchAsync(Func<long> activityTimeMs, CancellationToken cancellationToken);
    }


    internal sealed class StreamInactivityWatcher : IStreamInactivityWatcher
    {
        private readonly int _timeoutSeconds;
        private readonly int _delayMilliseconds;
        private volatile bool _isCompleted = false;
        private readonly CancellationTokenSource _cts;
        private volatile bool _isTimeout = false;
        public bool IsTimeout => _isTimeout;

        public StreamInactivityWatcher(CancellationTokenSource cts, int timeoutSeconds, int delayMilliseconds = 1000)
        {
            _cts = cts ?? throw new ArgumentNullException(nameof(cts));

            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout must be a positive number of seconds.");
            }
            if (delayMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds), "Delay must be a positive number of milliseconds.");
            }
            _timeoutSeconds = timeoutSeconds;
            _delayMilliseconds = delayMilliseconds;
        }

        public void SignalCompletion()
        {
            _isCompleted = true;
        }

        public async Task WatchAsync(Func<long> activityTimeMs, CancellationToken cancellationToken)
        {
            long lastActivityMs = activityTimeMs();
            long timeoutMs = _timeoutSeconds * 1000L;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_isCompleted)
                {
                    InternalLogger.Info("Stream completed, inactivity watcher exiting");
                    return;
                }

                await Task.Delay(_delayMilliseconds, cancellationToken).ConfigureAwait(false);

                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long activityMs = activityTimeMs();

                if (activityMs > lastActivityMs)
                    lastActivityMs = activityMs;


                if (nowMs - lastActivityMs > timeoutMs)
                {
                    InternalLogger.Error($"Stream inactivity timeout ({nowMs - lastActivityMs}ms) exceeded.");
                    _isTimeout = true;
                    _isCompleted = true;
                    _cts?.Cancel();
                    return;
                }
            }
        }
    }
}
