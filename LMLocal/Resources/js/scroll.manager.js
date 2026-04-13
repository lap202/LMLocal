/**
 * createScrollManager - utility to manage auto-scrolling behavior for a scrollable container.
 * Responsibilities:
 *  - Track whether the container is "stuck" to the bottom (within `thresholdPx`) or if the user has scrolled up.
 *  - Provide a `scrollToBottom` method that scrolls to the bottom if currently stuck, or if forced.
 * Notes:
 *  - Defensive: the implementation cancels pending RAFs on destroy and nulls the internal container reference.
 *  - The default `thresholdPx` controls how close to the bottom the container must be to be considered "stuck".
 */
export const createScrollManager = (container, thresholdPx = 50) => {
    let isStuckToBottom = true;
    let scrollScheduled = false;
    let rafId = null;

    const init = () => {
        const distanceFromBottom = container.scrollHeight - (container.scrollTop + container.clientHeight);
        isStuckToBottom = distanceFromBottom <= thresholdPx;
    };
    init();

    const handleManualScroll = () => {
        if (!container) return;

        const distanceFromBottom = container.scrollHeight - (container.scrollTop + container.clientHeight);
        isStuckToBottom = distanceFromBottom <= thresholdPx;
    };

    container.addEventListener('scroll', handleManualScroll, { passive: true });

    const scrollToBottom = (force = false) => {
        if (!container) return;


        if (force || isStuckToBottom) {
            if (scrollScheduled) return;

            scrollScheduled = true;
            rafId = requestAnimationFrame(() => {
                container.scrollTop = container.scrollHeight;
                scrollScheduled = false;
                isStuckToBottom = true;
                rafId = null;
            });
        }
    };

    return {
        scrollToBottom,
        destroy: () => {
            if (rafId != null) {
                cancelAnimationFrame(rafId);
                rafId = null;
                scrollScheduled = false;
            }
            if (container) {
                container.removeEventListener('scroll', handleManualScroll);
                container = null;
            }
        }
    };
};