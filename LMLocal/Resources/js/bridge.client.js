// Thin wrapper around host bridge API. Supports __bridgeOverridefor tests;getStatusAsync parses host JSON.
const BridgeClient = (() => {
    const getHost = () => window.__bridgeOverride ?? window.chrome?.webview?.hostObjects?.bridge;
    const getWebview = () => window.__bridgeOverride?.__webview ?? window.chrome?.webview;

    return {
        getStatusAsync: async () => JSON.parse(await getHost().GetStatusAsync()),
        executePromptAsync: async (prompt) => await getHost().ExecutePromptAsync(prompt),
        stopExecutionAsync: async () => await getHost().StopExecutionAsync(),
        resetHistoryAsync: async () => await getHost().ResetHistoryAsync(),
        copyToClipboardAsync: async (text) => await getHost().CopyToClipboardAsync(text),
        getWebview
    };
})();

export default BridgeClient