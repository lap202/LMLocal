import { MarkedRenderer, StreamingMarkdownRenderer, SimpleIncrementalRenderer, PlainTextRenderer } from './markdown.renderers.js';

/**
 * MessageRenderer - central abstraction for rendering message content.
 *
 * Responsibilities:
 *  - Provide multiple renderer implementations (markdown, streaming, incremental, plain).
 *  - Expose a stable API to switch renderer (`setRenderer`) and produce HTML (`render`).
 *  - Run code highlighting on rendered output (`highlightCodeBlocks`) when a global `hljs` is present.
 *  - Safely fall back to plain-escaped text when a renderer is not available.
 */
const MessageRenderer = (() => {
    let currentRenderer = null;

    // Available renderer types
    const RendererType = {
        MARKED: 'marked',
        STREAMING_MARKDOWN: 'streaming-markdown',
        SIMPLE_INCREMENTAL: 'simple-incremental',
        PLAIN: 'plain'
    };

    function setRenderer(type, options = {}) {
        switch (type) {
            case RendererType.MARKED:
                currentRenderer = new MarkedRenderer();
                break;
            case RendererType.STREAMING_MARKDOWN:
                currentRenderer = new StreamingMarkdownRenderer();
                break;
            case RendererType.SIMPLE_INCREMENTAL:
                currentRenderer = new SimpleIncrementalRenderer();
                break;
            case RendererType.PLAIN:
                currentRenderer = new PlainTextRenderer();
                break;
            default:
                throw new Error(`Unknown renderer type: ${type}`);
        }
    }

    function render(markdown) {
        if (!currentRenderer) {
            console.warn('Renderer not set, using fallback');
            return escapeHtml(markdown);
        }
        return currentRenderer.render(markdown);
    }
    // Uses global hljs if available; skip if highlight.js is not loaded.
    function highlightCodeBlocks(container) {
        if (!container || typeof hljs === 'undefined') return;
        container.querySelectorAll('pre code').forEach(block => {
            hljs.highlightElement(block);
        });
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Set default renderer
    setRenderer(RendererType.MARKED);

    return {
        render,
        highlightCodeBlocks,
        setRenderer,      // exported method to switch renderer
        RendererType      // exported renderer type constants for convenience
    };
})();

export default MessageRenderer;