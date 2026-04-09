import BridgeClient from './bridge.client.js';

// Listen to webview messages and dispatch known types ('ChatChunk','ChatComplete','Error','CompactionStart','CompactionEnd') to AppManager.
const BridgeMessageDispatcher = (() => {
    let isListening = false;
    let appManager = null;

    function start(manager) {
        if (!manager) {
            console.error('[BridgeMessageDispatcher] AppManager is required');
            return;
        }

        appManager = manager;

        const webview = BridgeClient.getWebview();
        if (!webview) return;

        if (isListening) return;

        webview.addEventListener('message', onMessage);
        isListening = true;
    }

    function stop() {
        const webview = BridgeClient.getWebview();
        if (webview && isListening) {
            webview.removeEventListener('message', onMessage);
            isListening = false;
        }
        appManager = null;
    }

    function onMessage(event) {
        if (!appManager) {
            console.warn('[BridgeMessageDispatcher] No AppManager, ignoring message');
            return;
        }

        const { Type, Payload, Count, TokensPerSecond } = event.data;
        switch (Type) {
            case 'ChatChunk':
                appManager.handleStreamChunk(Payload, Count, TokensPerSecond);
                break;
            case 'ChatComplete':
                appManager.handleStreamEnd();
                break;
            case 'Error':
                if (String(Payload || '').toLowerCase().includes("disconnected")) {
                    appManager.onFatalError(Payload);
                } else {
                    appManager.handleStreamError(Payload);
                }
                break;
            case 'CompactionStart':
                appManager.onCompactionStart();
                break;
            case 'CompactionEnd':
                appManager.onCompactionEnd();
                break;
            default:
                console.warn('[BridgeMessageDispatcher] Unknown message type:', Type);
        }
    }

    return { start, stop };
})();

export default BridgeMessageDispatcher