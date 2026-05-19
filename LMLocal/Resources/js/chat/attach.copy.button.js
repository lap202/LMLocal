import { UIText, Assets } from '@app/store/app.globals.js';

/**
 * Wraps unprocessed `<pre>` blocks in `.code-block-container` divs.
 * This creates the container structure needed by the highlight worker to add "View All" buttons
 * and should be called before highlighting.
 */
export function wrapCodeBlocks(containerElem) {
    if (!containerElem) return;
    const preElements = containerElem.querySelectorAll('pre');
    preElements.forEach(pre => {
        if (pre.hasAttribute('data-code-wrapped')) return;
        if (pre.closest('.code-block-container')) return;

        const wrapper = document.createElement('div');
        wrapper.className = 'code-block-container';
        if (!pre.parentNode) return;
        pre.parentNode.insertBefore(wrapper, pre);
        wrapper.appendChild(pre);
        pre.setAttribute('data-code-wrapped', 'true');
    });
}

/**
 * Scans `containerElem` for `<pre><code>` blocks and attaches a copy header to each unprocessed block.
 */
export function attachCopyButton(containerElem) {
    if (!containerElem) return;
    const preElements = containerElem.querySelectorAll('pre');
    preElements.forEach(pre => {
        if (pre.hasAttribute('data-copy-processed')) return;
        const container = pre.closest('.code-block-container');
        if (!container) return;
        if (container.querySelector('.code-header')) return;

        const codeElement = pre.querySelector('code');
        if (!codeElement) return;

        let lang = '';
        for (const cls of codeElement.classList) {
            if (cls.startsWith('language-')) {
                lang = cls.slice(9);
                break;
            }
        }
        if (!lang || ['undefined', 'null', 'unknown'].includes(lang)) {
            lang = 'text';
        }

        const header = document.createElement('div');
        header.className = 'code-header';
        header.innerHTML = `
            <span class="code-lang">${lang}</span>
            <button class="header-copy-btn">
                ${Assets.COPY_BUTTON_SVG}
                <span>${UIText.COPY_LABEL}</span>
            </button>`;

        container.insertBefore(header, pre);
        pre.setAttribute('data-copy-processed', 'true');
    });
}