using System.Collections.Generic;
using System.Linq;

namespace LMLocal.Infrastructure.Api
{
    internal class MessageContext
    {
        public Models.ChatMessage[] Input { get; }
        public string SystemPrompt { get; }

        public MessageContext(IEnumerable<Models.ChatMessage> input, string systemPrompt = null)
        {
            Input = input.ToArray();
            SystemPrompt = systemPrompt;
        }
    }
}
