/**
 * Create a streaming pipeline that coordinates buffering, parsing and rendering of streamed chunks.
 **/
export function createStreamingPipeline(streamBuffer, renderer, parser, scheduler) {
    let isRunning = false;
    let isAborted = false;
    let isEnded = false;

    let onAbortCallback = null;
    let onEndCallback = null;
    let onErrorCallback = (err) => console.error('Pipeline Error:', err);


    const processChunk = async (visibleText) => {
        if (isAborted) return;
        try {
            const html = await parser.parse(visibleText);
            renderer.write(visibleText, html);
        } catch (err) {
            await onErrorCallback?.(err);
        }
    };

    const startScheduler = () => {
        if (isRunning) return;
        isRunning = true;
        scheduler.start(processChunk);
    };

    const stopScheduler = () => {
        scheduler.stop();
        isRunning = false;
    };

    const reset = () => {

        if (isRunning) {
            stopScheduler();
        }

        onAbortCallback = null;
        onEndCallback = null;
        onErrorCallback = (err) => console.error('Pipeline Error:', err);

        isRunning = false;
        isAborted = false;
        isEnded = false;


        if (streamBuffer) {
            streamBuffer.reset();
        }
        if (scheduler) {
            scheduler.reset();
        }
    };

    return {
        attach(container) { renderer.start(container); },
        write(text) {
            if (isAborted || isEnded) return;

            streamBuffer.append(text);
            if (!isRunning) {
                startScheduler();
            } else {
                scheduler.notify();
            }
        },
        abort() {
            if (!isRunning) return;
            isAborted = true;
            stopScheduler();
            Promise.resolve().then(() => onAbortCallback?.());
        },
        end() {
            if (!isRunning) return;
            isEnded = true;
            scheduler.flush();
            stopScheduler();
            Promise.resolve().then(() => onEndCallback?.());
        },
        onAbort(fn) { onAbortCallback = fn; },
        onEnd(fn) { onEndCallback = fn; },
        onError(fn) { onErrorCallback = fn; },
        reset
    };
}