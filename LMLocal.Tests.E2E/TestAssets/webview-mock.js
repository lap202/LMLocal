const __mockBridge = {
    __webview: {
        addEventListener: () => {},
        removeEventListener: () => {}
    },
    ListModelsAsync: async () => {
        console.log('[mock] ListModelsAsync called');
        const result = JSON.stringify({
            models: [
                {
                    id: "test-model-1",
                    name: "Test Model",
                    maxTokens: 16384,
                    supportsMaxTokens: true,
                    isLoaded: false,
                    supportsToolUse: null
                }
            ],
            hasActiveModel: true,
            activeModel: {
                id: "test-model-instance",
                name: "Test Model",
                maxTokens: 16384,
                supportsMaxTokens: true,
                isLoaded: true,
                supportsToolUse: null
            },
            error: null
        });
        console.log('[mock] ListModelsAsync returning:', result);
        return result;
    },
    SetActiveModelAsync: async (modelId, contextLength) => {
        console.log('[mock] SetActiveModelAsync called with:', modelId, contextLength);
        return true;
    },
    ExecutePromptAsync: async (requestJson) => {},
    StopExecutionAsync: async () => {},
    ResetHistoryAsync: async () => {},
    CopyToClipboardAsync: async (text) => true,
    GetInstructionsAsync: async () => {
        console.log('[mock] GetInstructionsAsync called');
        return JSON.stringify({ tabs: [] });
    },
    UpdateInstructionsAsync: async (json) => {
        console.log('[mock] UpdateInstructionsAsync called');
        return true;
    },
    GetSettingsAsync: async () => {
        console.log('[mock] GetSettingsAsync called');
        return JSON.stringify({ AutoLoadOnStartup: true });
    },
    UpdateSettingsAsync: async (json) => {
        console.log('[mock] UpdateSettingsAsync called');
        return true;
    }
};

function __startMock() {
    if (typeof window.lmInit === 'function') {
        console.log('[mock] calling window.lmInit');
        window.__bridgeOverride = __mockBridge;
        window.lmInit(__mockBridge);
    } else {
        console.log('[mock] lmInit not ready, retrying...');
        setTimeout(__startMock, 10);
    }
}

if (document.readyState === 'complete') {
    __startMock();
} else {
    window.addEventListener('load', __startMock);
}
