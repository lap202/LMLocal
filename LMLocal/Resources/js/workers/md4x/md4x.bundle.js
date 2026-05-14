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

// Parser flags (MD_FLAG_*) from md4x.h
// These control parser behavior (bitmask). Enable by OR-ing values.
// Controls collapsing of whitespace in normal text.
const MD_FLAG_COLLAPSEWHITESPACE = 0x0001;
// Allow ATX headers without a space after the hashes ("###header").
const MD_FLAG_PERMISSIVEATXHEADERS = 0x0002;
// Recognize bare URLs as autolinks (without <>).
const MD_FLAG_PERMISSIVEURLAUTOLINKS = 0x0004;
// Recognize bare e-mail addresses as autolinks.
const MD_FLAG_PERMISSIVEEMAILAUTOLINKS = 0x0008;
// Disable indented (4-space) code blocks; only fenced blocks used.
const MD_FLAG_NOINDENTEDCODEBLOCKS = 0x0010;
// Disable raw HTML block parsing.
const MD_FLAG_NOHTMLBLOCKS = 0x0020;
// Disable raw HTML inline spans.
const MD_FLAG_NOHTMLSPANS = 0x0040;
// Enable table syntax support.
const MD_FLAG_TABLES = 0x0100;
// Enable strikethrough (~~text~~).
const MD_FLAG_STRIKETHROUGH = 0x0200;
// Recognize bare www-style autolinks (beginning with "www.").
const MD_FLAG_PERMISSIVEWWWAUTOLINKS = 0x0400;
// Enable GitHub-style task lists (- [ ] / - [x]).
const MD_FLAG_TASKLISTS = 0x0800;
// Support inline/display LaTeX math spans ($ and $$).
const MD_FLAG_LATEXMATHSPANS = 0x1000;
// Enable wiki-link style syntax (if implemented).
const MD_FLAG_WIKILINKS = 0x2000;
// Enable underline extension (changes '_' handling).
const MD_FLAG_UNDERLINE = 0x4000;
// Treat soft breaks as hard breaks.
const MD_FLAG_HARD_SOFT_BREAKS = 0x8000;
// Enable frontmatter (YAML) parsing for top-of-file metadata.
const MD_FLAG_FRONTMATTER = 0x10000;
// Enable inline/block component syntax (::component ...).
const MD_FLAG_COMPONENTS = 0x20000;
// Enable trailing attribute syntax (e.g. {.class} after elements).
const MD_FLAG_ATTRIBUTES = 0x40000;
// Enable alert/admonition block syntax (>[!TYPE] ...).
const MD_FLAG_ALERTS = 0x80000;

// Convenience combinations from md4x.h
// MD_FLAG_PERMISSIVEAUTOLINKS = EMAIL | URL | WWW
const MD_FLAG_PERMISSIVEAUTOLINKS = (MD_FLAG_PERMISSIVEEMAILAUTOLINKS | MD_FLAG_PERMISSIVEURLAUTOLINKS | MD_FLAG_PERMISSIVEWWWAUTOLINKS);
// MD_FLAG_NOHTML = NOHTMLBLOCKS | NOHTMLSPANS
const MD_FLAG_NOHTML = (MD_FLAG_NOHTMLBLOCKS | MD_FLAG_NOHTMLSPANS);

// Dialect presets: commonly used parser flag bundles.
// CommonMark: default, no extra flags.
const MD_DIALECT_COMMONMARK = 0;
// GitHub-flavored Markdown (GFM): enables permissive autolinks, tables,
// strikethrough, task lists and alerts (matches md4x.h MD_DIALECT_GITHUB).
const MD_DIALECT_GITHUB = (MD_FLAG_PERMISSIVEAUTOLINKS | MD_FLAG_TABLES | MD_FLAG_STRIKETHROUGH | MD_FLAG_TASKLISTS | MD_FLAG_ALERTS);
// ALL: enable all optional extensions supported by the parser.
const MD_DIALECT_ALL = (MD_FLAG_PERMISSIVEAUTOLINKS | MD_FLAG_TABLES | MD_FLAG_STRIKETHROUGH | MD_FLAG_TASKLISTS | MD_FLAG_LATEXMATHSPANS | MD_FLAG_WIKILINKS | MD_FLAG_UNDERLINE | MD_FLAG_FRONTMATTER | MD_FLAG_COMPONENTS | MD_FLAG_ATTRIBUTES | MD_FLAG_ALERTS);

// Imports passed to WebAssembly.instantiate.
// Provide a minimal mock for the `wasi_snapshot_preview1` namespace with
// callable no-op stubs so the module can instantiate without a full WASI
// implementation. This mirrors the earlier bundle behaviour.
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
            if (!_instance || !_instance.exports || !_instance.exports.memory) return 1;
            try {
                const mem = _instance.exports.memory;
                const bytes = new Uint8Array(mem.buffer, buf, len);
                if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
                    crypto.getRandomValues(bytes);
                    return 0;
                }
            } catch (e) { }
            return 1;
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

        new Uint8Array(memory.buffer, ptr, encoded.length).set(encoded);

        const ret = fn(ptr, encoded.length, ...extra);
        if (ret !== 0) throw new Error("md4x: render failed with code " + ret);

        outPtr = md4x_result_ptr();
        if (outPtr === 0) throw new Error("md4x: result allocation failed");

        const outSize = md4x_result_size();

        return _decoder.decode(new Uint8Array(memory.buffer, outPtr, outSize));

    } finally {
        if (ptr) md4x_free(ptr);
        if (outPtr) md4x_free(outPtr);
    }
}

let _initialized = false;
async function init(opts) {
    if (_initialized) return _getExports();
    if (!(opts?.wasm instanceof ArrayBuffer) && !(opts?.wasm instanceof Uint8Array)) {
        throw new Error('md4x: wasm buffer (ArrayBuffer or Uint8Array) is required in Worker');
    }
    const bytes = opts.wasm;
    const { instance } = await WebAssembly.instantiate(bytes, _imports);
    _setInstance(instance);
    _initialized = true;

    return instance.exports;
}

function renderToHtml(input, opts) {
    let rendererFlags = opts?.full ? MD_HTML_FLAG_FULL_HTML : 0;
    if (opts?.gfm) rendererFlags |= MD_DIALECT_GITHUB;
    if (opts?.heal) rendererFlags |= MD_HTML_FLAG_HEAL;

    // parserFlags: bitmask of MD_FLAG_* or MD_DIALECT_*; default 0 (CommonMark)
    const parserFlags = (typeof opts?.parserFlags === 'number') ? opts.parserFlags : 0;

    const exports = _getExports();
    // Call new wasm export that accepts parser_flags + renderer_flags
    if (!exports.md4x_to_html_with_parser_flags) throw new Error('md4x: wasm export md4x_to_html_with_parser_flags not found');
    return render(exports, exports.md4x_to_html_with_parser_flags, input, parserFlags, rendererFlags);
}

function heal(input) {
    const exports = _getExports();
    return render(exports, exports.md4x_heal, input);
}

function renderToText(input, opts) {
    const flags = opts?.heal ? MD_HTML_FLAG_HEAL : 0;
    const exports = _getExports();
    return render(exports, exports.md4x_to_text, input, flags);
}


self.init = init;
self.renderToHtml = renderToHtml;
self.heal = heal;
self.renderToText = renderToText;