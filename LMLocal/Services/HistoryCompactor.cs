using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure.Lm;
using LMLocal.Infrastructure.Lm.Responses;
using LMLocal.Infrastructure.WebView;
using LMLocal.Models;


namespace LMLocal.Services
{
    internal interface IHistoryCompactor
    {
        void SetMaxContext(int maxContext);
        bool NeedsCompaction();
        Task CompactIfNeededAsync(ILMStudioClient client, string modelId, CancellationToken cancellationToken);
    }

    internal class HistoryCompactor : IHistoryCompactor
    {
        private const int KeepRecentMessages = 6;
        private const double CompactionThresholdRatio = 0.8;

        private readonly IChatHistoryManager _history;
        private readonly Func<WebView2MessageType, Task> _onStatusChanged;
        private int _maxContext = 16384;
        private readonly ISettingsManager _settingsManager;

        public HistoryCompactor(IChatHistoryManager history, Func<WebView2MessageType, Task> onStatusChanged = null, ISettingsManager settingsManager = null)
        {
            _history = history;
            _onStatusChanged = onStatusChanged;
            _settingsManager = settingsManager;
        }

        public void SetMaxContext(int maxContext)
        {
            _maxContext = maxContext > 0 ? maxContext : 16384;
        }

        public bool NeedsCompaction()
        {
            bool enabled = _settingsManager?.Current?.EnableHistoryCompaction ?? false;
            if (!enabled)
                return false;

            int chars = _history.GetHistoryCopy().Sum(m => m.Content.Length);
            return (chars / 4) >= (int)(_maxContext * CompactionThresholdRatio);
        }

        public async Task CompactIfNeededAsync(ILMStudioClient client, string modelId, CancellationToken cancellationToken)
        {
            if (!NeedsCompaction())
                return;

            var snapshot = _history.GetHistoryCopy();
            var expectedSize = snapshot.Count;

            int toTake = snapshot.Count - KeepRecentMessages;
            if (toTake <= 0) return;
            var toSummarize = snapshot.Take(toTake).ToList();

            if (toSummarize.Count == 0)
                return;

            if (_onStatusChanged != null)
                await _onStatusChanged(WebView2MessageType.CompactionStart).ConfigureAwait(false);

            try
            {
                var summaryRequest = new List<ChatMessage>
                {
                    new ChatMessage("system", "Summarize the following conversation briefly, preserving key facts, decisions and code details."),
                    new ChatMessage("user", FormatForSummary(toSummarize))
                };

                var modelContext = new ModelContext(modelId: modelId, temperature: 0.3);
                var messageContext = new MessageContext(summaryRequest);

                SendChatResponse response = await client.SendChatAsync(messageContext, modelContext, cancellationToken).ConfigureAwait(false);
                if (response != null)
                {
                    var parsedSummary = response?.Choices?.FirstOrDefault(x => x != null)?.Message?.Content?.Trim();

                    if (!string.IsNullOrWhiteSpace(parsedSummary))
                    {
                        var recent = snapshot.Skip(toSummarize.Count);
                        var success = _history.ReplaceHistory(parsedSummary, recent, expectedSize);
                        if (!success)
                        {
                            InternalLogger.Debug("History size changed during compaction, skipping replace.");
                        }
                    }
                    else
                    {
                        InternalLogger.Warn("Compaction produced empty summary, skipping history replacement.");
                    }
                }
            }
            finally
            {
                if (_onStatusChanged != null)
                    await _onStatusChanged(WebView2MessageType.CompactionEnd).ConfigureAwait(false);
            }
        }

        private string FormatForSummary(List<ChatMessage> messages)
        {
            var sb = new StringBuilder();
            foreach (var msg in messages)
            {
                sb.AppendLine($"{msg.Role}: {msg.Content}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
