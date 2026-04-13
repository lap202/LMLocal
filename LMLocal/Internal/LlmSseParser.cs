using Newtonsoft.Json.Linq;

namespace LMLocal.Internal
{
    /// A stateless parser for Server-Sent Events (SSE) and JSON streams from local LLMs.
    internal static class LlmSseParser
    {

        /// Parses a single SSE JSON line, extracting both content and reasoning_content (if present)
        /// and calculating or extracting token usage.
        public static StreamChunk ExtractDelta(string line, ref int tokens)
        {
            if (line == "data: [DONE]" || !line.StartsWith("data: ")) return default;

            var dataJson = line.Substring(6).Trim();
            var json = JObject.Parse(dataJson);

            if (json["usage"] != null)
            {
                tokens = json["usage"]["total_tokens"]?.Value<int>() ?? tokens;
            }

            if (!(json["choices"] is JArray choices) || choices.Count == 0)
                return default;

            var content = choices[0]?["delta"]?["content"]?.ToString();
            var reasoning = choices[0]?["delta"]?["reasoning_content"]?.ToString();

            // If content present and tokens not provided in this chunk, increment
            if (!string.IsNullOrEmpty(content) && json["usage"] == null)
            {
                tokens++;
            }

            if (!string.IsNullOrEmpty(reasoning))
            {
                return new StreamChunk(reasoning, ChunkKind.Reasoning);
            }

            if (!string.IsNullOrEmpty(content))
            {
                return new StreamChunk(content, ChunkKind.Content);
            }

            return default;
        }
    }
}
