import StatusComponent from './status.component.js';
import InputComponent from './input.component.js';
import ChatComponent from './chat.component.js';
import AppManager from './app.manager.js';
import { AppSelectors, AppStore } from './app.globals.js';
import BridgeMessageDispatcher from './bridge.message.dispatcher.js';
import MenuComponent from './menu.component.js';

/**
 * AppController - central initializer and event router.
 * Waits for required DOM elements, initializes UI components (Status, Input, Chat, Menu),
 * wires AppStore subscriptions and component event handlers, and starts the BridgeMessageDispatcher.
 * AppController only bootstraps and routes events, it does not handle streaming itself.
 */
const AppController = (() => {

    let initialized = false;
    let _storeListener = null;
    let _globalClickHandler = null;

    async function init() {
        if (initialized) return;

        // Ensure DOM is ready and required elements exist before initializing components.
        // Centralized check here avoids adding the same guard inside every component.
        const ensureDomAndElements = async (selectors = [], timeoutMs = 2000) => {
            if (document.readyState === 'loading') {
                await new Promise(resolve => window.addEventListener('DOMContentLoaded', resolve, { once: true }));
            }
            const start = Date.now();
            const interval = 25;
            while (Date.now() - start < timeoutMs) {
                const allExist = selectors.every(s => !!document.querySelector(s));
                if (allExist) return true;
                await new Promise(r => setTimeout(r, interval));
            }
            return false;
        };

        await ensureDomAndElements(['#userInput', '#mainBtn', '#chat-container', '#conn-status'], 100);

        // 1. Initialize components (safe now that DOM elements are present)
        StatusComponent.init();
        InputComponent.init();
        ChatComponent.init();
        MenuComponent.init();

        // 2. Subscribe to store for UI updates
        _storeListener = (state, prev) => {
            StatusComponent.update(state, prev);
            InputComponent.update(state, prev);
            ChatComponent.update(state, prev);
        };
        AppStore.subscribe(_storeListener);

        // 3. Subscribe to InputComponent events
        InputComponent.onClick.on(async (text) => {
            const isGenerating = AppSelectors.isGenerating(AppStore.getState());
            if (isGenerating) {
                AppStore.setState({ userMessage: text });
                await AppManager.performStop();
                return false; // do not clear the field
            } else if (text && text.trim()) {
                await AppManager.performSendMessage(text);
                return true; // clear the field
            }
            return false;
        });

        InputComponent.onEnter.on(async (text) => {
            if (text && text.trim()) {
                await AppManager.performSendMessage(text);
                return true;
            }
            return false;
        });

        ChatComponent.onCopyCode.on(async (text) => {
            return await AppManager.performCopyCode(text);
        });


        MenuComponent.onClick.on(async (action) => {
            switch (action) {
                case "clear-chat":
                    await AppManager.performClearChat();
                    return true;
                default:
                    return false;
            }
        });

        StatusComponent.onRetry.on(async () => {
            await AppManager.onAppInit();
        });

        // 4. Global handlers (menu)
        _globalClickHandler = () => { MenuComponent.hideMenu(); };
        window.addEventListener('click', _globalClickHandler);

        BridgeMessageDispatcher.start(AppManager);

        initialized = true;
    }

    return {
        init,
        get initialized() { return initialized; },
        destroy() {
            // reverse initialization: remove handlers, unsubscribe, stop dispatcher and destroy components
            if (!initialized) return;

            if (_globalClickHandler) {
                window.removeEventListener('click', _globalClickHandler);
                _globalClickHandler = null;
            }

            if (_storeListener) {
                AppStore.unsubscribe(_storeListener);
                _storeListener = null;
            }

            BridgeMessageDispatcher.stop();

            StatusComponent.destroy();
            InputComponent.destroy();
            ChatComponent.destroy();
            MenuComponent.destroy();

            initialized = false;
        }
    };
})();

export default AppController;