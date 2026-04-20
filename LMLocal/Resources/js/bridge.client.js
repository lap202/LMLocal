/**
/*
 * Thin wrapper around host bridge API.
 * Supports __bridgeOverride for tests.
 */
class BridgeClient {
    constructor() {
        // nothing to init
    }

    _getHost() {
        return window.__bridgeOverride ?? window.chrome?.webview?.hostObjects?.bridge;
    }

    getWebview() {
        return window.__bridgeOverride?.__webview ?? window.chrome?.webview;
    }

    async _callHost(method, ...args) {
        const host = this._getHost();
        if (!host || typeof host[method] !== 'function') {
            throw new Error(`Bridge host method ${method} is unavailable`);
        }
        return host[method](...args);
    }

    async getStatusAsync() {
        return JSON.parse(await this._callHost("GetStatusAsync"));
    }

    async executePromptAsync(prompt, includeContent) {
        return await this._callHost("ExecutePromptAsync", prompt, includeContent);
    }

    async stopExecutionAsync() {
        return await this._callHost("StopExecutionAsync");
    }

    async resetHistoryAsync() {
        return await this._callHost("ResetHistoryAsync");
    }

    async copyToClipboardAsync(text) {
        return await this._callHost("CopyToClipboardAsync", text);
    }
}

const bridgeClient = new BridgeClient();
export default bridgeClient;
