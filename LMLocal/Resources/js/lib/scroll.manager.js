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
    _container = null;
    _thresholdPx = 50;
    _isStuckToBottom = true;
    _scrollScheduled = false;
    _rafId = null;

    constructor(container, thresholdPx = 50) {
        this.setup(container, thresholdPx);
    }

    setup(container, thresholdPx = 50) {
        this.reset();
        this._container = container;
        this._thresholdPx = thresholdPx;
        if (this._container) {
            this._updateStuckState();
            this._attachEvents();
        }
    }

    reset() {
        if (this._rafId) {
            cancelAnimationFrame(this._rafId);
            this._rafId = null;
        }
        this._scrollScheduled = false;
        this._detachEvents();
        this._container = null;
        this._thresholdPx = 50;
        this._isStuckToBottom = true;
    }

    _attachEvents() {
        if (this._container) {
            this._container.addEventListener('scroll', this._handleManualScroll, { passive: true });
        }
    }

    _detachEvents() {
        if (this._container) {
            this._container.removeEventListener('scroll', this._handleManualScroll);
        }
    }

    _updateStuckState() {
        if (!this._container) return;
        const distance = this._container.scrollHeight - (this._container.scrollTop + this._container.clientHeight);
        this._isStuckToBottom = distance <= this._thresholdPx;
    }

    _handleManualScroll = () => {
        this._updateStuckState();
    };

    scrollToBottom(force = false) {
        if (!this._container) return;

        if (force) {
            if (this._rafId) cancelAnimationFrame(this._rafId);
            this._scrollScheduled = false;
            this._isStuckToBottom = true;
        }

        if (this._scrollScheduled) return;

        const shouldScroll = force || this._isStuckToBottom;
        if (!shouldScroll) return;

        this._scrollScheduled = true;
        this._rafId = requestAnimationFrame(() => {
            if (!force && !this._isStuckToBottom) {
                this._scrollScheduled = false;
                this._rafId = null;
                return;
            }
            if (this._container) {
                this._container.scrollTop = this._container.scrollHeight;
                this._isStuckToBottom = true;
            }
            this._scrollScheduled = false;
            this._rafId = null;
        });
    }
}

export const createScrollManager = (container, threshold = 50) => new ScrollManager(container, threshold);

