export class Md4xWorkerClient {
    constructor() {
        this.worker = null;
        this.callbacks = new Map();
        this.nextId = 0;
        this.started = false;
    }

    __getWorkerCode() {
        const bundleUrl = "https://app.local/js/workers/md4x.bundle.js";
        const workerCode = `
            importScripts('${bundleUrl}');

            let isReady = false;

            self.onmessage = async function(e) {
                const { type } = e.data;

                if (type === 'INIT') {
                    const { wasmBuffer } = e.data;
                    try {
                        await self.init({ wasm: wasmBuffer });
                        isReady = true;
                        self.postMessage({ type: 'ready', id: '__init__' });
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
                        const html = self.renderToHtml(markdown, { heal: true }); 
                        self.postMessage({ id, html });
                    } catch (err) {
                        self.postMessage({ id, error: err.message });
                    }
                    return;
                }
            };
        `;
        return workerCode;
    }

    start() {
        if (this.started && this.worker) return;
        if (typeof Worker === 'undefined') throw new Error('Web Workers not supported');

        const blob = new Blob([this.__getWorkerCode()], { type: 'application/javascript' });
        const workerUrl = URL.createObjectURL(blob);
        this.worker = new Worker(workerUrl);
        URL.revokeObjectURL(workerUrl);

        this.worker.onmessage = (e) => this._handleMessage(e.data);
        this.worker.onerror = (err) => { console.error(err); this.stop(); };
        this.started = true;

        const wasmUrl = "https://app.local/js/workers/md4x.wasm";
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
                this.stop();
            });
    }

    stop() {
        if (!this.started) return;
        this.started = false;
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
        if (type === 'ready' || type === 'error') {

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

    async parse(markdown, options = {}) {
        if (!this.started) this.start();
        return new Promise((resolve, reject) => {
            const id = this.nextId++;
            this.callbacks.set(id, { resolve, reject });
            this.worker.postMessage({ type: 'PARSE', id, markdown, options });
        });
    }
}