export class BaseStoreClass {
    constructor(initialState = {}) {
        if (new.target === BaseStoreClass) {
            throw new Error('BaseStoreClass is abstract');
        }
        this.storeName = new.target.name;
        this.state = { ...initialState };
        this.listeners = new Set();
    }

    getState() {
        return { ...this.state };
    }

    subscribe(fn) {
        if (typeof fn !== 'function') {
            console.error(`${this.storeName}.subscribe: listener must be a function`, fn);
            return () => { };
        }
        this.listeners.add(fn);
        return () => this.listeners.delete(fn);
    }

    unsubscribe(fn) {
        if (typeof fn !== 'function') return false;
        return this.listeners.delete(fn);
    }

    setState(nextStateOrUpdater) {
        const prevState = this.state;
        const updates = typeof nextStateOrUpdater === 'function'
            ? nextStateOrUpdater(prevState)
            : nextStateOrUpdater;

        if (updates === null || typeof updates !== 'object') {
            console.error(`${this.storeName}.setState: updates must be an object`, updates);
            return;
        }

        this.state = { ...prevState, ...updates };

        const hasChanges = Object.keys(updates).some(key => prevState[key] !== updates[key]);

        if (hasChanges) {
            this.listeners.forEach(fn => fn(this.state, prevState));
        }
    }
}