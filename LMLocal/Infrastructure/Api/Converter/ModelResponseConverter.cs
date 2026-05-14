using System;
using System.Collections.Generic;
using LMLocal.Common;
using LMLocal.Infrastructure.Api.Responses;
using Newtonsoft.Json;

namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Converts provider-specific API responses to unified model information format.
    /// Handles LM Studio, OpenAI-compatible, and Ollama backends.
    /// </summary>
    internal static class ModelResponseConverter
    {
        /// <summary>
        /// Converts raw JSON response to unified format based on provider type.
        /// Delegates to provider-specific conversion methods.
        /// </summary>
        public static UnifiedListModelsResponse ConvertToUnified(string json, ModelProvider provider)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new UnifiedListModelsResponse { Models = new List<UnifiedModelInfo>() };
            }

            try
            {
                if (provider == ModelProvider.LmStudio)
                    return ConvertLmStudioResponseToUnified(json);
                else if (provider == ModelProvider.Ollama)
                    return ConvertOllamaResponseToUnified(json);
                else
                    return ConvertOpenAiResponseToUnified(json);
            }
            catch (JsonException ex)
            {
                InternalLogger.Warn($"ConvertToUnified failed for provider {provider}: {ex.Message}");
                return new UnifiedListModelsResponse { Models = new List<UnifiedModelInfo>() };
            }
        }

        public static UnifiedListModelsResponse ConvertLmStudioResponseToUnified(string json)
        {
            try
            {
                var lmStudioResponse = json.FromJson<LmStudioModelsResponse>();
                if (lmStudioResponse?.Models == null || lmStudioResponse.Models.Count == 0)
                {
                    return new UnifiedListModelsResponse { Error = "No models returned from LM Studio" };
                }

                var activeModels = new List<UnifiedModelInfo>();
                var inactiveModels = new List<UnifiedModelInfo>();

                foreach (var model in lmStudioResponse.Models)
                {
                    if (model.Type != "llm")
                        continue;

                    var isLoaded = model.LoadedInstances != null && model.LoadedInstances.Count > 0;
                    var instance = isLoaded ? model.LoadedInstances[0] : null;
                    var maxTokens = instance?.Config?.ContextLength ?? model.MaxContextLength;

                    var unifiedModel = new UnifiedModelInfo
                    {
                        Id = instance?.Id ?? model.Key,
                        Name = model.DisplayName ?? model.Key,
                        MaxTokens = maxTokens > 0 ? maxTokens : (int?)null,
                        SupportsMaxTokens = maxTokens > 0,
                        IsActive = isLoaded,
                        SupportsToolUse = model.Capabilities?.TrainedForToolUse
                    };

                    if (isLoaded)
                    {
                        activeModels.Add(unifiedModel);
                    }
                    else
                    {
                        inactiveModels.Add(unifiedModel);
                    }
                }

                activeModels.Sort((a, b) => string.Compare(a?.Id, b?.Id, StringComparison.OrdinalIgnoreCase));
                inactiveModels.Sort((a, b) => string.Compare(a?.Id, b?.Id, StringComparison.OrdinalIgnoreCase));


                var sortedModels = new List<UnifiedModelInfo>(activeModels.Count + inactiveModels.Count);
                sortedModels.AddRange(activeModels);
                sortedModels.AddRange(inactiveModels);

                var result = new UnifiedListModelsResponse
                {
                    Models = sortedModels
                };

                return result;
            }
            catch (JsonException ex)
            {
                InternalLogger.Warn($"ConvertLmStudioResponseToUnified: failed to parse LM Studio response: {ex.Message}");
                return new UnifiedListModelsResponse { Error = "Failed to parse LM Studio response" };
            }
        }

        /// <summary>
        /// Converts Ollama /api/ps response to unified format.
        /// All models returned by /api/ps are considered active (loaded).
        /// </summary>
        public static UnifiedListModelsResponse ConvertOllamaResponseToUnified(string json)
        {
            try
            {
                var ollamaResponse = json.FromJson<OllamaPsResponse>();
                if (ollamaResponse?.Models == null || ollamaResponse.Models.Count == 0)
                {
                    return new UnifiedListModelsResponse { Error = "No models loaded in Ollama" };
                }

                var result = new UnifiedListModelsResponse { Models = new List<UnifiedModelInfo>() };

                foreach (var ollamaModel in ollamaResponse.Models)
                {
                    int? maxTokens = ollamaModel.ContextLength > 0 ? ollamaModel.ContextLength : (int?)null;

                    var unifiedModel = new UnifiedModelInfo
                    {
                        Id = ollamaModel.Name ?? ollamaModel.Model ?? "unknown",
                        Name = ollamaModel.Name,
                        MaxTokens = maxTokens,
                        SupportsMaxTokens = maxTokens.HasValue && maxTokens > 0,
                        IsActive = true  
                    };

                    result.Models.Add(unifiedModel);
                }

                return result;
            }
            catch (JsonException ex)
            {
                InternalLogger.Warn($"ConvertOllamaResponseToUnified: failed to parse Ollama response: {ex.Message}");
                return new UnifiedListModelsResponse { Error = "Failed to parse Ollama response" };
            }
        }

        /// <summary>
        /// Converts OpenAI-compatible /v1/models response to unified format.
        /// OpenAI doesn't provide active model info, so HasActiveModel is always false.
        /// </summary>
        public static UnifiedListModelsResponse ConvertOpenAiResponseToUnified(string json)
        {
            try
            {
                var openAiResponse = json.FromJson<ListModelsResponse>();
                if (openAiResponse?.Data == null)
                {
                    return new UnifiedListModelsResponse { Error = "No models returned from OpenAI-compatible backend" };
                }

                var result = new UnifiedListModelsResponse { Models = new List<UnifiedModelInfo>() };

                foreach (var model in openAiResponse.Data)
                {
                    var unifiedModel = new UnifiedModelInfo
                    {
                        Id = model.Id,
                        Name = null,
                        MaxTokens = null,
                        SupportsMaxTokens = false,
                        IsActive = false
                    };

                    result.Models.Add(unifiedModel);
                }

                return result;
            }
            catch (JsonException ex)
            {
                InternalLogger.Warn($"ConvertOpenAiResponseToUnified: failed to parse OpenAI response: {ex.Message}");
                return new UnifiedListModelsResponse { Error = "Failed to parse OpenAI-compatible response" };
            }
        }

        /// <summary>
        /// Merges Ollama models from two sources: active models (from /api/ps) and all available models (from /v1/models).
        /// Active models retain their metadata (context length), inactive models are marked accordingly.
        /// </summary>
        public static UnifiedListModelsResponse MergeOllamaModels(
            UnifiedListModelsResponse activeModelsResponse,
            UnifiedListModelsResponse allModelsResponse)
        {
            if ((activeModelsResponse?.Models == null || activeModelsResponse.Models.Count == 0) &&
                (allModelsResponse?.Models == null || allModelsResponse.Models.Count == 0))
            {
                return new UnifiedListModelsResponse { Error = "No models available in Ollama" };
            }

            var mergedModels = new List<UnifiedModelInfo>();
            var processedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (activeModelsResponse?.Models != null)
            {
                foreach (var model in activeModelsResponse.Models)
                {
                    mergedModels.Add(model);
                    processedIds.Add(model.Id);
                }
            }

            if (allModelsResponse?.Models != null)
            {
                foreach (var model in allModelsResponse.Models)
                {
                    if (!processedIds.Contains(model.Id))
                    {
                        var inactiveModel = new UnifiedModelInfo
                        {
                            Id = model.Id,
                            Name = model.Name,
                            MaxTokens = model.MaxTokens,
                            SupportsMaxTokens = model.SupportsMaxTokens,
                            IsActive = false,
                            SupportsToolUse = model.SupportsToolUse
                        };
                        mergedModels.Add(inactiveModel);
                        processedIds.Add(model.Id);
                    }
                }
            }

            mergedModels.Sort((a, b) =>
            {
                if (a.IsActive && !b.IsActive) return -1;
                if (!a.IsActive && b.IsActive) return 1;
                return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });

            var result = new UnifiedListModelsResponse
            {
                Models = mergedModels,
                HasActiveModel = activeModelsResponse?.Models != null && activeModelsResponse.Models.Count > 0,
                ActiveModel = activeModelsResponse?.Models != null && activeModelsResponse.Models.Count > 0 
                    ? activeModelsResponse.Models[0] 
                    : null
            };

            return result;
        }
    }
}
