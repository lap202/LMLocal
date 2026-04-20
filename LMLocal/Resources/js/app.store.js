import { AppStatus, Config } from './app.globals.js';

"use strict";
/**
 * Simple observable application store that holds UI-related state and notifies
 * subscribers on changes. Designed as a minimal, synchronous state container
 * with a small API surface suitable for UI components to subscribe to updates.
 **/
class AppStoreClass {
    constructor() {
        this.state = {
            status: AppStatus.OFFLINE,
            modelName: '',
            tokenUsed: 0,
            tokenMax: Config.MAX_TOKENS,
            tokenSpeed: 0,
            error: null,
            accumulatedText: "",
            accumulatedThoughtText: "",
            userMessage: ""
        };
        this.listeners = new Set();
    }

    getState() {
        return { ...this.state };
    }

    subscribe(fn) {
        if (typeof fn !== 'function') {
            console.error('AppStore.subscribe: listener must be a function, received', fn);
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
            console.error('AppStore.setState: updates must be an object, received', updates);
            return;
        }

        this.state = { ...prevState, ...updates };

        const hasChanges = Object.keys(updates).some(key => prevState[key] !== updates[key]);

        if (hasChanges) {
            this.listeners.forEach(fn => fn(this.state, prevState));
        }
    }
}

const appStore = new AppStoreClass();
export default appStore;

export const appSelectors = {
    isTerminal: (state) => [AppStatus.OFFLINE, AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Busy if not IDLE (includes STOPPING, CONNECTING, OFFLINE, etc.)
    isBusy: (state) => ![AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Generating only during active token flow
    isGenerating: (state) => [AppStatus.PROCESSING, AppStatus.THINKING, AppStatus.STREAMING].includes(state.status),
    // Can send only when truly idle (implies online and if an error)
    canSend: (state) => [AppStatus.IDLE, AppStatus.ERROR].includes(state.status)
};
