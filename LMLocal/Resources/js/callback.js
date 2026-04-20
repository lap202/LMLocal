/*
 * Lightweight async callback helper. emit calls the registered handler and returns true unless the handler explicitly returns/fails with false.
 */
class Callback {
    constructor() {
        this._fn = null;
    }

    on(callback) {
        this._fn = typeof callback === 'function' ? callback : null;
        return this;
    }

    async emit(...args) {
        if (!this._fn) return true;
        const result = this._fn(...args);
        return (await result) !== false;
    }

    off() {
        this._fn = null;
    }
}

export { Callback };

export function createCallback() {
    return new Callback();
}
