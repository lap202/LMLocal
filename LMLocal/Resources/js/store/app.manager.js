import { AppStatus } from '@app/store/app.status.js';
import { appSelectors } from '@app/store/app.selectors.js';
import appStore from '@app/store/app.store.js';
import modelStore from '@app/store/model.store.js';
import instructionsStore from '@app/store/instructions.store.js';
import appDataService from '@app/services/app.data.service.js';
import { startupManager } from '@app/store/startup.manager.js';
import bridgeClient from '@app/api/bridge.client.js';

/**
 * AppManager - central controller for streaming and UI state.
 */
class AppManager {

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

            await startupManager.initialize();

        } catch (e) {
            console.error("onAppInit load failed:", e);
        } finally {
            await this.getInstructions();
        }
    }

    onFatalError(message) {
        appStore.setState({
            status: AppStatus.OFFLINE,
            error: message,
            tokenSpeed: 0
        });
    }

    async reloadActiveModel() {
        appStore.setState({ status: AppStatus.CONNECTING, accumulatedText: "", accumulatedThoughtText: "", error: null });
        await startupManager.initialize();
    }

    async performSendMessage(text, hasContent, instructionsMode) {
        const cleanText = (text || '').trim();
        if (!cleanText || !appSelectors.canSend(appStore.getState().status)) return;

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

    async performStop(text) {
        if (!appSelectors.isBusy(appStore.getState().status)) return;

        appStore.setState({ status: AppStatus.STOPPING, userMessage: text });

        try {
            await bridgeClient.stopExecutionAsync();
        } catch (e) {
            console.error("Stop signal failed", e);
            this.onFatalError("Failed to send stop signal.");
        }
    }

    async performCopyCode(text) {
        try {
            return await bridgeClient.copyToClipboardAsync(text);
        } catch (e) {
            console.error("Copy failed", e);
            return false;
        }
    }

    async performClearChat() {
        if (!appSelectors.isBusy(appStore.getState().status)) {

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
            }
        }
    }

    async getInstructions() {
        return await appDataService.getInstructions();
    }

    async updateInstructions(json) {
        return await appDataService.updateInstructions(json);
    }

    async getSettings() {
        return await appDataService.getSettings();
    }

    async updateSettings(settings) {
        return await appDataService.updateSettings(settings);
    }
}

const appManager = new AppManager();
export default appManager;
