using LMLocal.Internal;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

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
        }

        /// <summary>
        /// Gets the status of the LM Studio backend.
        /// </summary>
        public async Task<string> GetStatusAsync()
        {
            var result = new ConnectionResponse();
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
            try
            {
                await _chatService.GenerateStreamAsync(
                    prompt,
                    onChunk: async (delta, stats) =>
                    {
                        string escaped = delta.Replace("'", "\\'");
                        await _scriptExecutor.PostMessageAsJsonAsync(
                            new WebView2ScriptMessageWithCount() { Type = WebView2MessageType.ChatChunk, Payload = escaped, Count = stats.TotalTokens, TokensPerSecond = stats.TokensPerSecond }
                        );
                    },
                    onError: async (error) =>
                    {
                        string escaped = error.Replace("'", "\\'");
                        await _scriptExecutor.PostMessageAsJsonAsync(
                            new WebView2ScriptMessage() { Type = WebView2MessageType.Error, Payload = escaped }
                        );
                    }
                ).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected — no error to report
            }
            catch (Exception ex)
            {
                await _scriptExecutor.PostMessageAsJsonAsync(
                    new WebView2ScriptMessage() { Type = WebView2MessageType.Error, Payload = ex.Message }
                );
            }
            finally
            {
                await _scriptExecutor.PostMessageAsJsonAsync(
                    new WebView2ScriptMessage() { Type = WebView2MessageType.ChatComplete, Payload = "" }
                );
            }
        }


        /// <summary>
        /// Resets the chat history (async wrapper). Returns true if successful, false if generation is in progress.
        /// </summary>
        public Task<bool> ResetHistoryAsync()
        {
            return Task.FromResult(_chatService.ResetHistory());
        }

        /// <summary>
        /// Stops the current text generation process (async wrapper).
        /// </summary>
        public Task StopExecutionAsync()
        {
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
            catch
            {
                return false;
            }
        }
    }
}
