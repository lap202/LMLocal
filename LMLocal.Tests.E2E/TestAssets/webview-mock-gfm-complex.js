const _listeners = [];

const __mockBridge = {
    __webview: {
        addEventListener: (event, handler) => {
            if (event === 'message') _listeners.push(handler);
        },
        removeEventListener: (event, handler) => {
            const i = _listeners.indexOf(handler);
            if (i !== -1) _listeners.splice(i, 1);
        }
    },
    GetStatusAsync: async () => JSON.stringify({
        Status: "SUCCESS",
        ModelName: "Test Model",
        MaxContext: 16384,
        UsedTokens: 0
    }),
    ExecutePromptAsync: async (prompt) => {
        // Complex GFM payload with setext heading, nested lists, blockquote, reference links, image, tables, fenced code
        const gfm = `Setext Heading\n===============\n\n1. First item\n   - Nested bullet\n     - Deep nested\n\n> This is a blockquote\n>\n> - nested in blockquote\n\nInline code: ` + '`' + `const x = 1` + '`' + ` and fenced code:\n\n\`\`\`python\nprint('hello')\n\`\`\`\n\nImage: ![Alt text](https://via.placeholder.com/150)\n\nReference link: [GitHub][1]\n\n[1]: https://github.com\n\nAutolink: https://example.org\n\nTable:\n| Left | Center | Right |\n| :-- | :-: | --: |\n| L | C | R |\n`;

        setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'StreamContent', Payload: gfm, Count: 1, TokensPerSecond: 1.0 }
            }));
        }, 50);
        setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'StreamEnd' }
            }));
        }, 150);
    },
    StopExecutionAsync: async () => {
        setTimeout(() => {
            _listeners.forEach(fn => fn({
                data: { Type: 'StreamEnd' }
            }));
        }, 50);
    },
    ResetHistoryAsync: async () => {},
    CopyToClipboardAsync: async (text) => true
};

function __startMock() {
    if (typeof window.lmInit === 'function') {
        window.__bridgeOverride = __mockBridge;
        window.lmInit(__mockBridge);
    } else {
        setTimeout(__startMock, 10);
    }
}

if (document.readyState === 'complete') {
    __startMock();
} else {
    window.addEventListener('load', __startMock);
}
