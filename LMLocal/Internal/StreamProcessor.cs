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
        private readonly Func<string, TokenGenerationStats, Task> _onChunk;
        private readonly Func<string, Task> _onError;
        private readonly int _batchIntervalMs; // Batching interval (e.g., 100-150 ms)

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamProcessor"/> class.
        /// </summary>
        /// <param name="onChunk">Callback for each chunk of text and token generation statistics.</param>
        /// <param name="onError">Callback for errors.</param>
        /// <param name="batchIntervalMs">Batching interval in milliseconds.</param>
        public StreamProcessor(Func<string, TokenGenerationStats, Task> onChunk, Func<string, Task> onError, int batchIntervalMs = 150)
        {
            _onChunk = onChunk;
            _onError = onError;
            _batchIntervalMs = batchIntervalMs;
        }

        /// <summary>
        /// Processes the stream asynchronously, batching output and reporting tokens.
        /// </summary>
        /// <param name="stream">The stream to process.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A tuple containing the full response and total tokens.</returns>
        public async Task<string> ProcessStreamAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullResponse = new StringBuilder(); // Full text for history
            var chunkBuffer = new StringBuilder();  // Text for the current batch
            int currentTokens = 0;
            var speedCalculator = new TokenSpeedCalculator(windowSeconds: 5);

            var syncLock = new object();
            bool isReading = true;

            var cancelRegistration = cancellationToken.Register(() => stream.Close());

            // Consumer thread: periodically flushes the buffer to the UI
            var consumerTask = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        string chunkToSend = null;
                        TokenGenerationStats statsToSend = default;
                        bool done = false;

                        lock (syncLock)
                        {
                            if (chunkBuffer.Length > 0)
                            {
                                chunkToSend = chunkBuffer.ToString();
                                chunkBuffer.Clear();
                            }
                            statsToSend = new TokenGenerationStats(currentTokens, speedCalculator.GetTokensPerSecond());
                            done = !isReading;
                        }

                        if (chunkToSend != null && _onChunk != null)
                        {
                            await _onChunk(chunkToSend, statsToSend).ConfigureAwait(false);
                        }

                        // If reading is complete, we've flushed the final chunk, so we can exit.
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
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    // Do not forward cancellation as an error — treat cancellation as normal termination.
                    if (ex is OperationCanceledException) return;
                    if (_onError != null)
                        await _onError(ex.Message).ConfigureAwait(false);
                }
            });

            // Producer thread: reads the network stream as fast as possible
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
                            string delta = LlmSseParser.ExtractDelta(line, ref tempTokens);

                            if (!string.IsNullOrEmpty(delta) || tempTokens != currentTokens)
                            {
                                lock (syncLock)
                                {
                                    if (!string.IsNullOrEmpty(delta))
                                    {
                                        chunkBuffer.Append(delta);
                                    }
                                    currentTokens = tempTokens;
                                    speedCalculator.Update(currentTokens);
                                }

                                if (!string.IsNullOrEmpty(delta))
                                {
                                    fullResponse.Append(delta);
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // If it's cancellation propagate so outer handlers can treat it normally
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
                    // stream.Close() threw an IOException, which is expected on cancellation
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Do not forward cancellation as an error — treat it as normal termination.
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

            // Wait for consumer to finish flushing
            await consumerTask.ConfigureAwait(false);

            return fullResponse.ToString();
        }
    }
}
