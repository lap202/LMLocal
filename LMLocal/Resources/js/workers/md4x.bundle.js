'use strict';

let _instance;

const _encoder = new TextEncoder();
const _decoder = new TextDecoder();
function _setInstance(instance) {
    _instance = instance;
}

function _getExports() {
    if (!_instance?.exports) {
        throw new Error("md4x: WASM not initialized. Call await init() first.");
    }
    return _instance.exports;
}

const MD_HTML_FLAG_DEBUG = 0x0001;
const MD_HTML_FLAG_VERBATIM_ENTITIES = 0x0002;
const MD_HTML_FLAG_SKIP_UTF8_BOM = 0x0004;
const MD_HTML_FLAG_FULL_HTML = 0x0008;
const MD_HTML_FLAG_CODE_META = 0x0010;
const MD_HTML_FLAG_HEAL = 0x0100;

// WASI imports
const _imports = {
    wasi_snapshot_preview1: {
        fd_close: () => 0,
        fd_filestat_get: () => 0,
        fd_pwrite: () => 0,
        fd_read: () => 0,
        fd_seek: () => 0,
        fd_write: () => 0,
        proc_exit: () => { },
        random_get: (buf, len) => {
            if (!_instance) return 1;
            const bytes = new Uint8Array(_instance.exports.memory.buffer, buf, len);
            crypto.getRandomValues(bytes);
            return 0;
        },
    },
};
function str(input) {
    if (input == null) return "";
    if (typeof input !== "string") throw new TypeError("md4x: input must be a string");
    return input;
}

function render(exports, fn, input, ...extra) {
    const { memory, md4x_alloc, md4x_free, md4x_result_ptr, md4x_result_size } = exports;

    const encoded = _encoder.encode(str(input));
    const ptr = md4x_alloc(encoded.length);
    if (ptr === 0) throw new Error("md4x: allocation failed");

    let outPtr = null;

    try {

        new Uint8Array(memory.buffer).set(encoded, ptr);

        const ret = fn(ptr, encoded.length, ...extra);
        if (ret !== 0) throw new Error("md4x: render failed with code " + ret);

        outPtr = md4x_result_ptr();
        const outSize = md4x_result_size();

        return _decoder.decode(new Uint8Array(memory.buffer, outPtr, outSize));
    } finally {
        if (ptr) md4x_free(ptr);
        if (outPtr) md4x_free(outPtr);
    }
}

async function init(opts) {
    let bytes;
    if (opts?.wasm instanceof ArrayBuffer || opts?.wasm instanceof Uint8Array) {
        bytes = opts.wasm;
    } else {
        const url = opts?.wasm || "https://app.local/js/workers/md4x.wasm";
        bytes = await fetch(url).then(r => r.arrayBuffer());
    }
    const { instance } = await WebAssembly.instantiate(bytes, _imports);
    _setInstance(instance);
    return instance.exports;
}

function renderToHtml(input, opts) {
    let flags = opts?.full ? MD_HTML_FLAG_FULL_HTML : 0;
    if (opts?.heal) flags |= MD_HTML_FLAG_HEAL;
    //if (opts?.heal) input = heal(input);
    const exports = _getExports();
    return render(exports, exports.md4x_to_html, input, flags);
}

function heal(input) {
    const exports = _getExports();
    return render(exports, exports.md4x_heal, input);
}

function renderToText(input, opts) {
    const flags = opts?.heal ? HEAL_FLAG : 0;
    const exports = _getExports();
    return render(exports, exports.md4x_to_text, input, flags);
}


self.init = init;
self.renderToHtml = renderToHtml;
self.heal = heal;
self.renderToText = renderToText;