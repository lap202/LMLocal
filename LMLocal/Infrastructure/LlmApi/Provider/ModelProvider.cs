namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Enumeration of supported model providers/backends.
    /// </summary>
    internal enum ModelProvider
    {
        /// <summary>
        /// LM Studio backend
        /// </summary>
        LmStudio,

        /// <summary>
        /// OpenAI-compatible backend 
        /// </summary>
        OpenAi,

        /// <summary>
        /// Ollama backend 
        /// </summary>
        Ollama,

        /// <summary>
        /// Jan backend
        /// </summary>
        Jan
    }
}
