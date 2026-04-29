"use strict";

/**
 * Shared application constants, UI text.
 */

export const AppStatus = {
    // Connection states
    CONNECTING: 'CONNECTING',   // initial connection / reconnection in progress
    OFFLINE: 'OFFLINE',         // terminal, disconnected, show reload button

    // Operational states (only possible when ONLINE)
    IDLE: 'IDLE',               // terminal, ready to send, online
    PROCESSING: 'PROCESSING',   // pre-streaming (model thinking)
    THINKING: 'THINKING',       // actively receiving thinking tokens
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
    STATUS_THINKING: 'Reasoning...',
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
    SHOW_MORE: 'more',
    SHOW_LESS: 'less',
    CONFIRM_CLEAR_CONVERSATION: 'Are you sure you want to clear the conversation?',
    TEXT_TOKENS: 'tokens',
    TEXT_TOKENS_PER_SECOND: 't/s',
    TEXT_GENERATION_STOPPED: 'Generation stopped.',
};

export const Assets = {
    COPY_BUTTON_SVG: `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>`
};

export const Config = {
    MAX_DISPLAYED_MESSAGES: 200,
    RENDER_THROTTLE_MS: 90,
    RENDER_BATCH_SIZE_WORDS:2,
    STREAM_BUFFER_INTERVAL_MS: 100,
    USER_MESSAGE_COLLAPSE_CHAR_LIMIT: 500,
    USER_MESSAGE_COLLAPSE_LINES_LIMIT: 8,
    MAX_TOKENS: 16384,
    COPY_STATUS_RESET_MS: 2000,
    SCROLL_THRESHOLD_PX: 50,
    STREAM_INACTIVITY_TIMEOUT_MS: 30000
};


