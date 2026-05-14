"use strict";

/**
 * Shared  UI text.
 */

export const UIText = Object.freeze({
    BUTTON_SEND: 'Send',
    BUTTON_STOP: 'Stop',
    BUTTON_WAIT: '...',
    CONFIRM_CLEAR_CONVERSATION: 'Are you sure you want to clear the conversation?',
    COPY_ERROR: 'Error!',
    COPY_LABEL: 'Copy',
    COPY_SUCCESS: 'Done!',
    SHOW_LESS: 'less',
    SHOW_MORE: 'more',
    STATUS_CLEARING: 'Clearing conversation...',
    STATUS_COMPACTING: 'Optimizing memory...',
    STATUS_CONNECTING: 'Connecting...',
    STATUS_ERROR: 'Error',
    STATUS_EXECUTING: 'Running tool...',
    STATUS_FINISHING: 'Finishing...',
    STATUS_IDLE: 'Ready',
    STATUS_OFFLINE: 'Disconnected',
    STATUS_ONLINE: 'Connected',
    STATUS_PROCESSING: 'Thinking...',
    STATUS_RESPONDING: 'Tool result ready',
    STATUS_STOPPING: 'Stopping...',
    STATUS_STREAMING: 'Generating...',
    STATUS_THINKING: 'Reasoning...',
    STATUS_UNKNOWN: 'Wait...',
    TEXT_GENERATION_STOPPED: 'Generation stopped.',
    TEXT_TOKENS: 'tokens',
    TEXT_TOKENS_PER_SECOND: 't/s',
});

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


