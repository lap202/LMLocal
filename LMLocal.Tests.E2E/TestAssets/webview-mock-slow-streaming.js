// Mock that simulates extremely long/infinite streaming until manually stopped
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
        // Just delay, simulating network latency, so skeleton remains visible
        setTimeout(() => {
            // we won't even send the initial chunk for this test
            // this mimics cancellation during the 'thinking' phase
        }, 50);
    },
    StopExecutionAsync: async () => {
        // Stop triggers completion immediately
        setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'ChatComplete' }
            }));
        }, 10);
    },
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
