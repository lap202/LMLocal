import { AppStatus, Config } from './app.globals.js';
import { appSelectors } from './app.store.js';
import appStore from './app.store.js';
import bridgeClient from './bridge.client.js';
import { ChunkBuffer } from './chunk.buffer.js';

/**
 * AppManager - central controller for streaming and UI state.
 */
class AppManager {
    constructor() {
        this.contentBuffer = new ChunkBuffer(Config.STREAM_BUFFER_INTERVAL_MS);
        this.thoughtBuffer = new ChunkBuffer(Config.STREAM_BUFFER_INTERVAL_MS);
    }

    async onAppInit() {
        appStore.setState({ status: AppStatus.CONNECTING, accumulatedText: "", accumulatedThoughtText: "", error: null });
        try {
            const response = await bridgeClient.getStatusAsync();
            if (response.Status === "SUCCESS") {
                appStore.setState({
                    status: AppStatus.IDLE,
                    modelName: response.ModelName,
                    tokenUsed: response.UsedTokens ?? 0,
                tokenMax: response.MaxContext || Config.MAX_TOKENS,
                    tokenSpeed: 0,
                    error: null
                });
            } else {
                appStore.setState({
                    status: AppStatus.OFFLINE,
                    error: response.ErrorMessage,
                    tokenSpeed: 0
                });
            }
        } catch (e) {
            this.onFatalError("Bridge host object unreachable.");
        }
    }

    onFatalError(message) {
        this.contentBuffer.reset();
        this.thoughtBuffer.reset();

        appStore.setState({
            status: AppStatus.OFFLINE,
            error: message,
            tokenSpeed: 0
        });
    }

    onCompactionStart() {
        appStore.setState({ status: AppStatus.COMPACTING });
    }

    onCompactionEnd() {
        if (appStore.getState().status !== AppStatus.ERROR) {
            appStore.setState({ status: AppStatus.IDLE });
        }
    }

    async performSendMessage(text, include) {
        const cleanText = (text || '').trim();
        if (!cleanText || !appSelectors.canSend(appStore.getState())) return;

        appStore.setState({ status: AppStatus.PROCESSING, accumulatedText: "", accumulatedThoughtText: "", error: null, userMessage: cleanText });

        bridgeClient.executePromptAsync(cleanText, include).catch(e => {
            console.error("Async Bridge Error:", e);
            this.onFatalError("Critical bridge communication failure.");
        });

        return true;
    }

    async performStop() {
        if (!appSelectors.isGenerating(appStore.getState())) return;

        appStore.setState({ status: AppStatus.STOPPING });

        try {
            await bridgeClient.stopExecutionAsync();
        } catch (e) {
            console.error("Stop signal failed", e);
            this.onFatalError("Failed to send stop signal.");
        }
    }

    async performCopyCode(text) {
        try { return await bridgeClient.copyToClipboardAsync(text); } catch { return false; }
    }

    async performClearChat() {
        if (!appSelectors.isBusy(appStore.getState())) {

            appStore.setState({
                status: AppStatus.CLEARING,
                error: null
            });

            try {
                await bridgeClient.resetHistoryAsync();

                appStore.setState({
                    status: AppStatus.IDLE,
                    tokenUsed: 0,
                    tokenSpeed: 0,
                    accumulatedText: "",
                    accumulatedThoughtText: "",
                    userMessage: "",
                    error: null
                });
            } catch (error) {
                appStore.setState({
                    status: AppStatus.ERROR,
                    error: "Failed to clear chat history",
                    accumulatedText: "",
                    accumulatedThoughtText: "",
                    userMessage: "",
                    tokenUsed: 0,
                    tokenSpeed: 0
                });
            } finally {
                this.contentBuffer.reset();
                this.thoughtBuffer.reset();
            }
        }
    }

    handleStreamThought(chunk, count, speed) {
        const status = appStore.getState().status;
        if ([AppStatus.STOPPING, AppStatus.ERROR, AppStatus.OFFLINE].includes(status)) return;

        if (status !== AppStatus.THINKING && status === AppStatus.PROCESSING) {
            appStore.setState({ status: AppStatus.THINKING });
        }

        this.thoughtBuffer.append(chunk);
        const now = Date.now();
        if (this.thoughtBuffer.shouldFlush(now)) {
            const flushed = this.thoughtBuffer.flush();
            appStore.setState(prevState => ({
                status: AppStatus.THINKING,
                accumulatedThoughtText: prevState.accumulatedThoughtText + flushed,
                tokenUsed: count,
                tokenSpeed: speed
            }));
        }
    }

    handleStreamContent(chunk, count, speed) {
        const status = appStore.getState().status;

        if ([AppStatus.STOPPING, AppStatus.ERROR, AppStatus.OFFLINE].includes(status)) return;

        if (status !== AppStatus.STREAMING && status === AppStatus.THINKING) {
            appStore.setState({ status: AppStatus.STREAMING });
        }

        this.contentBuffer.append(chunk);
        const now = Date.now();
        if (this.contentBuffer.shouldFlush(now)) {
            const flushed = this.contentBuffer.flush();
            appStore.setState(prevState => ({
                status: AppStatus.STREAMING,
                accumulatedText: prevState.accumulatedText + flushed,
                tokenUsed: count,
                tokenSpeed: speed
            }));
        }
    }

    handleStreamEnd() {
        const status = appStore.getState().status;

        if ([AppStatus.ERROR, AppStatus.OFFLINE].includes(status)) {
            this.contentBuffer.reset();
            this.thoughtBuffer.reset();
            return;
        }

        if (status === AppStatus.STOPPING) {
            this.contentBuffer.reset();
            this.thoughtBuffer.reset();
            appStore.setState({
                status: AppStatus.IDLE,
                tokenSpeed: 0
            });
            return;
        }

        if (!this.thoughtBuffer.isEmpty()) {
            const flushedThought = this.thoughtBuffer.flush();
            appStore.setState(prevState => ({
                accumulatedThoughtText: prevState.accumulatedThoughtText + flushedThought
            }));
        }

        if (!this.contentBuffer.isEmpty()) {
            const flushed = this.contentBuffer.flush();
            appStore.setState(prevState => ({
                accumulatedText: prevState.accumulatedText + flushed
            }));
        }

        appStore.setState({
            status: AppStatus.FINISHING,
        });
    }

    handleHighlightingEnd() {
        Promise.resolve().then(() => {
            if (appStore.getState().status === AppStatus.IDLE) return;
            appStore.setState({
                status: AppStatus.IDLE,
                tokenSpeed: 0
            });
        });
    }

    handleStreamError(message) {
        this.contentBuffer.reset();
        this.thoughtBuffer.reset();

        appStore.setState({
            status: AppStatus.ERROR,
            error: message,
            tokenSpeed: 0
        });
    }
}

const appManager = new AppManager();
export default appManager;
