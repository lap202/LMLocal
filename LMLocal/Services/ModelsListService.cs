using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure.Api;
using LMLocal.Infrastructure.Api.Responses;

namespace LMLocal.Services
{
    /// <summary>
    /// Service for retrieving and managing models list from backend providers.
    /// Handles provider-specific logic and data aggregation.
    /// </summary>
    internal interface IModelsListService
    {
        Task<UnifiedListModelsResponse> ListModelsAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Implementation of models list retrieval service.
    /// Coordinates requests to different providers and merges results where needed.
    /// </summary>
    internal class ModelsListService : IModelsListService
    {
        private readonly IOpenApiAdapter _openApiAdapter;
        private readonly ISettingsManager _settingsManager;

        public ModelsListService(
            IOpenApiAdapter openApiAdapter,
            ISettingsManager settingsManager)
        {
            _openApiAdapter = openApiAdapter ?? throw new ArgumentNullException(nameof(openApiAdapter));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }

        public async Task<UnifiedListModelsResponse> ListModelsAsync(CancellationToken cancellationToken)
        {
            try
            {
                string baseUrl = GetBaseUrl();
                ModelProvider provider = ProviderResolver.ResolveProvider(baseUrl);
                string endpoint = ProviderResolver.GetListModelsEndpoint(provider);

                if (provider == ModelProvider.Ollama)
                {
                    return await GetOllamaModelsAsync(cancellationToken);
                }

                var json = await _openApiAdapter.ListModelsRawAsync(endpoint, cancellationToken);
                return ModelResponseConverter.ConvertToUnified(json, provider);
            }
            catch (Exception ex)
            {
                InternalLogger.Error("ListModelsAsync failed", ex);
                return new UnifiedListModelsResponse { Error = ex.Message };
            }
        }

        /// <summary>
        /// Retrieves Ollama models from both /api/ps (active) and /v1/models (all available).
        /// Merges results to provide complete list with active status indication.
        /// </summary>
        private async Task<UnifiedListModelsResponse> GetOllamaModelsAsync(CancellationToken cancellationToken)
        {
            var activeJson = await _openApiAdapter.ListModelsRawAsync("/api/ps", cancellationToken);
            var allJson = await _openApiAdapter.ListModelsRawAsync("/v1/models", cancellationToken);

            var activeModelsResponse = ModelResponseConverter.ConvertToUnified(activeJson, ModelProvider.Ollama);
            var allModelsResponse = ModelResponseConverter.ConvertToUnified(allJson, ModelProvider.OpenAi);

            return ModelResponseConverter.MergeOllamaModels(activeModelsResponse, allModelsResponse);
        }

        private string GetBaseUrl()
        {
            string url = _settingsManager.Current?.LmStudioBaseUrl;
            if (!string.IsNullOrEmpty(url))
                return url.TrimEnd('/');
            return "http://localhost:1234";
        }
    }
}
