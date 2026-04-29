using System;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Infrastructure.Lm;
using LMLocal.Models;

namespace LMLocal.Services
{
    /// <summary>
    /// Manages streaming generation of chat responses from the LM backend.
    /// Sends chat requests and processes incoming stream chunks, forwarding them to callers via callbacks.
    /// Coordinates conversation history (adds user/assistant messages) and triggers history compaction when needed.
    /// Ensures only one active generation at a time (request locking) and supports cancellation/stop.
    /// Provides history reset and max‑context configuration.
    /// </summary>
    internal interface IChatGenerationService
    {
        Task GenerateStreamAsync(GenerateStreamContext context, Func<StreamChunk, TokenGenerationStats, Task> onChunk, Func<string, Task> onError, Func<Task> onEnd);
        Task<bool> ResetHistoryAsync();
        void SetMaxContext(int maxContext);
        void StopExecution();
    }

    internal class ChatGenerationService : IChatGenerationService
    {
        private readonly ILMStudioClient _client;
        private readonly IChatHistoryManager _history;
        private readonly IHistoryCompactor _compactor;
        private readonly ISettingsManager _settingsManager;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _currentCts;
        private readonly object _ctsLock = new object();


        public ChatGenerationService(ILMStudioClient client, IChatHistoryManager history, IHistoryCompactor compactor, ISettingsManager settingsManager = null)
        {
            _client = client;
            _history = history;
            _compactor = compactor;
            _settingsManager = settingsManager;
        }

        public async Task GenerateStreamAsync(
            GenerateStreamContext context,
            Func<StreamChunk, TokenGenerationStats, Task> onChunk,
            Func<string, Task> onError,
            Func<Task> onEnd)
            
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

                var messages = _history.BuildUserMessagesWithHistory(context.Prompt, context.ActiveDocumentContent, context.AdditionalPrompt);
                _history.AddUserMessage(context.Prompt);

                int timeoutSeconds = _settingsManager?.Current?.StreamInactivityTimeoutSeconds ?? 0;


                //var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_currentCts.Token);

                var speedCalculator = new TokenSpeedCalculator(windowSeconds: 5);

                var batchIntervalMs = 100;

                IStreamInactivityWatcher watcher = timeoutSeconds > 0
                    ? new StreamInactivityWatcher(currentCts, timeoutSeconds)
                    : null;

                var processor = new StreamProcessor(onChunk, onError, onEnd, batchIntervalMs, speedCalculator, watcher);

                string fullResponse = null;


                var messgeContext = new MessageContext(messages);
                var modelContext = new ModelContext(context.ModelId);
                using (var streaming = await _client.SendChatStreamingAsync(messgeContext, modelContext, currentCts.Token).ConfigureAwait(false))
                {


                    fullResponse = await processor.ProcessStreamAsync(streaming.Stream, currentCts.Token).ConfigureAwait(false);
                }

                if (!currentCts.Token.IsCancellationRequested && !string.IsNullOrEmpty(fullResponse))
                {
                    _history.AddAssistantMessage(fullResponse);
                    _ = _compactor.CompactIfNeededAsync(_client, context.ModelId, CancellationToken.None);
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

        public async Task<bool> ResetHistoryAsync()
        {
            if (!await _requestLock.WaitAsync(0).ConfigureAwait(false))
                return false;

            try
            {
                _history.Clear();
            }
            finally
            {
                _requestLock.Release();
            }

            return true;
        }

        public void SetMaxContext(int maxContext)
        {
            _compactor.SetMaxContext(maxContext);
        }
    }
}
