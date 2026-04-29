using System;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Services;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class StreamInactivityWatcherTests
    {
        [Test]
        public void Constructor_Throws_OnInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new StreamInactivityWatcher(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamInactivityWatcher(new CancellationTokenSource(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamInactivityWatcher(new CancellationTokenSource(), 1, 0));
        }

        [Test]
        public async Task WatchAsync_ExitsOnSignalCompletion_WhenNotActive()
        {
            var cts = new CancellationTokenSource();
            var watcher = new StreamInactivityWatcher(cts, 1, 10);

            // Start watching; activity time always zero. SignalCompletion should make it exit quickly.
            var watchTask = watcher.WatchAsync(() => 0L, CancellationToken.None);
            watcher.SignalCompletion();

            // Should complete without throwing
            await watchTask.ConfigureAwait(false);
        }

        [Test]
        public async Task WatchAsync_ExitsOnSignalCompletion_WhenActive()
        {
            var cts = new CancellationTokenSource();
            var watcher = new StreamInactivityWatcher(cts, 1, 10);

            var watchTask = watcher.WatchAsync(() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), CancellationToken.None);
            watcher.SignalCompletion();

            await watchTask.ConfigureAwait(false);
        }

        [Test]
        public async Task WatchAsync_ReportsTimeout_WhenInactivityTimeoutExceeded()
        {
            // Short timeout so test runs quickly
            var internalCts = new CancellationTokenSource();
            var watcher = new StreamInactivityWatcher(internalCts, 1, 10);

            await watcher.WatchAsync(() => 0L, CancellationToken.None).ConfigureAwait(false);

            Assert.That(watcher.IsTimeout, Is.True);
        }

        [Test]
        public async Task WatchAsync_Respects_CancellationToken()
        {
            var internalCts = new CancellationTokenSource();
            var watcher = new StreamInactivityWatcher(internalCts, 10, 100);
            var cts = new CancellationTokenSource();

            var watchTask = watcher.WatchAsync(() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), cts.Token);

            // Cancel the token; the watcher should observe this and complete with a cancellation exception.
            cts.Cancel();

            try
            {
                await watchTask.ConfigureAwait(false);
                Assert.Fail("Expected OperationCanceledException or TaskCanceledException due to cancellation.");
            }
            catch (System.Exception ex)
            {
                // Accept either TaskCanceledException or OperationCanceledException (TaskCanceledException derives from OperationCanceledException)
                Assert.That(ex, Is.InstanceOf<System.OperationCanceledException>());
            }
        }
    }
}
