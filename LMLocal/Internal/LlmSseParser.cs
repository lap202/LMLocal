using Newtonsoft.Json.Linq;

namespace LMLocal.Internal
{
    /// <summary>
    /// A stateless parser for Server-Sent Events (SSE) and JSON streams from local LLMs.
    /// Extracted to adhere to the Single Responsibility Principle, allowing fast, allocation-light delta extraction.
    /// This structure allows for independent unit testing of parsing logic without involving streams.
    /// </summary>
    internal static class LlmSseParser
    {
        /// <summary>
        /// Parses a single SSE JSON line, extracting the delta content and calculating or extracting token usage.
        /// </summary>
        /// <param name="line">A single line from the network stream.</param>
        /// <param name="tokens">A reference to the current token count to be updated.</param>
        /// <returns>The newly generated text chunk (delta), or null if the line contains no delta content.</returns>
        public static string ExtractDelta(string line, ref int tokens)
        {
            if (line == "data: [DONE]" || !line.StartsWith("data: ")) return null;

            var dataJson = line.Substring(6).Trim();
            var json = JObject.Parse(dataJson);

            // Update tokens (if present in the chunk)
            if (json["usage"] != null)
            {
                tokens = json["usage"]["total_tokens"]?.Value<int>() ?? tokens;
            }

            // Safe navigation through JSON to get the delta content
            if (!(json["choices"] is JArray choices) || choices.Count == 0)
                return null;
            var delta = choices[0]?["delta"]?["content"]?.ToString();
            //TODO: add separatly reasoning_content

            if (!string.IsNullOrEmpty(delta) && json["usage"] == null)
            {
                tokens++;
            }

            return delta;
        }
    }
}
