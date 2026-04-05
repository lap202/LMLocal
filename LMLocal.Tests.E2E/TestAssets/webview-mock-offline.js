const __mockBridge = {
    __webview: {
        addEventListener: () => {},
        removeEventListener: () => {}
    },
    GetStatusAsync: async () => JSON.stringify({
        Status: "ERROR",
        ErrorMessage: "LM Studio unreachable"
    }),
    ExecutePromptAsync: async (prompt) => {},
    StopExecutionAsync: async () => {},
    ResetHistoryAsync: async () => {},
    CopyToClipboardAsync: async (text) => true
};

function __startMock() {
    if (typeof window.lmInit === 'function') {
        // Expose bridge so BridgeClient.getHost() can find it
        window.__bridgeOverride = __mockBridge;
        window.lmInit(__mockBridge);
    } else {
        setTimeout(__startMock, 10);
    }
}

if (document.readyState === 'complete') {
    __startMock();
} else {
    window.addEventListener('load', __startMock);
}
