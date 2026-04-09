import StatusComponent from './status.component.js';
import InputComponent from './input.component.js';
import ChatComponent from './chat.component.js';
import AppManager from './app.manager.js';
import { AppSelectors, AppStore } from './app.globals.js';
import BridgeMessageDispatcher from './bridge.message.dispatcher.js';

const AppController = (() => {
    // Public methods that will be available to other modules
    let initialized = false;
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

        // Wait for the small set of elements our components require.
        await ensureDomAndElements(['#userInput', '#mainBtn', '#chat-container', '#conn-status'], 2000);

        // 1. Initialize components (safe now that DOM elements are present)
        StatusComponent.init();
        InputComponent.init();
        ChatComponent.init();

        // 2. Subscribe to store for UI updates
        AppStore.subscribe((state, prev) => {
            StatusComponent.update(state, prev);
            InputComponent.update(state, prev);
            ChatComponent.update(state, prev);
        });

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


        // 4. Global handlers (clear chat, retry)
        document.querySelector('.btn-clear-icon')?.addEventListener('click', async () => {
            await AppManager.performClearChat();
        });

        const retryBtn = document.getElementById('retry-btn');
        if (retryBtn) {
            retryBtn.addEventListener('click', async () => {
                await AppManager.onAppInit();
            });
        }

        BridgeMessageDispatcher.start(AppManager);

        initialized = true;
    }

    return {
        init,
        get initialized() { return initialized; }
    };
})();

export default AppController;