// Mock that simulates streaming: fires chunks then StreamEnd via postMessage
const _listeners = [];
// timers for scheduled mock events so tests can cancel them when simulating errors
const _timers = [];
// Expose listeners and helpers for tests so they can simulate bridge messages and cancel pending timers.
window._listeners = _listeners;
window._mock_timers = _timers;
window.__emitBridgeMessage = (msg) => {
    // if a StreamError/disconnect is emitted, cancel pending scheduled mock events to avoid race
    try {
        console.log('[mock] __emitBridgeMessage called', msg);
        if (msg && msg.Type === 'StreamError') {
            console.log('[mock] StreamError emitted, clearing timers:', _timers.length);
            while (_timers.length) {
                const id = _timers.shift();
                try { clearTimeout(id); } catch (e) { }
            }
        }
    } catch (e) { }
    _listeners.forEach(fn => { try { fn({ data: msg }); } catch (e) { /* swallow */ } });
};

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
    ExecutePromptAsync: async (requestJson) => {
        _timers.push(setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'StreamContent', Payload: '```javascript\nconsole.log("hello");\n```', Count: 10, TokensPerSecond: 15.5 }
            }));
        }, 50));
        _timers.push(setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'StreamEnd' }
            }));
        }, 150));
    },
    StopExecutionAsync: async () => {
        _timers.push(setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'StreamEnd' }
            }));
        }, 50));
    },
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
        return JSON.stringify({});
    },
    UpdateSettingsAsync: async (json) => {
        console.log('[mock] UpdateSettingsAsync called');
        return true;
    }
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
