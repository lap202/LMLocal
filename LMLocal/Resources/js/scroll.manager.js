// Scroll manager: scrolls to bottom when content is near tail (thresholdPx) or when forced; uses requestAnimationFrame to batch DOM updates.
export const createScrollManager = (container, thresholdPx = 50) => {
    // State to track if we're "stuck" to the bottom (within threshold) or if user has scrolled up
    let isStuckToBottom = true;
    let scrollScheduled = false;

    const init = () => {
        // Set initial state based on current scroll position
        const distanceFromBottom = container.scrollHeight - (container.scrollTop + container.clientHeight);
        isStuckToBottom = distanceFromBottom <= thresholdPx;
    };
    init();

    // Handle user scrolls: if they scroll up beyond the threshold, we "unstick"; if they scroll back down within the threshold, we "stick" again
    const handleManualScroll = () => {
        if (!container) return;

        const distanceFromBottom = container.scrollHeight - (container.scrollTop + container.clientHeight);

        // If the user scrolls up beyond the threshold, we "unstick"; if they scroll back down within the threshold, we "stick" again
        isStuckToBottom = distanceFromBottom <= thresholdPx;
    };

    container.addEventListener('scroll', handleManualScroll, { passive: true });

    const scrollToBottom = (force = false) => {
        if (!container) return;

        // Scroll if:
        // 1. We're forced to (force)
        // 2. We were "stuck" to the bottom before this function call
        if (force || isStuckToBottom) {
            if (scrollScheduled) return;

            scrollScheduled = true;
            requestAnimationFrame(() => {
                container.scrollTop = container.scrollHeight;
                scrollScheduled = false;

                // After scrolling, we should be "stuck" to the bottom again
                isStuckToBottom = true;
            });
        }
    };

    return {
        scrollToBottom,
        destroy: () => container.removeEventListener('scroll', handleManualScroll)
    };
};