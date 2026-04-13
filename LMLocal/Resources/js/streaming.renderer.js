/**
 * This module provides different DOM update strategies for streaming text.
 * Each strategy receives a `renderFunction` (except 'incremental') that converts
 * Markdown to HTML (for example, using a Marked-style renderer). The 'incremental'
 * mode bypasses `renderFunction` and uses the `streaming-markdown` library
 * directly to incrementally build the DOM.
 */
import * as smd from './smd.min.js';

/**
 * Mode "full" — complete overwrite with throttling.
 *
 * Uses `renderFunction` to convert the whole accumulated text to HTML and replaces
 * the container's innerHTML. Throttling reduces update frequency to avoid
 * excessive reflows.
 */
function createFullRenderer(renderFunction, throttleMs, onUpdate) {
    let element = null;
    let lastText = '';
    let updateScheduled = false;
    let pendingText = null;
    let throttleTimeout = null;

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
        throttleTimeout = setTimeout(() => {
            updateScheduled = false;
            if (pendingText !== null) {
                throttledUpdate(pendingText);
                pendingText = null;
            }
        }, throttleMs);
    };

    return {
        start(targetElement, initialText) {
            this.reset();
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
            if (throttleTimeout) {
                clearTimeout(throttleTimeout);
                throttleTimeout = null;
            }
            if (pendingText !== null) {
                applyUpdate(pendingText);
                pendingText = null;
            }
            updateScheduled = false;
            element = null;
            lastText = '';
        },
        reset() { this.end(); },
        isActive() { return element !== null; }
    };
}

/**
 * Mode "incremental" — true incremental parsing using streaming-markdown.
 *
 * Does not use an external `renderFunction`. Instead, it directly uses the
 * `streaming-markdown` library (imported as `smd`) to incrementally construct
 * DOM nodes. This appends only new content and avoids re-rendering existing DOM.
 */
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

/**
 * Mode "block-tail" — stable committed blocks with a live tail element.
 *
 * Keeps completed blocks stable in the container and maintains a dedicated
 * tail element that updates with the currently streaming (unfinished) block.
 */
