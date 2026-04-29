using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure.Lm;
using LMLocal.Infrastructure.Vs;
using LMLocal.Models;
using LMLocal.Services;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace LMLocal.Infrastructure.WebView
{
    public interface IWebViewBridge
    {
        Task<bool> CopyToClipboardAsync(string text);
        Task ExecutePromptAsync(string requestJson);
        Task<string> GetSettingsAsync();
        Task<string> GetStatusAsync();
        Task<bool> ResetHistoryAsync();
        Task StopExecutionAsync();
        Task<bool> UpdateSettingsAsync(string newSettingsJson);
        Task<string> GetInstructionsAsync();
        Task<bool> UpdateInstructionsAsync(string newInstructionsJson);
    }

    /// <summary>
    /// Bridge class for communication between WebView2 and backend logic.
    /// </summary>
    [ComVisible(true)]
    public class WebViewBridge : IWebViewBridge
    {
        // Single shared HttpClient — Timeout managed via CancellationToken per request
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private readonly ILMStudioClient _lmStudioClient;
        private readonly WebViewScriptExecutor _scriptExecutor;
        private readonly ChatGenerationService _chatService;
        private readonly InstructionsManager _instructionsManager;
        private readonly ISettingsManager _settingsManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebViewBridge"/> class.
        /// </summary>
        public WebViewBridge(Microsoft.Web.WebView2.Wpf.WebView2 webView, ISettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _scriptExecutor = new WebViewScriptExecutor(webView);

            var persistence = new ChatPersistenceService(settingsManager);
            var historyManager = new ChatHistoryManager(Defaults.DefaultSystemPrompt, persistence, settingsManager);
            var compactor = new HistoryCompactor(historyManager, async type =>
            {
                await _scriptExecutor.PostMessageAsJsonAsync(
                    new WebView2ScriptMessage { Type = type, Payload = "" });
            }, settingsManager);

            _lmStudioClient = new LMStudioClient(HttpClient, settingsManager.Current.LmStudioBaseUrl, settingsManager);


            _chatService = new ChatGenerationService(_lmStudioClient, historyManager, compactor, settingsManager);
            _instructionsManager = new InstructionsManager();

            InternalLogger.Info("LMLocalBridge initialized");
        }

        /// <summary>
        /// Gets the status of the LM Studio backend.
        /// </summary>
        public async Task<string> GetStatusAsync()
        {
            var result = new GetStatusResponse();
            var inactivityTimeout = _settingsManager.Current.StreamInactivityTimeoutSeconds == 0 ? 15 : _settingsManager.Current.StreamInactivityTimeoutSeconds;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(inactivityTimeout)))
                {
                    var response = await _lmStudioClient.ListModelsAsync(cts.Token).ConfigureAwait(false);
                    if (!response.Models.Any())
                    {
                        result.Status = "ERROR";
                        result.ErrorMessage = "No models found";
                        return result.ToJson();
                    }

                    var model = response.Models.FirstOrDefault(m => m.Type == "llm" && m.LoadedInstances.Any());
                    if (model == null)
                    {
                        result.Status = "ERROR";
                        result.ErrorMessage = "No LLM model is currently loaded";
                        return result.ToJson();
                    }

                    var instance = model.LoadedInstances[0];

                    result.Status = "SUCCESS";

                    result.ModelName = model.DisplayName ?? model.Key;
                    result.ModelId = instance.Id;

                    result.MaxContext = instance.Config.ContextLength;

                    result.ModelDetails = JObject.FromObject(model);

                    _chatService.SetMaxContext(instance.Config.ContextLength);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error("GetStatusAsync failed", ex);
                result.Status = "ERROR";
                result.ErrorMessage = "LM Studio unreachable: " + ex.Message;
            }

            return result.ToJson();
        }

        /// <summary>
        /// Executes the provided prompt against LM Studio and streams the response to WebView2.
        /// </summary>
        public async Task ExecutePromptAsync(string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                InternalLogger.Error("ExecutePromptAsync: requestJson is null or empty");
                return;
            }

            ExecutePromptRequest request = requestJson.FromJson<ExecutePromptRequest>();

            if (request == null)
            {
                InternalLogger.Error("ExecutePromptAsync: deserialized request is null");
                return;
            }

            try
            {
                var context = new GenerateStreamContext
                {
                    Prompt = request.Prompt,
                    ActiveDocumentContent = request.IncludeContent ? await new ActiveDocumentProvider().GetContentAsync() : null,
                    AdditionalPrompt = request.AdditionalPrompt,
                    ModelId = request.ModelId
                };

                await _chatService.GenerateStreamAsync(
                    context,
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
                    },
                    onEnd: async () =>
                    {
                        await _scriptExecutor.PostMessageAsJsonAsync(
                            new WebView2ScriptMessage() { Type = WebView2MessageType.StreamEnd, Payload = "" }
                        );
                    }
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                InternalLogger.Error("ExecutePromptAsync failed", ex);
            }
        }


        /// <summary>
        /// Resets the chat history (async wrapper). Returns true if successful, false if generation is in progress.
        /// </summary>
        public async Task<bool> ResetHistoryAsync()
        {
            InternalLogger.Info("ResetHistoryAsync called");
            return await _chatService.ResetHistoryAsync().ConfigureAwait(false);
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

        public Task<string> GetSettingsAsync()
        {
            try
            {
                return Task.FromResult(_settingsManager.Current.ToJson());
            }
            catch (Exception ex)
            {
                InternalLogger.Error("GetSettingsAsync failed", ex);
                return Task.FromResult<string>(null);
            }
        }

        public async Task<bool> UpdateSettingsAsync(string newSettingsJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newSettingsJson))
                {
                    return false;
                }

                var newSettings = newSettingsJson.FromJson<AppSettings>();

                await _settingsManager.SaveAsync(newSettings).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                InternalLogger.Error("UpdateSettingsAsync failed", ex);
                return false;
            }
        }

        public async Task<string> GetInstructionsAsync()
        {
            try
            {
                return await _instructionsManager.GetAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                InternalLogger.Error("GetInstructionsAsync failed", ex);
                return "{}";
            }
        }

        public async Task<bool> UpdateInstructionsAsync(string newInstructionsJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newInstructionsJson))
                {
                    return false;
                }


                await _instructionsManager.UpdateAsync(newInstructionsJson).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                InternalLogger.Error("UpdateInstructionsAsync failed", ex);
                return false;
            }
        }
    }
}
