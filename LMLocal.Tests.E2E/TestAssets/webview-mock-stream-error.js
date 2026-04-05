// Mock that simulates a server-side stream error via PostWebMessage
const _listeners = [];

const __mockBridge = {
    __webview: {
        addEventListener: (event, handler) => {
            if (event === 'message') _listeners.push(handler);
        },
        removeEventListener: (event, handler) => {
            const i = _listeners.indexOf(handler);
            if (i !== -1) _listeners.splice(i, 1);
        }
    },
    GetStatusAsync: async () => JSON.stringify({
        Status: "SUCCESS",
        ModelName: "Test Model",
        MaxContext: 16384,
        UsedTokens: 0
    }),
    ExecutePromptAsync: async (prompt) => {
        setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'Error', Payload: 'model crashed' }
            }));
        }, 50);
    },
    StopExecutionAsync: async () => {},
    ResetHistoryAsync: async () => {},
    CopyToClipboardAsync: async (text) => true
};

function __startMock() {
    if (typeof window.lmInit === 'function') {
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
