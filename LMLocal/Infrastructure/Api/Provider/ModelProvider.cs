namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Enumeration of supported model providers/backends.
    /// </summary>
    internal enum ModelProvider
    {
        /// <summary>
        /// LM Studio backend (supports max context length, loaded instances, capabilities).
        /// Typically runs on port 1234.
        /// </summary>
        LmStudio,

        /// <summary>
        /// OpenAI-compatible backend (minimal model info: id, object, created, owned_by).
        /// Default for unknown providers.
        /// </summary>
        OpenAi,

        /// <summary>
        /// Ollama backend (Ollama-specific API format).
        /// Typically runs on port 11434.
        /// </summary>
        Ollama
    }
}
