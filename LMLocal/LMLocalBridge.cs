using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Internal;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LMLocalBridgeNamespace
{
    /// <summary>
    /// Bridge class for communication between WebView2 and backend logic.
    /// </summary>
    [ComVisible(true)]
    public class LMLocalBridge
    {
        // Single shared HttpClient — Timeout managed via CancellationToken per request
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };

        private readonly LMStudioClient _lmStudioClient;
        private readonly WebView2ScriptExecutor _scriptExecutor;
        private readonly ChatGenerationService _chatService;

        private const string SystemPrompt = "You are a helpful coding assistant inside Visual Studio.";

        /// <summary>
        /// Initializes a new instance of the <see cref="LMLocalBridge"/> class.
        /// </summary>
        public LMLocalBridge(Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            _scriptExecutor = new WebView2ScriptExecutor(webView);
            var historyManager = new ChatHistoryManager(SystemPrompt);
            var compactor = new HistoryCompactor(historyManager, SystemPrompt, async type =>
            {
                await _scriptExecutor.PostMessageAsJsonAsync(
                    new WebView2ScriptMessage { Type = type, Payload = "" });
            });
            _lmStudioClient = new LMStudioClient(HttpClient);
            _chatService = new ChatGenerationService(_lmStudioClient, historyManager, compactor);

            InternalLogger.Info("LMLocalBridge initialized");
        }

        /// <summary>
        /// Gets the status of the LM Studio backend.
        /// </summary>
        public async Task<string> GetStatusAsync()
        {
            var result = new GetStatusResponse();
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var root = await _lmStudioClient.GetModelsAsync(cts.Token).ConfigureAwait(false);

                    if (!(root["models"] is JArray models) || !models.Any())
                    {
                        result.Status = "ERROR";
                        result.ErrorMessage = "No models found";
                        return JsonConvert.SerializeObject(result);
                    }

                    var loadedLlm = models.FirstOrDefault(m =>
                        (string)m["type"] == "llm" &&
                        m["loaded_instances"] is JArray instances &&
                        instances.Any());

                    if (loadedLlm == null)
                    {
                        result.Status = "ERROR";
                        result.ErrorMessage = "No LLM model is currently loaded";
                        return JsonConvert.SerializeObject(result);
                    }

                    var instance = loadedLlm["loaded_instances"][0];

                    var capabilities = loadedLlm["capabilities"];
                    if (capabilities != null)
                    {
                        if (capabilities["reasoning"] is JObject reasoningToken)
                        {
                            if (result.Reasoning == null) result.Reasoning = new GetStatusResponse.ReasoningInfo();
                            result.Reasoning.Default = reasoningToken.Value<string>("default");
                            result.Reasoning.AllowedOptions = reasoningToken["allowed_options"]?.Values<string>()?.ToList() ?? new List<string>();
                        }
                    }
                    result.Status = "SUCCESS";

                    result.ModelName = loadedLlm["display_name"]?.ToString() ?? loadedLlm["key"]?.ToString();

                    result.MaxContext = instance["config"]?["context_length"]?.Value<int>()
                                        ?? loadedLlm["max_context_length"]?.Value<int>()
                                        ?? 16384;

                    _chatService.SetMaxContext(result.MaxContext);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error("GetStatusAsync failed", ex);
                result.Status = "ERROR";
                result.ErrorMessage = "LM Studio unreachable: " + ex.Message;
            }

            return JsonConvert.SerializeObject(result);
        }

        /// <summary>
        /// Executes the provided prompt against LM Studio and streams the response to WebView2.
        /// </summary>
        public async Task ExecutePromptAsync(string prompt)
        {
            InternalLogger.Info($"ExecutePromptAsync start (len={prompt?.Length})");
            try
            {
                await _chatService.GenerateStreamAsync(
                    prompt,
                    onChunk: async (streamChunk, stats) =>
                    {
                        var msgType = streamChunk.Kind == ChunkKind.Reasoning
                            ? WebView2MessageType.StreamThought
                            : WebView2MessageType.StreamContent;

                        await _scriptExecutor.PostMessageAsJsonAsync(
                            new WebView2ScriptMessageWithCount()
                            {
                                Type = msgType,
                                Payload = streamChunk.Text,
                                Count = stats.TotalTokens,
                                TokensPerSecond = stats.TokensPerSecond
                            }
                        ).ConfigureAwait(false);
                    },
                    onError: async (error) =>
                    {
                        await _scriptExecutor.PostMessageAsJsonAsync(
                            new WebView2ScriptMessage() { Type = WebView2MessageType.StreamError, Payload = error }
                        );
                    }
                ).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected — no error to report
                InternalLogger.Info("ExecutePromptAsync canceled");
            }
            catch (Exception ex)
            {
                InternalLogger.Error("ExecutePromptAsync failed", ex);
                await _scriptExecutor.PostMessageAsJsonAsync(
                    new WebView2ScriptMessage() { Type = WebView2MessageType.StreamError, Payload = ex.Message }
                );
            }
            finally
            {
                await _scriptExecutor.PostMessageAsJsonAsync(
                    new WebView2ScriptMessage() { Type = WebView2MessageType.StreamEnd, Payload = "" }
                );
            }
        }


        /// <summary>
        /// Resets the chat history (async wrapper). Returns true if successful, false if generation is in progress.
        /// </summary>
        public Task<bool> ResetHistoryAsync()
        {
            InternalLogger.Info("ResetHistoryAsync called");
            return Task.FromResult(_chatService.ResetHistory());
        }

        /// <summary>
        /// Stops the current text generation process (async wrapper).
        /// </summary>
        public Task StopExecutionAsync()
        {
            InternalLogger.Info("StopExecutionAsync called");
            _chatService.StopExecution();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Copies the specified text to the clipboard.
        /// </summary>
        public async Task<bool> CopyToClipboardAsync(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return false;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex)
            {
                InternalLogger.Error("CopyToClipboardAsync failed", ex);
                return false;
            }
        }
    }
}
