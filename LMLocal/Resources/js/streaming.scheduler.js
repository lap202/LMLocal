/**
  * Adaptive scheduler that drives incremental rendering from a `streamBuffer`.
 **/
export function createStreamingScheduler(streamBuffer, options = {}) {
    const {
        baseIntervalMs = 60,          // base interval for rendering updates
        minIntervalMs = 16,           // minimum interval (max speed)
        maxRemainingWords = 300,      // at this number of remaining words, we reach minIntervalMs
        idleIntervalMs = 200,         // when there is no data at all
    } = options;

    let timer = null;
    let isRunning = false;
    let currentTaskFn = null;
    let lastDuration = baseIntervalMs;
    let pendingPromise = Promise.resolve();  

    const getRemainingWords = () => streamBuffer.getRemainingWordsCount();

    const getTargetInterval = () => {
        const remaining = getRemainingWords();
        if (remaining === 0) return idleIntervalMs;

        const t = Math.min(1, remaining / maxRemainingWords);

        let interval = baseIntervalMs - (baseIntervalMs - minIntervalMs) * t;
        return Math.max(minIntervalMs, Math.min(baseIntervalMs, interval));
    };

    const computeDelay = () => {
        const target = getTargetInterval();

        let delay = target - lastDuration;

        return Math.max(minIntervalMs, delay);
    };

    const callRenderer = async (textToRender) => {
        if (!currentTaskFn) return;
        const start = performance.now();
        try {
            await currentTaskFn(textToRender);
        } catch (e) {

        }
        lastDuration = Math.max(1, performance.now() - start);
    };

    const processNext = async () => {
        if (!isRunning) return;

        streamBuffer.readNext();

        await pendingPromise;
        pendingPromise = callRenderer(streamBuffer.visibleText);

        if (getRemainingWords() > 0) {
            scheduleNext();
        } else {
            if (timer) {
                clearTimeout(timer);
                timer = null;
            }
        }
    };

    const scheduleNext = () => {
        if (timer) clearTimeout(timer);
        let delay = computeDelay();

        delay = Math.max(minIntervalMs, delay);
        timer = setTimeout(() => {
            timer = null;
            processNext();
        }, Math.round(delay));
    };

    const start = (taskFn) => {
        if (isRunning) return;
        if (!taskFn) return;
        currentTaskFn = taskFn;
        isRunning = true;

        if (getRemainingWords() > 0 || streamBuffer.visibleText.length === 0) {
            processNext();
        }
    };

    const stop = () => {
        if (timer) {
            clearTimeout(timer);
            timer = null;
        }
        isRunning = false;

    };

    const notify = () => {
        if (!isRunning) return;

        if (timer) {
            clearTimeout(timer);
            timer = null;
        }
        processNext();
    };

    const flush = () => {
        stop();
        const finalText = streamBuffer.flush();
        if (currentTaskFn) {

            pendingPromise.then(() => currentTaskFn(finalText));
        }
        pendingPromise = Promise.resolve();
    };

    const reset = () => {
        stop();
        streamBuffer.reset();
        lastDuration = baseIntervalMs;
        pendingPromise = Promise.resolve();
    };

    return { start, stop, notify, flush, reset };
}