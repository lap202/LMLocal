import { AppStatus, Config } from '@app/store/app.globals.js';
import { appSelectors } from '@app/store/app.selectors.js';
import appStore from '@app/store/app.store.js';
import modelStore from '@app/store/model.store.js';
import bridgeClient from '@app/api/bridge.client.js';
import instructionsStore from '@app/store/instructions.store.js';
import settingsStore from '@app/store/settings.store.js';
import { ChunkBuffer } from '@app/store/chunk.buffer.js';

/**
 * AppManager - central controller for streaming and UI state.
 */
class AppManager {
    constructor() {
        this.contentBuffer = new ChunkBuffer(Config.STREAM_BUFFER_INTERVAL_MS);
        this.thoughtBuffer = new ChunkBuffer(Config.STREAM_BUFFER_INTERVAL_MS);
    }

    async loadStatus() {
        try {
            appStore.setState({
                status: AppStatus.CONNECTING,
                error: "",
                tokenSpeed: 0
            });

            const response = await bridgeClient.getStatusAsync();
            if (response.Status === "SUCCESS") {
                modelStore.setState({
                    modelName: response.ModelName,
                    modelId: response.ModelId,
                    tokenMax: response.MaxContext || Config.MAX_TOKENS,
                    modelDetails: response.ModelDetails
                });
                appStore.setState({
                    status: AppStatus.IDLE,
                    tokenUsed: response.UsedTokens ?? 0,
                    tokenSpeed: 0,
                    error: null
                });
                await appManager.getInstructions();
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

    async onAppInit() {
        appStore.setState({ status: AppStatus.CONNECTING, accumulatedText: "", accumulatedThoughtText: "", error: null });

        try {
            const settings = await this.getSettings();

            if (settings.AutoLoadOnStartup === false) {
                appStore.setState({
                    status: AppStatus.OFFLINE,
                    error: null,
                    tokenSpeed: 0
                });
                return;
            }

            await this.loadStatus();
        } catch (e) {
            console.error("Settings load failed:", e);
            await this.loadStatus();
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

    async performSendMessage(text, hasContent, instructionsMode) {
        const cleanText = (text || '').trim();
        if (!cleanText || !appSelectors.canSend(appStore.getState())) return;

        appStore.setState({ status: AppStatus.PROCESSING, accumulatedText: "", accumulatedThoughtText: "", error: null, userMessage: cleanText });

        let activeInstructions = "";
        const instructions = instructionsStore.getState().instructions?.tabs;

        if (Array.isArray(instructions)) {
            instructions.forEach(instr => {
                if ((instr.isDefault || instr.name === instructionsMode) && instr.prompt) {
                    activeInstructions += instr.prompt;
                }
            });
        }

        const request = {
            prompt: cleanText,
            includeContent: hasContent,
            additionalPrompt: activeInstructions || null,
            modelId: modelStore.getState().modelId || ""
        };

        bridgeClient.executePromptAsync(request).catch(e => {
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

    async getInstructions() {
        instructionsStore.setState({
            loading: true,
            error: null
        });

        try {
            const instructions = await bridgeClient.getInstructions();
            instructionsStore.setState({
                instructions,
                loading: false,
                error: null
            });
            return instructions;
        } catch (error) {
            console.error('Failed to load instructions:', error);
            instructionsStore.setState({
                loading: false,
                error: "Failed to load instructions"
            });
            throw error;
        }
    }

    async updateInstructions(json) {
        instructionsStore.setState({
            loading: true,
            error: null
        });

        try {
            const result = await bridgeClient.updateInstructions(json);
            instructionsStore.setState({
                instructions: json,
                loading: false,
                error: null
            });
            return result;
        } catch (error) {
            console.error('Failed to update instructions:', error);
            instructionsStore.setState({
                loading: false,
                error: "Failed to update instructions"
            });
            throw error;
        }
    }

    async getSettings() {
        const settings = await bridgeClient.getSettingsAsync();
        settingsStore.setState(settings);
        return settings;
    }

    async updateSettings(settings) {
        const result = await bridgeClient.updateSettingsAsync(settings);
        settingsStore.setState(settings);
        return result;
    }
}

const appManager = new AppManager();
export default appManager;
