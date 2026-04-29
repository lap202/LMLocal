/**
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

    async executePromptAsync(request) {
        const requestJson = JSON.stringify(request);
        return await this._callHost("ExecutePromptAsync", requestJson);
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

    async getSettingsAsync() {
        const res = await this._callHost("GetSettingsAsync");
        return JSON.parse(res);
    }

    async updateSettingsAsync(settings) {
        const payload = JSON.stringify(settings);
        return await this._callHost("UpdateSettingsAsync", payload);
    }

    async getInstructions() {
        const res = await this._callHost("GetInstructionsAsync");
        return JSON.parse(res);
    }

    async updateInstructions(instructions) {
        const payload = JSON.stringify(instructions);
        return await this._callHost("UpdateInstructionsAsync", payload);
    }
}

const bridgeClient = new BridgeClient();
export default bridgeClient;
