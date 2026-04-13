/**
 * Thin wrapper around host bridge API.
 * Supports __bridgeOverride for tests.
 */
const BridgeClient = (() => {
    const getHost = () => window.__bridgeOverride ?? window.chrome?.webview?.hostObjects?.bridge;
    const getWebview = () => window.__bridgeOverride?.__webview ?? window.chrome?.webview;

    const callHost = async (method, ...args) => {
        const host = getHost();
        if (!host || typeof host[method] !== 'function') {
            throw new Error(`Bridge host method ${method} is unavailable`);
        }
        return host[method](...args);
    };

    return {
        getStatusAsync: async () => JSON.parse(await callHost("GetStatusAsync")),
        executePromptAsync: async (prompt) => await callHost("ExecutePromptAsync", prompt),
        stopExecutionAsync: async () => await callHost("StopExecutionAsync"),
        resetHistoryAsync: async () => await callHost("ResetHistoryAsync"),
        copyToClipboardAsync: async (text) => await callHost("CopyToClipboardAsync", text),
        getWebview
    };
})();

export default BridgeClient