import { UIText, Assets } from './app.globals.js';

/**
 * Scans `containerElem` for `<pre><code>` blocks and attaches a copy header to each unprocessed block.
 */
export function attachCopyButtons(containerElem) {
    if (!containerElem) return;
    const preElements = containerElem.querySelectorAll('pre');
    preElements.forEach(pre => {
        if (pre.hasAttribute('data-copy-processed')) return;
        if (pre.closest('.code-block-container')) return;
        const codeElement = pre.querySelector('code');
        if (!codeElement) return;
        const langMatch = codeElement.className.match(/language-([^\s]+)/);
        let lang = langMatch ? langMatch[1] : 'code';
        if (!lang || lang === 'undefined' || lang === 'null' || lang === 'unknown') {
            lang = 'text';
        }
        const wrapper = document.createElement('div');
        wrapper.className = 'code-block-container';
        const header = document.createElement('div');
        header.className = 'code-header';
        header.innerHTML = `
            <span class="code-lang">${lang}</span>
            <button class="header-copy-btn">
                ${Assets.COPY_BUTTON_SVG}
                <span>${UIText.COPY_LABEL}</span>
            </button>`;
        if (!pre.parentNode) return;
        pre.parentNode.insertBefore(wrapper, pre);
        wrapper.append(header, pre);
        pre.setAttribute('data-copy-processed', 'true');
    });
}