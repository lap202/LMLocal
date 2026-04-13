import MessageRenderer from './message.renderer.js';
import { attachCopyButtons } from './copy.buttons.js';

/**
 * Factory that creates an AI message DOM element, caches its internal blocks,
 * and returns an API to manipulate the message.
 */
export function createAiMessage(container, streamingRenderer) {
    const element = document.createElement('div');
    element.className = 'message ai-message';

    element.innerHTML = `
        <div class="loading-indicator" data-element="loading-indicator"><div class="dot"></div><div class="dot"></div><div class="dot"></div></div>
        <div class="thought-container" style="display: none;" data-element="thought-container">
            <div class="reasoning-header">
                <div class="reasoning-title">Thoughts  
                    <div class="loading-indicator" data-element="thought-loader">
                        <div class="dot"></div><div class="dot"></div><div class="dot"></div>
                    </div>
                </div>
                <button class="toggle-thought-btn"></button>
            </div>
            <div class="thought-content" data-element="thought-content"></div>
        </div>
        <div class="ai-response-container" style="display: none;" data-element="response-container"></div>
    `;
    container.appendChild(element);

    let elements = {
        loadingIndicator: element.querySelector('[data-element="loading-indicator"]'),
        thoughtContainer: element.querySelector('[data-element="thought-container"]'),
        thoughtContent: element.querySelector('[data-element="thought-content"]'),
        responseContainer: element.querySelector('[data-element="response-container"]'),
        thoughtLoader: element.querySelector('[data-element="thought-loader"]')
    };

    let isStreaming = false;

    const api = {
        updateLoadingIndicator: (text) => {
            if (elements.thoughtContent) elements.thoughtContent.textContent = text;
            if (elements.thoughtContainer) {
                elements.thoughtContainer.style.display = 'block';
                elements.thoughtContainer.classList.add('is-thinking');
            }
        },

        stopLoadingIndicator: () => {
            if (elements.loadingIndicator) elements.loadingIndicator.remove();
            elements.loadingIndicator = null;
        },

        updateThought: (text) => {
            api.stopLoadingIndicator();
            if (elements.thoughtContent) elements.thoughtContent.textContent = text;
            if (elements.thoughtContainer) {
                elements.thoughtContainer.style.display = 'block';
                elements.thoughtContainer.classList.add('is-thinking');
            }
        },

        stopThoughts: () => {
            if (elements.thoughtContainer) elements.thoughtContainer.classList.remove('is-thinking');
            if (elements.thoughtLoader) elements.thoughtLoader.style.display = 'none';
        },

        startStreaming: (initialText) => {
            api.stopLoadingIndicator();
            if (elements.responseContainer) {
                elements.responseContainer.style.display = 'block';
                streamingRenderer.start(elements.responseContainer, initialText || '');
            }
            isStreaming = true;
        },

        updateStreaming: (fullText) => {
            if (!isStreaming) api.startStreaming(fullText);
            streamingRenderer.writeFull(fullText);
        },

        finishStreaming: () => {
            if (isStreaming) {
                streamingRenderer.end();
                isStreaming = false;
            }
            element.classList.add('completed');
            if (elements.responseContainer) {
                MessageRenderer.highlightCodeBlocks(elements.responseContainer);
                attachCopyButtons(elements.responseContainer);
            }
        },

        stopStreaming: (message) => {
            if (isStreaming) {
                streamingRenderer.end();
                isStreaming = false;
            }
            api.stopLoadingIndicator();
            api.stopThoughts();
            if (elements.responseContainer) {
                elements.responseContainer.style.display = 'block';

                const stopDiv = document.createElement('div');
                stopDiv.className = 'generation-stopped';
                stopDiv.textContent = message || 'Generation stopped';
                elements.responseContainer.appendChild(stopDiv);
            }
            element.classList.add('stopped');
        },
        finalize: () => {
            if (isStreaming) {
                streamingRenderer?.end();
                isStreaming = false;
            }

            streamingRenderer = null;

            if (elements) {
                Object.keys(elements).forEach(key => elements[key] = null);
                elements = null;
            }
        },
        destroy: () => {
            api.finalize();
            if (element) {
                element.remove();
                element = null;
            }
        }
    };

    return api;
}
