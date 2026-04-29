using System.Collections.Generic;
using System.Linq;

namespace LMLocal.Infrastructure.Lm
{
    internal class MessageContext
    {
        public LMLocal.Models.ChatMessage[] Input { get; }
        public string SystemPrompt { get; }

        public MessageContext(IEnumerable<LMLocal.Models.ChatMessage> input, string systemPrompt = null)
        {
            Input = input.ToArray();
            SystemPrompt = systemPrompt;
        }
    }
}
