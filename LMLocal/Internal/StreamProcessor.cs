using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Internal
{
    /// <summary>
    /// Processes streaming responses from the LM Studio backend.
    /// </summary>
    internal class StreamProcessor
    {
        private readonly Func<StreamChunk, TokenGenerationStats, Task> _onChunk;
        private readonly Func<string, Task> _onError;
        private readonly int _batchIntervalMs;
        private readonly ITokenSpeedCalculator _tokenSpeedCalculator;

        public StreamProcessor(Func<StreamChunk, TokenGenerationStats, Task> onChunk, Func<string, Task> onError, int batchIntervalMs = 50, ITokenSpeedCalculator tokenSpeedCalculator = null)
        {
            _onChunk = onChunk;
            _onError = onError;
            _batchIntervalMs = batchIntervalMs;
            _tokenSpeedCalculator = tokenSpeedCalculator;
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

            var cancelRegistration = cancellationToken.Register(() => stream.Close());

            var consumerTask = Task.Run(async () =>
            {
                try
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

                        if (done || cancellationToken.IsCancellationRequested) break;

                        try
                        {
                            await Task.Delay(_batchIntervalMs, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during cancellation; log at debug level
                    InternalLogger.Info("StreamProcessor consumerTask canceled");
                }
                catch (Exception ex)
                {
                    // Log unexpected exceptions and forward to onError
                    InternalLogger.Error("StreamProcessor consumerTask exception", ex);
                    if (ex is OperationCanceledException) return;
                    if (_onError != null)
                        await _onError(ex.Message).ConfigureAwait(false);
                }
            });

            using (var reader = new StreamReader(stream))
            {
                try
                {
                    string line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try
                        {
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
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // Log parse/processing errors and notify consumer
                            InternalLogger.Error("StreamProcessor: error while parsing stream line", ex);
                            if (ex is OperationCanceledException) throw;
                            if (_onError != null)
                                await _onError(ex.Message).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log unexpected reader exceptions and notify consumer
                    InternalLogger.Error("StreamProcessor: reader loop exception", ex);
                    if (ex is OperationCanceledException) throw;
                    if (_onError != null)
                        await _onError(ex.Message).ConfigureAwait(false);
                }
                finally
                {
                    lock (syncLock) { isReading = false; }
                    cancelRegistration.Dispose();
                }
            }

            await consumerTask.ConfigureAwait(false);

            return fullResponse.ToString();
        }
    }
}
