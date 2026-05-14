using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure.Api;
using LMLocal.Infrastructure.Api.Responses;
using LMLocal.Infrastructure.Vs.Implementations;
using LMLocal.Models;
using LMLocal.Services;
using LMLocal.Services.ChatSession;
using Microsoft.VisualStudio.Shell;

namespace LMLocal.Infrastructure.WebView
{
    /// <summary>
    /// Bridge class for communication between WebView2 and backend logic.
    /// </summary>
    public interface IWebViewBridge
    {
        Task<bool> CopyToClipboardAsync(string text);
        Task ExecutePromptAsync(string requestJson);
        Task<string> GetSettingsAsync();
        Task<string> ListModelsAsync();
        Task<bool> SetActiveModelAsync(string modelId, int contextLength);
        Task<bool> ResetHistoryAsync();
        Task StopExecutionAsync();
        Task<bool> UpdateSettingsAsync(string newSettingsJson);
        Task<string> GetInstructionsAsync();
        Task<bool> UpdateInstructionsAsync(string newInstructionsJson);
    }



    [ComVisible(true)]
    public class WebViewBridge : IWebViewBridge
    {
        private readonly IModelsListService _modelsListService;
        private readonly IWebViewScriptExecutor _scriptExecutor;
        private readonly InstructionsManager _instructionsManager;
        private readonly ISettingsManager _settingsManager;
        private readonly IActiveDocumentTool _activeDocumentTool;
        private readonly ISessionManager _sessionManager;
        private readonly IActiveModelContext _activeModelContext;
        private readonly IChatHistoryManager _chatHistoryManager;

        internal WebViewBridge(
            ISettingsManager settingsManager,
            IModelsListService modelsListService,
            IWebViewScriptExecutor scriptExecutor,
            IActiveDocumentTool activeDocumentTool,
            ISessionManager sessionManager,
            IActiveModelContext activeModelContext,
            IChatHistoryManager chatHistoryManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _scriptExecutor = scriptExecutor ?? throw new ArgumentNullException(nameof(scriptExecutor));
            _modelsListService = modelsListService ?? throw new ArgumentNullException(nameof(modelsListService));
            _activeDocumentTool = activeDocumentTool ?? throw new ArgumentNullException(nameof(activeDocumentTool));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _activeModelContext = activeModelContext ?? throw new ArgumentNullException(nameof(activeModelContext));
            _chatHistoryManager = chatHistoryManager ?? throw new ArgumentNullException(nameof(chatHistoryManager));
            _instructionsManager = new InstructionsManager();
        }

        /// <summary>
        /// Returns list of available models from different providers and activeModel if any.
        /// </summary>
        public async Task<string> ListModelsAsync()
        {
            var requestTimeout = _settingsManager.RequestTimeoutSeconds;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeout)))
                {
                    var response = await _modelsListService.ListModelsAsync(cts.Token).ConfigureAwait(false);
                    if (response != null)
                    {
                        ApplyCurrentActiveModel(response, _activeModelContext.CurrentModelId);
                    }

                    return response == null ? "{}" : response.ToJson();
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error("ListModelsAsync failed", ex);
                return new { Error = "Failed to list models: " + ex.Message }.ToJson();
            }
        }

        private void ApplyCurrentActiveModel(UnifiedListModelsResponse response, string currentModelId)
        {
            if (response?.Models == null || response.Models.Count == 0)
                return;

            if (string.IsNullOrEmpty((string)currentModelId))
                return;

            var currentModel = response.Models.FirstOrDefault(m => m.Id == currentModelId);
            if (currentModel != null)
            {
                response.ActiveModel = currentModel;
                response.HasActiveModel = true;
            }
        }

        /// <summary>
        /// Sets the active model and its max context length. If contextLength is not provided or <= 0, defaults to 16384.
        /// </summary>
        public Task<bool> SetActiveModelAsync(string modelId, int contextLength)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(modelId)) return Task.FromResult(false);

                var maxContext = contextLength <= 0 ? 16384 : contextLength;
                _activeModelContext.SetActiveModel(modelId, maxContext);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                InternalLogger.Error("SetActiveModelAsync failed", ex);
                return Task.FromResult(false);
            }
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
                    ActiveDocumentContent = request.IncludeContent ? await _activeDocumentTool.GetContentAsync() : null,
                    AdditionalPrompt = request.AdditionalPrompt,
                    ModelId = request.ModelId
                };

                async Task OnMessageAsync(WebView2ScriptMessage message)
                {
                    await _scriptExecutor.PostMessageAsJsonAsync(message).ConfigureAwait(false);
                }

                if (!await _sessionManager.TryStartSessionAsync(
                    context,
                    OnMessageAsync,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false))
                {
                    InternalLogger.Info("ExecutePromptAsync: Session already running");
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error("ExecutePromptAsync failed", ex);
            }
        }


        /// <summary>
        /// Resets the chat history. Returns false if a session is running, true if successful.
        /// </summary>
        public Task<bool> ResetHistoryAsync()
        {
            try
            {
                if (_sessionManager.IsSessionRunning)
                {
                    InternalLogger.Info("ResetHistoryAsync: Cannot reset while session is running");
                    return Task.FromResult(false);
                }

                _chatHistoryManager.Clear();
                InternalLogger.Info("ResetHistoryAsync: History cleared successfully");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                InternalLogger.Error("ResetHistoryAsync failed", ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Stops the current text generation process and active tools.
        /// </summary>
        public Task StopExecutionAsync()
        {
            InternalLogger.Info("StopExecutionAsync called");
            _sessionManager.TryStopSession();
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
