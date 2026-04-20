/**
 * createScrollManager - utility to manage auto-scrolling behavior for a scrollable container.
 * Responsibilities:
 *  - Track whether the container is "stuck" to the bottom (within `thresholdPx`) or if the user has scrolled up.
 *  - Provide a `scrollToBottom` method that scrolls to the bottom if currently stuck, or if forced.
 * Notes:
 *  - Defensive: the implementation cancels pending RAFs on destroy and nulls the internal container reference.
 *  - The default `thresholdPx` controls how close to the bottom the container must be to be considered "stuck".
 */
class ScrollManager {
    #container = null;
    #thresholdPx = 50;
    #isStuckToBottom = true;
    #scrollScheduled = false;
    #rafId = null;

    constructor(container, thresholdPx = 50) {
        this.setup(container, thresholdPx);
    }

    setup(container, thresholdPx = 50) {
        this.reset();
        this.#container = container;
        this.#thresholdPx = thresholdPx;
        if (this.#container) {
            this.#updateStuckState();
            this._attachEvents();
        }
    }

    reset() {
        if (this.#rafId) {
            cancelAnimationFrame(this.#rafId);
            this.#rafId = null;
        }
        this.#scrollScheduled = false;
        this._detachEvents();
        this.#container = null;
        this.#thresholdPx = 50;
        this.#isStuckToBottom = true;
    }

    _attachEvents() {
        if (this.#container) {
            this.#container.addEventListener('scroll', this.#handleManualScroll, { passive: true });
        }
    }

    _detachEvents() {
        if (this.#container) {
            this.#container.removeEventListener('scroll', this.#handleManualScroll);
        }
    }

    #updateStuckState() {
        if (!this.#container) return;
        const distance = this.#container.scrollHeight - (this.#container.scrollTop + this.#container.clientHeight);
        this.#isStuckToBottom = distance <= this.#thresholdPx;
    }

    #handleManualScroll = () => {
        this.#updateStuckState();
    };

    scrollToBottom(force = false) {
        if (!this.#container || this.#scrollScheduled) return;
        if (force || this.#isStuckToBottom) {
            if (force) this.#isStuckToBottom = true;
            this.#scrollScheduled = true;
            this.#rafId = requestAnimationFrame(() => {
                if (this.#container) {
                    this.#container.scrollTop = this.#container.scrollHeight;
                    this.#isStuckToBottom = true;
                }
                this.#scrollScheduled = false;
                this.#rafId = null;
            });
        }
    }
}

export const createScrollManager = (container, threshold = 50) => new ScrollManager(container, threshold);

