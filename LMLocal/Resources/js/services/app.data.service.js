import bridgeClient from '@app/api/bridge.client.js';
import modelStore from '@app/store/model.store.js';
import settingsStore from '@app/store/settings.store.js';
import instructionsStore from '@app/store/instructions.store.js';
import appStore from '@app/store/app.store.js';
import { AppStatus } from '@app/store/app.status.js';

class AppDataService {
    async loadModels() {
        return await bridgeClient.listModelsAsync();
    }

    async setActiveModel(modelId, modelName, supportsMaxTokens, tokenMax) {
        const result = await bridgeClient.setActiveModelAsync(modelId, tokenMax || 0);

        if (result) {
            modelStore.setState({
                modelId: modelId,
                modelName: modelName || modelId,
                tokenMax: tokenMax || 0,
                tokenUsed: 0,
                supportsMaxTokens: supportsMaxTokens
            });
            const appState = appStore.getState();
            if (appState.status == AppStatus.OFFLINE || appState.status == AppStatus.CONNECTING || appState.status == AppStatus.ERROR) {
                appStore.setState({
                    status: AppStatus.IDLE,
                    tokenUsed: 0,
                    tokenSpeed: 0,
                    error: null
                });
            }
        }

        return result;
    }

    async getSettings() {
        const settings = await bridgeClient.getSettingsAsync();
        settingsStore.setState(settings);
        return settings;
    }

    async updateSettings(newSettings) {
        const result = await bridgeClient.updateSettingsAsync(newSettings);

        if (result) {
            settingsStore.setState(newSettings);
        }

        return result;
    }

    async getInstructions() {
        instructionsStore.setState({
            loading: true,
            error: null
        });

        try {
            const instructions = await bridgeClient.getInstructionsAsync();
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
            const result = await bridgeClient.updateInstructionsAsync(json);
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
}

const appDataService = new AppDataService();
export default appDataService;
