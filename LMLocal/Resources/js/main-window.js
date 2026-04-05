"use strict";

/**
 * Application State Enums
 */
const AppStatus = {
    // Connection states
    CONNECTING: 'CONNECTING',   // initial connection / reconnection in progress
    OFFLINE: 'OFFLINE',         // terminal, disconnected, show reload button

    // Operational states (only possible when ONLINE)
    IDLE: 'IDLE',               // terminal, ready to send, online
    PROCESSING: 'PROCESSING',   // pre-streaming (model thinking)
    STREAMING: 'STREAMING',     // actively receiving tokens
    COMPACTING: 'COMPACTING',   // background KV-cache optimization
    FINISHING: 'FINISHING',     // post-processing (highlighting, copy buttons)

    // Interruption / error states
    STOPPING: 'STOPPING',       // user requested stop, cleaning up
    ERROR: 'ERROR'              // terminal,online but an error occurred
};

const UIText = {
    STATUS_CONNECTING: 'Connecting...',
    STATUS_OFFLINE: 'Disconnected',
    STATUS_ONLINE: 'Connected',
    STATUS_IDLE: 'Ready',
    STATUS_PROCESSING: 'Thinking...',
    STATUS_STREAMING: 'Generating...',
    STATUS_FINISHING: 'Finishing...',
    STATUS_STOPPING: 'Stopping...',
    STATUS_COMPACTING: 'Optimizing memory...',
    STATUS_ERROR: 'Error',
    STATUS_UNKNOWN: 'Wait...',
    BUTTON_SEND: 'Send',
    BUTTON_STOP: 'Stop',
    BUTTON_WAIT: '...',
    COPY_LABEL: 'Copy',
    COPY_SUCCESS: 'Done!',
    COPY_ERROR: 'Error!',
    SHOW_MORE: 'Show more',
    SHOW_LESS: 'Show less',
    CONFIRM_CLEAR_CONVERSATION: 'Are you sure you want to clear the conversation?'
};

const CONFIG = {
    MAX_DISPLAYED_MESSAGES: 200,
    RENDER_THROTTLE_MS: 60,
    USER_MSG_COLLAPSE_THRESHOLD: 500,
    USER_MSG_LINES_COLLAPSE_THRESHOLD: 8,
    MAX_TOKENS: 16384,
    COPY_STATUS_RESET_MS: 2000,
    SCROLL_THRESHOLD_PX: 200
};

