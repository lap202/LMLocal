import { AppStatus } from '@app/store/app.globals.js';

export const appSelectors = {
    isTerminal: (state) => [AppStatus.OFFLINE, AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Busy if not IDLE (includes STOPPING, CONNECTING, OFFLINE, etc.)
    isBusy: (state) => ![AppStatus.IDLE, AppStatus.ERROR].includes(state.status),
    // Generating only during active token flow
    isGenerating: (state) => [AppStatus.PROCESSING, AppStatus.THINKING, AppStatus.STREAMING].includes(state.status),
    // Can send only when truly idle (implies online and if an error)
    canSend: (state) => [AppStatus.IDLE, AppStatus.ERROR].includes(state.status)
};
