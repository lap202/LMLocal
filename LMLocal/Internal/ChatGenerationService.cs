using System;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Internal
{
    internal class ChatGenerationService
    {
        private readonly ILMStudioClient _client;
        private readonly IChatHistoryManager _history;
        private readonly IHistoryCompactor _compactor;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _currentCts;
        private readonly object _ctsLock = new object();

        public ChatGenerationService(ILMStudioClient client, IChatHistoryManager history, IHistoryCompactor compactor)
        {
            _client = client;
            _history = history;
            _compactor = compactor;
        }

        // NOTE: onChunk now receives StreamChunk so callers can distinguish reasoning vs content
        public async Task GenerateStreamAsync(
            string prompt,
            Func<StreamChunk, TokenGenerationStats, Task> onChunk,
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
                    }
                }
                currentCts?.Dispose();
                _requestLock.Release();
            }
        }

        public void StopExecution()
        {
            lock (_ctsLock)
            {
                _currentCts?.Cancel();
            }
        }

        public bool ResetHistory()
        {
            lock (_ctsLock)
            {
                if (_currentCts != null && !_currentCts.IsCancellationRequested)
                {
                    return false;
                }
            }

            _history.Clear();
            return true;
        }

        public void SetMaxContext(int maxContext)
        {
            _compactor.SetMaxContext(maxContext);
        }
    }
}
