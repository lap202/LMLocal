using System;
using System.Deployment.Internal;
using LMLocal.Common;

namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Resolves the model provider and corresponding API endpoint based on the base URL.
    /// Encapsulates provider detection logic and endpoint selection.
    /// </summary>
    internal class ProviderResolver
    {
        /// <summary>
        /// Determines the model provider based on the base URL port number.
        /// Heuristic matching:
        /// - Port 1234: LM Studio
        /// - Port 11434: Ollama
        /// - Other ports: OpenAI-compatible (default)
        /// </summary>
        public static ModelProvider ResolveProvider(string baseUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(baseUrl))
                    return ModelProvider.OpenAi;

                var uri = new Uri(baseUrl);
                int port = uri.Port;

                if (port == 1234)
                    return ModelProvider.LmStudio;
                else if (port == 11434)
                    return ModelProvider.Ollama;
                else
                    return ModelProvider.OpenAi;
            }
            catch
            {
                InternalLogger.Warn($"Failed to parse base URL '{baseUrl}'. Defaulting to OpenAI provider.");
                return ModelProvider.OpenAi;
            }
        }

        /// <summary>
        /// Gets the API endpoint for listing models based on the provider type.
        /// </summary>
        public static string GetListModelsEndpoint(ModelProvider provider)
        {
            if (provider == ModelProvider.LmStudio)
                return "/api/v1/models";
            else if (provider == ModelProvider.Ollama)
                return "/api/ps";
            else
                return "/v1/models";
        }
    }
}
