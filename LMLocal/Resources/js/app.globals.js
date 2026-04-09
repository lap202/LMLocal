"use strict";

// Shared application constants, UI text and lightweight AppStore.
// Exported as an ES module so other modules (components) can import without
// relying on globals and to avoid circular dependencies when separating files.

export const AppStatus = {
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
    ERROR: 'ERROR',             // terminal,online but an error occurred, can send message to try to recover
    CLEARING: 'CLEARING'        // clearing conversation, resetting state
};

export const UIText = {
    STATUS_CONNECTING: 'Connecting...',
    STATUS_OFFLINE: 'Disconnected',
    STATUS_ONLINE: 'Connected',
    STATUS_IDLE: 'Ready',
    STATUS_PROCESSING: 'Thinking...',
    STATUS_STREAMING: 'Generating...',
    STATUS_FINISHING: 'Finishing...',
    STATUS_STOPPING: 'Stopping...',
    STATUS_COMPACTING: 'Optimizing memory...',
    STATUS_CLEARING: 'Clearing conversation...',
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
    CONFIRM_CLEAR_CONVERSATION: 'Are you sure you want to clear the conversation?',
    TEXT_TOKENS: 'tokens',
    TEXT_TOKENS_PER_SECOND: 't/s',
};

export const Assets = {
    COPY_BUTTON_SVG: `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>`
};

export const CONFIG = {
    MAX_DISPLAYED_MESSAGES: 200,
    RENDER_THROTTLE_MS: 30,
    STREAM_BUFFER_INTERVAL_MS: 30,
    USER_MSG_COLLAPSE_THRESHOLD: 500,
    USER_MSG_LINES_COLLAPSE_THRESHOLD: 8,
    MAX_TOKENS: 16384,
    COPY_STATUS_RESET_MS: 2000,
    SCROLL_THRESHOLD_PX: 200
};

export const AppSelectors = {
    isTerminal: (state) => [AppStatus.OFFLINE, AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Busy if not IDLE (includes STOPPING, CONNECTING, OFFLINE, etc.)
    isBusy: (state) => ![AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Generating only during active token flow
    isGenerating: (state) => [AppStatus.PROCESSING, AppStatus.STREAMING].includes(state.status),
    // Can send only when truly idle (implies online and no error)
    canSend: (state) => [AppStatus.IDLE, AppStatus.ERROR].includes(state.status)
};

// --- AppStore: Reactive Data Container ---
export const AppStore = (() => {
    let state = {
        status: AppStatus.CONNECTING,
        modelName: '',
        tokenUsed: 0,
        tokenMax: CONFIG.MAX_TOKENS,
        tokenSpeed: 0,
        error: null,
        accumulatedText: "",
        userMessage: ""
    };

    const listeners = new Set();

    return {
        getState: () => ({ ...state }),

        subscribe: (fn) => {
            if (typeof fn !== 'function') {
                console.error('AppStore.subscribe: listener must be a function, received', fn);
                return () => { };
            }
            listeners.add(fn);
            return () => listeners.delete(fn);
        },

        setState: (nextStateOrUpdater) => {
            const prevState = state;
            const updates = typeof nextStateOrUpdater === 'function'
                ? nextStateOrUpdater(prevState)
                : nextStateOrUpdater;

            if (updates === null || typeof updates !== 'object') {
                console.error('AppStore.setState: updates must be an object, received', updates);
                return;
            }

            state = { ...prevState, ...updates };

            const hasChanges = Object.keys(updates).some(key => prevState[key] !== updates[key]);

            if (hasChanges) {
                listeners.forEach(fn => fn(state, prevState));
            }
        }
    };
})();
