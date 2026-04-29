import { AppStatus, UIText, Config } from '@app/store/app.globals.js';
import { createCallback } from '@app/lib/callback.js';
import { createScrollManager } from '@app/lib/scroll.manager.js';
import { createAiMessage } from '@app/chat/ai.message.js';
import { createHighlightWorkerClient } from '@app/workers/highlight.worker.client.js';
import { createMarkDownParser, ParserType } from '@app/workers/markdown.parser.js';
import { createUserMessage } from '@app/chat/user.message.js';
import { StreamingBuffer } from '@app/streaming/streaming.buffer.js';
import { createStreamingPipeline } from '@app/streaming/streaming.pipeline.js';
import { createStreamingRenderer, StreamingMode } from '@app/streaming/streaming.renderer.js';
import { createStreamingScheduler } from '@app/streaming/streaming.scheduler.js';
/**
 * ChatController — manages chat UI and message lifecycle.
 */
class ChatController {
    constructor() {
        this.container = null;
        this.currentAi = null;
        this.scrollManager = null;
        this.markdownParser = null;
        this.highlightParser = null;
        this.activeTimeouts = [];
        this.onCopyCode = createCallback();
        this.onHighlightCode = createCallback();
    }

    _getContainer() {
        return document.getElementById('chat-container');
    }

    _enforceMessageLimit() {
        const messages = Array.from(this.container.getElementsByClassName('message'));
        if (messages.length <= Config.MAX_DISPLAYED_MESSAGES) return;
        const toRemove = messages.length - Config.MAX_DISPLAYED_MESSAGES;
        messages.slice(0, toRemove).forEach(el => {
            el.remove();
        });
    }

    // Event delegation for copy buttons
    _onContainerClick = (e) => {
        const copyBtn = e.target.closest('.header-copy-btn');
        if (copyBtn) {
            const wrapper = copyBtn.closest('.code-block-container');
            if (!wrapper) return;
            const codeElement = wrapper.querySelector('pre code') || wrapper.querySelector('pre');
            if (!codeElement) return;
            const textToCopy = codeElement.textContent;

            const statusSpan = copyBtn.querySelector('span');
            if (!statusSpan) return;

            this.onCopyCode.emit(textToCopy).then(success => {
                if (success) {
                    statusSpan.textContent = UIText.COPY_SUCCESS;
                    copyBtn.classList.add('success');
                    const timeoutId = setTimeout(() => {
                        statusSpan.textContent = UIText.COPY_LABEL;
                        copyBtn.classList.remove('success');
                        this.activeTimeouts = this.activeTimeouts.filter(id => id !== timeoutId);
                    }, Config.COPY_STATUS_RESET_MS);
                    this.activeTimeouts.push(timeoutId);
                } else {
                    statusSpan.textContent = UIText.COPY_ERROR;
                    const timeoutId = setTimeout(() => {
                        statusSpan.textContent = UIText.COPY_LABEL;
                        this.activeTimeouts = this.activeTimeouts.filter(id => id !== timeoutId);
                    }, Config.COPY_STATUS_RESET_MS);
                    this.activeTimeouts.push(timeoutId);
                }
            }).catch(err => {
                console.error('Copy failed', err);
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
    };

    _renderMessageFlow(state, prev = {}) {
        if (state.status === prev.status &&
            state.accumulatedText === prev.accumulatedText &&
            state.accumulatedThoughtText === prev.accumulatedThoughtText) {
            return;
        }

        switch (state.status) {
            case AppStatus.PROCESSING:
                if (state.userMessage) {
                    this._enforceMessageLimit();
                    createUserMessage(state.userMessage, this.container, this.scrollManager);
                    if (this.currentAi) {
                        this.currentAi.finalize();
                    }

                    this.currentAi = createAiMessage(
                        this.container,
                        this.highlightParser,
                        this._createPipeline(this.markdownParser)
                    );

                    this.scrollManager.scrollToBottom(true);
                }
                break;

            case AppStatus.THINKING:
                this.currentAi?.updateThought(state.accumulatedThoughtText);
                break;

            case AppStatus.STREAMING:
                if (prev.status === AppStatus.THINKING) this.currentAi?.stopThoughts();
                this.currentAi?.updateStreaming(state.accumulatedText);
                break;

            case AppStatus.FINISHING: {
                if (this.currentAi) {
                    this.currentAi.finishStreaming().then(async () => {
                        await this.onHighlightCode.emit(true);
                    }).finally(() => {
                        this.scrollManager.scrollToBottom();
                    });
                }
                break;
            }

            case AppStatus.IDLE:
                if (prev.status === AppStatus.FINISHING) {
                    this.currentAi?.finalize();
                    this.currentAi = null;
                }
                break;

            case AppStatus.STOPPING:
                if (this.currentAi) {
                    this.currentAi.stopStreaming(UIText.TEXT_GENERATION_STOPPED);
                    this.currentAi.finalize();
                    this.currentAi = null;
                }
                break;

            case AppStatus.ERROR:
                if (this.currentAi && !state.accumulatedText) {
                    const errorMsg = `An error occurred: ${state.error || 'Unknown error'}`;
                    this.currentAi.stopStreaming(errorMsg);
                    this.currentAi.finalize();
                    this.currentAi = null;
                }
                break;

            case AppStatus.OFFLINE:
                if (this.currentAi && !state.accumulatedText) {
                    this.currentAi.stopStreaming("You are offline");
                    this.currentAi.finalize();
                    this.currentAi = null;
                }
                break;

            case AppStatus.CLEARING:
                if (this.currentAi) {
                    this.currentAi.clear();
                    this.currentAi = null;
                }
                this.reset();
                this.container?.replaceChildren();
                this.setup(); // Re-setup to reinitialize everything after clearing.

                break;
            default:
                //do nothing.
                break;
        }
    }

    _attachEvents() {
        if (this.container) {
            this.container.addEventListener('click', this._onContainerClick);
        }
    }

    _detachEvents() {
        if (this.container) {
            this.container.removeEventListener('click', this._onContainerClick);
        }
    }

    _clearTimeouts() {
        for (const timeoutId of this.activeTimeouts) {
            clearTimeout(timeoutId);
        }
        this.activeTimeouts = [];
    }

    _createPipeline(markdownParser) {
        let streamingRenderer = createStreamingRenderer({
            mode: StreamingMode.BLOCK_TAIL,
            onUpdate: () => this.scrollManager.scrollToBottom(),
        });

        let streamBuffer = new StreamingBuffer(2);

        let scheduler = createStreamingScheduler(streamBuffer, {
            baseIntervalMs: 60,
            minIntervalMs: 30,
            maxIntervalMs: 300,
            targetQueueLength: 2
        });

        let streamingPipeline = createStreamingPipeline(
            streamBuffer,
            streamingRenderer,
            markdownParser,
            scheduler
        );
        return streamingPipeline;
    }

    setup() {
        this.reset();
        this.container = this._getContainer();
        if (!this.container) return this;

        this.scrollManager = createScrollManager(this.container, Config.SCROLL_THRESHOLD_PX);

        this.markdownParser = createMarkDownParser(ParserType.MARKED_WORKER);
        this.markdownParser.start();

        this.highlightParser = createHighlightWorkerClient();
        this.highlightParser.start();



        this._attachEvents();
        return this;
    }

    update(state, prev) {
        this._renderMessageFlow(state, prev);
    }

    reset() {
        this._clearTimeouts();
        this._detachEvents();

        this.scrollManager?.reset();

        this.currentAi?.clear();
        this.currentAi = null;
    }
}

const chatController = new ChatController();
export default chatController;