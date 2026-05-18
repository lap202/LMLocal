namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Resolves the model provider and corresponding API endpoint.
    /// </summary>
    internal class ProviderResolver
    {
        /// <summary>
        /// Determines the model provider based on provider name string.
        /// </summary>
        public static ModelProvider ResolveProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
                return ModelProvider.LmStudio;

            switch (providerName.ToLowerInvariant())
            {
                case "lmstudio":
                    return ModelProvider.LmStudio;
                case "ollama":
                    return ModelProvider.Ollama;
                case "openai":
                    return ModelProvider.OpenAi;
                case "jan":
                    return ModelProvider.Jan;
                default:
                    return ModelProvider.LmStudio;
            }
        }

        /// <summary>
        /// Gets the API endpoint for listing models based on the provider type.
        /// </summary>
        public static string GetListModelsEndpoint(ModelProvider provider)
        {
            if (provider == ModelProvider.LmStudio)
                return ApiEndpoints.LmStudioListModels;
            else if (provider == ModelProvider.Ollama)
                return ApiEndpoints.OllamaRunningModels;
            else if (provider == ModelProvider.Jan)
                return ApiEndpoints.ListModels;
            else
                return ApiEndpoints.ListModels;
        }
    }
}
