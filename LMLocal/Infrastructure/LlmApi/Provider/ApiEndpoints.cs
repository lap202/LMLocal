namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Standard API endpoints used across providers.
    /// </summary>
    internal static class ApiEndpoints
    {
        /// <summary>
        /// LM Studio-specific endpoint for listing models.
        /// </summary>
        public const string LmStudioListModels = "/api/v1/models";

        /// <summary>
        /// Standard OpenAI-compatible endpoint for listing models.
        /// </summary>
        public const string ListModels = "/v1/models";

        /// <summary>
        /// OpenAI-compatible endpoint for chat completions.
        /// </summary>
        public const string ChatCompletions = "/v1/chat/completions";

        /// <summary>
        /// Ollama-specific endpoint for listing running models.
        /// </summary>
        public const string OllamaRunningModels = "/api/ps";
    }
}
