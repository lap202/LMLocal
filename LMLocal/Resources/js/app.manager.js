import { AppStatus, UIText, CONFIG, AppSelectors, AppStore } from './app.globals.js';
import BridgeClient from './bridge.client.js';


const AppManager = {
    // Buffer incoming stream chunks and flush to AppStore at intervals (CONFIG.STREAM_BUFFER_INTERVAL_MS) to throttle UI updates.
    _streamBuffer: "",
    _lastRenderTimestamp: 0,

    async onAppInit() {
        AppStore.setState({ status: AppStatus.CONNECTING, error: null });
        try {
            const response = await BridgeClient.getStatusAsync();
            if (response.Status === "SUCCESS") {
                AppStore.setState({
                    status: AppStatus.IDLE,
                    modelName: response.ModelName,
                    tokenUsed: response.UsedTokens || 0,
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
        const cleanText = text.trim();
        if (!cleanText || !AppSelectors.canSend(AppStore.getState())) return;

        AppStore.setState({ status: AppStatus.PROCESSING, accumulatedText: "", error: null, userMessage: cleanText });
        // We don't await this because we want the UI to update to PROCESSING immediately, and the streaming will come through the message dispatcher
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
        // Check if we're not busy and confirm with the user before clearing
        if (!AppSelectors.isBusy(AppStore.getState()) && confirm(UIText.CONFIRM_CLEAR_CONVERSATION)) {

            AppStore.setState({
                status: AppStatus.CLEARING,
                error: null
            });

            try {
                // Clear history on the backend
                await BridgeClient.resetHistoryAsync();

                // Reset state to IDLE after clearing
                AppStore.setState({
                    status: AppStatus.IDLE,
                    tokenUsed: 0,
                    tokenSpeed: 0,
                    accumulatedText: "",
                    userMessage: "",
                    error: null
                });
            } catch (error) {
                // If there's an error clearing history, we should show an error message
                AppStore.setState({
                    status: AppStatus.ERROR,
                    error: "Failed to clear chat history",
                    accumulatedText: "",
                    userMessage: "",
                    tokenUsed: 0,
                    tokenSpeed: 0
                });
            }
        }
    },

    handleStreamChunk(chunk, count, speed) {
        if ([AppStatus.STOPPING, AppStatus.ERROR, AppStatus.OFFLINE].includes(AppStore.getState().status)) return;

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
        const state = AppStore.getState();
        const currentStatus = state.status;

        if ([AppStatus.ERROR, AppStatus.OFFLINE].includes(currentStatus)) {
            this._streamBuffer = "";
            return;
        }

        if (currentStatus === AppStatus.STOPPING) {
            this._streamBuffer = "";
            AppStore.setState({
                status: AppStatus.IDLE,
                tokenSpeed: 0
            });
            return;
        }

        const finalContent = state.accumulatedText + this._streamBuffer;
        this._streamBuffer = "";

        AppStore.setState({
            status: AppStatus.FINISHING,
            accumulatedText: finalContent
        });

        // Use a microtask to ensure the UI updates to FINISHING before switching back to IDLE, allowing any final animations or effects to play out
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
        AppStore.setState({
            status: AppStatus.ERROR,
            error: message,
            tokenSpeed: 0
        });
    }
};

export default AppManager;