function createProgressiveBlockRenderer(renderFunction, onUpdate, throttleMs = 30) {
    let container = null;
    let activeTailDiv = null;
    let lastCompletedChildrenCount = 0;
    let lastRenderedText = '';
    let tempDiv = document.createElement('div');

    let updateScheduled = false;
    let pendingText = null;
    let throttleTimeout = null;

    const moveChildrenToContainer = (el) => {
        if (!el || !container) return;
        const fragment = document.createDocumentFragment();
        while (el.firstChild) fragment.appendChild(el.firstChild);
        container.appendChild(fragment);
    };

    const applyRender = (fullText) => {
        if (!container || fullText === lastRenderedText) return;

        tempDiv.innerHTML = renderFunction(fullText);

        const foundChildren = tempDiv.children.length;

        if (!activeTailDiv) {
            activeTailDiv = document.createElement('div');
            activeTailDiv.className = 'streaming-tail';
            activeTailDiv.style.display = 'contents';
            container.appendChild(activeTailDiv);
        }
        let hasUpdates = false;
        while (foundChildren > lastCompletedChildrenCount + 1) {
            const finishedNode = tempDiv.children[lastCompletedChildrenCount];
            const nodeToCommit = finishedNode.cloneNode(true);
            container.insertBefore(nodeToCommit, activeTailDiv);
            lastCompletedChildrenCount++;
            hasUpdates = true;
        }

        const lastActiveNode = tempDiv.lastElementChild;
        if (lastActiveNode) {
            const newTailContent = lastActiveNode.cloneNode(true);
            if (!activeTailDiv.firstChild?.isEqualNode(newTailContent)) {
                activeTailDiv.innerHTML = '';
                activeTailDiv.appendChild(newTailContent);
                activeTailDiv.dataset.tag = lastActiveNode.tagName.toLowerCase();
                hasUpdates = true;
            }
        }

        lastRenderedText = fullText;
        if (hasUpdates) onUpdate?.();
    };

    const throttledRender = (fullText) => {
        if (throttleMs <= 0) {
            applyRender(fullText);
            return;
        }
        if (updateScheduled) {
            pendingText = fullText;
            return;
        }
        applyRender(fullText);
        updateScheduled = true;
        throttleTimeout = setTimeout(() => {
            updateScheduled = false;
            if (pendingText !== null) {
                throttledRender(pendingText);
                pendingText = null;
            }
        }, throttleMs);
    };

    return {
        start(targetElement, initialText) {
            this.reset();
            container = targetElement;
            if (initialText) throttledRender(initialText);
        },
        writeFull(fullText) {
            if (container) throttledRender(fullText);
        },
        flush() {
            if (activeTailDiv && activeTailDiv.firstChild) {
                const childCount = activeTailDiv.children.length;
                moveChildrenToContainer(activeTailDiv);
                activeTailDiv.remove();
                activeTailDiv = null;

                lastCompletedChildrenCount += childCount;
            }
        },
        end() {
            if (throttleTimeout) {
                clearTimeout(throttleTimeout);
                throttleTimeout = null;
            }
            if (pendingText !== null) {
                applyRender(pendingText); 
                pendingText = null;
            }
            // Ensure the latest rendered tail is committed
            this.flush();
            container = null;
            activeTailDiv = null;
            lastCompletedChildrenCount = 0;
            lastRenderedText = '';
            tempDiv.innerHTML = '';
        },
        reset() {
            if (throttleTimeout) {
                clearTimeout(throttleTimeout);
                throttleTimeout = null;
            }
            if (activeTailDiv?.parentNode) activeTailDiv.remove();
            container = null;
            activeTailDiv = null;
            lastCompletedChildrenCount = 0;
            lastRenderedText = '';
            tempDiv.innerHTML = '';
            updateScheduled = false;
            pendingText = null;
        },
        isActive() { return container !== null; }
    };
}

/**
 * Create a renderer that performs a DOM diff between the current container
 * and a newly rendered HTML tree. The diff attempts to patch text nodes and
 * attributes in-place, while preserving nodes like PRE/CODE by replacing them
 * entirely when their text changes.
 *
 * This is more surgical than full replacement and avoids re-creating nodes
 * with attached event listeners where possible.
 */
