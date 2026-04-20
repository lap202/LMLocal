import statusComponent from './status.component.js';
import inputComponent from './input.component.js';
import chatController from './chat.controller.js';
import appManager from './app.manager.js';
import appStore, { appSelectors } from './app.store.js';
import bridgeMessageDispatcher from './bridge.message.dispatcher.js';
import menuComponent from './menu.component.js';
import { Modal } from './modal.js';
import { UIText } from './app.globals.js';
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

        inputComponent.onClick.on(async (text, include) => {
            const isGenerating = appSelectors.isGenerating(appStore.getState());
            if (isGenerating) {
                appStore.setState({ userMessage: text });
                await appManager.performStop();
                return false;
            } else if (text && text.trim()) {
                await appManager.performSendMessage(text, include);
                return true;
            }
            return false;
        });

        inputComponent.onEnter.on(async (text, include) => {
            if (text && text.trim()) {
                await appManager.performSendMessage(text, include);
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
                    const modal = new Modal();
                    const isConfirmed = await modal.confirm(UIText.CONFIRM_CLEAR_CONVERSATION);
                    if (!isConfirmed) return false;
                    await appManager.performClearChat();
                    return true;
                default:
                    return false;
            }
        });

        statusComponent.onRetry.on(async () => {
            await appManager.onAppInit();
        });

        this._globalClickHandler = () => {
            menuComponent.hideMenu();
        };
        window.addEventListener('click', this._globalClickHandler);
    }

    _detachEvents() {
        if (this._storeListener) {
            appStore.unsubscribe(this._storeListener);
            this._storeListener = null;
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

