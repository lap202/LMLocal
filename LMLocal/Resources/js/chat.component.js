import { AppStatus, UIText, Assets, CONFIG } from './app.globals.js';
import createCallback from './callback.js';
import MessageRenderer from './message.renderer.js';
import { createScrollManager } from './scroll.manager.js';
import { createStreamingRenderer } from './streaming.renderer.js';

const ChatComponent = (() => {
    let container = null;
    let currentAiMsgDiv = null;
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

    function removeSkeleton() {
        if (!currentAiMsgDiv) return;
        const skeletonLoader = currentAiMsgDiv.querySelector('.skeleton-loader');
        if (skeletonLoader) skeletonLoader.remove();
    }

    // Create an AI message container; optionally show a skeleton loader until streaming begins.
    function createAiMessageContainer(withSkeleton) {
        const div = document.createElement('div');
        div.className = 'message ai-message';
        if (withSkeleton) {
            div.innerHTML = `<div class="skeleton-loader"><div class="skeleton-line"></div><div class="skeleton-line"></div></div>`;
        }
        container.appendChild(div);
        scrollManager.scrollToBottom(true);
        currentAiMsgDiv = div;
        return div;
    }

    function attachCopyButtons(containerElem) {
        if (!containerElem) return;
        const preElements = containerElem.querySelectorAll('pre');
        if (preElements.length === 0) return;

        preElements.forEach(pre => {
            if (pre.hasAttribute('data-copy-processed')) return;
            if (pre.closest('.code-block-container')) return;
            const codeElement = pre.querySelector('code');
            if (!codeElement) return;
            const langMatch = codeElement.className.match(/language-([^\s]+)/);
            const lang = langMatch ? langMatch[1] : 'code';

            const wrapper = document.createElement('div');
            wrapper.className = 'code-block-container';
            const header = document.createElement('div');
            header.className = 'code-header';
            header.innerHTML = `
                <span class="code-lang">${lang}</span>
                <button class="header-copy-btn">
                    ${Assets.COPY_BUTTON_SVG}
                    <span>${UIText.COPY_LABEL}</span>
                </button>`;
            pre.parentNode.insertBefore(wrapper, pre);
            wrapper.append(header, pre);

            pre.setAttribute('data-copy-processed', 'true');
        });
    }

    // Event delegation for copy buttons
    function onContainerClick(e) {
        const copyBtn = e.target.closest('.header-copy-btn');
        if (!copyBtn) return;
        const wrapper = copyBtn.closest('.code-block-container');
        if (!wrapper) return;
        const codeElement = wrapper.querySelector('pre code') || wrapper.querySelector('pre');
        if (!codeElement) return;
        const textToCopy = codeElement.textContent;

        const statusSpan = copyBtn.querySelector('span');
        if (!statusSpan) return;

        // Callback to main process to handle copying (for better compatibility and security)
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
    }
    // Manage UI state transitions for incoming messages: create/remove skeletons, start/update/end streaming renderer, and finalize AI message DOM.
    function renderMessageFlow(state, prev) {
        if (state.userMessage && state.userMessage !== prev?.userMessage) {
            appendUserMessage(state.userMessage);
        }


        // Start thinking: create AI block with skeleton
        if (state.status === AppStatus.PROCESSING && prev?.status !== AppStatus.PROCESSING) {
            createAiMessageContainer(true);
        }

        // Remove skeleton when leaving PROCESSING
        if (prev?.status === AppStatus.PROCESSING && state.status !== AppStatus.PROCESSING) {
            removeSkeleton();
        }

        // Streaming: update text
        if (state.status === AppStatus.STREAMING && state.accumulatedText !== prev?.accumulatedText) {
            if (currentAiMsgDiv) {
                if (!streamingRenderer.isActive()) {
                    removeSkeleton();
                    streamingRenderer.start(currentAiMsgDiv, state.accumulatedText);
                } else {
                    // Option A: call `streamingRenderer.writeFull` with the full accumulated text.
                    // - Pros: the renderer computes the diff and applies minimal DOM updates for you (simpler to use).
                    // - Cons: may re-compare larger strings (acceptable for typical message sizes).
                    //
                    // Option B: compute and send only the new chunk via `streamingRenderer.writeChunk`.
                    // - Pros: avoids internal diffing when you can cheaply determine the new slice (more efficient for very large / high-frequency streams).
                    // - Cons: you must compute chunk boundaries and keep `getProcessedLength()` in sync.
                    //
                    // Example:
                    // const newChunk = state.accumulatedText.slice(streamingRenderer.getProcessedLength());
                    // if (newChunk) streamingRenderer.writeChunk(newChunk);

                    streamingRenderer.writeFull(state.accumulatedText);
                }
            }
        }

        // In FINISHING section:
        if (state.status === AppStatus.FINISHING && prev?.status !== AppStatus.FINISHING) {
            if (streamingRenderer) streamingRenderer.end();
            removeSkeleton();
            if (currentAiMsgDiv) {
                MessageRenderer.highlightCodeBlocks(currentAiMsgDiv);
                attachCopyButtons(currentAiMsgDiv);
                currentAiMsgDiv.classList.add('completed');
            }
            currentAiMsgDiv = null;
        }

        if (state.status === AppStatus.CLEARING && prev?.status !== AppStatus.CLEARING) {
            clearChat();
            if (streamingRenderer) streamingRenderer.reset();
        }

        // Error / Stop cleanup: remove empty AI bubble
        if (([AppStatus.ERROR, AppStatus.OFFLINE, AppStatus.STOPPING].includes(state.status)) &&
            !state.accumulatedText && currentAiMsgDiv) {
            removeSkeleton();
            if (streamingRenderer) streamingRenderer.end();
            currentAiMsgDiv.remove();
            currentAiMsgDiv = null;
        }
    }

    // Public methods
    function appendUserMessage(text) {
        enforceMessageLimit();

        const div = document.createElement('div');
        div.className = 'message user-message expandable';
        const content = document.createElement('div');
        content.className = 'message-content';
        content.textContent = text;
        div.appendChild(content);

        if (text.length > CONFIG.USER_MSG_COLLAPSE_THRESHOLD || text.split('\n').length > CONFIG.USER_MSG_LINES_COLLAPSE_THRESHOLD) {
            const btn = document.createElement('button');
            btn.className = 'show-more-btn';
            btn.textContent = UIText.SHOW_MORE;
            btn.onclick = () => {
                div.classList.toggle('expanded');
                btn.textContent = div.classList.contains('expanded') ? UIText.SHOW_LESS : UIText.SHOW_MORE;
            };
            div.appendChild(btn);
        }

        container.appendChild(div);
        scrollManager.scrollToBottom();
    }

    function clearChat() {
        container.innerHTML = '';
        currentAiMsgDiv = null;
        if (streamingRenderer) streamingRenderer.reset();
    }

    return {
        init() {
            container = getContainer();
            if (!container) return this;
            scrollManager = createScrollManager(container, CONFIG.SCROLL_THRESHOLD_PX);


            /*
              Available streaming renderer modes — pick based on UX and performance needs:
  
              - 'incremental-tail'
                Renders incrementally and keeps the view pinned to the tail.
                Use for long, continuous streams where immediate auto-scrolling is desired.
                Example usage below shows `renderFunction` to convert Markdown to HTML.
  
              - 'incremental'
                True incremental rendering using `streaming-markdown` (most efficient).
                Best for large or complex Markdown streams; does not require `renderFunction`.
  
              - 'block'
                Buffers incoming data and renders completed blocks (split by double newline).
                Use when you want completed sections rendered once and only the last block updated.
  
              - 'full'
                Renders from the entire accumulated text; the renderer may diff and apply minimal DOM updates.
                Simpler to integrate and can be throttled via `throttleMs`.
  
              Examples:
  
              streamingRenderer = createStreamingRenderer({
                  mode: 'incremental-tail',
                  renderFunction: (text) => MessageRenderer.render(text),
                  onUpdate: () => scrollManager.scrollToBottom()
              });
  
              streamingRenderer = createStreamingRenderer({
                  mode: 'incremental',
                  onUpdate: () => scrollManager.scrollToBottom()
              });
  
              streamingRenderer = createStreamingRenderer({
                  mode: 'block',
                  renderFunction: (text) => MessageRenderer.render(text),
                  onUpdate: () => scrollManager.scrollToBottom()
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
        appendUserMessage,
        onCopyCode
    };
})();

export default ChatComponent;