const AppSelectors = {
    isTerminal: (state) => [AppStatus.OFFLINE, AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Busy if not IDLE (includes STOPPING, CONNECTING, OFFLINE, etc.)
    isBusy: (state) => ![AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Generating only during active token flow
    isGenerating: (state) => [AppStatus.PROCESSING, AppStatus.STREAMING].includes(state.status),
    // Can send only when truly idle (implies online and no error)
    canSend: (state) => [AppStatus.IDLE, AppStatus.ERROR].includes(state.status)
};

// --- 1. AppStore: Reactive Data Container ---
const AppStore = (() => {
    let state = {
        status: AppStatus.CONNECTING,   // start with connecting
        modelName: '',
        tokenUsed: 0,
        tokenMax: CONFIG.MAX_TOKENS,
        tokenSpeed: 0,
        error: null,
        accumulatedText: ""
    };

    const listeners = [];

    return {
        getState: () => state,
        subscribe: (fn) => {
            listeners.push(fn);
            return () => {
                const idx = listeners.indexOf(fn);
                if (idx !== -1) listeners.splice(idx, 1);
            };
        },
        setState: (nextStateOrUpdater) => {
            const prevState = state;
            const nextState = typeof nextStateOrUpdater === 'function'
                ? nextStateOrUpdater(prevState)
                : { ...prevState, ...nextStateOrUpdater };
            state = { ...state, ...nextState };
            listeners.forEach(fn => fn(state, prevState));
        }
    };
})();

// --- 2. UIManager: Passive DOM Renderer ---
const UIManager = (() => {
    const el = {
        chatContainer: document.getElementById('chat-container'),
        userInput: document.getElementById('userInput'),
        mainBtn: document.getElementById('mainBtn'),
        tokenBarFill: document.getElementById('token-bar-fill'),
        infoTooltip: document.getElementById('info-tooltip'),
        modelName: document.getElementById('model-name'),
        connStatus: document.getElementById('conn-status'),
        statusText: document.getElementById('status-text'),
        tokenCountText: document.getElementById('token-number'),
        tokensSpeed: document.getElementById('tokens-speed'),
        separator: document.getElementById('status-separator'),
        statusBar: document.getElementById('status-bar'),
        liveTokenCount: document.getElementById('live-token-count'),
        retryBtn: document.getElementById('retry-btn')
    };

    let currentAiMsgDiv = null;

    // Helper: update connection indicator based on status
    function updateConnectionUI(state) {
        const status = state.status;
        let connText = '';
        let connClass = '';

        if (status === AppStatus.CONNECTING) {
            connText = UIText.STATUS_CONNECTING;
            connClass = 'status-label waiting';
        } else if (status === AppStatus.OFFLINE) {
            connText = UIText.STATUS_OFFLINE;
            connClass = 'status-label offline';
        } else if (status === AppStatus.IDLE ||
            status === AppStatus.PROCESSING ||
            status === AppStatus.STREAMING ||
            status === AppStatus.FINISHING ||
            status === AppStatus.COMPACTING ||
            status === AppStatus.STOPPING ||
            status === AppStatus.ERROR) {
            connText = UIText.STATUS_ONLINE;
            connClass = 'status-label online';
        } else {
            connText = UIText.STATUS_UNKNOWN;
            connClass = 'status-label';
        }

        el.connStatus.innerText = connText;
        el.connStatus.className = connClass;

        // Show retry button only when OFFLINE
        if (el.retryBtn) {
            el.retryBtn.style.display = status === AppStatus.OFFLINE ? 'inline-block' : 'none';
        }

        // Show model name and separator only when not in CONNECTING or OFFLINE
        const showModel = (status !== AppStatus.CONNECTING && status !== AppStatus.OFFLINE);
        if (el.modelName) el.modelName.style.display = showModel ? 'inline' : 'none';
        if (el.separator) el.separator.style.display = showModel ? 'inline' : 'none';
        if (showModel && el.modelName) {
            el.modelName.innerText = state.modelName;
        }
    }

    // Main entry point for state changes
    const syncWithState = (state, prev) => {
        // Update status-dependent UI (button, status text, connection indicator)
        if (state.status !== prev.status || state.error !== prev.error) {
            updateControls(state);
            updateConnectionUI(state);
        }
        //Update stats about used tokens and speed
        if (state.tokenUsed !== prev.tokenUsed || state.tokenSpeed !== prev.tokenSpeed) {
            updateTokenStats(state);
        }

        renderMessageFlow(state, prev);
    };

    function updateControls(state) {
        const isBusy = AppSelectors.isBusy(state);
        const isGenerating = AppSelectors.isGenerating(state);
        const isStopping = state.status === AppStatus.STOPPING;
        const isTerminal = AppSelectors.isTerminal(state);


        el.userInput.disabled = isBusy;
        el.mainBtn.disabled = isStopping || state.status === AppStatus.FINISHING;
        el.mainBtn.innerText = isGenerating ? UIText.BUTTON_STOP : (isStopping ? UIText.BUTTON_WAIT : UIText.BUTTON_SEND);
        el.mainBtn.className = `main-btn ${(isGenerating || isStopping) ? 'btn-stop' : ''}`;

        if (state.status === AppStatus.ERROR) {
            el.statusText.innerText = state.error || "Error";
            el.statusText.classList.add('error');
        } else if (state.status === AppStatus.OFFLINE && state.error) {
            el.statusText.innerText = state.error;
            el.statusText.classList.add('error');
        } else if ([AppStatus.CONNECTING, AppStatus.OFFLINE].includes(state.status)) {
            el.statusText.innerText = '';
            el.statusText.classList.remove('error');
        } else {
            el.statusText.classList.remove('error');
            const label = UIText[`STATUS_${state.status}`] || UIText.STATUS_UNKNOWN;
            el.statusText.innerText = label;
        }

        const showAnimation = !isTerminal;
        el.statusBar.classList.toggle('generating', showAnimation);
    }

    function removeSkeleton() {
        if (currentAiMsgDiv) {
            const loader = currentAiMsgDiv.querySelector('.skeleton-loader');
            if (loader) loader.remove();
        }
    }

    function renderMessageFlow(state, prev) {
        // Start thinking: Create AI block with skeleton
        if (state.status === AppStatus.PROCESSING && prev.status !== AppStatus.PROCESSING) {
            currentAiMsgDiv = createAiMessageContainer(true);
        }

        // Reset skeleton when streaming starts (handles cases where processing phase is skipped)
        if ((state.status === AppStatus.STREAMING && prev.status === AppStatus.PROCESSING) ||
            (prev.status === AppStatus.PROCESSING && state.status !== AppStatus.PROCESSING)) {
            removeSkeleton();
        }

        // Streaming: Update text content
        if (state.status === AppStatus.STREAMING && state.accumulatedText !== prev.accumulatedText) {
            if (currentAiMsgDiv) {
                currentAiMsgDiv.innerHTML = marked.parse(state.accumulatedText);
                scrollChatToBottom();
            }
        }

        // Finishing: Highlighting and copy buttons
        if (state.status === AppStatus.FINISHING && prev.status !== AppStatus.FINISHING) {
            removeSkeleton();
            if (currentAiMsgDiv) {
                currentAiMsgDiv.querySelectorAll('pre code').forEach(block => hljs.highlightElement(block));
                attachCopyButtons(currentAiMsgDiv);
                currentAiMsgDiv.classList.add('completed');
            }
            currentAiMsgDiv = null;
        }

        // Error / Stop cleanup: Remove empty AI bubbles if they failed immediately
        if (([AppStatus.ERROR, AppStatus.OFFLINE, AppStatus.STOPPING].includes(state.status)) && !state.accumulatedText && currentAiMsgDiv) {
            removeSkeleton();
            currentAiMsgDiv.remove();
            currentAiMsgDiv = null;
        }
    }

    function createAiMessageContainer(withSkeleton) {
        const div = document.createElement('div');
        div.className = 'message ai-message';
        if (withSkeleton) {
            div.innerHTML = `<div class="skeleton-loader"><div class="skeleton-line"></div><div class="skeleton-line"></div></div>`;
        }
        el.chatContainer.appendChild(div);
        scrollChatToBottom(true);
        return div;
    }

    function attachCopyButtons(container) {
        container.querySelectorAll('pre').forEach(pre => {
            if (pre.closest('.code-block-container')) return;

            const wrapper = document.createElement('div');
            wrapper.className = 'code-block-container';

            const codeElement = pre.querySelector('code');
            const lang = (codeElement?.className.match(/language-(\w+)/) || [])[1] || 'code';

            const header = document.createElement('div');
            header.className = 'code-header';

            header.innerHTML = `
                <span class="code-lang">${lang}</span>
                <button class="header-copy-btn">
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                        <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                    </svg>
                    <span>${UIText.COPY_LABEL}</span>
                </button>`;

            pre.parentNode.insertBefore(wrapper, pre);
            wrapper.append(header, pre);
        });
    }

    function updateTokenStats(state) {
        const used = state.tokenUsed || 0;
        const speed = state.tokenSpeed || 0;
        const max = state.tokenMax ?? CONFIG.MAX_TOKENS;

        const percent = max > 0 ? Math.min((used / max) * 100, 100) : 0;
        if (el.tokenBarFill) el.tokenBarFill.style.height = percent + "%";

        if (el.liveTokenCount) {
            const isWorking = AppSelectors.isBusy(state);
            el.liveTokenCount.style.display = isWorking ? "inline" : "none";
        }

        if (el.tokenCountText) {
            el.tokenCountText.innerText = `${used} tokens`;
        }

        if (el.tokensSpeed) {
            el.tokensSpeed.innerText = speed > 0 ? `(${speed.toFixed(1)} t/s)` : "";
        }
        
    }

    function scrollChatToBottom(force = false) {
        const c = el.chatContainer;
        if (force || (c.scrollHeight - c.scrollTop <= c.clientHeight + CONFIG.SCROLL_THRESHOLD_PX)) {
            requestAnimationFrame(() => c.scrollTop = c.scrollHeight);
        }
    }

    function enforceMessageLimit() {
        const messages = el.chatContainer.querySelectorAll('.message');
        if (messages.length > CONFIG.MAX_DISPLAYED_MESSAGES) {
            const toRemove = messages.length - CONFIG.MAX_DISPLAYED_MESSAGES;
            for (let i = 0; i < toRemove; i++) {
                messages[i].remove();
            }
        }
    }

    function appendUserMessage(text) {
        enforceMessageLimit();

        const div = document.createElement('div');
        div.className = 'message user-message expandable';

        const content = document.createElement('div');
        content.className = 'message-content';
        content.innerText = text;

        div.appendChild(content);

        if (text.length > CONFIG.USER_MSG_COLLAPSE_THRESHOLD || text.split('\n').length > CONFIG.USER_MSG_LINES_COLLAPSE_THRESHOLD) {
            const btn = document.createElement('button');
            btn.className = 'show-more-btn';
            btn.innerText = UIText.SHOW_MORE;
            btn.onclick = () => {
                div.classList.toggle('expanded');
                btn.innerText = div.classList.contains('expanded') ? UIText.SHOW_LESS : UIText.SHOW_MORE;
            };
            div.appendChild(btn);
        }

        el.chatContainer.appendChild(div);
        el.userInput.value = '';
        el.userInput.style.height = 'auto';
    }

    return {
        syncWithState,
        appendUserMessage,
        clearChat: () => {
            el.chatContainer.innerHTML = '';
            currentAiMsgDiv = null;
        }
    };
})();

// --- 3. AppManager: Centralized Logic Controller ---
const AppManager = {
    _streamBuffer: "",
    _lastRenderTimestamp: 0,

    isBusy() {
        return AppSelectors.isBusy(AppStore.getState());
    },

    isGenerating() {
        return AppSelectors.isGenerating(AppStore.getState());
    },

    canSend() {
        return AppSelectors.canSend(AppStore.getState());
    },

    async onAppInit() {
        AppStore.setState({ status: AppStatus.CONNECTING, error: null });
        try {
            const response = await BridgeClient.fetchConnectionInfo();
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
                    tokenUsed: AppStore.getState().tokenUsed,
                    tokenMax: AppStore.getState().tokenMax,
                    tokenSpeed: 0
                });
            }
        } catch (e) {
            this.onFatalError("Bridge host object unreachable.");
        }
    },

    onFatalError(message) {
        AppStore.setState({
            status: AppStatus.OFFLINE,
            error: message,
            tokenUsed: AppStore.getState().tokenUsed,
            tokenMax: AppStore.getState().tokenMax,
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
        if (!cleanText || !this.canSend()) return;

        UIManager.appendUserMessage(cleanText);
        AppStore.setState({ status: AppStatus.PROCESSING, accumulatedText: "", error: null });

        try {
            await BridgeClient.sendRequest(cleanText);
        } catch (e) {
            this.onFatalError("Critical bridge communication failure.");
        }
    },

    async performStop() {
        if (!this.isGenerating()) return;

        this._streamBuffer = "";
        AppStore.setState({ status: AppStatus.STOPPING });
        try {
            await BridgeClient.abortGeneration();
            // After successful abort, transition to IDLE
            AppStore.setState({ status: AppStatus.IDLE, accumulatedText: "" });
        } catch (e) {
            this.onFatalError("Failed to send stop signal.");
        }
    },

    async performCopyCode(text) {
        try { return await BridgeClient.copyToClipboard(text); } catch { return false; }
    },

    async performClearChat() {
        if (!this.isBusy() && confirm(UIText.CONFIRM_CLEAR_CONVERSATION)) {
            UIManager.clearChat();
            await BridgeClient.clearHistory();
            AppStore.setState({
                tokenUsed: 0,
                tokenMax: AppStore.getState().tokenMax,
                tokenSpeed: 0,
                error: null
            });
        }
    },

    handleStreamChunk(chunk, count, speed) {
        if ([AppStatus.STOPPING, AppStatus.ERROR, AppStatus.OFFLINE].includes(AppStore.getState().status)) return;

        this._streamBuffer += chunk;
        const now = Date.now();

        if (now - this._lastRenderTimestamp > CONFIG.RENDER_THROTTLE_MS) {
            AppStore.setState({
                status: AppStatus.STREAMING,
                accumulatedText: AppStore.getState().accumulatedText + this._streamBuffer,
                tokenUsed: count,
                tokenMax: AppStore.getState().tokenMax,
                tokenSpeed: speed
            });
            this._streamBuffer = "";
            this._lastRenderTimestamp = now;
        }
    },

    handleStreamEnd() {
        const currentStatus = AppStore.getState().status;
        if ([AppStatus.ERROR, AppStatus.OFFLINE].includes(currentStatus)) {
            this._streamBuffer = "";
            return;
        }

        const finalContent = AppStore.getState().accumulatedText + this._streamBuffer;
        this._streamBuffer = "";

        AppStore.setState({ status: AppStatus.FINISHING, accumulatedText: finalContent });

        Promise.resolve().then(() => {
            AppStore.setState(prev => {
                if (prev.status === AppStatus.ERROR) return {};
                return {
                    status: AppStatus.IDLE,
                    tokenUsed: prev.tokenUsed,
                    tokenMax: prev.tokenMax,
                    tokenSpeed: 0
                };
            });
        });
    },

    handleStreamError(message) {
        this._streamBuffer = "";
        AppStore.setState({
            status: AppStatus.ERROR,
            error: message,
            tokenUsed: AppStore.getState().tokenUsed,
            tokenMax: AppStore.getState().tokenMax,
            tokenSpeed: 0
        });
    }
};

// --- 4. BridgeClient: WebView2 Interaction ---
const BridgeClient = (() => {
    const getHost = () => window.__bridgeOverride ?? window.chrome?.webview?.hostObjects?.bridge;
    const getWebview = () => window.__bridgeOverride?.__webview ?? window.chrome?.webview;

    return {
        fetchConnectionInfo: async () => JSON.parse(await getHost().GetStatusAsync()),
        sendRequest: async (prompt) => await getHost().ExecutePromptAsync(prompt),
        abortGeneration: async () => await getHost().StopExecutionAsync(),
        clearHistory: async () => await getHost().ResetHistoryAsync(),
        copyToClipboard: async (text) => await getHost().CopyToClipboardAsync(text),
        getWebview: getWebview
    };
})();

// --- 5. Event Listeners ---
const initialState = AppStore.getState();
AppStore.subscribe(UIManager.syncWithState);
UIManager.syncWithState(initialState, { status: null, error: null, tokenUsed: 0, tokenMax: 0, tokenSpeed: 0 });

window.lmInit = async () => {
    await AppManager.onAppInit();
};

window.onload = () => {
    if (typeof marked !== 'undefined') marked.setOptions({ gfm: true, breaks: true });

    const input = document.getElementById('userInput');
    input.addEventListener('input', function () {
        this.style.height = 'auto';
        this.style.height = this.scrollHeight + 'px';
    });

    input.addEventListener('keydown', async e => {
        if (e.key === 'Enter' && !e.shiftKey && AppManager.canSend()) {
            e.preventDefault();
            await AppManager.performSendMessage(input.value);
        }
    });

    document.getElementById('mainBtn').onclick = async () => {
        if (AppManager.isGenerating()) {
            await AppManager.performStop();
            return;
        }
        await AppManager.performSendMessage(input.value);
    };

    document.querySelector('.btn-clear-icon')?.addEventListener('click', async () => await AppManager.performClearChat());

    document.getElementById('chat-container')?.addEventListener('click', async (e) => {
        const copyBtn = e.target.closest('.header-copy-btn');
        if (!copyBtn) return;

        const container = copyBtn.closest('.code-block-container');
        if (!container) return;
        const codeElement = container.querySelector('pre code') || container.querySelector('pre');
        if (!codeElement) return;
        const textToCopy = codeElement.innerText;

        const statusSpan = copyBtn.querySelector('span');
        const success = await AppManager.performCopyCode(textToCopy);

        if (success) {
            statusSpan.innerText = UIText.COPY_SUCCESS;
            copyBtn.classList.add('success');
            setTimeout(() => {
                statusSpan.innerText = UIText.COPY_LABEL;
                copyBtn.classList.remove('success');
            }, CONFIG.COPY_STATUS_RESET_MS);
        } else {
            statusSpan.innerText = UIText.COPY_ERROR;
            setTimeout(() => statusSpan.innerText = UIText.COPY_LABEL, CONFIG.COPY_STATUS_RESET_MS);
        }
    });

    document.getElementById('retry-btn')?.addEventListener('click', async () => {
        await AppManager.onAppInit();
    });

    const webview = BridgeClient.getWebview();
    if (webview) {
        webview.addEventListener('message', event => {
            const { Type, Payload, Count, TokensPerSecond } = event.data;

            switch (Type) {
                case 'ChatChunk':
                    AppManager.handleStreamChunk(Payload, Count, TokensPerSecond);
                    break;
                case 'ChatComplete':
                    AppManager.handleStreamEnd();
                    break;
                case 'Error':
                    if (Payload.toLowerCase().includes("disconnected")) {
                        AppManager.onFatalError(Payload);
                    } else {
                        AppManager.handleStreamError(Payload);
                    }
                    break;
                case 'CompactionStart':
                    AppManager.onCompactionStart();
                    break;
                case 'CompactionEnd':
                    AppManager.onCompactionEnd();
                    break;
            }
        });
    }
};