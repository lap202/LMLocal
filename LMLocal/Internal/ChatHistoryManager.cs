using System.Collections.Generic;
using System.Linq;

namespace LMLocal.Internal
{
    internal interface IChatHistoryManager
    {
        void AddUserMessage(string content);
        void AddAssistantMessage(string content);
        void Clear();
        IReadOnlyList<ChatMessage> GetHistoryCopy();
        void ReplaceHistory(string systemPrompt, string summary, IEnumerable<ChatMessage> recent);
        List<ChatMessage> BuildMessagesForRequest(string userPrompt);
    }

    internal class ChatHistoryManager : IChatHistoryManager
    {
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private readonly object _lock = new object();
        private readonly string _systemPrompt;

        public ChatHistoryManager(string systemPrompt)
        {
            _systemPrompt = systemPrompt;
        }

        public void AddUserMessage(string content)
        {
            lock (_lock)
            {
                _history.Add(new ChatMessage("user", MarkdownStripper.Strip(content)));
            }
        }

        public void AddAssistantMessage(string content)
        {
            if (string.IsNullOrEmpty(content)) return;
            lock (_lock)
            {
                _history.Add(new ChatMessage("assistant", MarkdownStripper.Strip(content)));
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }

        public IReadOnlyList<ChatMessage> GetHistoryCopy()
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }

        public void ReplaceHistory(string systemPrompt, string summary, IEnumerable<ChatMessage> recent)
        {
            lock (_lock)
            {
                _history.Clear();
                _history.Add(new ChatMessage("system", systemPrompt));
                _history.Add(new ChatMessage("assistant", summary));
                _history.AddRange(recent);
            }
        }

        public List<ChatMessage> BuildMessagesForRequest(string userPrompt)
        {
            var messages = new List<ChatMessage>();
            lock (_lock)
            {
                if (_history.Count == 0)
                    messages.Add(new ChatMessage("system", _systemPrompt));
                messages.AddRange(_history);
            }
            messages.Add(new ChatMessage("user", userPrompt));
            return messages;
        }
    }
}

