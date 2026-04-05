const __mockBridge = {
    __webview: {
        addEventListener: () => {},
        removeEventListener: () => {}
    },
    GetStatusAsync: async () => {
        console.log('[mock] GetStatusAsync called');
        const result = JSON.stringify({
            Status: "SUCCESS",
            ModelName: "Test Model",
            MaxContext: 16384,
            UsedTokens: 0
        });
        console.log('[mock] GetStatusAsync returning:', result);
        return result;
    },
    ExecutePromptAsync: async (prompt) => {},
    StopExecutionAsync: async () => {},
    ResetHistoryAsync: async () => {},
    CopyToClipboardAsync: async (text) => true
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
