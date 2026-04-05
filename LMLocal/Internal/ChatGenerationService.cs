using System;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Internal
{
    /// <summary>
    /// Manages chat history, cancellation tokens, and coordinates interactions with the LLM API client 
    /// (<see cref="LMStudioClient"/>) and stream processing (<see cref="StreamProcessor"/>).
    /// Decoupling this from the LMLocalBridge enables unit testing of chat logic and stream lifecycles without a UI context.
    /// </summary>
    internal class ChatGenerationService
    {
        private readonly ILMStudioClient _client;
        private readonly IChatHistoryManager _history;
        private readonly IHistoryCompactor _compactor;

        // Concurrency controls to ensure only one stream generates at a time
        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _currentCts;
        private readonly object _ctsLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatGenerationService"/> class.
        /// </summary>
        public ChatGenerationService(ILMStudioClient client, IChatHistoryManager history, IHistoryCompactor compactor)
        {
            _client = client;
            _history = history;
            _compactor = compactor;
        }

        /// <summary>
        /// Initiates a streaming LLM request, processing sequential chunks through <paramref name="onChunk"/>.
        /// Automatically manages cancellation state and chat history accumulation.
        /// </summary>
        public async Task GenerateStreamAsync(
            string prompt,
            Func<string, TokenGenerationStats, Task> onChunk,
            Func<string, Task> onError)
        {
            await _requestLock.WaitAsync().ConfigureAwait(false);
            CancellationTokenSource currentCts = null;

            try
            {
                lock (_ctsLock)
                {
                    _currentCts?.Cancel();
                    _currentCts?.Dispose();
                    _currentCts = new CancellationTokenSource();
                    currentCts = _currentCts;
                }

                var messages = _history.BuildMessagesForRequest(prompt);
                _history.AddUserMessage(prompt);

                var processor = new StreamProcessor(onChunk, onError);
                string fullResponse = null;

                using (var streaming = await _client.SendChatRequestAsync(messages, currentCts.Token).ConfigureAwait(false))
                {
                    fullResponse = await processor.ProcessStreamAsync(streaming.Stream, currentCts.Token).ConfigureAwait(false);
                }

                // If not cancelled and response exists, persist context
                if (!currentCts.Token.IsCancellationRequested && !string.IsNullOrEmpty(fullResponse))
                {
                    _history.AddAssistantMessage(fullResponse);
                    _ = _compactor.CompactIfNeededAsync(_client, CancellationToken.None);
                }
            }
            finally
            {
                lock (_ctsLock)
                {
                    if (_currentCts == currentCts)
                    {
                        _currentCts = null;
                        currentCts = null; // Mark as detached so we don't accidentally dispose it twice if we want it to live
                    }
                }
                currentCts?.Dispose();
                _requestLock.Release();
            }
        }

        /// <summary>
        /// Stops the current generation process if any is actively running.
        /// </summary>
        public void StopExecution()
        {
            lock (_ctsLock)
            {
                _currentCts?.Cancel();
                _currentCts?.Dispose();
                _currentCts = null;
            }
        }

        /// <summary>
        /// Clears the active chat history. Fails if generation is currently in-flight.
        /// </summary>
        /// <returns>True if successful, false if a generation request is currently active.</returns>
        public bool ResetHistory()
        {
            lock (_ctsLock)
            {
                if (_currentCts != null && !_currentCts.IsCancellationRequested)
                {
                    // Can't clear while generating
                    return false;
                }
            }

            _history.Clear();
            return true;
        }

        /// <summary>
        /// Updates the internal compactor configuration when context sizes change.
        /// </summary>
        public void SetMaxContext(int maxContext)
        {
            _compactor.SetMaxContext(maxContext);
        }
    }
}
