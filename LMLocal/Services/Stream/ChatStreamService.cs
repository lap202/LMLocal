using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure.Lm;
using LMLocal.Models;

namespace LMLocal.Services
{
    /// <summary>
    /// Manages streaming generation of chat responses from the LM backend.
    /// Sends chat requests and processes incoming stream chunks, forwarding them to callers via callbacks.
    /// Coordinates conversation history (adds user/assistant messages) and triggers history compaction when needed.
    /// Ensures only one active generation at a time (request locking) and supports cancellation/stop.
    /// </summary>
    internal interface IChatStreamService
    {
        /// <summary>
        /// Generates initial response from LLM without tool results.
        /// </summary>
        Task GenerateStreamAsync(
            GenerateStreamContext context,
            Func<TextStreamChunk, TokenGenerationStats, Task> onChunk,
            Func<StreamCompletionResult, Task> onComplete);

        /// <summary>
        /// Generates response from LLM in follow-up round, providing tool execution results.
        /// </summary>
        Task GenerateWithToolResultsAsync(
            GenerateStreamContext context,
            List<ToolResultMessage> toolResults,
            Func<TextStreamChunk, TokenGenerationStats, Task> onChunk,
            Func<StreamCompletionResult, Task> onComplete);

        Task<bool> ResetHistoryAsync();
        void StopExecution();
    }

    internal class ChatStreamService : IChatStreamService
    {
        private readonly ILMStudioClient _client;
        private readonly IChatHistoryManager _history;
        private readonly ISettingsManager _settingsManager;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _currentCts;
        private readonly object _ctsLock = new object();

        public ChatStreamService(
            ILMStudioClient client,
            IChatHistoryManager history,
            ISettingsManager settingsManager)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }

        public async Task GenerateStreamAsync(
            GenerateStreamContext context,
            Func<TextStreamChunk, TokenGenerationStats, Task> onChunk,
            Func<StreamCompletionResult, Task> onComplete)
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

                var messages = _history.BuildUserMessagesWithHistory(
                    context.Prompt,
                    context.ActiveDocumentContent,
                    context.AdditionalPrompt);

                _history.AddUserMessage(context.Prompt);

                var speedCalculator = new TokenSpeedCalculator(
                    windowSeconds: _settingsManager.DefaultWindowSeconds);

                int timeoutSeconds = _settingsManager.Current?.StreamInactivityTimeoutSeconds ?? 0;
                IStreamInactivityWatcher watcher = timeoutSeconds > 0
                    ? new StreamInactivityWatcher(currentCts, timeoutSeconds)
                    : null;

                var processor = new StreamProcessor(
                    onChunk,
                    speedCalculator,
                    watcher,
                    _settingsManager.DefaultBatchIntervalMs);

                var messgeContext = new MessageContext(messages);
                var modelContext = new ModelContext(context.ModelId);

                using (var streaming = await _client.SendChatStreamingAsync(
                    messgeContext,
                    modelContext,
                    currentCts.Token).ConfigureAwait(false))
                {
                    var result = await processor.ProcessStreamAsync(
                        streaming.Stream,
                        currentCts.Token).ConfigureAwait(false);

                    if (!result.WasCancelled &&
                        !currentCts.Token.IsCancellationRequested &&
                        !string.IsNullOrEmpty(result.ContentResponse))
                    {
                        _history.AddAssistantMessage(result.ContentResponse);
                    }

                    if (onComplete != null)
                    {
                        await onComplete(result).ConfigureAwait(false);
                    }
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

        public async Task GenerateWithToolResultsAsync(
            GenerateStreamContext context,
            List<ToolResultMessage> toolResults,
            Func<TextStreamChunk, TokenGenerationStats, Task> onChunk,
            Func<StreamCompletionResult, Task> onComplete)
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
                var messages = new List<ChatMessage>();

                foreach (var toolResult in toolResults)
                {
                    messages.Add(new ChatMessage("tool", toolResult.Result, toolResult.ToolCallId.ToString()));
                }

                var speedCalculator = new TokenSpeedCalculator(
                    windowSeconds: _settingsManager.DefaultWindowSeconds);

                int timeoutSeconds = _settingsManager.Current?.StreamInactivityTimeoutSeconds ?? 0;
                IStreamInactivityWatcher watcher = timeoutSeconds > 0
                    ? new StreamInactivityWatcher(currentCts, timeoutSeconds)
                    : null;

                var processor = new StreamProcessor(
                    onChunk,
                    speedCalculator,
                    watcher,
                    _settingsManager.DefaultBatchIntervalMs);

                var messgeContext = new MessageContext(messages);
                var modelContext = new ModelContext(context.ModelId);

                using (var streaming = await _client.SendChatStreamingAsync(
                    messgeContext,
                    modelContext,
                    currentCts.Token).ConfigureAwait(false))
                {
                    var result = await processor.ProcessStreamAsync(
                        streaming.Stream,
                        currentCts.Token).ConfigureAwait(false);

                    if (!result.WasCancelled &&
                        !currentCts.Token.IsCancellationRequested &&
                        !string.IsNullOrEmpty(result.ContentResponse))
                    {
                        _history.AddAssistantMessage(result.ContentResponse);
                    }

                    if (onComplete != null)
                    {
                        await onComplete(result).ConfigureAwait(false);
                    }
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
    }
}
