import { AppStatus, UIText, CONFIG } from './app.globals.js';
import createCallback from './callback.js';
import { createScrollManager } from './scroll.manager.js';
import { createStreamingRenderer } from './streaming.renderer.js';
import { createAiMessage } from './ai-message.js';

/**
 * ChatComponent — manages chat UI and message lifecycle.
 * Handles creation and cleanup of user/AI messages, streaming updates,
 * thought rendering, code block highlighting and copy buttons.
 */
const ChatComponent = (() => {
    let container = null;
    let currentAi = null;
    let scrollManager = null;
    let streamingRenderer = null;
    const onCopyCode = createCallback();

    function getContainer() {
        return document.getElementById('chat-container');
    }
    function enforceMessageLimit() {
        const messages = container.querySelectorAll('.message');
        if (messages.length <= CONFIG.MAX_DISPLAYED_MESSAGES) return;
        const toRemove = messages.length - CONFIG.MAX_DISPLAYED_MESSAGES;
        for (let i = 0; i < toRemove; i++) {
            messages[0].remove(); // Remove oldest messages
        }
    }

    function appendUserMessage(text) {
        enforceMessageLimit();

        const div = document.createElement('div');
        div.className = 'message user-message expandable';
        const content = document.createElement('div');
        content.className = 'message-content';
        content.textContent = text;
        div.appendChild(content);

        if (text.length > CONFIG.USER_MESSAGE_COLLAPSE_CHAR_LIMIT || text.split('\n').length > CONFIG.USER_MESSAGE_COLLAPSE_LINES_LIMIT) {
            const btn = document.createElement('button');
            btn.className = 'show-more-btn';
            div.appendChild(btn);
        }

        container.appendChild(div);
        scrollManager.scrollToBottom();
    }

    // Event delegation for copy buttons
    function onContainerClick(e) {
        const copyBtn = e.target.closest('.header-copy-btn');
        if (copyBtn) {
            const wrapper = copyBtn.closest('.code-block-container');
            if (!wrapper) return;
            const codeElement = wrapper.querySelector('pre code') || wrapper.querySelector('pre');
            if (!codeElement) return;
            const textToCopy = codeElement.textContent;

            const statusSpan = copyBtn.querySelector('span');
            if (!statusSpan) return;


            onCopyCode.emit(textToCopy).then(success => {
                if (success) {
                    statusSpan.textContent = UIText.COPY_SUCCESS;
                    copyBtn.classList.add('success');
                    setTimeout(() => {
                        statusSpan.textContent = UIText.COPY_LABEL;
                        copyBtn.classList.remove('success');
                    }, CONFIG.COPY_STATUS_RESET_MS);
                } else {
                    statusSpan.textContent = UIText.COPY_ERROR;
                    setTimeout(() => statusSpan.textContent = UIText.COPY_LABEL, CONFIG.COPY_STATUS_RESET_MS);
                }
            });
            return;
        }

        const thoughtBlock = e.target.closest('.thought-container');
        if (thoughtBlock) {
            const isToggleBtn = e.target.classList.contains('toggle-thought-btn');
            const isHeader = e.target.closest('.reasoning-header');
            if (isToggleBtn || isHeader) {
                const content = thoughtBlock.querySelector('.thought-content');
                if (content) {
                    content.classList.toggle('expanded');
                }
                e.stopPropagation();
            }
        }

        const showMoreBtn = e.target.closest('.show-more-btn');
        if (showMoreBtn) {
            const userMessageDiv = showMoreBtn.closest('.message.user-message');
            if (userMessageDiv) {
                userMessageDiv.classList.toggle('expanded');
            }
            e.stopPropagation();
            return;
        }
    }

    function renderMessageFlow(state, prev = {}) {
        if (state.status === prev.status &&
            state.accumulatedText === prev.accumulatedText &&
            state.accumulatedThoughtText === prev.accumulatedThoughtText) {
            return;
        }

        switch (state.status) {
            case AppStatus.PROCESSING:
                if (state.userMessage) {
                    appendUserMessage(state.userMessage);
                    if (currentAi) {
                        currentAi.finalize();
                    }
                    currentAi = createAiMessage(container, streamingRenderer);
                    scrollManager.scrollToBottom(true);
                }
                break;

            case AppStatus.THINKING:
                currentAi?.updateThought(state.accumulatedThoughtText);
                break;

            case AppStatus.STREAMING:
                if (prev.status === AppStatus.THINKING) currentAi?.stopThoughts();
                currentAi?.updateStreaming(state.accumulatedText);
                break;

            case AppStatus.FINISHING:
                currentAi?.finishStreaming();
                currentAi?.finalize();
                currentAi = null;
                break;

            case AppStatus.STOPPING:
                if (currentAi) {
                    currentAi.stopStreaming(UIText.TEXT_GENERATION_STOPPED);
                    currentAi.finalize();
                    currentAi = null;
                }
                break;

            case AppStatus.ERROR:
                if (currentAi && !state.accumulatedText) {
                    const errorMsg = `An error occurred: ${state.error || 'Unknown error'}`;
                    currentAi.stopStreaming(errorMsg);
                    currentAi.finalize();
                    currentAi = null;
                }
                break;

            case AppStatus.OFFLINE:
                if (currentAi && !state.accumulatedText) {
                    currentAi.stopStreaming("You are offline");
                    currentAi.finalize();
                    currentAi = null;
                }
                break;

            case AppStatus.CLEARING:
                if (currentAi) {
                    currentAi.destroy();
                    currentAi = null;
                }
                container.innerHTML = '';
                streamingRenderer?.reset();
                break;
            default:
                //do nothing.
                break;
        }
    }

    return {
        init() {
            this.destroy();
            container = getContainer();
            if (!container) return this;
            scrollManager = createScrollManager(container, CONFIG.SCROLL_THRESHOLD_PX);

            /*
            Available streaming renderer modes — pick based on UX and performance needs:
              
            - 'block-tail'
            Maintains stable, committed blocks while keeping a live "tail" element for the currently streaming block.
            Completed sections are cloned into the main container and preserved as stable DOM nodes; the last unfinished block is kept in a dedicated tail element that updates in-place.
            Ideal for long, continuous streams where you want minimal DOM churn for completed content and immediate auto-scrolling to the active tail.
            Example usage below shows `renderFunction` to convert Markdown to HTML.
              
            - 'diff'
            Similar to 'full' but relies on the renderer to compute and apply diffs for efficient updates.
            Use when your renderer has built-in diffing capabilities and you want to minimize re-rendering overhead.
            
            - 'incremental'
            True incremental rendering using `streaming-markdown` (most efficient).
            Best for large or complex Markdown streams; does not require `renderFunction`.
              
            - 'full'
            Renders from the entire accumulated text; the renderer may diff and apply minimal DOM updates.
            Simpler to integrate and can be throttled via `throttleMs`.
            
            
              
            Examples:
              
            streamingRenderer = createStreamingRenderer({
                mode: 'block-tail',
                renderFunction: (text) => MessageRenderer.render(text),
                onUpdate: () => scrollManager.scrollToBottom(),
                throttleMs: CONFIG.RENDER_THROTTLE_MS
            });
              
            streamingRenderer = createStreamingRenderer({
                mode: 'incremental',
                onUpdate: () => scrollManager.scrollToBottom()
            });
              
            streamingRenderer = createStreamingRenderer({
                mode: 'diff',
                renderFunction: (text) => MessageRenderer.render(text),
                onUpdate: () => scrollManager.scrollToBottom(),
                throttleMs: CONFIG.RENDER_THROTTLE_MS
            });
            
            streamingRenderer = createStreamingRenderer({
                mode: 'full',
                renderFunction: (text) => MessageRenderer.render(text),
                onUpdate: () => scrollManager.scrollToBottom(),
                throttleMs: CONFIG.RENDER_THROTTLE_MS
            });
            
            */

            streamingRenderer = createStreamingRenderer({
                mode: 'incremental',
                onUpdate: () => scrollManager.scrollToBottom()
            });

            container.addEventListener('click', onContainerClick);
            return this;
        },
        update(state, prev) {
            renderMessageFlow(state, prev);
        },
        onCopyCode,
        destroy() {
            if (container) {
                container.removeEventListener('click', onContainerClick);
                container = null;
            }
            if (scrollManager?.destroy) scrollManager.destroy();
            scrollManager = null;
            if (streamingRenderer) {
                streamingRenderer.reset?.();
                streamingRenderer.destroy?.();
                streamingRenderer = null;
            }
            if (currentAi) {
                currentAi.destroy();
                currentAi = null;
            }
            return this;
        }
    };
})();

export default ChatComponent;