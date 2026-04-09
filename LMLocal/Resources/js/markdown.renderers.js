import * as smd from './smd.min.js';

marked.setOptions({
    gfm: true,   // Enable support for GitHub Flavored Markdown (GFM)
    breaks: false // Disable line breaks with a single newline 
});

// Base class (optional) defining the interface for all Markdown → HTML converters.
class BaseRenderer {
    render(markdown) {
        throw new Error('Not implemented');
    }
}

// 1. Full Markdown parser using the 'marked' library.
//    Converts the entire markdown string to HTML in one synchronous call.
//    Used for final render or fallback, not for incremental streaming.
export class MarkedRenderer extends BaseRenderer {
    render(markdown) {
        if (!markdown) return '';
        if (typeof marked === 'undefined') {
            console.warn('marked not loaded');
            return this.escapeHtml(markdown);
        }
        try {
            return marked.parse(markdown);
        } catch (e) {
            return this.escapeHtml(markdown);
        }
    }
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// 2. Full Markdown parser using the 'streaming-markdown' library.
//    This creates a temporary DOM element, feeds the entire markdown text,
//    and returns the final HTML. It does NOT perform incremental rendering;
//    it's just a one‑time conversion. The library's incremental capability
//    is used separately in the 'incremental' DOM update strategy.
export class StreamingMarkdownRenderer extends BaseRenderer {
    render(markdown) {
        if (!markdown) return '';
        const temp = document.createElement('div');
        const renderer = smd.default_renderer(temp);
        const parser = smd.parser(renderer);
        smd.parser_write(parser, markdown);
        smd.parser_end(parser);
        return temp.innerHTML;
    }
}

// 3. Simple custom parser that splits text by double newline (`\n\n`).
//    Each paragraph is wrapped in <p>, and single newlines inside a paragraph
//    become <br>. This does not handle complex Markdown (lists, code blocks, etc.).
//    Suitable for very simple formatting or as a lightweight fallback.
export class SimpleIncrementalRenderer extends BaseRenderer {
    render(markdown) {
        if (!markdown) return '';
        return markdown.split(/\n\n/).map(para => {
            const escaped = this.escapeHtml(para);
            const withBreaks = escaped.replace(/\n/g, '<br>');
            return `<p>${withBreaks}</p>`;
        }).join('');
    }
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// 4. Plain text renderer: escapes HTML and wraps everything in a <pre> block.
//    No Markdown formatting is applied.
export class PlainTextRenderer extends BaseRenderer {
    render(markdown) {
        return `<pre>${this.escapeHtml(markdown)}</pre>`;
    }
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}