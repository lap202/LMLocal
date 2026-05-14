using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LMLocal.Infrastructure.WebView
{
    /// <summary>
    /// Base message class for all WebView2 communication.
    /// </summary>
    internal class WebView2ScriptMessage
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public WebView2MessageType Type { get; set; }

        /// <summary>
        /// String payload (usually JSON-encoded content or error message).
        /// </summary>
        public string Payload { get; set; }
    }
}
