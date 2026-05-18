namespace LMLocal.Models
{
    public class TestConnectionRequest
    {
        /// <summary>
        /// Provider name: "lmstudio", "ollama", or "openai"
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Base URL of the provider backend (e.g., "http://localhost:1234")
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Optional API key for authentication
        /// </summary>
        public string ApiKey { get; set; }
    }
}
