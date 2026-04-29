using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure.Lm.Requests;
using LMLocal.Infrastructure.Lm.Responses;
using LMLocal.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LMLocal.Infrastructure.Lm
{
    /// <summary>
    /// Client for communicating with the LM Studio backend API.
    /// </summary>
    internal interface ILMStudioClient
    {
        Task<ListModelsResponse> ListModelsAsync(CancellationToken cancellationToken);
        Task<SendChatResponse> SendChatAsync(MessageContext messageContext, ModelContext modelContext, CancellationToken cancellationToken);
        Task<StreamingResponse> SendChatStreamingAsync(MessageContext messageContext, ModelContext modelContext, CancellationToken cancellationToken);
    }


    internal class LMStudioClient : ILMStudioClient
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsManager _settingsManager;
        private readonly string _defaultBaseUrl;

        public LMStudioClient(HttpClient httpClient, string baseUrl = "http://localhost:1234", ISettingsManager settingsManager = null)
        {
            _httpClient = httpClient;
            _settingsManager = settingsManager;
            _defaultBaseUrl = baseUrl?.TrimEnd('/');
        }

        private string GetBaseUrl()
        {
            string url = _settingsManager?.Current?.LmStudioBaseUrl;
            if (!string.IsNullOrEmpty(url))
                return url.TrimEnd('/');
            return _defaultBaseUrl;
        }

        /// <summary>
        /// Gets list of available models from the LM Studio backend.
        /// </summary>
        public async Task<ListModelsResponse> ListModelsAsync(CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(GetBaseUrl() + "/api/v1/models", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var userMessage = TryExtractErrorMessage(json) ?? $"Failed to get models: {response.StatusCode}";
                    throw new HttpRequestException(userMessage);
                }

                return json.FromJson<ListModelsResponse>();
            }
        }

        /// <summary>
        /// Opens a streaming chat request and returns the response stream.
        /// The caller must dispose the returned <see cref="StreamingResponse"/>.
        /// </summary>
        public async Task<StreamingResponse> SendChatStreamingAsync(
            MessageContext messageContext,
            ModelContext modelContext,
            CancellationToken cancellationToken)
        {
            var openAiRequest = BuildRequest(messageContext, modelContext, stream: true, store: false);

            var content = new StringContent(openAiRequest.ToJson(), Encoding.UTF8, "application/json");
            HttpRequestMessage request = null;
            HttpResponseMessage response = null;
            bool success = false;

            try
            {
                request = new HttpRequestMessage(HttpMethod.Post, GetBaseUrl() + "/v1/chat/completions") { Content = content };
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var rawError = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var userMessage = TryExtractErrorMessage(rawError) ?? $"Failed to send chat request: {response.StatusCode}";
                    throw new HttpRequestException(userMessage);
                }

                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var streamingResponse = new StreamingResponse(stream, response, request, content);

                success = true;
                return streamingResponse;
            }
            finally
            {
                if (!success)
                {
                    response?.Dispose();
                    request?.Dispose();
                    content?.Dispose();
                }
            }
        }

        /// <summary>
        /// Sends chat request and returns the full response content.
        /// </summary>
        public async Task<SendChatResponse> SendChatAsync(
            MessageContext messageContext,
            ModelContext modelContext,
            CancellationToken cancellationToken)
        {
            var openAiRequest = BuildRequest(messageContext, modelContext, stream: false, store: false);

            using (var content = new StringContent(openAiRequest.ToJson(), Encoding.UTF8, "application/json"))
            using (var request = new HttpRequestMessage(HttpMethod.Post, GetBaseUrl() + "/v1/chat/completions") { Content = content })
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var userMessage = TryExtractErrorMessage(json) ?? $"Generation failed: {response.StatusCode}";
                    throw new HttpRequestException(userMessage);
                }

                try
                {
                    return json.FromJson<SendChatResponse>();
                }
                catch (JsonException ex)
                {
                    InternalLogger.Error("SendChatAsync: failed to parse response JSON", ex);
                    return null;
                }
            }
        }

        private SendChatRequest BuildRequest(
            MessageContext messageContext,
            ModelContext modelContext,
            bool stream, bool store)
        {
            var messages = new List<Message>();

            foreach (var msg in messageContext.Input)
            {
                messages.Add(new Message
                {
                    Role = msg.Role,
                    Content = msg.Content
                });
            }

            return new SendChatRequest
            {
                Model = modelContext.ModelId,
                Messages = messages,
                Stream = stream,
                Store = store,
                Temperature = modelContext.Temperature,
                TopP = modelContext.TopP,
                MaxCompletionTokens = modelContext.MaxOutputTokens,
                PresencePenalty = modelContext.PresencePenalty,
                FrequencyPenalty = modelContext.FrequencyPenalty,
                ReasoningEffort = modelContext.Reasoning,
                StreamOptions = stream ? new StreamOptions { IncludeUsage = stream } : null // Only set this when you set stream: true.
            };
        }

        internal static string TryExtractErrorMessage(string rawResponse)
        {
            try
            {
                var parsed = JObject.Parse(rawResponse);
                return parsed["error"]?["message"]?.ToString();
            }
            catch (JsonException ex)
            {
                InternalLogger.Warn($"TryExtractErrorMessage: invalid JSON response: {ex.Message}");
                return null;
            }
        }
    }
}