function createCleanDiffRenderer(renderFunction, onUpdate, throttleMs = 30) {
    let container = null;
    let hiddenTemplate = document.createElement('template');

    let updateScheduled = false;
    let pendingText = null;
    let lastRenderedText = '';
    let throttleTimeout = null;

    const updateRecursive = (oldNode, newNode) => {
        if (oldNode.isEqualNode && newNode.isEqualNode && oldNode.isEqualNode(newNode)) {
            return;
        }

        if (oldNode.nodeType !== newNode.nodeType) {
            const cloned = newNode.cloneNode(true);
            oldNode.parentNode.replaceChild(cloned, oldNode);
            return;
        }

        if (oldNode.nodeType === Node.TEXT_NODE && newNode.nodeType === Node.TEXT_NODE) {
            if (oldNode.textContent !== newNode.textContent) {
                oldNode.textContent = newNode.textContent;
            }
            return;
        }

        if (oldNode.nodeType === Node.ELEMENT_NODE && newNode.nodeType === Node.ELEMENT_NODE) {
            if (oldNode.tagName === 'PRE' || oldNode.tagName === 'CODE') {
                if (oldNode.textContent !== newNode.textContent) {
                    const cloned = newNode.cloneNode(true);
                    oldNode.parentNode.replaceChild(cloned, oldNode);
                }
                return;
            }

            if (oldNode.tagName !== newNode.tagName) {
                const cloned = newNode.cloneNode(true);
                oldNode.parentNode.replaceChild(cloned, oldNode);
                return;
            }

            for (const attr of oldNode.attributes) {
                if (!newNode.hasAttribute(attr.name)) {
                    oldNode.removeAttribute(attr.name);
                }
            }

            for (const attr of newNode.attributes) {
                if (oldNode.getAttribute(attr.name) !== attr.value) {
                    oldNode.setAttribute(attr.name, attr.value);
                }
            }

            let oldChild = oldNode.firstChild;
            let newChild = newNode.firstChild;
            while (oldChild || newChild) {
                if (!oldChild) {
                    const nextNew = newChild.nextSibling;
                    oldNode.appendChild(newChild.cloneNode(true));
                    newChild = nextNew;
                } else if (!newChild) {
                    const nextOld = oldChild.nextSibling;
                    oldChild.remove();
                    oldChild = nextOld;
                } else {
                    const nextOld = oldChild.nextSibling;
                    const nextNew = newChild.nextSibling;
                    updateRecursive(oldChild, newChild);
                    oldChild = nextOld;
                    newChild = nextNew;
                }
            }
        }
    };

    const applyUpdate = (fullText) => {
        if (!container || fullText === lastRenderedText) return;

        hiddenTemplate.innerHTML = renderFunction(fullText);

        let oldNode = container.firstChild;
        let newNode = hiddenTemplate.content.firstChild;
        while (oldNode || newNode) {
            if (!oldNode) {
                const nextNew = newNode.nextSibling;
                container.appendChild(newNode.cloneNode(true));
                newNode = nextNew;
            } else if (!newNode) {
                const nextOld = oldNode.nextSibling;
                oldNode.remove();
                oldNode = nextOld;
            } else {
                const nextOld = oldNode.nextSibling;
                const nextNew = newNode.nextSibling;
                updateRecursive(oldNode, newNode);
                oldNode = nextOld;
                newNode = nextNew;
            }
        }

        lastRenderedText = fullText;
        onUpdate?.();
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
        throttleTimeout = setTimeout(() => {
            updateScheduled = false;
            if (pendingText !== null) {
                throttledUpdate(pendingText);
                pendingText = null;
            }
        }, throttleMs);
    };

    return {
        start(targetElement, initialText) {
            this.reset();
            container = targetElement;
            if (initialText) throttledUpdate(initialText);
        },
        writeFull(fullText) {
            if (container) throttledUpdate(fullText);
        },
        end() {
            if (throttleTimeout) {
                clearTimeout(throttleTimeout);
                throttleTimeout = null;
            }
            if (pendingText !== null) {
                applyUpdate(pendingText);
                pendingText = null;
            }

            container = null;
            hiddenTemplate.innerHTML = '';
            lastRenderedText = '';
            updateScheduled = false;
        },
        reset() {
            if (throttleTimeout) {
                clearTimeout(throttleTimeout);
                throttleTimeout = null;
            }
            container = null;
            hiddenTemplate.innerHTML = '';
            lastRenderedText = '';
            updateScheduled = false;
            pendingText = null;
        },
        isActive() { return container !== null; }
    };
}

/**
 * Factory that returns a streaming renderer according to options or legacy args.
 *
 * Supported modes:
 *  - 'full'       : full overwrite with optional throttling (requires renderFunction)
 *  - 'incremental': true incremental parsing using streaming-markdown (no renderFunction)
 *  - 'block-tail' : commit completed blocks and keep a dedicated live tail element
 *  - 'diff'       : virtual DOM diff / surgical updates (requires renderFunction)
 */
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
    } else if (mode === 'incremental') {
        //This mode does not need a renderFunction; it uses streaming-markdown directly.
        internal = createIncrementalRenderer(onUpdate);
    } else if (mode === 'block-tail') {
        if (!renderFunction) throw new Error('renderFunction required for mode "block-tail"');
        internal = createProgressiveBlockRenderer(renderFunction, onUpdate);
    } else if (mode === 'diff') {
        if (!renderFunction) throw new Error('renderFunction required for mode "diff"');
        internal = createCleanDiffRenderer(renderFunction, onUpdate);
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