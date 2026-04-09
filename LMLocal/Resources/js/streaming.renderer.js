// This module provides different DOM update strategies for streaming text.
// Each strategy receives a `renderFunction` (except 'incremental') that converts
// Markdown to HTML (e.g., using `MarkedRenderer` from markdown.renderers.js).
// The 'incremental' mode bypasses `renderFunction` and uses the `streaming-markdown`
// library directly to incrementally build the DOM.
import * as smd from './smd.min.js';

// ---------- Mode: 'full' (complete overwrite with throttling) ----------
// Uses renderFunction to convert the whole accumulated text to HTML and replaces
// the entire container.innerHTML. Throttling reduces the update frequency.
function createFullRenderer(renderFunction, throttleMs, onUpdate) {
    let element = null;
    let lastText = '';
    let updateScheduled = false;
    let pendingText = null;

    const applyUpdate = (fullText) => {
        if (!element) return;
        const html = renderFunction(fullText);
        if (element.innerHTML !== html) {
            element.innerHTML = html;
            onUpdate?.();
        }
    };

    const throttledUpdate = (fullText) => {
        if (throttleMs <= 0) {
            applyUpdate(fullText);
            return;
        }
        if (updateScheduled) {
            pendingText = fullText;
            return;
        }
        applyUpdate(fullText);
        updateScheduled = true;
        setTimeout(() => {
            updateScheduled = false;
            if (pendingText !== null) {
                throttledUpdate(pendingText);
                pendingText = null;
            }
        }, throttleMs);
    };

    return {
        start(targetElement, initialText) {
            element = targetElement;
            lastText = initialText || '';
            if (lastText) throttledUpdate(lastText);
        },
        writeFull(fullText) {
            if (!element) return;
            if (fullText === lastText) return;
            lastText = fullText;
            throttledUpdate(fullText);
        },
        end() {
            if (pendingText) applyUpdate(pendingText);
            updateScheduled = false;
            element = null;
            lastText = '';
        },
        reset() { this.end(); },
        isActive() { return element !== null; }
    };
}

// ---------- Mode: 'experimental block' (block‑based incremental) ----------
// Splits the text by double newline (`\n\n`). Each completed block is rendered once
// via renderFunction and added as a permanent `<div class="message-block">`.
// Only the last (unfinished) block is re‑rendered on each update.
function createBlockRenderer(renderFunction, onUpdate) {
    let blockElements = [];
    let container = null;

    const render = (cont, fullText) => {
        if (!cont) return;
        const blocks = fullText.split(/\n\n/);
        while (blockElements.length < blocks.length) {
            const newBlock = document.createElement('div');
            newBlock.className = 'message-block';
            cont.appendChild(newBlock);
            blockElements.push(newBlock);
        }
        while (blockElements.length > blocks.length) {
            const extra = blockElements.pop();
            extra.remove();
        }
        blocks.forEach((blockText, index) => {
            const isLast = (index === blocks.length - 1);
            const element = blockElements[index];
            if (!isLast && element.hasAttribute('data-rendered')) return;
            const html = renderFunction(blockText);
            if (element.innerHTML !== html) {
                element.innerHTML = html;
                if (!isLast) element.setAttribute('data-rendered', 'true');
                else element.removeAttribute('data-rendered');
            }
        });
        onUpdate?.();
    };

    return {
        start(targetElement, initialText) {
            this.reset();
            container = targetElement;
            if (initialText) render(container, initialText);
        },
        writeFull(fullText) {
            if (container) render(container, fullText);
        },
        end() {
            container = null;
            blockElements = [];
        },
        reset() {
            blockElements.forEach(el => el.remove());
            blockElements = [];
            container = null;
        },
        isActive() { return container !== null; }
    };
}

// ---------- Mode: 'incremental' (true incremental using streaming-markdown) ----------
// Does NOT use the external renderFunction. Instead, it directly uses the
// `streaming-markdown` library (imported as `smd`) to incrementally build the DOM.
// This is the most efficient mode for streaming, as it only appends new DOM nodes
// and never re‑renders existing content. It also handles complex Markdown correctly.
function createIncrementalRenderer(onUpdate) {
    let element = null;
    let parser = null;
    let active = false;
    let lastLength = 0;

    return {
        start(targetElement, initialText) {
            element = targetElement;
            element.innerHTML = '';
            const renderer = smd.default_renderer(element);
            parser = smd.parser(renderer);
            active = true;
            if (initialText) {
                smd.parser_write(parser, initialText);
                lastLength = initialText.length;
                onUpdate?.();
            }
        },
        writeFull(fullText) {
            if (!active || !parser) return;
            const newText = fullText.slice(lastLength);
            if (newText) {
                smd.parser_write(parser, newText);
                lastLength = fullText.length;
                onUpdate?.();
            }
        },
        end() {
            if (parser) {
                smd.parser_end(parser);
                parser = null;
            }
            active = false;
            element = null;
            lastLength = 0;
        },
        reset() { this.end(); },
        isActive() { return active; }
    };
}

