/**
 * HighlightWorkerClient manages a Web Worker for syntax highlighting code blocks within a container.
 * It provides methods to start/stop the worker and to highlight code blocks in a given container.
 * The worker processes code blocks in batches and returns highlighted HTML, which is then applied to the DOM.
 */
export class HighlightWorkerClient {
    constructor() {
        this.worker = null;
        this.callbacks = new Map();
        this.nextId = 0;
        this.pendingContainers = new WeakMap();
        this.started = false;
    }
    _getWorkerCode() {
        let workerCode = `
            importScripts('https://app.local/js/workers/highlight/highlight.min.js');
            if (typeof hljs === 'undefined') {
                throw new Error('highlight.js library not loaded');
            }
            self.onmessage = function(e) {
                const { id, blocks } = e.data;
                const results = blocks.map(block => {
                    try {
                        const lang = block.language || 'text';
                        const canHighlight = lang && hljs.getLanguage(lang);
                        const html = canHighlight
                            ? hljs.highlight(block.code, { language: lang }).value
                            : hljs.highlightAuto(block.code).value;
                        return { success: true, html };
                    } catch (err) {
                        return { success: false, html: block.code, error: err.message };
                    }
                });
                self.postMessage({ id, results });
            };
    `;
        return workerCode;
    }


    start() {
        if (this.started && this.worker) return;
        if (typeof Worker === 'undefined') throw new Error('Web Workers not supported');

        const blob = new Blob([this._getWorkerCode()], { type: 'application/javascript' });
        const workerUrl = URL.createObjectURL(blob);
        this.worker = new Worker(workerUrl);
        URL.revokeObjectURL(workerUrl);

        this.worker.onmessage = (e) => this._handleMessage(e.data);
        this.worker.onerror = (err) => {
            console.error('Worker error', err);
            this.stop();
        };
        this.started = true;
    }
    stop() {
        if (!this.started) return;
        this.started = false;
        if (this.worker) {
            this.worker.terminate();
            this.worker = null;
        }

        for (const { reject } of this.callbacks.values()) {
            reject(new Error('HighlightClient stopped'));
        }
        this.callbacks.clear();
        this.pendingContainers = new WeakMap();
        this.nextId = 0;
    }
    _handleMessage({ id, results }) {
        const cb = this.callbacks.get(id);
        if (!cb) return;
        const { resolve, reject, codeBlocks } = cb;
        try {
            codeBlocks.forEach((block, idx) => {
                if (!block.isConnected) return;
                const res = results[idx];
                if (res?.success) {
                    block.innerHTML = res.html;
                    block.classList.add('hljs');
                } else {
                    const errorInfo = res?.error;
                    console.warn('Highlighting failed for block:', block, errorInfo);
                }

                // If the code block exceeds 400px in height, restrict its max-height and add a "View All" button
                if (block.scrollHeight > 400) {
                    block.classList.add('restrictCodeBlockHeight');

                    const container = block.closest('.code-block-container');
                    if (!container) return;

                    if (container.querySelector('.view-all-button')) return;

                    const viewAllButton = document.createElement('button');
                    viewAllButton.type = 'button';
                    viewAllButton.textContent = 'View All';
                    viewAllButton.className = 'view-all-button';
                    viewAllButton.setAttribute('aria-expanded', 'false');

                    // Generate a unique ID for accessibility if not already present
                    if (!block.id) {
                        block.id = `code-block-${Math.random().toString(36).slice(2, 9)}`;
                    }
                    viewAllButton.setAttribute('aria-controls', block.id);
                    viewAllButton.setAttribute('aria-label', 'View all code');

                    // Insert button after the <pre> element, inside the container
                    const pre = block.parentElement;
                    if (pre && pre.nextSibling) {
                        pre.parentElement.insertBefore(viewAllButton, pre.nextSibling);
                    } else if (pre) {
                        pre.parentElement.appendChild(viewAllButton);
                    } else {
                        container.appendChild(viewAllButton);
                    }

                    let expanded = false;

                    viewAllButton.addEventListener('click', () => {
                        expanded = !expanded;
                        viewAllButton.setAttribute('aria-expanded', String(expanded));

                        if (expanded) {
                            block.classList.remove('restrictCodeBlockHeight');
                            viewAllButton.textContent = 'Collapse';
                        } else {
                            block.classList.add('restrictCodeBlockHeight');
                            viewAllButton.textContent = 'View All';
                        }
                    });
                }
            });
            resolve();
        } catch (err) {
            reject(err);
        } finally {
            this.callbacks.delete(id);
        }
    }

    _doHighlight(container) {
        return new Promise((resolve, reject) => {
            if (!this.started || !this.worker) {
                reject(new Error('HighlightClient not started. Call start() first.'));
                return;
            }
            const codeBlocks = Array.from(container.querySelectorAll('pre code'));
            if (codeBlocks.length === 0) {
                resolve();
                return;
            }
            const blocks = codeBlocks.map(block => {
                let lang = '';
                for (const cls of block.classList) {
                    if (cls.startsWith('language-')) {
                        lang = cls.slice(9);
                        break;
                    }
                }
                if (!lang || ['undefined', 'null', 'unknown'].includes(lang)) {
                    lang = 'text';
                }

                return {
                    code: block.textContent,
                    language: lang
                };
            });
            const id = this.nextId++;
            this.callbacks.set(id, { resolve, reject, codeBlocks });
            this.worker.postMessage({ id, blocks });
        });
    }


    highlightContainer(container) {
        if (!container) return Promise.resolve();
        if (!this.started) return Promise.reject(new Error('HighlightWorkerClient not started.'));

        if (this.pendingContainers.has(container)) {
            return this.pendingContainers.get(container);
        }
        const promise = this._doHighlight(container);
        this.pendingContainers.set(container, promise);
        promise.finally(() => {
            if (this.pendingContainers.get(container) === promise) {
                this.pendingContainers.delete(container);
            }
        });
        return promise;
    }
}

