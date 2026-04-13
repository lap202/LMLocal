import { AppStatus, UIText, CONFIG, AppSelectors, AppStore } from './app.globals.js';
import BridgeClient from './bridge.client.js';

/**
 * AppManager - central controller for streaming and UI state.
 * Buffers incoming stream chunks and flushes them to `AppStore` on intervals or at stream end,
 * coordinates `BridgeClient` operations and overall app lifecycle transitions,
 * and services `BridgeMessageDispatcher` callbacks.
 */
const AppManager = {

    _streamBuffer: "",
    _thoughtBuffer: "",
    _lastRenderTimestamp: 0,
    _lastRenderTimestampThought: 0,

    async onAppInit() {
        AppStore.setState({ status: AppStatus.CONNECTING, error: null });
        try {
            const response = await BridgeClient.getStatusAsync();
            if (response.Status === "SUCCESS") {
                AppStore.setState({
                    status: AppStatus.IDLE,
                    modelName: response.ModelName,
                    tokenUsed: response.UsedTokens ?? 0,
                    tokenMax: response.MaxContext || CONFIG.MAX_TOKENS,
                    tokenSpeed: 0,
                    error: null
                });
            } else {
                AppStore.setState({
                    status: AppStatus.OFFLINE,
                    error: response.ErrorMessage,
                    tokenSpeed: 0
                });
            }
        } catch (e) {
            this.onFatalError("Bridge host object unreachable.");
        }
    },

    onFatalError(message) {
        this._streamBuffer = "";
        this._thoughtBuffer = "";
        AppStore.setState({
            status: AppStatus.OFFLINE,
            error: message,
            tokenSpeed: 0
        });
    },

    onCompactionStart() {
        AppStore.setState({ status: AppStatus.COMPACTING });
    },

    onCompactionEnd() {
        if (AppStore.getState().status !== AppStatus.ERROR) {
            AppStore.setState({ status: AppStatus.IDLE });
        }
    },

    async performSendMessage(text) {
        const cleanText = (text || '').trim();
        if (!cleanText || !AppSelectors.canSend(AppStore.getState())) return;

        AppStore.setState({ status: AppStatus.PROCESSING, accumulatedText: "", accumulatedThoughtText: "", error: null, userMessage: cleanText });

        BridgeClient.executePromptAsync(cleanText).catch(e => {
            console.error("Async Bridge Error:", e);
            this.onFatalError("Critical bridge communication failure.");
        });

        return true;
    },

    async performStop() {
        if (!AppSelectors.isGenerating(AppStore.getState())) return;

        AppStore.setState({ status: AppStatus.STOPPING });

        try {
            await BridgeClient.stopExecutionAsync();
        } catch (e) {
            console.error("Stop signal failed", e);
            this.onFatalError("Failed to send stop signal.");
        }
    },

    async performCopyCode(text) {
        try { return await BridgeClient.copyToClipboardAsync(text); } catch { return false; }
    },

    async performClearChat() {
        if (!AppSelectors.isBusy(AppStore.getState()) && confirm(UIText.CONFIRM_CLEAR_CONVERSATION)) {

            AppStore.setState({
                status: AppStatus.CLEARING,
                error: null
            });

            try {

                await BridgeClient.resetHistoryAsync();

                this._streamBuffer = "";
                this._thoughtBuffer = "";

                AppStore.setState({
                    status: AppStatus.IDLE,
                    tokenUsed: 0,
                    tokenSpeed: 0,
                    accumulatedText: "",
                    accumulatedThoughtText: "",
                    userMessage: "",
                    error: null
                });
            } catch (error) {
                AppStore.setState({
                    status: AppStatus.ERROR,
                    error: "Failed to clear chat history",
                    accumulatedText: "",
                    accumulatedThoughtText: "",
                    userMessage: "",
                    tokenUsed: 0,
                    tokenSpeed: 0
                });
            }
        }
    },
    handleStreamThought(chunk, count, speed) {
        const status = AppStore.getState().status;
        if ([AppStatus.STOPPING, AppStatus.ERROR, AppStatus.OFFLINE].includes(status)) return;

        if (status !== AppStatus.THINKING && status === AppStatus.PROCESSING) {
            AppStore.setState({ status: AppStatus.THINKING });
        }

        this._thoughtBuffer += chunk;

        const now = Date.now();

        if (now - this._lastRenderTimestampThought > CONFIG.STREAM_BUFFER_INTERVAL_MS) {
            AppStore.setState(prevState => ({
                status: AppStatus.THINKING,
                accumulatedThoughtText: prevState.accumulatedThoughtText + this._thoughtBuffer,
                tokenUsed: count,
                tokenSpeed: speed
            }));
            this._thoughtBuffer = "";
            this._lastRenderTimestampThought = now;
        }
    },
    handleStreamContent(chunk, count, speed) {
        const status = AppStore.getState().status;

        if ([AppStatus.STOPPING, AppStatus.ERROR, AppStatus.OFFLINE].includes(status)) return;

        if (status !== AppStatus.STREAMING && status === AppStatus.THINKING) {
            AppStore.setState({ status: AppStatus.STREAMING });
        }

        this._streamBuffer += chunk;
        const now = Date.now();

        if (now - this._lastRenderTimestamp > CONFIG.STREAM_BUFFER_INTERVAL_MS) {
            AppStore.setState(prevState => ({
                status: AppStatus.STREAMING,
                accumulatedText: prevState.accumulatedText + this._streamBuffer,
                tokenUsed: count,
                tokenSpeed: speed
            }));
            this._streamBuffer = "";
            this._lastRenderTimestamp = now;
        }
    },

    handleStreamEnd() {
        const status = AppStore.getState().status;

        if ([AppStatus.ERROR, AppStatus.OFFLINE].includes(status)) {
            this._streamBuffer = "";
            this._thoughtBuffer = "";
            return;
        }

        if (status === AppStatus.STOPPING) {
            this._streamBuffer = "";
            this._thoughtBuffer = "";
            AppStore.setState({
                status: AppStatus.IDLE,
                tokenSpeed: 0
            });
            return;
        }

        if (this._thoughtBuffer) {
            AppStore.setState(prevState => ({
                accumulatedThoughtText: prevState.accumulatedThoughtText + this._thoughtBuffer
            }));
            this._thoughtBuffer = "";
            this._lastRenderTimestampThought = Date.now();
        }

        if (this._streamBuffer) {
            AppStore.setState(prevState => ({
                accumulatedText: prevState.accumulatedText + this._streamBuffer
            }));
            this._streamBuffer = "";
            this._lastRenderTimestamp = Date.now();
        }

        AppStore.setState({
            status: AppStatus.FINISHING,
        });

        Promise.resolve().then(() => {
            if (AppStore.getState().status === AppStatus.IDLE) return;
            AppStore.setState({
                status: AppStatus.IDLE,
                tokenSpeed: 0
            });
        });
    },

    handleStreamError(message) {
        this._streamBuffer = "";
        this._thoughtBuffer = "";

        AppStore.setState({
            status: AppStatus.ERROR,
            error: message,
            tokenSpeed: 0
        });
    }
};

export default AppManager;