import bridgeClient from './bridge.client.js';

/**
 * BridgeMessageDispatcher - binds a single listener to the host/webview `message`
 * event and routes validated messages to the supplied `AppManager`.
 * Converted to class with singleton `bridgeMessageDispatcher`.
 */
class BridgeMessageDispatcher {
    constructor() {
        this.isListening = false;
        this.appManager = null;

        this._onMessage = this._onMessage.bind(this);
    }

    start(manager) {
        if (!manager) {
            console.error('[BridgeMessageDispatcher] AppManager is required');
            return;
        }

        this.appManager = manager;

        const webview = bridgeClient.getWebview();
        if (!webview) {
            console.error('[BridgeMessageDispatcher] WebView is unavailable');
            return;
        }

        if (this.isListening) return;

        webview.addEventListener('message', this._onMessage);
        this.isListening = true;
    }

    stop() {
        const webview = bridgeClient.getWebview();
        if (webview && this.isListening) {
            webview.removeEventListener('message', this._onMessage);
            this.isListening = false;
        }
        this.appManager = null;
    }

    _onMessage(event) {
        if (!this.appManager) {
            console.warn('[BridgeMessageDispatcher] No AppManager, ignoring message');
            return;
        }

        const { Type, Payload, Count, TokensPerSecond } = event.data;
        switch (Type) {
            case 'StreamContent':
                this.appManager.handleStreamContent(Payload, Count, TokensPerSecond);
                break;
            case 'StreamThought':
                this.appManager.handleStreamThought(Payload, Count, TokensPerSecond);
                break;
            case 'StreamEnd':
                this.appManager.handleStreamEnd();
                break;
            case 'StreamError':
                if (String(Payload || '').toLowerCase().includes("disconnected")) {
                    this.appManager.onFatalError(Payload);
                } else {
                    this.appManager.handleStreamError(Payload);
                }
                break;
            case 'CompactionStart':
                this.appManager.onCompactionStart();
                break;
            case 'CompactionEnd':
                this.appManager.onCompactionEnd();
                break;
            default:
                console.warn('[BridgeMessageDispatcher] Unknown message type:', Type);
        }
    }
}

const bridgeMessageDispatcher = new BridgeMessageDispatcher();
export default bridgeMessageDispatcher;
