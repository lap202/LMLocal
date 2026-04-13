// Mock that simulates rendering an avalanche of large markdown chunks
const _listeners = [];

// A large, complex markdown text to stress test the rendering
const baseText = `
### Large Header
Here is some text with **bold** and *italic*.
- List item 1
- List item 2

\`\`\`csharp
public class PerfTest {
    public void Run() {
        Console.WriteLine("Stress testing highlight.js and marked.js");
    }
}
\`\`\`
`.repeat(100); // Repeat to make it roughly ~1 page

const totalChunks = 500;
const chunkSize = Math.ceil(baseText.length / totalChunks);
const chunks = [];
for(let i=0; i<baseText.length; i+=chunkSize) {
    chunks.push(baseText.substring(i, i + chunkSize));
}

// Global state to track streaming progress
window.__streamingState = {
    isStreaming: false,
    totalChunksToSend: totalChunks,
    chunksSent: 0,
    isComplete: false
};

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
        Status: "SUCCESS", ModelName: "Benchmark Model", MaxContext: 16384, UsedTokens: 0
    }),
    ExecutePromptAsync: async (prompt) => {
        let i = 0;
        window.__streamingState.isStreaming = true;
        window.__streamingState.chunksSent = 0;
        window.__streamingState.isComplete = false;

        console.log(`Starting stream with ${chunks.length} chunks`);
        performance.mark('stream-start');

        function sendNext() {
            if (i < chunks.length) {
                // Send chunk
                _listeners.forEach(fn => fn({
                    data: { Type: 'StreamContent', Payload: chunks[i], Count: i, TokensPerSecond: 100 }
                }));
                window.__streamingState.chunksSent = i + 1;
                i++;
                // Delay 1ms to allow browser to yield and UI to process the render event
                setTimeout(sendNext, 1);
            } else {
                _listeners.forEach(fn => fn({ data: { Type: 'StreamEnd' } }));
                window.__streamingState.isStreaming = false;
                window.__streamingState.isComplete = true;
                console.log(`Stream complete: sent ${window.__streamingState.chunksSent} chunks`);
                performance.mark('stream-end');
                performance.measure('stream-duration', 'stream-start', 'stream-end');
            }
        }
        sendNext();
    },
    StopExecutionAsync: async () => {},
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

if (document.readyState === 'complete') __startMock();
else window.addEventListener('load', __startMock);
