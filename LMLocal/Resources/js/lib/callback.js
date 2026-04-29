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

    async emitResult(...args) {
        if (!this._fn) {
            return { success: false, data: null, error: new Error('No handler registered') };
        }
        try {
            const raw = await this._fn(...args);
            if (raw && typeof raw === 'object' && 'success' in raw) {
                return raw;
            }
            const success = raw !== false;
            return {
                success,
                data: success ? raw : null,
                error: success ? null : new Error('Callback explicitly returned false')
            };
        } catch (err) {
            return { success: false, data: null, error: err };
        }
    }
    off() {
        this._fn = null;
    }
}

export { Callback };

export function createCallback() {
    return new Callback();
}
