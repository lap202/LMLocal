import { statusComponent } from '@app/components/status.component.js';
import { menuComponent } from '@app/components/menu.component.js';
import { inputComponent } from '@app/components/input.component.js';
import themeComponent from '@app/components/theme.component.js';
import chatController from '@app/chat/chat.controller.js';
import appManager from '@app/store/app.manager.js';
import appStore from '@app/store/app.store.js';
import { appSelectors }  from '@app/store/app.selectors.js';
import modelStore from '@app/store/model.store.js';
import instructionsStore from '@app/store/instructions.store.js';
import settingsStore from '@app/store/settings.store.js';
import bridgeMessageDispatcher from '@app/api/bridge.message.dispatcher.js';

import { ConfirmDialog } from '@app/dialogs/confirm.dialog.js';
import { SettingsDialog } from '@app/dialogs/settings.dialog.js';
import { InstructionsDialog } from '@app/dialogs/instructions.dialog.js';
import { UIText } from '@app/store/app.globals.js';
/**
 * AppController - central initializer and event router.
 * Waits for required DOM elements, initializes UI components (Status, Input, Chat, Menu),
 * wires AppStore subscriptions and component event handlers, and starts the BridgeMessageDispatcher.
 * AppController only bootstraps and routes events, it does not handle streaming itself.
 */
class AppController {
    constructor() {
        this._initialized = false;
        this._storeListener = null;
        this._modelListener = null;
        this._instructionsListener = null;
        this._settingsListener = null;
        this._globalClickHandler = null;
    }

    setup() {
        if (this._initialized) return;
        this.reset();

        statusComponent.setup();
        inputComponent.setup();
        chatController.setup();
        menuComponent.setup();

        this._attachEvents();

        bridgeMessageDispatcher.start(appManager);
        this._initialized = true;
    }

    reset() {
        if (!this._initialized) return;

        this._detachEvents();

        bridgeMessageDispatcher.stop();

        statusComponent.reset();
        inputComponent.reset();
        chatController.reset();
        menuComponent.reset();

        this._initialized = false;
    }

    _attachEvents() {
        this._storeListener = (state, prev) => {
            statusComponent.update(state, prev);
            inputComponent.update(state, prev);
            chatController.update(state, prev);
        };
        appStore.subscribe(this._storeListener);

        this._modelListener = (state, prev) => {
            statusComponent.updateModelName(state.modelName, prev?.modelName);
            statusComponent.updateModelContext(state.tokenMax, prev?.tokenMax);
        };
        modelStore.subscribe(this._modelListener);

        this._instructionsListener = (state, prev) => {
            inputComponent.updateInstructionsState(state, prev);
        };
        instructionsStore.subscribe(this._instructionsListener);

        this._settingsListener = (state, prev) => {
            themeComponent.updateTheme(state, prev);
        };
        settingsStore.subscribe(this._settingsListener);

        themeComponent.setup();

        inputComponent.onClick.on(async (text, hasActiveContent, instructionsMode) => {
            const isGenerating = appSelectors.isGenerating(appStore.getState());
            if (isGenerating) {
                appStore.setState({ userMessage: text });
                await appManager.performStop();
                return false;
            } else if (text && text.trim()) {
                await appManager.performSendMessage(text, hasActiveContent, instructionsMode);
                return true;
            }
            return false;
        });

        inputComponent.onEnter.on(async (text, hasActiveContent, instructionsMode) => {
            if (text && text.trim()) {
                await appManager.performSendMessage(text, hasActiveContent, instructionsMode);
                return true;
            }
            return false;
        });

        chatController.onCopyCode.on(async (text) => {
            return await appManager.performCopyCode(text);
        });

        chatController.onHighlightCode.on(() => {
            return appManager.handleHighlightingEnd();
        });

        menuComponent.onClick.on(async (action) => {
            switch (action) {
                case 'clear-chat':
                    const confirmDialog = new ConfirmDialog();
                    const isConfirmed = await confirmDialog.confirm(UIText.CONFIRM_CLEAR_CONVERSATION);
                    if (!isConfirmed) return false;
                    await appManager.performClearChat();
                    return true;
                case 'open-settings':
                    const settingsDialog = new SettingsDialog();
                    settingsDialog.onLoad.on(async () => {
                        return await appManager.getSettings();
                    });
                    settingsDialog.onSave.on(async (settings) => {
                        return await appManager.updateSettings(settings);
                    });
                    await settingsDialog.show();
                    return true;
                case 'open-instructions':
                    //disabled due beta feedback, will be re-enabled in the future
                    return true;
                    const instructionsDialog = new InstructionsDialog();
                    instructionsDialog.onLoad.on(async () => {
                        return await appManager.getInstructions();
                    });
                    instructionsDialog.onSave.on(async (json) => {
                        return await appManager.updateInstructions(json);
                    });
                    await instructionsDialog.show();
                    return true;
                default:
                    return false;
            }
        });

        statusComponent.onRetry.on(async () => {
            await appManager.loadStatus();
        });

        this._globalClickHandler = () => {
            menuComponent.hideMenu();
            inputComponent.hideDropdown();
        };
        window.addEventListener('click', this._globalClickHandler);
    }

    _detachEvents() {
        if (this._storeListener) {
            appStore.unsubscribe(this._storeListener);
            this._storeListener = null;
        }

        if (this._modelListener) {
            modelStore.unsubscribe(this._modelListener);
            this._modelListener = null;
        }

        if (this._instructionsListener) {
            instructionsStore.unsubscribe(this._instructionsListener);
            this._instructionsListener = null;
        }

        if (this._settingsListener) {
            settingsStore.unsubscribe(this._settingsListener);
            this._settingsListener = null;
        }

        if (this._globalClickHandler) {
            window.removeEventListener('click', this._globalClickHandler);
            this._globalClickHandler = null;
        }

        inputComponent.onClick.off();
        inputComponent.onEnter.off();
        chatController.onCopyCode.off();
        chatController.onHighlightCode.off();
        menuComponent.onClick.off();
        statusComponent.onRetry.off();
    }

    get initialized() {
        return this._initialized;
    }
}

const appController = new AppController();
export default appController;

