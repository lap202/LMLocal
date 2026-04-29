using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure.Lm;
using LMLocal.Models;

namespace LMLocal.Services
{
    internal interface IStreamProcessor
    {
        Task<string> ProcessStreamAsync(Stream stream, CancellationToken cancellationToken);
    }

    internal class StreamProcessor : IStreamProcessor
    {
        private readonly Func<StreamChunk, TokenGenerationStats, Task> _onChunk;
        private readonly Func<string, Task> _onError;
        private readonly Func<Task> _onEnd;
        private readonly int _batchIntervalMs;
        private readonly ITokenSpeedCalculator _tokenSpeedCalculator;
        private readonly IStreamInactivityWatcher _inactivityWatcher;

        public StreamProcessor(Func<StreamChunk, TokenGenerationStats, Task> onChunk, Func<string, Task> onError, Func<Task> onEnd, int batchIntervalMs = 50, ITokenSpeedCalculator tokenSpeedCalculator = null, IStreamInactivityWatcher inactivityWatcher = null)
        {
            _onChunk = onChunk;
            _onError = onError;
            _onEnd = onEnd;
            _batchIntervalMs = batchIntervalMs;
            _tokenSpeedCalculator = tokenSpeedCalculator;
            _inactivityWatcher = inactivityWatcher;
        }

        public async Task<string> ProcessStreamAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {

            cancellationToken.ThrowIfCancellationRequested();

            var fullResponse = new StringBuilder();
            var contentBuffer = new StringBuilder();
            var reasoningBuffer = new StringBuilder();
            int currentTokens = 0;
            var speedCalculator = _tokenSpeedCalculator ?? (ITokenSpeedCalculator)new TokenSpeedCalculator(windowSeconds: 5);

            var syncLock = new object();
            bool isReading = true;

            long lastLineMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var cancelRegistration = cancellationToken.Register(() => stream.Close());

            try
            {
                Task inactivityWatcherTask = Task.CompletedTask;
                if (_inactivityWatcher != null)
                {
                    inactivityWatcherTask = _inactivityWatcher.WatchAsync(
                        () =>
                        {
                            lock (syncLock)
                            {
                                return lastLineMs;
                            }
                        },
                        cancellationToken
                    );
                }

                var consumerTask = Task.Run(async () =>
                {
                    while (true)
                    {
                        StreamChunk? chunkToSend = null;
                        TokenGenerationStats statsToSend = default;
                        bool done = false;

                        lock (syncLock)
                        {
                            if (reasoningBuffer.Length > 0)
                            {
                                var t = reasoningBuffer.ToString();
                                reasoningBuffer.Clear();
                                chunkToSend = new StreamChunk(t, ChunkKind.Reasoning);
                            }
                            else if (contentBuffer.Length > 0)
                            {
                                var t = contentBuffer.ToString();
                                contentBuffer.Clear();
                                chunkToSend = new StreamChunk(t, ChunkKind.Content);
                            }

                            statsToSend = new TokenGenerationStats(currentTokens, speedCalculator.GetTokensPerSecond());
                            done = !isReading && reasoningBuffer.Length == 0 && contentBuffer.Length == 0;
                        }

                        if (chunkToSend.HasValue && !chunkToSend.Value.IsEmpty && _onChunk != null)
                        {
                            await _onChunk(chunkToSend.Value, statsToSend).ConfigureAwait(false);
                        }

                        if (done || cancellationToken.IsCancellationRequested)
                        {
                            _inactivityWatcher?.SignalCompletion();

                            break;
                        }

                        try
                        {
                            await Task.Delay(_batchIntervalMs, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }, cancellationToken);

                using (var reader = new StreamReader(stream))
                {
                    try
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            lock (syncLock)
                            {
                                lastLineMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            }
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            int tempTokens = currentTokens;
                            var chunk = LlmSseParser.ExtractDelta(line, ref tempTokens);

                            if (chunk.Equals(default(StreamChunk)) == false)
                            {
                                lock (syncLock)
                                {
                                    if (chunk.Kind == ChunkKind.Reasoning)
                                    {
                                        reasoningBuffer.Append(chunk.Text);
                                    }
                                    else
                                    {
                                        contentBuffer.Append(chunk.Text);
                                        fullResponse.Append(chunk.Text);
                                    }
                                    currentTokens = tempTokens;
                                    speedCalculator.Update(currentTokens);
                                }
                            }
                        }
                    }
                    finally
                    {
                        lock (syncLock) { isReading = false; }
                        cancelRegistration.Dispose();
                        _inactivityWatcher?.SignalCompletion();
                    }
                }

                await consumerTask.ConfigureAwait(false);

                await inactivityWatcherTask.ConfigureAwait(false);

                await _onEnd().ConfigureAwait(false);

            }
            catch (ObjectDisposedException ex)
            {
                if (_inactivityWatcher?.IsTimeout == true)
                {
                    InternalLogger.Info($"Stream canceled due to inactivity timeout: {ex.Message}");
                    if (_onError != null) await _onError("Stream canceled due to inactivity timeout").ConfigureAwait(false);
                }
                else
                {
                    InternalLogger.Info($"Stream canceled: {ex.Message}");
                    if (_onEnd != null) await _onEnd().ConfigureAwait(false);
                }

            }
            catch (OperationCanceledException ex)
            {
                if (_inactivityWatcher?.IsTimeout == true)
                {
                    InternalLogger.Info($"Stream canceled due to inactivity timeout: {ex.Message}");
                    if (_onError != null) await _onError("Stream canceled due to inactivity timeout").ConfigureAwait(false);
                }
                else
                {
                    InternalLogger.Info($"Stream canceled: {ex.Message}");
                    if (_onEnd != null) await _onEnd().ConfigureAwait(false);
                }

            }
            catch (Exception ex)
            {
                InternalLogger.Error($"Error in StreamProcessor: {ex.Message}", ex);
                if (_onError != null)
                {
                    await _onError(ex.Message).ConfigureAwait(false);
                }
            }
            finally
            {
                stream?.Close();
                _inactivityWatcher?.SignalCompletion();
            }

            return fullResponse.ToString();
        }

    }
}
