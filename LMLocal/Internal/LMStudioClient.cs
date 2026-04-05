using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Internal
{
    internal interface ILMStudioClient
    {
        Task<JObject> GetModelsAsync(CancellationToken cancellationToken);
        Task<StreamingResponse> SendChatRequestAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken);
        Task<string> SendNonStreamingAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Client for communicating with the LM Studio backend API.
    /// </summary>
    internal class LMStudioClient : ILMStudioClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="LMStudioClient"/> class.
        /// </summary>
        public LMStudioClient(HttpClient httpClient, string baseUrl = "http://localhost:1234")
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl?.TrimEnd('/');
        }

        /// <summary>
        /// Gets the available models from the LM Studio backend.
        /// </summary>
        public async Task<JObject> GetModelsAsync(CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(_baseUrl + "/api/v1/models", cancellationToken).ConfigureAwait(false))
            {
                var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var userMessage = TryExtractErrorMessage(raw) ?? $"Failed to get models: {response.StatusCode}";
                    throw new HttpRequestException(userMessage);
                }

                return JObject.Parse(raw);
            }
        }

        /// <summary>
        /// Opens a streaming chat request and returns the response stream.
        /// The caller must dispose the returned <see cref="StreamingResponse"/>.
        /// </summary>
        public async Task<StreamingResponse> SendChatRequestAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = "local-model",
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                temperature = 0.7,
                stream = true,
                stream_options = new { include_usage = true }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            HttpRequestMessage request = null;
            HttpResponseMessage response = null;
            bool success = false;

            try
            {
                request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/chat/completions") { Content = content };
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
        /// Sends a non-streaming chat request and returns the full response content.
        /// </summary>
        public async Task<string> SendNonStreamingAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = "local-model",
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                temperature = 0.3,
                stream = false
            };

            using (var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"))
            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/chat/completions") { Content = content })
            using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var userMessage = TryExtractErrorMessage(json) ?? $"Generation failed: {response.StatusCode}";
                    throw new HttpRequestException(userMessage);
                }

                try
                {
                    var parsed = JObject.Parse(json);
                    return parsed["choices"]?[0]?["message"]?["content"]?.ToString();
                }
                catch (JsonException)
                {
                    return null; // Handle unparsable response gracefully
                }
            }
        }

        internal static string TryExtractErrorMessage(string rawResponse)
        {
            try
            {
                var parsed = JObject.Parse(rawResponse);
                return parsed["error"]?["message"]?.ToString();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public class ConnectionResponse
    {
        public string Status { get; set; }
        public string ModelName { get; set; }
        public int MaxContext { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Owns the HTTP response and its stream for a streaming chat request.
    /// </summary>
    public sealed class StreamingResponse : IDisposable
    {
        private readonly HttpResponseMessage _response;
        private readonly HttpRequestMessage _request;
        private readonly StringContent _content;
        private bool _disposed;

        public Stream Stream { get; }

        public StreamingResponse(Stream stream, HttpResponseMessage response, HttpRequestMessage request, StringContent content)
        {
            Stream = stream;
            _response = response;
            _request = request;
            _content = content;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stream.Dispose();
            _response.Dispose();
            _request.Dispose();
            _content.Dispose();
        }
    }

    public class ChatMessage
    {
        public string Role { get; }
        public string Content { get; }
        public ChatMessage(string role, string content) => (Role, Content) = (role, content);
    }
}
