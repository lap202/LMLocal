using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Services;
using LMLocal.Models;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class StreamProcessorTests
    {
        private class MockStreamInactivityWatcher : IStreamInactivityWatcher
        {
            public bool IsTimeout => false;

            public Task WatchAsync(Func<long> isActive, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public void SignalCompletion()
            {
            }
        }

        [Test]
        public void ProcessStreamAsync_Throws_OnCancellation()
        {
            var processor = new StreamProcessor(
                async (chunk, stats) => { await Task.CompletedTask; },
                async (err) => { await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            var json = "data: {\"choices\":[{\"delta\":{\"content\":\"cancel\"}}]}\n";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var cts = new CancellationTokenSource();
                cts.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await processor.ProcessStreamAsync(stream, cts.Token));
            }
        }

        [Test]
        public async Task ProcessStreamAsync_BatchesChunks_WhenIntervalElapsed()
        {
            int chunkCount = 0;
            var processor = new StreamProcessor(
                async (chunk, stats) => { chunkCount++; await Task.CompletedTask; },
                async (err) => { await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                1,
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            var json = new StringBuilder();
            json.AppendLine("data: {\"choices\":[{\"delta\":{\"content\":\"a\"}}]}");
            json.AppendLine("data: {\"choices\":[{\"delta\":{\"content\":\"b\"}}]}");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString())))
            {
                await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(chunkCount, Is.GreaterThanOrEqualTo(1));
            }
        }

        [Test]
        public async Task ProcessStreamAsync_IgnoresDoneAndEmptyLines()
        {
            bool chunkCalled = false;
            var processor = new StreamProcessor(
                async (chunk, stats) => { chunkCalled = true; await Task.CompletedTask; },
                async (err) => { await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            var json = "\n   \ndata: [DONE]\n";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var fullResponse = await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(chunkCalled, Is.False);
                Assert.That(fullResponse, Is.EqualTo(""));
            }
        }

        [Test]
        public async Task ProcessStreamAsync_CallsOnError_OnInvalidJson()
        {
            bool errorCalled = false;
            var processor = new StreamProcessor(
                async (chunk, tokens) => { await Task.CompletedTask; },
                async (err) => { errorCalled = true; await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            var invalid = "data: {not a json}\n";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalid)))
            {
                await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(errorCalled, Is.True);
            }
        }

        [Test]
        public async Task ProcessStreamAsync_ReportsTokens_ViaOnChunk_FromUsage()
        {
            int reportedTokens = 0;
            var processor = new StreamProcessor(
                async (chunk, stats) => { reportedTokens = stats.TotalTokens; await Task.CompletedTask; },
                async (err) => { await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                0,
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            var json = "data: {\"choices\":[{\"delta\":{\"content\":\"hi\"}}],\"usage\":{\"total_tokens\":42}}\n";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var fullResponse = await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(fullResponse, Is.EqualTo("hi"));
                Assert.That(reportedTokens, Is.EqualTo(42));
            }
        }

        [Test]
        public async Task ProcessStreamAsync_SendsFinalChunk_AtEnd()
        {
            int chunkCount = 0;
            var processor = new StreamProcessor(
                async (chunk, tokens) => { chunkCount++; await Task.CompletedTask; },
                async (err) => { await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                5000,
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            var json = "data: {\"choices\":[{\"delta\":{\"content\":\"final\"}}]}\n";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(chunkCount, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task ProcessStreamAsync_DoesNotCallChunk_OnEmptyStream()
        {
            bool chunkCalled = false;
            var processor = new StreamProcessor(
                async (chunk, tokens) => { chunkCalled = true; await Task.CompletedTask; },
                async (err) => { await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            using (var stream = new MemoryStream())
            {
                var fullResponse = await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(chunkCalled, Is.False);
                Assert.That(fullResponse, Is.EqualTo(""));
            }
        }

        [Test]
        public async Task ProcessStreamAsync_ProcessesChunksAndReturnsResult()
        {
            var chunkCalled = false;
            var errorCalled = false;
            int reportedTokens = 0;
            var processor = new StreamProcessor(
                async (chunk, stats) => { chunkCalled = true; reportedTokens = stats.TotalTokens; await Task.CompletedTask; },
                async (err) => { errorCalled = true; await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                10,
                inactivityWatcher: new MockStreamInactivityWatcher()
            );
            var json = "data: {\"choices\":[{\"delta\":{\"content\":\"hello\"}}]}\n";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var fullResponse = await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(chunkCalled, Is.True);
                Assert.That(errorCalled, Is.False);
                Assert.That(fullResponse, Is.EqualTo("hello"));
                Assert.That(reportedTokens, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task ProcessStreamAsync_UsesInactivityWatcher_WhenProvided()
        {
            bool watcherCalled = false;
            var watcherMock = new CustomStreamInactivityWatcher(() => watcherCalled = true);

            var processor = new StreamProcessor(
                async (chunk, stats) => { await Task.CompletedTask; },
                async (err) => { await Task.CompletedTask; },
                async () => { await Task.CompletedTask; },
                inactivityWatcher: watcherMock
            );

            var json = "data: {\"choices\":[{\"delta\":{\"content\":\"test\"}}]}\n";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                await processor.ProcessStreamAsync(stream, CancellationToken.None);
                Assert.That(watcherCalled, Is.True);
            }
        }

            private class CustomStreamInactivityWatcher : IStreamInactivityWatcher
            {
                private readonly Action _onWatchCalled;

                public CustomStreamInactivityWatcher(Action onWatchCalled)
                {
                    _onWatchCalled = onWatchCalled;
                }

                public bool IsTimeout => false;

                public Task WatchAsync(Func<long> isActive, CancellationToken cancellationToken)
                {
                    _onWatchCalled();
                    return Task.CompletedTask;
                }

                public void SignalCompletion()
                {
                }
            }
    }
}