// ---------- Mode: 'experimental incremental-tail' (custom: stable blocks + live tail) ----------
// Similar to 'block', but it maintains a persistent "tail" container that holds
// the last unfinished block. Completed blocks are inserted before the tail.
// Uses renderFunction for both completed blocks and the tail.
function createIncrementalTailRenderer(renderFunction, onUpdate) {
    let container = null;
    let activeTailDiv = null;
    let lastTextOffset = 0;

    const render = (fullText) => {
        if (!container) return;
        const lastDoubleNewline = fullText.lastIndexOf('\n\n');
        if (lastDoubleNewline > lastTextOffset) {
            const completedChunk = fullText.substring(lastTextOffset, lastDoubleNewline);
            const htmlChunk = renderFunction(completedChunk);
            if (activeTailDiv) {
                activeTailDiv.remove();
                activeTailDiv = null;
            }
            container.insertAdjacentHTML('beforeend', htmlChunk);
            lastTextOffset = lastDoubleNewline;
        }
        const tailText = fullText.substring(lastTextOffset);
        if (tailText) {
            if (!activeTailDiv) {
                activeTailDiv = document.createElement('div');
                activeTailDiv.className = 'streaming-tail';
                container.appendChild(activeTailDiv);
            }
            const tailHtml = renderFunction(tailText);
            if (activeTailDiv.innerHTML !== tailHtml) {
                activeTailDiv.innerHTML = tailHtml;
            }
        } else {
            if (activeTailDiv) {
                activeTailDiv.remove();
                activeTailDiv = null;
            }
        }
        onUpdate?.();
    };

    return {
        start(targetElement, initialText) {
            this.reset();
            container = targetElement;
            if (initialText) render(initialText);
        },
        writeFull(fullText) {
            if (container) render(fullText);
        },
        end() {
            container = null;
            activeTailDiv = null;
            lastTextOffset = 0;
        },
        reset() {
            if (container) container.innerHTML = '';
            container = null;
            activeTailDiv = null;
            lastTextOffset = 0;
        },
        isActive() { return container !== null; }
    };
}

// ---------- Mode: 'cursor' (experimental, plain text only) ----------
// Does not use any Markdown parser. Inserts raw text as text nodes using a
// hidden span cursor. For testing purposes only.
function createCursorRenderer(onUpdate) {
    let container = null;
    let cursor = null;

    const insertText = (text) => {
        if (!container || !cursor || !text) return;
        const textNode = document.createTextNode(text);
        container.insertBefore(textNode, cursor);
        container.insertBefore(cursor, textNode.nextSibling);
        onUpdate?.();
    };

    return {
        start(targetElement, initialText) {
            this.reset();
            container = targetElement;
            container.innerHTML = '';
            cursor = document.createElement('span');
            cursor.style.display = 'none';
            container.appendChild(cursor);
            if (initialText) insertText(initialText);
        },
        writeChunk(chunk) {
            insertText(chunk);
        },
        writeFull(fullText) {
            this.start(container, fullText);
        },
        end() {
            if (cursor && cursor.parentNode) cursor.remove();
            cursor = null;
            container = null;
        },
        reset() {
            if (container) container.innerHTML = '';
            container = null;
            if (cursor) cursor.remove();
            cursor = null;
        },
        isActive() { return container !== null; }
    };
}

// ---------- Factory with support for old and new syntax ----------
// Factory returning different DOM update strategies: 'incremental' (best for streaming), 'block', 'incremental-tail', 'full', 'cursor'.
export const createStreamingRenderer = (arg1, arg2, arg3) => {
    let options;
    if (typeof arg1 === 'function' && (arg2 === undefined || typeof arg2 === 'function')) {
        options = {
            mode: 'full',
            renderFunction: arg1,
            onUpdate: arg2,
            throttleMs: arg3
        };
    } else if (typeof arg1 === 'object') {
        options = arg1;
    } else {
        throw new Error('Invalid arguments: expected either (renderFunction, onUpdate, throttleMs) or (options)');
    }

    const {
        mode = 'full',
        renderFunction = null,
        onUpdate = null,
        throttleMs = 60
    } = options;

    let internal;
    if (mode === 'full') {
        if (!renderFunction) throw new Error('renderFunction required for mode "full"');
        internal = createFullRenderer(renderFunction, throttleMs, onUpdate);
    } else if (mode === 'block') {
        if (!renderFunction) throw new Error('renderFunction required for mode "block"');
        internal = createBlockRenderer(renderFunction, onUpdate);
    } else if (mode === 'incremental') {
        // This mode does NOT need renderFunction; it uses streaming-markdown directly.
        internal = createIncrementalRenderer(onUpdate);
    } else if (mode === 'incremental-tail') {
        if (!renderFunction) throw new Error('renderFunction required for mode "incremental-tail"');
        internal = createIncrementalTailRenderer(renderFunction, onUpdate);
    } else if (mode === 'cursor') {
        internal = createCursorRenderer(onUpdate);
    } else {
        throw new Error(`Unknown mode: ${mode}`);
    }

    return {
        start: (element, initialText) => internal.start(element, initialText),
        writeFull: (fullText) => internal.writeFull(fullText),
        end: () => internal.end(),
        reset: () => internal.reset(),
        isActive: () => internal.isActive()
    };
};