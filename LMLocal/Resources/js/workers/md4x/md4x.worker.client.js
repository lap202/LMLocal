export class Md4xWorkerClient {
    constructor() {
        this.worker = null;
        this.callbacks = new Map();
        this.nextId = 0;
        this.started = false;

        this.readyPromise = null;
        this.resolveReady = null;
        this.rejectReady = null;
    }

    _getWorkerCode() {
        const bundleUrl = "https://app.local/js/workers/md4x/md4x.bundle.js";
        return `
            importScripts('${bundleUrl}');

            if (typeof self.init !== 'function' || typeof self.renderToHtml !== 'function') {
                throw new Error('md4x bundle did not expose init() or renderToHtml()');
            }

            let isReady = false;

            self.onmessage = async function(e) {
                const { type } = e.data;

                if (type === 'INIT') {
                    const { wasmBuffer } = e.data;
                    try {
                        await self.init({ wasm: wasmBuffer });
                        isReady = true;
                        self.postMessage({ type: 'ready' });
                    } catch (err) {
                        self.postMessage({ type: 'error', error: err.message });
                    }
                    return;
                }

                if (type === 'PARSE') {
                    if (!isReady) {
                        self.postMessage({ id: e.data.id, error: 'Worker not ready' });
                        return;
                    }
                    const { id, markdown } = e.data;
                    if (markdown === undefined) {
                        self.postMessage({ id, error: 'Missing markdown' });
                        return;
                    }
                    try {
                        const html = self.renderToHtml(markdown, { heal: true, gfm : true });
                        self.postMessage({ id, html });
                    } catch (err) {
                        self.postMessage({ id, error: err.message });
                    }
                    return;
                }
            };
        `;
    }

    start() {
        if (this.started && this.worker) return;
        if (typeof Worker === 'undefined') throw new Error('Web Workers not supported');

        this.readyPromise = new Promise((resolve, reject) => {
            this.resolveReady = resolve;
            this.rejectReady = reject;
        });

        const blob = new Blob([this._getWorkerCode()], { type: 'application/javascript' });
        const workerUrl = URL.createObjectURL(blob);
        this.worker = new Worker(workerUrl);
        URL.revokeObjectURL(workerUrl);

        this.worker.onmessage = (e) => this._handleMessage(e.data);
        this.worker.onerror = (err) => {
            console.error('Worker error', err);
            this.stop();
            if (this.rejectReady) this.rejectReady(err);
        };
        this.started = true;

        const wasmUrl = "https://app.local/js/workers/md4x/md4x.wasm";
        fetch(wasmUrl)
            .then(r => {
                if (!r.ok) throw new Error(`Failed to fetch WASM: ${r.status}`);
                return r.arrayBuffer();
            })
            .then(wasmBuffer => {
                if (this.worker) {
                    this.worker.postMessage({ type: 'INIT', wasmBuffer }, [wasmBuffer]);
                }
            })
            .catch(err => {
                console.error("WASM load error:", err);
                if (this.rejectReady) this.rejectReady(err);
                this.stop();
            });
    }

    stop() {
        if (!this.started) return;
        this.started = false;
        this.readyPromise = null;

        if (this.rejectReady) {
            this.rejectReady(new Error('Worker stopped'));
            this.resolveReady = null;
            this.rejectReady = null;
        }

        if (this.worker) {
            this.worker.terminate();
            this.worker = null;
        }

        for (const { reject } of this.callbacks.values()) {
            reject(new Error('Worker stopped'));
        }
        this.callbacks.clear();
        this.nextId = 0;
    }

    _handleMessage({ id, html, error, type }) {
        if (type === 'ready') {
            if (this.resolveReady) this.resolveReady();
            return;
        }
        if (type === 'error') {
            const err = new Error(error);
            if (this.rejectReady) this.rejectReady(err);
            return;
        }
        const cb = this.callbacks.get(id);
        if (!cb) return;
        if (error) {
            cb.reject(new Error(error));
        } else {
            cb.resolve(html);
        }
        this.callbacks.delete(id);
    }

    async parse(markdown) {
        if (!this.started) throw new Error('MD4X worker not started. Call start() first.');
        await this.readyPromise;
        if (!this.started) throw new Error('Worker was stopped');
        return new Promise((resolve, reject) => {
            const id = this.nextId++;
            this.callbacks.set(id, { resolve, reject });
            this.worker.postMessage({ type: 'PARSE', id, markdown });
        });
    }
}