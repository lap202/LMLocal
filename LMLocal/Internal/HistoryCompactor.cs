using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Internal
{
    internal interface IHistoryCompactor
    {
        void SetMaxContext(int maxContext);
        bool NeedsCompaction();
        Task CompactIfNeededAsync(ILMStudioClient client, CancellationToken cancellationToken);
    }

    internal class HistoryCompactor : IHistoryCompactor
    {
        private const int KeepRecentMessages = 6;
        private const double CompactionThresholdRatio = 0.8;

        private readonly IChatHistoryManager _history;
        private readonly string _systemPrompt;
        private readonly Func<WebView2MessageType, Task> _onStatusChanged;
        private int _maxContext = 16384;

        public HistoryCompactor(IChatHistoryManager history, string systemPrompt, Func<WebView2MessageType, Task> onStatusChanged = null)
        {
            _history = history;
            _systemPrompt = systemPrompt;
            _onStatusChanged = onStatusChanged;
        }

        public void SetMaxContext(int maxContext)
        {
            _maxContext = maxContext > 0 ? maxContext : 16384;
        }

        public bool NeedsCompaction()
        {
            int chars = _history.GetHistoryCopy().Sum(m => m.Content.Length);
            return (chars / 4) >= (int)(_maxContext * CompactionThresholdRatio);
        }

        public async Task CompactIfNeededAsync(ILMStudioClient client, CancellationToken cancellationToken)
        {
            if (!NeedsCompaction())
                return;

            var snapshot = _history.GetHistoryCopy();
            var skip = snapshot.Count > 0 && snapshot[0].Role == "system" ? 1 : 0;
            var toSummarize = snapshot.Skip(skip).Take(snapshot.Count - skip - KeepRecentMessages).ToList();

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

                var summary = await client.SendNonStreamingAsync(summaryRequest, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    var recent = snapshot.Skip(skip + toSummarize.Count);
                    _history.ReplaceHistory(_systemPrompt, summary, recent);
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
